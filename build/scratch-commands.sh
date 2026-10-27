set -x
python3 - <<'PY'
import json
p='build/ci/real-repos.json'
d=json.load(open(p))
d['repositories']=[r for r in d['repositories'] if r['name'] in ('SimplePaint','SimpleDrawingApp','WinFormsSnakeGame','WpfSimpleCalculator')]
json.dump(d,open(p,'w'))
PY
python3 build/ci/real-repos.py "$RUNNER_TEMP/real-repos"
mkdir -p ci-out/screenshots
TABSTUDIO_REAL_REPOS="$RUNNER_TEMP/real-repos" TABSTUDIO_REAL_REPOS_OUT="$PWD/ci-out" TABSTUDIO_SCREENSHOTS="$PWD/ci-out/screenshots" \
  dotnet test tests/TabStudio.App.Tests -c Release -p:TreatWarningsAsErrors=true --filter "FullyQualifiedName~RealRepositoryTests" 2>&1 | grep -E "error|Failed|Passed!|Total|improved|baseline" | head -40
cat ci-out/summary.md
for f in ci-out/*.txt; do echo "== $f"; grep -E "load error|Compatibility|: |errors|x " "$f" | head -40; done
