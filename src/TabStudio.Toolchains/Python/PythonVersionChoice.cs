using System.Globalization;

namespace TabStudio.Toolchains.Python;

/// <summary>The Python a project's environment is created with, and why.</summary>
/// <param name="Minor">The minor version, such as 11 for Python 3.11.</param>
/// <param name="UsesSystemPython">Whether it is the toolchain's own <c>python3</c>, which needs no download.</param>
/// <param name="Reason">Why this version, for the Output panel, in lower case, such as "pyproject.toml allows Python &gt;=3.9,&lt;3.12".</param>
public sealed record PythonVersionChoice(int Minor, bool UsesSystemPython, string Reason)
{
    /// <summary>The CPython 3 minor versions uv installs: its musl builds on the tablet, and glibc ones elsewhere.</summary>
    public static IReadOnlyList<int> ManagedMinors { get; } = [9, 10, 11, 12, 13, 14];

    /// <summary>The version, such as <c>3.11</c>.</summary>
    public string Version => "3." + Minor.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Chooses the Python for a project: the one it asks for, and within what it allows, the newest on which its
    /// pinned packages install from wheels, rather than always the newest Python.
    /// </summary>
    /// <param name="request">What the project asks for, or <see langword="null"/>.</param>
    /// <param name="pins">The newest version the pinned packages install on without compiling, from <see cref="PythonWheels.NewestSupported"/>, with the packages that decided it.</param>
    /// <param name="systemMinor">The toolchain's own Python's minor version, such as 14, or <see langword="null"/> when unknown.</param>
    /// <param name="available">The versions that can be installed besides it; <see cref="ManagedMinors"/> by default.</param>
    /// <returns>The choice, or <see langword="null"/> to use the toolchain's own Python without saying more.</returns>
    public static PythonVersionChoice? Choose(PythonVersionRequest? request, (int Minor, IReadOnlyList<PythonRelease> Limiting)? pins, int? systemMinor, IReadOnlyList<int>? available = null)
    {
        available ??= ManagedMinors;
        var candidates = available.Concat(systemMinor is { } own ? [own] : []).Distinct().Order().ToList();
        if (request?.Range.ExactMinor is { } exact)
        {
            return new(exact, exact == systemMinor, $"{request.Source} asks for Python {request.Range.Text}");
        }

        var allowed = request is null ? candidates : [.. candidates.Where(request.Range.Allows)];
        if (allowed.Count == 0)
        {
            return null;
        }

        var asked = request is null ? null : $"{request.Source} allows Python {request.Range.Text}";
        if (pins is { } limit && allowed.Where(m => m <= limit.Minor).DefaultIfEmpty(-1).Max() is var newest and >= 0)
        {
            if (newest == allowed.Max() && request is null && newest == systemMinor)
            {
                return null;
            }

            var names = string.Join(", ", limit.Limiting.Take(3).Select(r => $"{r.Name} {r.Version}"));
            var why = $"the newest Python the pinned packages install on from prebuilt wheels ({names}{(limit.Limiting.Count > 3 ? ", …" : string.Empty)})";
            return new(newest, newest == systemMinor, asked is null ? why : $"{asked}; {why}");
        }

        var chosen = systemMinor is { } system && allowed.Contains(system) ? system : allowed.Max();
        return request is null && chosen == systemMinor ? null : new(chosen, chosen == systemMinor, asked ?? $"Python 3.{chosen}");
    }
}
