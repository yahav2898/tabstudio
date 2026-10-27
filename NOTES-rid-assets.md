# rid-assets progress notes (not for the final work; delete before the final commit)

Task: items 7 (RID-specific + native package assets) and 8 (honest SQL Server message). See agent prompt.

## Done (all on wip/rid-assets)
- Packages: RuntimeGraph (fallback portable graph subset + packages' runtime.json), AssetSelector runtimes/{rid}/lib + native,
  RestoreResult.NativeLibraries/RuntimeFiles, cache format 4. Unit tests RuntimeAssetTests (real layouts). PASS (b1).
- Compilation: PackageNativeLibraries resolver; ProgramLoadContext/TestLoadContext/PackageLoadContext/SubmissionLoadContext
  LoadUnmanagedDll. App consumers use RuntimeFiles; Swarm + wasm filter natives. IDE test NativePackageTests PASS (b2).
- Core: SqlServerConnectionString/Transport/Connection/Usage/Detector (+probe, Explain, IsSqlServerFailure),
  CompatibilityStatus.NeedsDatabase. Tests SqlServerDetectorTests PASS (b3).
- App: WorkspaceViewModel.SqlServer.cs (doctor row, F5 output, crash explanation, Connect to SQL Server..., Use PostgreSQL/SQLite),
  CompatibilityView buttons, DatabaseExplorer.BeginConnection/LoadSavedConnectionsAsync/SaveRunConnectionAsync.
  Test SqlServerAdviceTests.
- Real repos: SimplCommerce 3472ba02 with serve {project, path, status, contains, connections, migrate}; real-repos.py --migrate
  (dotnet-ef 8.0.11), ci.yml mssql service + TABSTUDIO_SQLSERVER; TABSTUDIO_REAL_REPOS_ONLY filter.
- Docs: ARCHITECTURE NuGet packages + Database Explorer + Run hosts; features/database.md.

## Next
- scratch rid-assets-b4: App tests for item 8 + SimplCommerce serve with docker mssql. Fix what fails.
- Final: remove this file, squash-free final commit, report.
