set -x
dotnet build tests/TabStudio.App.Tests -c Release 2>&1 | grep -E " error |Build succeeded" | sort -u | head -30
dotnet test tests/TabStudio.App.Tests -c Release --no-build --filter "FullyQualifiedName~SimplePaint" 2>&1 | grep -E "Failed|Error Message|Assert|Total|Passed|Expected|Actual" | head -30
