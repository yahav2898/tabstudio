using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TabStudio.Toolchains.Embedded.Projects;

/// <summary>An import decision that needs review, with its location in the original project.</summary>
/// <param name="File">The original project or source file.</param>
/// <param name="Line">The one-based XML line, or zero when the note applies to the project.</param>
/// <param name="Code">A stable diagnostic code.</param>
/// <param name="Message">What the user must check before using the generated build.</param>
public sealed record VendorImportNote(string File, int Line, string Code, string Message);

/// <summary>The independent GCC/CMake project produced by an import.</summary>
/// <param name="Directory">The newly created destination.</param>
/// <param name="CMakeFile">Its CMakeLists.txt.</param>
/// <param name="NotesFile">The machine-readable import report.</param>
/// <param name="Configuration">The selected vendor configuration.</param>
/// <param name="Notes">Every conversion caveat; an import is not a successful firmware build.</param>
public sealed record VendorImportResult(string Directory, string CMakeFile, string NotesFile, string Configuration, IReadOnlyList<VendorImportNote> Notes);

/// <summary>
/// Converts Keil uVision, IAR Embedded Workbench and Atmel Studio project metadata into a separate GCC/CMake
/// build. It never changes source files, executes project commands, or treats vendor linker scripts as GNU scripts.
/// </summary>
public static partial class VendorProjectImporter
{
    private const long MaxProjectBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions ReportOptions = new() { WriteIndented = true };

    /// <summary>Imports one selected configuration to a new sibling folder; an existing destination is refused.</summary>
    /// <param name="projectFile">An existing .uvprojx, .ewp or .cproj file.</param>
    /// <param name="destination">A new folder outside the original project directory.</param>
    /// <param name="configuration">An exact target/configuration name, or null only for single-configuration projects.</param>
    /// <exception cref="InvalidDataException">The format, configuration or path requires a user decision.</exception>
    public static VendorImportResult Import(string projectFile, string destination, string? configuration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        projectFile = Path.GetFullPath(projectFile);
        destination = Path.GetFullPath(destination);
        var original = Path.GetDirectoryName(projectFile) ?? throw new InvalidDataException("The project has no parent folder.");
        EnsureNoLinks(original);
        EnsureNoLinks(destination);
        if (IsWithin(destination, original) || Directory.Exists(destination) || File.Exists(destination))
        {
            throw new InvalidDataException("Import requires a new folder outside the original project; existing files are never overwritten.");
        }

        if ((File.GetAttributes(original) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Choose a project directory that is not a symbolic link.");
        }

        var info = new FileInfo(projectFile);
        if (info.Length > MaxProjectBytes)
        {
            throw new InvalidDataException("The project XML exceeds the 8 MiB import limit.");
        }

        using var reader = XmlReader.Create(projectFile, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxProjectBytes,
            IgnoreComments = true,
        });
        var document = XDocument.Load(reader, LoadOptions.SetLineInfo);
        var notes = new List<VendorImportNote>();
        var extension = Path.GetExtension(projectFile).ToLowerInvariant();
        var model = extension switch
        {
            ".uvprojx" => ReadKeil(document, projectFile, configuration, notes),
            ".ewp" => ReadIar(document, projectFile, configuration, notes),
            ".cproj" => ReadAtmel(document, projectFile, configuration, notes),
            _ => throw new InvalidDataException("Only .uvprojx, .ewp and .cproj projects can be imported."),
        };

        var sources = ResolvePaths(model.Sources, original, projectFile, notes, source: true);
        var includes = ResolvePaths(model.Includes, original, projectFile, notes, source: false);
        notes.Add(new(projectFile, 0, "EMBIMPORT001", "Review the MCU, startup code, vector table, linker script, ABI, clock and memory layout before flashing. A converted project is not verified firmware."));
        if (sources.Count == 0)
        {
            notes.Add(new(projectFile, 0, "EMBIMPORT002", "No supported C/C++/assembly source files were found. Add the project's generated or external sources."));
        }

        // Staging makes parse/validation failures leave no half-imported destination. A final atomic directory
        // rename also refuses a destination created by another import while this one was being prepared.
        var parent = Path.GetDirectoryName(destination) ?? throw new InvalidDataException("The destination has no parent folder.");
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, ".tabstudio-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            File.WriteAllText(Path.Combine(staging, "CMakeLists.txt"), GenerateCMake(model, sources, includes, destination), Encoding.UTF8);
            File.WriteAllText(Path.Combine(staging, "tabstudio-gcc.cmake"), GenerateToolchain(model.Avr), Encoding.UTF8);
            var report = new VendorImportResult(destination, Path.Combine(destination, "CMakeLists.txt"), Path.Combine(destination, "import-notes.json"), model.Configuration, notes);
            File.WriteAllText(Path.Combine(staging, "import-notes.json"), JsonSerializer.Serialize(report, ReportOptions), Encoding.UTF8);
            File.WriteAllText(Path.Combine(staging, "IMPORT.md"), GenerateReport(projectFile, model, notes), Encoding.UTF8);
            Directory.Move(staging, destination);
            return report;
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }
    }

    private static ImportModel ReadKeil(XDocument document, string file, string? selected, List<VendorImportNote> notes)
    {
        var targets = document.Descendants().Where(e => e.Name.LocalName == "Target").ToArray();
        var target = Select(targets, e => Child(e, "TargetName"), selected, "Keil target");
        var toolset = Child(target, "ToolsetName");
        if (toolset.Length > 0 && !toolset.Contains("ARM", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Only Keil ARM targets can be converted by this importer; C51/C166 require their own compiler and ABI.");
        }
        var sourceFiles = target.Descendants().Where(e => e.Name.LocalName == "File").ToArray();
        var sources = new List<string>();
        foreach (var source in sourceFiles)
        {
            if (source.Descendants().Any(e => e.Name.LocalName == "IncludeInBuild" && e.Value.Trim() == "0"))
            {
                Note(notes, file, source, "EMBIMPORT104", "Source excluded by the selected Keil target was omitted: " + Child(source, "FilePath"));
                continue;
            }

            if (Child(source, "FilePath") is { Length: > 0 } path)
            {
                sources.Add(path);
            }
        }
        var controls = target.Descendants().Where(e => e.Name.LocalName == "VariousControls").ToArray();
        var includes = controls.SelectMany(e => Children(e, "IncludePath")).SelectMany(e => SplitList(e.Value)).ToArray();
        var defines = controls.SelectMany(e => Children(e, "Define")).SelectMany(e => SplitList(e.Value)).ToArray();
        foreach (var item in target.Descendants().Where(e => e.Name.LocalName is "ScatterFile" or "MiscControls" && !string.IsNullOrWhiteSpace(e.Value)))
        {
            Note(notes, file, item, "EMBIMPORT101", item.Name.LocalName == "ScatterFile"
                ? "Keil scatter file is not a GNU linker script. Convert its regions/sections and provide a reviewed -T script."
                : "Vendor-specific compiler/assembler/linker options were not copied: " + item.Value);
        }

        notes.Add(new(file, Line(target), "EMBIMPORT102", "ARMCC/armclang language extensions, __attribute__/__asm syntax, libraries, runtime and assembly dialect need GCC review. Per-file vendor options are not translated."));
        AddCustomStepNotes(target, file, notes);
        var cpuText = target.Descendants().FirstOrDefault(e => e.Name.LocalName == "Cpu")?.Value ?? string.Empty;
        var cpu = CortexCpuRegex().Match(cpuText);
        var cpuName = cpu.Success ? cpu.Groups[1].Value.ToLowerInvariant().Replace("+", "plus", StringComparison.Ordinal) : null;
        if (cpuName is null)
        {
            notes.Add(new(file, 0, "EMBIMPORT103", "No unambiguous Cortex CPU was found. Set the GCC -mcpu option and floating-point ABI explicitly."));
        }

        return new(Child(target, "TargetName"), sources, includes, defines, cpuName, false, null);
    }

    private static ImportModel ReadIar(XDocument document, string file, string? selected, List<VendorImportNote> notes)
    {
        var configurations = document.Root?.Elements().Where(e => e.Name.LocalName == "configuration").ToArray() ?? [];
        var configuration = Select(configurations, e => Child(e, "name"), selected, "IAR configuration");
        var name = Child(configuration, "name");
        var sources = new List<string>();
        foreach (var source in document.Descendants().Where(e => e.Name.LocalName == "file"))
        {
            if (Children(source, "excluded").SelectMany(e => Children(e, "configuration")).Any(e => e.Value == name))
            {
                Note(notes, file, source, "EMBIMPORT203", "Source excluded by the selected IAR configuration was omitted: " + Child(source, "name"));
                continue;
            }

            if (Child(source, "name") is { Length: > 0 } path)
            {
                sources.Add(path);
            }
        }
        var options = configuration.Descendants().Where(e => e.Name.LocalName == "option").ToArray();
        string[] Values(params string[] names) => options.Where(e => names.Contains(Child(e, "name"), StringComparer.Ordinal))
            .SelectMany(e => Children(e, "state")).Select(e => e.Value).Where(s => s.Length > 0).ToArray();
        var toolchain = configuration.Descendants().FirstOrDefault(e => e.Name.LocalName == "toolchain");
        var avr = toolchain is not null && Child(toolchain, "name").Contains("AVR", StringComparison.OrdinalIgnoreCase);
        if (toolchain is null || !avr && !Child(toolchain, "name").Equals("ARM", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Only IAR ARM and AVR projects can be converted by this importer; select the original toolchain explicitly in the vendor project.");
        }
        foreach (var option in options.Where(e => Child(e, "name").Contains("IlinkIcf", StringComparison.OrdinalIgnoreCase) || Child(e, "name").Contains("Override", StringComparison.OrdinalIgnoreCase)))
        {
            Note(notes, file, option, "EMBIMPORT201", "IAR linker configuration/override needs manual GNU linker conversion: " + string.Join(", ", Children(option, "state").Select(e => e.Value)));
        }

        notes.Add(new(file, Line(configuration), "EMBIMPORT202", "IAR intrinsics, __root/__no_init/__interrupt, pragmas, runtime libraries, assembly and per-file exclusions need GCC review. CPU/ABI and a GNU linker script must be selected explicitly."));
        AddCustomStepNotes(configuration, file, notes);
        return new(name, sources, Values("CCIncludePath", "CCIncludePath2", "AUserIncludes"), Values("CCDefines", "CCDefines2", "ADefines"), null, avr, null);
    }

    private static ImportModel ReadAtmel(XDocument document, string file, string? selected, List<VendorImportNote> notes)
    {
        var groups = document.Descendants().Where(e => e.Name.LocalName == "PropertyGroup" && e.Attribute("Condition") is not null).ToArray();
        var names = groups.Select(e => ConfigurationRegex().Match(e.Attribute("Condition")?.Value ?? string.Empty))
            .Where(m => m.Success).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();
        if (selected is null && names.Length > 1)
        {
            throw new InvalidDataException("Select an Atmel configuration: " + string.Join(", ", names));
        }

        var name = selected ?? names.FirstOrDefault() ?? "Default";
        if (selected is not null && !names.Contains(selected, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Unknown Atmel configuration: " + selected);
        }

        var sourceElements = document.Descendants().Where(e => e.Name.LocalName == "Compile").ToArray();
        var sources = sourceElements.Where(e => !Children(e, "ExcludedFromBuild").Any(v => v.Value.Equals("true", StringComparison.OrdinalIgnoreCase)
                && (v.Attribute("Condition") is null || ConfigurationRegex().Match(v.Attribute("Condition")?.Value ?? string.Empty).Groups[1].Value == name)))
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty).Where(s => s.Length > 0).ToArray();
        var selectedGroups = document.Root?.Elements().Where(e => e.Name.LocalName == "PropertyGroup" && (e.Attribute("Condition") is null
            || ConfigurationRegex().Match(e.Attribute("Condition")?.Value ?? string.Empty).Groups[1].Value == name)).ToArray() ?? [];
        var options = selectedGroups.SelectMany(e => e.Descendants()).ToArray();
        var device = document.Descendants().FirstOrDefault(e => e.Name.LocalName is "avrdevice" or "Device")?.Value;
        var architecture = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "ToolchainName")?.Value ?? string.Empty;
        var avr = (architecture.Contains("AVR8", StringComparison.OrdinalIgnoreCase) || architecture.Contains("AVRGCC8", StringComparison.OrdinalIgnoreCase))
            || device is not null && (device.StartsWith("ATmega", StringComparison.OrdinalIgnoreCase) || device.StartsWith("ATtiny", StringComparison.OrdinalIgnoreCase));
        if (!avr && !architecture.Contains("ARM", StringComparison.OrdinalIgnoreCase)
            && !(device?.StartsWith("ATSAM", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            throw new InvalidDataException("Only Atmel AVR8 and ARM projects can be converted; AVR32 and unknown target architectures require another toolchain.");
        }
        var includes = options.Where(e => e.Name.LocalName.EndsWith("Directories", StringComparison.Ordinal))
            .SelectMany(e => e.Descendants().Where(v => v.Name.LocalName == "Value")).Select(e => e.Value).ToArray();
        var defines = options.Where(e => e.Name.LocalName.EndsWith("DefinedSymbols", StringComparison.Ordinal))
            .SelectMany(e => e.Descendants().Where(v => v.Name.LocalName == "Value")).Select(e => e.Value).ToArray();
        notes.Add(new(file, 0, "EMBIMPORT301", "Atmel Studio toolchain/device-pack paths, ASF-generated settings, per-file conditions, custom build steps and linker options need review. Restore the exact device headers/startup and select a GNU linker script."));
        AddCustomStepNotes(document.Root ?? throw new InvalidDataException("Missing XML root."), file, notes);
        return new(name, sources, includes, defines, null, avr, avr ? device?.ToLowerInvariant() : null);
    }

    private static XElement Select(XElement[] elements, Func<XElement, string> name, string? selected, string kind)
    {
        if (elements.Length == 0)
        {
            throw new InvalidDataException("The project has no " + kind + ".");
        }

        if (selected is null && elements.Length != 1)
        {
            throw new InvalidDataException("Select a " + kind + ": " + string.Join(", ", elements.Select(name)));
        }

        return selected is null ? elements[0] : elements.FirstOrDefault(e => name(e) == selected)
            ?? throw new InvalidDataException("Unknown " + kind + ": " + selected);
    }

    private static List<string> ResolvePaths(IEnumerable<string> values, string original, string project, List<VendorImportNote> notes, bool source)
    {
        var paths = new List<string>();
        foreach (var value in values.Distinct(StringComparer.Ordinal))
        {
            var path = value.Trim().Trim('"').Replace('\\', '/');
            path = path.Replace("$PROJ_DIR$", original, StringComparison.OrdinalIgnoreCase)
                .Replace("$(ProjectDir)", original + "/", StringComparison.OrdinalIgnoreCase);
            if (path.Length == 0)
            {
                continue;
            }

            if (path.Contains('$', StringComparison.Ordinal) || WindowsDriveRegex().IsMatch(path))
            {
                notes.Add(new(project, 0, "EMBIMPORT401", "Unresolved vendor/Windows path was excluded: " + value));
                continue;
            }

            path = Path.GetFullPath(path, original);
            if (source && Path.GetExtension(path).ToLowerInvariant() is not (".c" or ".cc" or ".cpp" or ".cxx" or ".s" or ".asm"))
            {
                notes.Add(new(project, 0, "EMBIMPORT402", "Non-source or proprietary input was excluded: " + value));
                continue;
            }

            if (!IsWithin(path, original))
            {
                notes.Add(new(project, 0, "EMBIMPORT403", "The generated build references a file/directory outside the original project: " + path));
            }

            if (source && !File.Exists(path) || !source && !Directory.Exists(path))
            {
                notes.Add(new(project, 0, "EMBIMPORT404", "Referenced path is missing; restore it before building: " + path));
            }

            if (source && Path.GetExtension(path).Equals(".asm", StringComparison.OrdinalIgnoreCase))
            {
                notes.Add(new(project, 0, "EMBIMPORT405", "Vendor .asm source is listed for review but excluded from GCC until translated: " + path));
                continue;
            }

            paths.Add(path);
        }

        return paths;
    }

    private static string GenerateCMake(ImportModel model, List<string> sources, List<string> includes, string destination)
    {
        var text = new StringBuilder("cmake_minimum_required(VERSION 3.20)\n# Review IMPORT.md before building or flashing.\nset(CMAKE_TOOLCHAIN_FILE \"${CMAKE_CURRENT_LIST_DIR}/tabstudio-gcc.cmake\" CACHE FILEPATH \"\")\nproject(imported_firmware LANGUAGES C CXX ASM)\nset(CMAKE_EXPORT_COMPILE_COMMANDS ON)\n");
        if (sources.Count == 0)
        {
            text.Append("message(FATAL_ERROR \"No source files were imported; see IMPORT.md\")\n");
            return text.ToString();
        }

        text.Append("add_executable(firmware\n");
        foreach (var source in sources)
        {
            text.Append("  ").Append(Quote(Path.GetRelativePath(destination, source).Replace('\\', '/'))).Append('\n');
        }

        text.Append(")\nset_target_properties(firmware PROPERTIES SUFFIX .elf)\n");
        AppendList(text, "target_include_directories", includes.Select(path => Path.GetRelativePath(destination, path).Replace('\\', '/')));
        AppendList(text, "target_compile_definitions", model.Defines);
        if (model.Cpu is not null)
        {
            text.Append("target_compile_options(firmware PRIVATE -mcpu=").Append(model.Cpu).Append(" -mthumb)\n")
                .Append("target_link_options(firmware PRIVATE -mcpu=").Append(model.Cpu).Append(" -mthumb)\n");
        }

        if (model.AvrDevice is not null && AvrDeviceRegex().IsMatch(model.AvrDevice))
        {
            text.Append("target_compile_options(firmware PRIVATE -mmcu=").Append(model.AvrDevice).Append(")\n")
                .Append("target_link_options(firmware PRIVATE -mmcu=").Append(model.AvrDevice).Append(")\n");
        }

        text.Append("# Add the reviewed GNU linker script, CPU/FPU ABI, startup code and libraries here.\n")
            .Append("target_compile_options(firmware PRIVATE -ffunction-sections -fdata-sections -fstack-usage)\n")
            .Append("target_link_options(firmware PRIVATE -Wl,--gc-sections -Wl,-Map=${CMAKE_CURRENT_BINARY_DIR}/firmware.map)\n");
        return text.ToString();
    }

    private static void AppendList(StringBuilder text, string command, IEnumerable<string> values)
    {
        var items = values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
        if (items.Length == 0)
        {
            return;
        }

        text.Append(command).Append("(firmware PRIVATE\n");
        foreach (var item in items)
        {
            text.Append("  ").Append(Quote(item)).Append('\n');
        }

        text.Append(")\n");
    }

    private static string GenerateToolchain(bool avr)
    {
        var prefix = avr ? "avr" : "arm-none-eabi";
        return "set(CMAKE_SYSTEM_NAME Generic)\nset(CMAKE_SYSTEM_PROCESSOR " + (avr ? "avr" : "arm") + ")\n"
            + "set(CMAKE_TRY_COMPILE_TARGET_TYPE STATIC_LIBRARY)\n"
            + "set(CMAKE_C_COMPILER " + prefix + "-gcc)\nset(CMAKE_CXX_COMPILER " + prefix + "-g++)\nset(CMAKE_ASM_COMPILER " + prefix + "-gcc)\n";
    }

    private static string GenerateReport(string project, ImportModel model, List<VendorImportNote> notes) =>
        "# GCC/CMake import\n\nOriginal: " + project + "\n\nConfiguration: " + model.Configuration
        + "\n\nThe original files are unchanged and referenced by relative paths. This build is a starting point; no hardware compatibility or successful link is implied.\n\n"
        + "Configure with `cmake -S . -B build`, then `cmake --build build`. Review the toolchain and target options before either step.\n\n"
        + string.Join("\n", notes.Select(note => "- " + note.Code + " (line " + note.Line + "): " + note.Message)) + "\n";

    // Bracket arguments prevent CMake expansion, semicolon-list splitting, command injection and quote escaping.
    private static string Quote(string value)
    {
        var equals = string.Empty;
        while (value.Contains("]" + equals + "]", StringComparison.Ordinal))
        {
            equals += "=";
        }

        return "[" + equals + "[" + value + "]" + equals + "]";
    }

    private static bool IsWithin(string path, string root) => path.Equals(root, StringComparison.Ordinal)
        || path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static void EnsureNoLinks(string path)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            if (Path.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Project and destination paths must not pass through symbolic links: " + current);
            }
        }
    }

    private static string[] SplitList(string text) => text.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    private static IEnumerable<XElement> Children(XElement element, string name) => element.Elements().Where(e => e.Name.LocalName == name);
    private static string Child(XElement element, string name) => Children(element, name).FirstOrDefault()?.Value ?? string.Empty;
    private static int Line(XElement element) => element is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
    private static void Note(List<VendorImportNote> notes, string file, XElement element, string code, string message) => notes.Add(new(file, Line(element), code, message));

    private static void AddCustomStepNotes(XElement element, string file, List<VendorImportNote> notes)
    {
        foreach (var item in element.Descendants().Where(e => e.Name.LocalName is "UserProg1Name" or "UserProg2Name" or "PreBuildEvent" or "PostBuildEvent" or "CustomBuild" && !string.IsNullOrWhiteSpace(e.Value)))
        {
            Note(notes, file, item, "EMBIMPORT501", "Custom build command was not executed or copied: " + item.Value);
        }
    }

    [GeneratedRegex("(Cortex-M[0-9]+(?:plus|\\+)?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CortexCpuRegex();

    [GeneratedRegex("^[A-Za-z]:[/\\\\]")]
    private static partial Regex WindowsDriveRegex();

    [GeneratedRegex("==\\s*'([^'|]+)\\|", RegexOptions.CultureInvariant)]
    private static partial Regex ConfigurationRegex();

    [GeneratedRegex("^[a-z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AvrDeviceRegex();

    private sealed record ImportModel(string Configuration, IReadOnlyList<string> Sources, IReadOnlyList<string> Includes, IReadOnlyList<string> Defines, string? Cpu, bool Avr, string? AvrDevice);
}
