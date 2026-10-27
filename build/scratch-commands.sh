set -x
dotnet build tests/TabStudio.App.Tests -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E "error|warning|Build succeeded" | sed 's|/home/runner/work/tabstudio/tabstudio/||' | sort -u | head -100
dotnet test tests/TabStudio.App.Tests -c Release --no-build --logger "console;verbosity=normal" --filter "FullyQualifiedName~WindowsFormsAppTests" 2>&1 | grep -vE "^\s*$" | grep -E "Failed|Error Message|Assert|at TabStudio|Exception|Total|Passed|Expected|Actual|Timed out" | head -120
