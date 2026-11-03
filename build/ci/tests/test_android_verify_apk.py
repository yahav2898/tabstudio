import os
from pathlib import Path
import stat
import subprocess
import tempfile
import textwrap
import unittest
import zipfile

SCRIPT = Path(__file__).parents[1] / "android-verify-apk.sh"

# Stand-ins for the Android SDK's aapt2 and apksigner and the JDK's keytool. aapt2's badging is far longer than a
# pipe holds, as a real APK's is, so a reader that stops at its first match leaves aapt2 writing into a closed pipe.
AAPT2 = """\
#!/usr/bin/env bash
case "$2" in
  badging)
    echo "package: name='com.tabstudio.ide' versionCode='1' versionName='0.21.10'"
    if [ "${FAKE_LAUNCHER:-1}" = 1 ]; then
      echo "launchable-activity: name='com.tabstudio.ide.MainActivity'  label='TabStudio' icon=''"
    fi
    yes "uses-feature-not-required: name='android.hardware.touchscreen.multitouch.distinct'" | head -n 50000
    ;;
  xmltree)
    cat <<'TREE'
N: android=http://schemas.android.com/apk/res/android (line=2)
  E: manifest (line=2)
    E: application (line=10)
      E: activity (line=11)
        A: http://schemas.android.com/apk/res/android:name(0x01010003)="com.tabstudio.ide.MainActivity" (Raw: "com.tabstudio.ide.MainActivity")
        A: http://schemas.android.com/apk/res/android:configChanges(0x0101001f)=0x50001400
TREE
    ;;
esac
"""

APKSIGNER = """\
#!/usr/bin/env bash
echo "Verifies"
echo "Signer #1 certificate SHA-256 digest: 0a1b2c"
"""

KEYTOOL = """\
#!/usr/bin/env bash
echo "Alias name: tabstudio"
echo "	 SHA256: 0A:1B:2C"
"""


class VerifyApkTests(unittest.TestCase):
    def run_script(self, **environment):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            tools = root / "sdk" / "build-tools" / "36.0.0"
            bin_folder = root / "bin"
            tools.mkdir(parents=True)
            bin_folder.mkdir()
            for path, text in ((tools / "aapt2", AAPT2), (tools / "apksigner", APKSIGNER), (bin_folder / "keytool", KEYTOOL)):
                path.write_text(text)
                path.chmod(path.stat().st_mode | stat.S_IXUSR)
            apk = root / "TabStudio.apk"
            with zipfile.ZipFile(apk, "w") as archive:
                archive.writestr("AndroidManifest.xml", "manifest")
                archive.writestr("lib/arm64-v8a/libTabStudio.so", "library")
            env = dict(os.environ, ANDROID_HOME=str(root / "sdk"), PATH=f"{bin_folder}{os.pathsep}{os.environ['PATH']}", **environment)
            return subprocess.run(["bash", str(SCRIPT), str(apk), str(root / "apk-info.txt")], cwd=root, env=env,
                                  capture_output=True, text=True, timeout=120)

    def test_a_good_apk_passes_however_long_its_badging_is(self):
        # grep -q stopped reading at the launcher's line, aapt2 died of SIGPIPE writing the rest, and pipefail
        # reported that the APK had no launcher activity.
        result = self.run_script()
        self.assertEqual(0, result.returncode, textwrap.shorten(result.stderr, 2000))
        self.assertIn("MainActivity handles its configuration changes itself (configChanges 0x50001400).", result.stdout)
        self.assertIn("Signed with the TabStudio development key.", result.stdout)

    def test_an_apk_without_the_launcher_activity_fails(self):
        result = self.run_script(FAKE_LAUNCHER="0")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("no launcher activity com.tabstudio.ide.MainActivity", result.stderr)


if __name__ == "__main__":
    unittest.main()
