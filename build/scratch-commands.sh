set -x
for p in tests/TabStudio.Forms.Design.Tests tests/TabStudio.Compilation.Tests tests/TabStudio.App.Tests; do
  dotnet build $p -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E "error|warning|Build succeeded" | sed 's|/home/runner/work/tabstudio/tabstudio/||' | sort -u | head -60
done
dotnet test tests/TabStudio.Forms.Design.Tests -c Release --no-build 2>&1 | grep -E "Failed|Error Message|Assert|Total|Passed!|Failed!|Expected|Actual" | head -60
dotnet test tests/TabStudio.Compilation.Tests -c Release --no-build --filter "FullyQualifiedName~Resource|FullyQualifiedName~WindowsForms|FullyQualifiedName~Forms" 2>&1 | grep -E "Failed|Error Message|Assert|Total|Passed!|Failed!|Expected|Actual" | head -60
dotnet test tests/TabStudio.App.Tests -c Release --no-build --filter "FullyQualifiedName~FormsDesigner|FullyQualifiedName~LegacyFramework|FullyQualifiedName~Porting" 2>&1 | grep -E "Failed|Error Message|Assert|Total|Passed!|Failed!|Expected|Actual" | head -60
