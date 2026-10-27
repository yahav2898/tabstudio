set -x
dotnet build tests/TabStudio.App.Tests -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E " error | warning |Build succeeded" | sort -u | head -20
dotnet test tests/TabStudio.App.Tests -c Release --no-build --filter "FullyQualifiedName~SqlServerAdvice|FullyQualifiedName~NativePackageTests" 2>&1 | grep -E "^(Passed|Failed)!|  Failed " | head
