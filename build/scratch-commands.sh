set -x
export JAVA_HOME="${JAVA_HOME_21_X64:-$JAVA_HOME}"
echo "JAVA_HOME=$JAVA_HOME ANDROID_HOME=$ANDROID_HOME"
(dotnet workload install android || sudo -E "$(command -v dotnet)" workload install android) 2>&1 | tail -5
dotnet build src/TabStudio.Android -c Release -f net10.0-android -p:TreatWarningsAsErrors=true -p:TabStudioRuntimeIdentifiers=android-x64 > ci-out/android-build.log 2>&1; echo "exit $?"
grep -E ': (warning|error) ' ci-out/android-build.log | sort -u | head -60
tail -n 30 ci-out/android-build.log
