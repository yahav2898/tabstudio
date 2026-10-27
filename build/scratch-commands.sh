set -x
for p in Packages Compilation Core TestRunner Notebooks Debugging LiveTesting Web Wpf Uno Swarm Database Services App; do
  dotnet build tests/TabStudio.$p.Tests -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E " error | warning |Build succeeded" | sort -u | head -20
done
dotnet build src/TabStudio.Desktop -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E " error | warning |Build succeeded" | sort -u | head -20
for p in Packages Compilation Core TestRunner Notebooks Debugging LiveTesting Web Wpf Uno Swarm Database Services; do
  dotnet test tests/TabStudio.$p.Tests -c Release --no-build 2>&1 | grep -E "^(Passed|Failed)!|  Failed |error" | head -20
done
timeout 55m dotnet test tests/TabStudio.App.Tests -c Release --no-build --blame-hang-timeout 10m --blame-hang-dump-type none --logger "console;verbosity=minimal" 2>&1 | grep -E "^\s+Failed |^(Passed|Failed)!|Total tests|Error Message" -A3 | head -120
