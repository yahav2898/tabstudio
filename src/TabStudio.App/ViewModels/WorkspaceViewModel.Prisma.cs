using TabStudio.Services.Compose;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

/// <summary>
/// A Node.js app on Prisma, run against the database Services runs for its schema: the run's <c>DATABASE_URL</c> goes
/// into the project's <c>.env</c> when it has none, for Prisma's own commands and the app's later runs, and the
/// schema's migrations create the tables before the app starts. A database the project's <c>.env</c> names on this
/// device gets the migrations too, as its README asks (<c>npx prisma migrate deploy</c>); one on the network does not.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>
    /// Before a Node.js project runs: when Services started the database its Prisma schema names, fills the URL into
    /// <c>.env</c> and applies the migrations.
    /// </summary>
    /// <returns>The failed migration, or <see langword="null"/> when there was nothing to do or it succeeded.</returns>
    private async Task<OutputProcessResult?> PreparePrismaDatabaseAsync(ToolchainProject project, CancellationToken cancellationToken)
    {
        if (project.Toolchain.Id != "node" || PrismaProject.Detect(project.Directory) is not { UrlVariable: { } variable } prisma)
        {
            return null;
        }

        if (_serviceEnvironment.TryGetValue(variable, out var url) && url is not null)
        {
            if (prisma.FillEnvironmentFile(url, PrismaServices.IsOwn))
            {
                Output.WriteLine($"{variable} in .env now points at the {prisma.DatabaseName} database in Services.", OutputKind.Info);
            }
        }
        else if (prisma.EnvironmentFileUrl() is not { } configured || !PrismaServices.IsLocal(configured))
        {
            // A database on the network is the project's own business: its migrations are applied by whoever runs it.
            return null;
        }

        if (!prisma.HasMigrations)
        {
            return null;
        }

        StatusText = $"Applying {project.Name}'s database migrations";
        var migrate = WithServiceEnvironment(RunPlanner.PlanPrismaMigrate(project, prisma, _toolchains.Platform));
        Output.WriteLine("> " + migrate.CommandLine, OutputKind.Info);
        var result = await OutputProcess.RunAsync(_toolchains.Runtime, migrate, Output, null, cancellationToken, toolchain: project.Toolchain.Id);
        return result.Succeeded ? null : result;
    }
}
