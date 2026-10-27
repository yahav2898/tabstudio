set -x
for p in tests/TabStudio.Core.Tests tests/TabStudio.Compilation.Tests tests/TabStudio.App.Tests; do
  dotnet build $p -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E "error|warn|Build succeeded|Time Elapsed" | sort -u | head -60
done
dotnet test tests/TabStudio.Core.Tests -c Release --no-build --logger "console;verbosity=normal" --filter "FullyQualifiedName~SqlServer" 2>&1 | grep -E "Failed|Error|Assert|Expected|Actual|Total|  at |Passed:" | head -60
dotnet test tests/TabStudio.Compilation.Tests -c Release --no-build --logger "console;verbosity=normal" --filter "FullyQualifiedName~PackageNativeLibraries" 2>&1 | grep -E "Failed|Error|Assert|Expected|Actual|Total|Passed:" | head -40
dotnet test tests/TabStudio.App.Tests -c Release --no-build --logger "console;verbosity=normal" --filter "FullyQualifiedName~SqlServerAdvice|FullyQualifiedName~NativePackageTests|FullyQualifiedName~WindowsBridgeIde|FullyQualifiedName~ServicesTests" 2>&1 | grep -vE "^\s*$" | tail -60
dotnet test tests/TabStudio.Core.Tests -c Release --no-build 2>&1 | tail -3
