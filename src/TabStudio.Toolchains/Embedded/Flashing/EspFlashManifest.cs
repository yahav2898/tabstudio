using System.Globalization;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded.Flashing;

/// <summary>One file and exact flash offset emitted by the native ESP build system.</summary>
public sealed record EspFlashImage(ulong Address, string Path, ulong Length, string Sha256);

/// <summary>A validated plain-text ESP flash set. Gaps are never expanded into a merged image.</summary>
public sealed record EspFlashManifest(string Path, string Chip, IReadOnlyList<string> FlashArguments,
    IReadOnlyList<EspFlashImage> Images, IReadOnlyList<EmbeddedFirmwareInput> Inputs, bool UseStub, string Before)
{
    /// <summary>The actual built application BIN, excluding bootloader/partition images; suitable for an existing compatible ArduinoOTA target.</summary>
    public required string ApplicationPath { get; init; }
}

/// <summary>Reads ESP-IDF flasher_args.json or the template's resolved PlatformIO upload export, without executing its arguments.</summary>
public static class EspFlashManifestReader
{
    private static readonly HashSet<string> Chips = new(StringComparer.Ordinal)
    {
        "esp32", "esp32s2", "esp32s3", "esp32c3", "esp32c6", "esp32h2",
    };
    // ESP-IDF v5.4.1 components/bootloader/Kconfig.projbuild and esp_security/Kconfig.
    // *_SUPPORTED, *_PREFERRED and SECURE_ROM_DL_MODE_ENABLED describe capability/default choices;
    // they are true in ordinary unencrypted builds and never authorize irreversible configuration.
    private static readonly HashSet<string> SecurityEnableSwitches = new(StringComparer.Ordinal)
    {
        "SECURE_BOOT", "SECURE_BOOT_ENABLED", "SECURE_BOOT_V1_ENABLED", "SECURE_BOOT_V2_ENABLED",
        "SECURE_BOOT_V2_ECDSA_ENABLED", "SECURE_BOOT_V2_RSA_ENABLED", "SECURE_BOOT_FLASH_ENC_KEYS_BURN_TOGETHER",
        "SECURE_BOOT_BUILD_SIGNED_BINARIES", "SECURE_BOOTLOADER_ONE_TIME_FLASH", "SECURE_BOOTLOADER_REFLASHABLE",
        "SECURE_SIGNED_APPS", "SECURE_SIGNED_ON_BOOT", "SECURE_SIGNED_ON_UPDATE",
        "SECURE_SIGNED_APPS_NO_SECURE_BOOT", "SECURE_SIGNED_ON_BOOT_NO_SECURE_BOOT", "SECURE_SIGNED_ON_UPDATE_NO_SECURE_BOOT",
        "SECURE_FLASH_ENC_ENABLED", "FLASH_ENCRYPTION_ENABLED", "SECURE_FLASH_ENCRYPTION_MODE_DEVELOPMENT",
        "SECURE_FLASH_ENCRYPTION_MODE_RELEASE", "SECURE_FLASH_REQUIRE_ALREADY_ENABLED", "SECURE_FLASH_PSEUDO_ROUND_FUNC",
        "SECURE_DISABLE_ROM_DL_MODE", "SECURE_ENABLE_SECURE_ROM_DL_MODE", "BOOTLOADER_APP_ANTI_ROLLBACK",
        "ESP_CRYPTO_FORCE_ECC_CONSTANT_TIME_POINT_MUL",
    };

    /// <summary>Loads every image, validates nonoverlapping erase sectors and binds metadata/configuration/images by SHA-256.</summary>
    public static EspFlashManifest Read(string path, string? expectedChip = null)
    {
        try
        {
            return ReadCore(path, expectedChip);
        }
        catch (Exception error) when (error is KeyNotFoundException or JsonException or InvalidOperationException)
        {
            throw new InvalidDataException("ESP flash metadata has a missing, malformed or incorrectly typed member.", error);
        }
    }

    private static EspFlashManifest ReadCore(string path, string? expectedChip)
    {
        path = System.IO.Path.GetFullPath(path);
        var root = System.IO.Path.GetDirectoryName(path)!;
        var inputs = new List<EmbeddedFirmwareInput>();
        var json = ReadBoundFile(path, 256 * 1024);
        inputs.Add(Bound(path, json));
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        var manifest = document.RootElement;
        Unique(manifest);
        Object(manifest);
        var allowed = new HashSet<string>(["write_flash_args", "flash_settings", "flash_files", "extra_esptool_args", "tabstudio_source"], StringComparer.Ordinal);
        var extra = manifest.GetProperty("extra_esptool_args");
        ExactKeys(extra, "chip", "before", "after", "stub");
        var chip = extra.GetProperty("chip").GetString() ?? string.Empty;
        if (!Chips.Contains(chip) || expectedChip is not null && !string.Equals(chip, expectedChip, StringComparison.Ordinal))
        {
            throw new InvalidDataException("ESP metadata chip is unsupported or differs from the selected target.");
        }
        var useStub = extra.GetProperty("stub").GetBoolean();
        if (Normalize(extra.GetProperty("before").GetString()) is not ("default-reset" or "usb-reset" or "no-reset") ||
            Normalize(extra.GetProperty("after").GetString()) is not ("hard-reset" or "no-reset" or "soft-reset"))
        {
            throw new NotSupportedException("ESP metadata requests an unsupported reset policy.");
        }
        var settings = manifest.GetProperty("flash_settings");
        ExactKeys(settings, "flash_mode", "flash_freq", "flash_size");
        var mode = settings.GetProperty("flash_mode").GetString() ?? string.Empty;
        var frequency = settings.GetProperty("flash_freq").GetString() ?? string.Empty;
        var size = settings.GetProperty("flash_size").GetString() ?? string.Empty;
        if (mode is not ("qio" or "qout" or "dio" or "dout" or "keep") ||
            frequency is not ("20m" or "26m" or "40m" or "80m" or "120m" or "keep") ||
            !size.EndsWith("MB", StringComparison.Ordinal) ||
            !uint.TryParse(size.AsSpan(0, size.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var megabytes) ||
            megabytes is not (1 or 2 or 4 or 8 or 16 or 32 or 64 or 128))
        {
            throw new NotSupportedException("ESP flash mode/frequency or explicit flash capacity is unsupported. Detection/encryption/protection flags are not upload metadata.");
        }
        var capacity = megabytes * 1024ul * 1024;
        var writeArguments = manifest.GetProperty("write_flash_args").EnumerateArray().Select(element => element.GetString() ?? string.Empty).ToArray();
        if (writeArguments.Length != 6)
        {
            throw new NotSupportedException("Only explicit flash mode, frequency and size arguments are accepted; force, encryption, erase-all and protection operations are refused.");
        }
        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < writeArguments.Length; i += 2)
        {
            var key = Normalize(writeArguments[i]);
            if (key is not ("--flash-mode" or "--flash-freq" or "--flash-size") || !arguments.TryAdd(key, writeArguments[i + 1]))
            {
                throw new NotSupportedException("Only unique flash-mode, flash-freq and flash-size arguments are accepted.");
            }
        }
        if (arguments.GetValueOrDefault("--flash-mode") != mode || arguments.GetValueOrDefault("--flash-freq") != frequency || arguments.GetValueOrDefault("--flash-size") != size)
        {
            throw new InvalidDataException("ESP flash settings disagree with write_flash_args.");
        }
        var configuration = System.IO.Path.Combine(root, "config", "sdkconfig.json");
        var platformIo = manifest.TryGetProperty("tabstudio_source", out var source) && source.GetString() == "platformio-resolved-upload";
        if (File.Exists(configuration))
        {
            var configurationBytes = ReadBoundFile(configuration, 4 * 1024 * 1024);
            using var config = JsonDocument.Parse(configurationBytes);
            Unique(config.RootElement);
            foreach (var property in config.RootElement.EnumerateObject())
            {
                var name = property.Name.StartsWith("CONFIG_", StringComparison.Ordinal) ? property.Name[7..] : property.Name;
                if (SecurityEnableSwitches.Contains(name) && IsTrue(property.Value))
                {
                    throw new NotSupportedException("ESP startup security switch " + name + " may require signed/encrypted firmware or change irreversible device configuration. Ordinary upload refuses this build.");
                }
            }
            inputs.Add(Bound(configuration, configurationBytes));
        }
        else if (!platformIo)
        {
            throw new InvalidDataException("ESP-IDF config/sdkconfig.json is required to rule out automatic secure-boot/eFuse changes before first boot.");
        }
        var files = manifest.GetProperty("flash_files");
        Object(files);
        var images = new List<EspFlashImage>();
        var descriptors = new HashSet<(ulong Address, string Path)>();
        foreach (var property in manifest.EnumerateObject())
        {
            if (allowed.Contains(property.Name)) continue;
            var descriptor = property.Value;
            ExactKeys(descriptor, "offset", "file", "encrypted");
            if (IsTrue(descriptor.GetProperty("encrypted")) || !IsFalse(descriptor.GetProperty("encrypted")))
            {
                throw new NotSupportedException("Encrypted ESP images and security/protection updates are refused by ordinary upload.");
            }
            descriptors.Add((Address(descriptor.GetProperty("offset").GetString()), Resolve(root, descriptor.GetProperty("file").GetString())));
        }
        foreach (var property in files.EnumerateObject())
        {
            if (images.Count >= 32) throw new InvalidDataException("ESP metadata has more than 32 images.");
            var address = Address(property.Name);
            var imagePath = Resolve(root, property.Value.GetString());
            if (!descriptors.Remove((address, imagePath)))
            {
                throw new InvalidDataException("Every ESP flash file needs a matching unencrypted offset/file descriptor.");
            }
            var bytes = ReadBoundFile(imagePath, 128 * 1024 * 1024);
            var length = (ulong)bytes.Length;
            if (address % 4096 != 0 || address >= capacity || length > capacity - address)
            {
                throw new InvalidDataException("ESP image must start on an erase-sector boundary and fit the declared flash capacity.");
            }
            images.Add(new(address, imagePath, length, Convert.ToHexStringLower(SHA256.HashData(bytes))));
            inputs.Add(Bound(imagePath, bytes));
        }
        if (images.Count < 2 || descriptors.Count != 0 || !manifest.TryGetProperty("bootloader", out _) || !manifest.TryGetProperty("app", out _) ||
            !manifest.TryGetProperty("partition-table", out _))
        {
            throw new InvalidDataException("Full ESP upload requires matching bootloader, application and partition-table metadata; no offsets are guessed.");
        }
        images.Sort((a, b) => a.Address.CompareTo(b.Address));
        for (var i = 1; i < images.Count; i++)
        {
            var previousEraseEnd = (images[i - 1].Address + images[i - 1].Length + 4095) & ~4095ul;
            if (previousEraseEnd > images[i].Address)
            {
                throw new InvalidDataException("ESP image bytes or erase sectors overlap.");
            }
        }
        ValidatePartitions(manifest, root, images, capacity);
        if (inputs.Select(input => input.Path).Distinct(StringComparer.Ordinal).Count() != inputs.Count)
        {
            throw new InvalidDataException("ESP flash metadata repeats an input file.");
        }
        return new(path, chip, ["--flash-mode", mode, "--flash-freq", frequency, "--flash-size", size], images, inputs, useStub,
            Normalize(extra.GetProperty("before").GetString()))
        {
            ApplicationPath = Resolve(root, manifest.GetProperty("app").GetProperty("file").GetString()),
        };
    }

    private static void ValidatePartitions(JsonElement metadata, string root, List<EspFlashImage> images, ulong capacity)
    {
        var tablePath = Resolve(root, metadata.GetProperty("partition-table").GetProperty("file").GetString());
        var bytes = ReadBoundFile(tablePath, 4096);
        var partitions = new List<(byte Type, byte Subtype, FlashAddressRange Range)>();
        for (var at = 0; at + 32 <= bytes.Length; at += 32)
        {
            var entry = bytes.AsSpan(at, 32);
            var magic = BinaryPrimitives.ReadUInt16LittleEndian(entry);
            if (magic == 0xFFFF)
            {
                if (bytes.AsSpan(at).ContainsAnyExcept((byte)0xFF))
                    throw new InvalidDataException("ESP partition table contains data after its end marker.");
                break;
            }
            if (magic == 0xEBEB)
            {
                // ESP-IDF's partition-table checksum is MD5 by format, not used as a trust/signature decision.
#pragma warning disable CA5351
                if (entry[2..16].ContainsAnyExcept((byte)0xFF)
                    || !MD5.HashData(bytes.AsSpan(0, at)).AsSpan().SequenceEqual(entry[16..])
                    || bytes.AsSpan(at + 32).ContainsAnyExcept((byte)0xFF))
#pragma warning restore CA5351
                {
                    throw new InvalidDataException("ESP partition table checksum differs from its entries.");
                }
                break;
            }
            // ESP-IDF emits bytes AA 50: its little-endian integer representation is 0x50AA.
            if (magic != 0x50AA) throw new InvalidDataException("ESP partition table has an unknown/corrupt entry.");
            if ((BinaryPrimitives.ReadUInt32LittleEndian(entry[28..]) & 1) != 0)
            {
                throw new NotSupportedException("Encrypted ESP partitions require a separately reviewed security workflow; ordinary upload refuses this build.");
            }
            var range = new FlashAddressRange(BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]), BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]));
            if (range.Start % 4096 != 0 || range.Length == 0 || range.Length % 4096 != 0 || range.End > capacity ||
                partitions.Any(partition => partition.Range.Start < range.End && range.Start < partition.Range.End))
            {
                throw new InvalidDataException("ESP partition ranges are unaligned, overlapping or outside declared flash.");
            }
            partitions.Add((entry[2], entry[3], range));
        }
        var app = Address(metadata.GetProperty("app").GetProperty("offset").GetString());
        if (!partitions.Any(partition => partition.Type == 0 && partition.Range.Start == app))
        {
            throw new InvalidDataException("The generated application offset is not the start of an actual application partition.");
        }
        var bootloader = Resolve(root, metadata.GetProperty("bootloader").GetProperty("file").GetString());
        foreach (var image in images)
        {
            var erased = new FlashAddressRange(image.Address, ((image.Length + 4095) & ~4095ul));
            var overlaps = partitions.Where(partition => partition.Range.Start < erased.End && erased.Start < partition.Range.End).ToArray();
            if (overlaps.Length == 0 && (image.Path == bootloader || image.Path == tablePath)) continue;
            if (overlaps.Length != 1 || !overlaps[0].Range.Contains(erased) ||
                !(overlaps[0].Type == 0 || overlaps[0] is { Type: 1, Subtype: 0 }))
            {
                throw new InvalidDataException("ESP image/erase sectors would overwrite NVS, keys, filesystems or another partition. Normal upload permits application partitions and explicit OTA initialization only.");
            }
        }
    }

    /// <summary>Builds one discontiguous write vector followed by a separate verified operation for every image.</summary>
    public static EmbeddedFlashPlan Plan(EmbeddedFlashRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SerialPort) || !System.IO.Path.IsPathRooted(request.SerialPort) || request.SerialPort.Any(char.IsControl))
        {
            throw new ArgumentException("Select an absolute serial bridge port.", nameof(request));
        }
        if (request.BaudRate is < 300 or > 4_000_000) throw new ArgumentOutOfRangeException(nameof(request));
        var manifest = Read(request.EspFlasherArguments!, request.Target is "FROM_METADATA" or "" ? null : request.Target);
        var steps = new List<ProcessStartSpec>();
        string[] Header(string before, string after) => ["--chip", manifest.Chip, "--port", request.SerialPort, "--baud", request.BaudRate.ToString(CultureInfo.InvariantCulture),
            "--before", before, "--after", after, .. manifest.UseStub ? Array.Empty<string>() : new[] { "--no-stub" }];
        ProcessStartSpec Command(string[] arguments) => new() { Program = "esptool.py", Arguments = arguments, WorkingDirectory = request.WorkingDirectory };
        steps.Add(Command([.. Header(manifest.Before, "no-reset"), "write-flash", .. manifest.FlashArguments,
            .. manifest.Images.SelectMany(image => new[] { "0x" + image.Address.ToString("X", CultureInfo.InvariantCulture), image.Path })]));
        for (var i = 0; i < manifest.Images.Count; i++)
        {
            var image = manifest.Images[i];
            steps.Add(Command([.. Header("no-reset", i + 1 == manifest.Images.Count ? "hard-reset" : "no-reset"), "verify-flash", .. manifest.FlashArguments,
                "0x" + image.Address.ToString("X", CultureInfo.InvariantCulture), image.Path]));
        }
        return new(steps, false, "Each metadata-declared image is verified using the same flash-header settings. Separate image offsets preserve unmentioned erase sectors, including NVS; no merged FF padding or whole-chip erase is used.",
            FirmwarePath: manifest.Path, FirmwareSha256: manifest.Inputs[0].Sha256) { FirmwareInputs = manifest.Inputs, TargetChip = manifest.Chip };
    }

    private static byte[] ReadBoundFile(string path, long maximum)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length <= 0 || file.Length > maximum || (file.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("ESP input must be a nonempty regular file within its size limit: " + path);
        }
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
        {
            if ((parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("ESP input paths cannot traverse symbolic links.");
        }
        return File.ReadAllBytes(path);
    }

    private static EmbeddedFirmwareInput Bound(string path, byte[] bytes) => new(path, Convert.ToHexStringLower(SHA256.HashData(bytes)));
    private static string Normalize(string? value) => value?.Replace('_', '-') ?? string.Empty;
    private static bool IsTrue(JsonElement element) => element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.String && element.GetString() is "true" or "y" or "1";
    private static bool IsFalse(JsonElement element) => element.ValueKind == JsonValueKind.False || element.ValueKind == JsonValueKind.String && element.GetString() is "false" or "n" or "0";
    private static void Object(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException("ESP metadata member must be an object.");
    }
    private static void ExactKeys(JsonElement element, params string[] keys)
    {
        Object(element);
        if (element.EnumerateObject().Count() != keys.Length || keys.Any(key => !element.TryGetProperty(key, out _)))
        {
            throw new NotSupportedException("ESP metadata contains unknown/missing options; arbitrary reset, encryption, protection and erase flags are refused.");
        }
    }
    private static void Unique(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("ESP metadata repeats a JSON property.");
                Unique(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) Unique(item);
        }
    }
    private static ulong Address(string? value)
    {
        if (value is null || !ulong.TryParse(value.AsSpan(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 2 : 0),
            value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out var address))
        {
            throw new InvalidDataException("ESP image offset is not an explicit unsigned byte address.");
        }
        return address;
    }
    private static string Resolve(string root, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || System.IO.Path.IsPathRooted(relative) || relative.Any(char.IsControl) || relative.Contains('\\') ||
            relative.Split('/').Any(part => part is ".." or "." or ""))
        {
            throw new InvalidDataException("ESP image paths must remain relative to their build metadata directory.");
        }
        var path = System.IO.Path.GetFullPath(relative, root);
        if (!path.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal)) throw new InvalidDataException("ESP image path leaves its build directory.");
        return path;
    }
}
