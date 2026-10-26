using System.Collections;
using Avalonia.Headless.XUnit;
using Microsoft.CodeAnalysis;
using TabStudio.App.Services.Database;
using TabStudio.App.ViewModels.Database;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Database;

public sealed class EfCoreTests
{
    [Fact] public async Task In_memory_build_discovers_context_scaffolds_migration_lists_it_and_updates_sqlite()
    {
        using var temp = new TempDirectory();
        var database = temp.Resolve("ef.db");
        var source = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Design;
            public sealed class Customer { public int Id { get; set; } public string Name { get; set; } = ""; }
            public sealed class ShopContext : DbContext {
              public DbSet<Customer> Customers => Set<Customer>();
              protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite("Data Source=DATABASE_PATH");
            }
            public sealed class ShopFactory : IDesignTimeDbContextFactory<ShopContext> {
              public ShopContext CreateDbContext(string[] args) => new ShopContext();
            }
            """.Replace("DATABASE_PATH", database.Replace("\\", "\\\\", StringComparison.Ordinal), StringComparison.Ordinal);
        var projectFile = temp.Write("Shop.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        temp.Write("Shop.cs", source);
        var project = ProjectParser.Load(projectFile);
        var assemblies = Directory.GetFiles(AppContext.BaseDirectory, "*.dll").Where(p => Path.GetFileName(p).StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) || Path.GetFileName(p).StartsWith("Microsoft.Extensions", StringComparison.Ordinal)).ToArray();
        var runtime = Directory.GetFiles(AppContext.BaseDirectory, "*.dll");
        var references = ReferenceAssemblies.LoadMapped(temp.Resolve("references"));
        var compiler = new CSharpCompiler(references);
        BuildResult Build()
        {
            var compiled = compiler.Compile(new CompilationRequest { AssemblyName = "Shop", Kind = AssemblyKind.Library,
                Documents = Directory.GetFiles(temp.Path, "*.cs", SearchOption.AllDirectories).Select(p => new SourceDocument(p, File.ReadAllText(p))).ToArray(),
                AdditionalReferences = assemblies.Select(p => MetadataReference.CreateFromFile(p)).ToArray() });
            Assert.True(compiled.Success, string.Join("\n", compiled.Diagnostics));
            return new BuildResult("Shop", compiled.Diagnostics, compiled.Assembly, [], compiled.Duration);
        }
        var build = Build();
        var contexts = Assert.IsType<object[]>(EfCoreOperations.Execute(build, build, runtime, project, "GetContextTypes", new Hashtable()));
        Assert.Equal("ShopContext", Assert.IsAssignableFrom<IDictionary>(Assert.Single(contexts))["FullName"]);
        EfCoreOperations.Execute(build, build, runtime, project, "AddMigration", new Hashtable { ["name"] = "InitialCreate", ["contextType"] = "ShopContext", ["outputDir"] = "Migrations", ["dryRun"] = false });
        Assert.Equal(3, Directory.GetFiles(temp.Resolve("Migrations"), "*.cs").Length);
        build = Build();
        var migrations = Assert.IsType<object[]>(EfCoreOperations.Execute(build, build, runtime, project, "GetMigrations", new Hashtable { ["contextType"] = "ShopContext", ["noConnect"] = true }));
        Assert.Contains("InitialCreate", (string)Assert.IsAssignableFrom<IDictionary>(Assert.Single(migrations))["Id"]!, StringComparison.Ordinal);
        EfCoreOperations.Execute(build, build, runtime, project, "UpdateDatabase", new Hashtable { ["contextType"] = "ShopContext" });
        var connection = new global::TabStudio.Database.DatabaseConnection(Guid.NewGuid(), "EF", global::TabStudio.Database.DatabaseProvider.SQLite, "Data Source=" + database);
        var schema = await new global::TabStudio.Database.SchemaReader().ReadAsync(connection);
        Assert.Contains(schema.Tables, t => t.Name == "Customers");
    }

    [AvaloniaFact] public async Task Ef_panel_lists_projects_contexts_migrations_and_passes_operations_to_the_build_host()
    {
        var calls = new List<string>();
        using var vm = new EfCoreViewModel((target, startup, operation, args, token) =>
        {
            calls.Add(operation);
            return Task.FromResult<object?>(operation switch
            {
                "GetContextTypes" => new[] { new Hashtable { ["Name"] = "Shop", ["FullName"] = "App.Shop" } },
                "GetMigrations" => new[] { new Hashtable { ["Id"] = "202610260001_Initial" } },
                _ => new Hashtable { ["MigrationFile"] = "Migrations/AddName.cs" },
            });
        });
        vm.LoadProjects([new TabStudio.Core.Workspaces.ProjectInfo { Name = "App", FilePath = "/App/App.csproj", Language = ProjectLanguage.CSharp }]);
        await vm.ListContextsCommand.ExecuteAsync(null); Assert.Equal("App.Shop", vm.SelectedContext!.FullName);
        await vm.ListMigrationsCommand.ExecuteAsync(null); Assert.Single(vm.Migrations);
        await vm.UpdateDatabaseCommand.ExecuteAsync(null); vm.MigrationName = "AddName"; await vm.AddMigrationCommand.ExecuteAsync(null);
        Assert.Equal(new[] { "GetContextTypes", "GetMigrations", "UpdateDatabase", "AddMigration" }, calls);
        Assert.Contains("Migration added", vm.Status, StringComparison.Ordinal);
    }
}
