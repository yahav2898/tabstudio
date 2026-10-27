set -x
dotnet build tests/TabStudio.App.Tests -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E "error|warn|Build succeeded" | sort -u | head -60
dotnet build tests/TabStudio.Wpf.Tests -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E "error|warn|Build succeeded" | sort -u | head -60
dotnet build tests/TabStudio.Core.Tests -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E "error|warn|Build succeeded" | sort -u | head -60
dotnet build tests/TabStudio.Compilation.Tests -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E "error|warn|Build succeeded" | sort -u | head -60
dotnet test tests/TabStudio.Core.Tests -c Release --no-build 2>&1 | tail -3
dotnet test tests/TabStudio.Compilation.Tests -c Release --no-build --filter "FullyQualifiedName~Wpf" 2>&1 | tail -3
dotnet test tests/TabStudio.Wpf.Tests -c Release --no-build 2>&1 | grep -E "Failed |Passed!|Failed!|Assert" | head -30
dotnet test tests/TabStudio.App.Tests -c Release --no-build --filter "FullyQualifiedName~PreviewZoom|FullyQualifiedName~WebPreview|FullyQualifiedName~WpfPreviewFit|FullyQualifiedName~WebAppTests|FullyQualifiedName~BlazorAppTests|FullyQualifiedName~StaticSiteTests|FullyQualifiedName~LegacyFrameworkAppTests|FullyQualifiedName~Swarm|FullyQualifiedName~SelfTest|FullyQualifiedName~WpfXamlEditorTests|FullyQualifiedName~PanelAudit|FullyQualifiedName~Design" --logger "console;verbosity=normal" 2>&1 | grep -E "^\s+Failed |Error Message|Assert|Total tests|Passed:|Failed:|Skipped:" | head -80
ls ci-out/screenshots | grep -i -E "wpf-fit"
