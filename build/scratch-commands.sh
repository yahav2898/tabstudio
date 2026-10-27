set -x
docker run -d --name mssql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD=TabStudio-ci-1433 -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest > /dev/null 2>&1
dotnet build tests/TabStudio.App.Tests -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E "error|warn|Build succeeded|Time Elapsed" | sort -u | head -60
dotnet test tests/TabStudio.App.Tests -c Release --no-build --logger "console;verbosity=normal" --filter "FullyQualifiedName~SqlServerAdvice|FullyQualifiedName~NativePackageTests|FullyQualifiedName~WebAppTests|FullyQualifiedName~WorkspaceViewModelTests.F5" 2>&1 | grep -vE "^\s*$" | grep -v "^\s*Passed " | tail -60
export TABSTUDIO_SQLSERVER='Server=tcp:127.0.0.1,1433;Database=SimplCommerce;User ID=sa;Password=TabStudio-ci-1433;TrustServerCertificate=True;MultipleActiveResultSets=true'
export TABSTUDIO_REAL_REPOS_ONLY=SimplCommerce
python3 build/ci/real-repos.py "$RUNNER_TEMP/real-repos" > /dev/null
for i in $(seq 1 30); do docker exec mssql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P TabStudio-ci-1433 -Q 'SELECT 1' -b > /dev/null 2>&1 && break; sleep 3; done
python3 build/ci/real-repos.py --migrate "$RUNNER_TEMP/real-repos" 2>&1 | tail -n 3
mkdir -p ci-out/screenshots
export TABSTUDIO_REAL_REPOS="$RUNNER_TEMP/real-repos" TABSTUDIO_REAL_REPOS_OUT="$PWD/ci-out" TABSTUDIO_SCREENSHOTS="$PWD/ci-out/screenshots"
timeout 40m dotnet test tests/TabStudio.App.Tests -c Release --no-build --filter "FullyQualifiedName~RealRepositoryTests" --logger "console;verbosity=normal" > ci-out/test.log 2>&1
tail -n 8 ci-out/test.log
cat ci-out/summary.md
grep -n "Serve\|comes from the Database Explorer\|no engine" ci-out/SimplCommerce.txt | head -20
