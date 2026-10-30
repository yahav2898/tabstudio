using System.Text.Json;
using System.Text.RegularExpressions;

namespace TabStudio.Remote;

/// <summary>A saved SSH destination. Credentials are kept separately by the platform key store.</summary>
public sealed record SshHost(string Name, string HostName, string User, int Port = 22, string? IdentityFile = null)
{
    public string KeyId => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{User}@{HostName}:{Port}")))[..24];
    public string DisplayName => $"{Name} ({User}@{HostName}:{Port})";

    /// <summary>Where the host is, such as <c>deploy@dev.example.com:22</c>.</summary>
    public string Address => $"{User}@{HostName}:{Port}";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(HostName) || string.IsNullOrWhiteSpace(User)
            || HostName.Any(char.IsControl) || User.Any(char.IsControl) || Port is < 1 or > 65535)
        {
            throw new ArgumentException("Enter a host name, user and a port between 1 and 65535.");
        }
    }
}

/// <summary>Imports concrete aliases using OpenSSH's first-value-wins and wildcard defaults rules.</summary>
public static partial class SshConfig
{
    public static IReadOnlyList<SshHost> Parse(string text, string home, string defaultUser)
    {
        var blocks = new List<(string[] Patterns, List<(string Key, string Value)> Options)>();
        var current = (Patterns: new[] { "*" }, Options: new List<(string Key, string Value)>());
        blocks.Add(current);
        foreach (var line in text.Split('\n'))
        {
            var words = Words(line);
            if (words.Count < 2) { continue; }
            var key = words[0].ToLowerInvariant();
            if (key == "host")
            {
                current = (words.Skip(1).ToArray(), []);
                blocks.Add(current);
            }
            else if (key == "match")
            {
                // Match conditions, executable ProxyCommand and Include are never executed during import.
                current = ([], []);
                blocks.Add(current);
            }
            else { current.Options.Add((key, string.Join(' ', words.Skip(1)))); }
        }

        var hosts = new List<SshHost>();
        foreach (var alias in blocks.SelectMany(b => b.Patterns).Where(p => !p.Contains('*') && !p.Contains('?') && !p.StartsWith('!')).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var block in blocks.Where(b => Matches(b.Patterns, alias)))
            {
                foreach (var (key, value) in block.Options) { options.TryAdd(key, value); }
            }
            // Refuse routing/authentication semantics we cannot preserve; never silently connect directly instead.
            if (options.ContainsKey("proxycommand") || options.ContainsKey("proxyjump")) { continue; }
            var hostName = options.GetValueOrDefault("hostname", alias);
            var user = options.GetValueOrDefault("user", defaultUser);
            var identity = options.GetValueOrDefault("identityfile");
            if (identity is not null)
            {
                identity = identity.Replace("%d", home, StringComparison.Ordinal).Replace("%h", hostName, StringComparison.Ordinal).Replace("%r", user, StringComparison.Ordinal);
                if (identity.StartsWith("~/", StringComparison.Ordinal)) { identity = Path.Combine(home, identity[2..]); }
                if (!Path.IsPathRooted(identity)) { identity = Path.Combine(home, identity); }
            }
            var port = 22;
            if (options.TryGetValue("port", out var portValue)
                && (!int.TryParse(portValue, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535))
            {
                throw new FormatException($"Host {alias} in the SSH config has Port {portValue}, which is not a port between 1 and 65535.");
            }

            var host = new SshHost(alias, hostName, user, port, identity);
            host.Validate();
            hosts.Add(host);
        }
        return hosts;
    }

    private static bool Matches(string[] patterns, string alias) =>
        patterns.Any(p => !p.StartsWith('!') && Glob(p, alias)) && !patterns.Any(p => p.StartsWith('!') && Glob(p[1..], alias));

    private static bool Glob(string pattern, string value) => Regex.IsMatch(value, "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal) + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static List<string> Words(string line)
    {
        var words = new List<string>();
        var word = new System.Text.StringBuilder();
        char quote = '\0';
        foreach (var c in line.Trim())
        {
            if (quote == '\0' && c == '#') { break; }
            if (c == quote) { quote = '\0'; }
            else if (quote == '\0' && c is '\'' or '"') { quote = c; }
            else if (quote == '\0' && (char.IsWhiteSpace(c) || c == '='))
            {
                if (word.Length > 0) { words.Add(word.ToString()); word.Clear(); }
            }
            else { word.Append(c); }
        }
        if (word.Length > 0) { words.Add(word.ToString()); }
        return words;
    }
}

/// <summary>Stores hosts and pinned server fingerprints, never private keys.</summary>
public sealed class SshHostStore(string directory)
{
    public IReadOnlyList<SshHost> Load() => File.Exists(HostsPath) ? JsonSerializer.Deserialize<List<SshHost>>(File.ReadAllText(HostsPath)) ?? [] : [];
    private string HostsPath => Path.Combine(directory, "hosts.json");
    public void Save(IEnumerable<SshHost> hosts)
    {
        Directory.CreateDirectory(directory);
        var list = hosts.ToList();
        foreach (var host in list) { host.Validate(); }
        AtomicWrite(HostsPath, JsonSerializer.Serialize(list, JsonOptions));
    }
    public string? Fingerprint(SshHost host) => Pins().GetValueOrDefault($"{host.HostName.ToLowerInvariant()}:{host.Port}");
    public void Trust(SshHost host, string fingerprint)
    {
        var pins = Pins();
        var key = $"{host.HostName.ToLowerInvariant()}:{host.Port}";
        if (pins.TryGetValue(key, out var existing) && existing != fingerprint) { throw new IOException("The server key changed. Verify it with the server administrator before replacing its saved fingerprint."); }
        pins[key] = fingerprint;
        Directory.CreateDirectory(directory);
        AtomicWrite(Path.Combine(directory, "known-hosts.json"), JsonSerializer.Serialize(pins, JsonOptions));
    }
    private Dictionary<string, string> Pins() => File.Exists(Path.Combine(directory, "known-hosts.json")) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(directory, "known-hosts.json"))) ?? [] : [];
    internal static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    internal static void AtomicWrite(string path, string text)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, text); File.Move(temp, path, overwrite: true); }
        finally { if (File.Exists(temp)) { File.Delete(temp); } }
    }
}
