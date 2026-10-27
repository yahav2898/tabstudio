set -x
for p in tests/TabStudio.Compilation.Tests tests/TabStudio.App.Tests; do
  dotnet build $p -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E "error|warn|Build succeeded|Time Elapsed" | sort -u | head -60
done
dotnet test tests/TabStudio.Compilation.Tests -c Release --no-build --logger "console;verbosity=normal" --filter "FullyQualifiedName~PackageNativeLibraries|FullyQualifiedName~ProgramRunner" 2>&1 | grep -E "Passed|Failed|Error|Assert|Expected|Actual|Total|at " | head -60
dotnet test tests/TabStudio.App.Tests -c Release --no-build --logger "console;verbosity=normal" --filter "FullyQualifiedName~NativePackageTests|FullyQualifiedName~WorkspaceViewModelTests.F5_builds" 2>&1 | grep -vE "^\s*$" | tail -80
dotnet test tests/TabStudio.TestRunner.Tests -c Release 2>&1 | tail -3
dotnet test tests/TabStudio.Notebooks.Tests -c Release 2>&1 | tail -3
