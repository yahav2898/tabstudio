set -x
dotnet build tests/TabStudio.Forms.Tests -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E "error|warning|Build succeeded" | sed 's|/home/runner/work/tabstudio/tabstudio/||' | sort -u | head -150
dotnet test tests/TabStudio.Forms.Tests -c Release --no-build --logger "console;verbosity=normal" 2>&1 | grep -vE "^\s*$" | grep -E "Failed|Error Message|Assert|differs|reference|at TabStudio|Exception|Total|Passed!|Failed!|Expected|Actual|↓|↑" | head -150
