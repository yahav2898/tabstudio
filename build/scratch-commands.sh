set -x
for p in tests/TabStudio.Core.Tests tests/TabStudio.Wpf.Tests tests/TabStudio.App.Tests tests/TabStudio.Forms.Tests; do
  dotnet build $p -c Release -p:TreatWarningsAsErrors=true 2>&1 | grep -E "error|warning|Build succeeded" | sed 's|/home/runner/work/tabstudio/tabstudio/||' | sort -u | head -40
done
dotnet test tests/TabStudio.Core.Tests -c Release --no-build 2>&1 | grep -E "Failed |Error Message|Passed!|Failed!" | head -20
dotnet test tests/TabStudio.Forms.Tests -c Release --no-build 2>&1 | grep -E "Failed |Error Message|Passed!|Failed!" | head -20
dotnet test tests/TabStudio.App.Tests -c Release --no-build --filter "FullyQualifiedName~PreviewZoom|FullyQualifiedName~WebPreview|FullyQualifiedName~WpfPreviewFit|FullyQualifiedName~WindowsForms|FullyQualifiedName~LegacyFramework|FullyQualifiedName~SelfTest" 2>&1 | grep -E "Failed |Error Message|Assert|Passed!|Failed!" | head -40
