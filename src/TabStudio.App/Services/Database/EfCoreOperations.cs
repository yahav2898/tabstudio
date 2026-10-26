using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using TabStudio.Compilation.Building;
using TabStudio.Compilation.Running;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.Services.Database;

// Uses the same design-time operations as dotnet-ef, in the program's isolated load context.
// No SDK, shell, process, current-directory change or application entry point is required.
public static class EfCoreOperations
{
    public static object? Execute(BuildResult target, BuildResult startup, IReadOnlyList<string> packages,
        ProjectInfo project, string operation, IDictionary arguments)
    {
        if (startup.EntryAssembly is not { } entry || target.EntryAssembly is not { } targetEntry)
            throw new InvalidOperationException("Build the EF Core projects successfully before running a design operation.");
        var dependencies = startup.Dependencies.Concat(target.Dependencies).Append(targetEntry)
            .Where(a => a.Name != entry.Name).DistinctBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        using var program = LoadedProgram.Load(entry, dependencies, packages);
        var context = AssemblyLoadContext.GetLoadContext(program.EntryAssembly)!;
        using var reflection = context.EnterContextualReflection();
        var design = context.LoadFromAssemblyName(new AssemblyName("Microsoft.EntityFrameworkCore.Design"));
        var reportType = design.GetType("Microsoft.EntityFrameworkCore.Design.OperationReportHandler", throwOnError: true)!;
        // Do not retain verbose provider messages (which can contain passwords or SQL literals).
        var reporter = Activator.CreateInstance(reportType, [null, null, null, null])!;
        var executorType = design.GetType("Microsoft.EntityFrameworkCore.Design.OperationExecutor", throwOnError: true)!;
        var executor = Activator.CreateInstance(executorType, reporter, new Hashtable
        {
            ["targetName"] = targetEntry.Name, ["startupTargetName"] = entry.Name,
            ["project"] = project.FilePath, ["projectDir"] = project.DirectoryPath,
            ["rootNamespace"] = project.Properties.GetValueOrDefault("RootNamespace") ?? project.Name,
            ["language"] = "C#", ["nullable"] = project.Properties.GetValueOrDefault("Nullable") == "enable",
            // DbContext factories can consume these as an ordinary application's design-time args.
            ["remainingArguments"] = new[] { "--contentRoot", Path.GetDirectoryName(startupProjectPath())!, "--environment", "Development" },
        })!;
        var resultType = design.GetType("Microsoft.EntityFrameworkCore.Design.OperationResultHandler", throwOnError: true)!;
        var result = Activator.CreateInstance(resultType)!;
        var op = executorType.GetNestedType(operation) ?? throw new InvalidOperationException("Unknown EF Core operation.");
        Activator.CreateInstance(op, executor, result, arguments);
        if (resultType.GetProperty("ErrorType")!.GetValue(result) is string error)
            throw new InvalidOperationException($"EF Core {operation} failed ({error}). Check the context's design-time factory, configuration and provider.");
        // Materialize lazy enumerables before unloading the context. Dictionaries contain only BCL values.
        var value = resultType.GetProperty("Result")!.GetValue(result);
        return value is IEnumerable enumerable and not string and not IDictionary ? enumerable.Cast<object>().ToArray() : value;

        string startupProjectPath() => arguments["startupProjectPath"] as string ?? project.FilePath;
    }
}
