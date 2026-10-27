set -x
for p in tests/TabStudio.Packages.Tests tests/TabStudio.App.Tests tests/TabStudio.TestRunner.Tests tests/TabStudio.Notebooks.Tests; do
  dotnet build $p -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E "error|warn|Build succeeded|Time Elapsed" | sort -u | head -60
done
dotnet test tests/TabStudio.Packages.Tests -c Release --no-build --logger "console;verbosity=normal" --filter "FullyQualifiedName~RuntimeAsset|FullyQualifiedName~AssetSelector" 2>&1 | grep -E "Passed|Failed|Error|Assert|Expected|Actual|Total" | head -80
dotnet test tests/TabStudio.Packages.Tests -c Release --no-build 2>&1 | tail -5
find $NUGET_PACKAGES -maxdepth 1 -iname 'microsoft.data.sql*' -o -maxdepth 1 -iname 'sqlitepcl*' | head
grep -rn "SqlConnection\|SqliteConnection\|UseSqlServer" tests/TabStudio.App.Tests --include=*.cs -l | head
