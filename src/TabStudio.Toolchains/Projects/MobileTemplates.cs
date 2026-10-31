using System.Text.Json;

namespace TabStudio.Toolchains.Projects;

/// <summary>The mobile templates shared by the IDE and the ARM Alpine CI build.</summary>
public static class MobileTemplates
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    /// <summary>Writes a Kotlin Android application using a pinned Android Gradle plugin.</summary>
    public static void CreateAndroid(string directory, string name)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(name);
        var package = PackageName(name);
        Write(directory, "settings.gradle.kts", """
            pluginManagement { repositories { google(); mavenCentral(); gradlePluginPortal() } }
            dependencyResolutionManagement { repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS); repositories { google(); mavenCentral() } }
            rootProject.name = "TabStudioApp"
            include(":app")
            """);
        Write(directory, "build.gradle.kts", """
            plugins {
                id("com.android.application") version "8.7.3" apply false
                id("org.jetbrains.kotlin.android") version "2.0.21" apply false
            }
            """);
        Write(directory, "gradle.properties", """
            org.gradle.jvmargs=-Xmx1536m -Dfile.encoding=UTF-8
            org.gradle.workers.max=2
            org.gradle.vfs.watch=false
            kotlin.compiler.execution.strategy=in-process
            android.useAndroidX=true
            """);
        Write(directory, "app/build.gradle.kts", """
            plugins { id("com.android.application"); id("org.jetbrains.kotlin.android") }
            android {
                namespace = "com.example.tabstudioapp"
                compileSdk = 35
                buildToolsVersion = "35.0.0"
                defaultConfig {
                    applicationId = "com.example.tabstudioapp"
                    minSdk = 24
                    targetSdk = 35
                    versionCode = 1
                    versionName = "1.0"
                }
                compileOptions { sourceCompatibility = JavaVersion.VERSION_17; targetCompatibility = JavaVersion.VERSION_17 }
                kotlinOptions { jvmTarget = "17" }
            }
            """.Replace("com.example.tabstudioapp", package, StringComparison.Ordinal));
        Write(directory, "app/src/main/AndroidManifest.xml", """
            <manifest xmlns:android="http://schemas.android.com/apk/res/android">
              <application android:label="@string/app_name" android:theme="@android:style/Theme.Material.Light.NoActionBar">
                <activity android:name=".MainActivity" android:exported="true">
                  <intent-filter>
                    <action android:name="android.intent.action.MAIN" />
                    <category android:name="android.intent.category.LAUNCHER" />
                  </intent-filter>
                </activity>
              </application>
            </manifest>
            """);
        Write(directory, "app/src/main/res/values/strings.xml", $"<resources><string name=\"app_name\">{System.Security.SecurityElement.Escape(name)}</string></resources>\n");
        Write(directory, "app/src/main/java/" + package.Replace('.', '/') + "/MainActivity.kt", """
            package com.example.tabstudioapp

            import android.app.Activity
            import android.os.Bundle
            import android.util.Log
            import android.widget.Button
            import android.widget.LinearLayout
            import android.widget.TextView

            class MainActivity : Activity() {
                override fun onCreate(savedInstanceState: Bundle?) {
                    super.onCreate(savedInstanceState)
                    val layout = LinearLayout(this).apply { orientation = LinearLayout.VERTICAL; setPadding(32, 64, 32, 32) }
                    val title = TextView(this).apply { text = "Built on my tablet"; textSize = 28f }
                    var count = 0
                    val button = Button(this).apply {
                        text = "Tap me"
                        setOnClickListener { count++; title.text = "Tapped $count times"; Log.i("TabStudioApp", "Tapped $count times") }
                    }
                    layout.addView(title)
                    layout.addView(button)
                    setContentView(layout)
                    Log.i("TabStudioApp", "Hello from an app built with TabStudio")
                }
            }
            """.Replace("com.example.tabstudioapp", package, StringComparison.Ordinal));
        Write(directory, ".gitignore", ".gradle/\n**/build/\nlocal.properties\n*.jks\n");
    }

    /// <summary>
    /// The Capacitor template's <c>package-lock.json</c>, with the project's name. Without it every new project resolved
    /// Vite's and Capacitor's own dependencies afresh, so a package published minutes earlier was chosen while npm's
    /// registry still answered 404 for its download (rollup 4.67.3), and the template's first install failed. With the
    /// lock file the template installs the same, tested packages every time, with <c>npm ci</c>.
    /// </summary>
    /// <param name="packageName">The name in its <c>package.json</c>.</param>
    internal static string CapacitorLockFile(string packageName)
    {
        using var stream = typeof(MobileTemplates).Assembly.GetManifestResourceStream("TabStudio.Toolchains.Projects.capacitor-package-lock.json")
            ?? throw new InvalidOperationException("The Capacitor template's lock file is missing from the assembly.");
        var root = System.Text.Json.Nodes.JsonNode.Parse(stream)!.AsObject();
        root["name"] = packageName;
        root["packages"]![""]!["name"] = packageName;
        return root.ToJsonString(JsonOptions) + "\n";
    }

    /// <summary>Writes a Vite + TypeScript web app with Capacitor Android and iOS dependencies.</summary>
    public static void CreateCapacitor(string directory, string name)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(name);
        var packageName = name.ToLowerInvariant().Replace('.', '-').Replace('_', '-');
        Write(directory, "package.json", JsonSerializer.Serialize(new
        {
            name = packageName,
            version = "1.0.0",
            type = "module",
            scripts = new Dictionary<string, string>
            {
                ["start"] = "vite --host 127.0.0.1",
                ["dev"] = "vite --host 127.0.0.1",
                ["build"] = "tsc --noEmit && vite build",
                ["android"] = "npm run build && cap sync android",
            },
            dependencies = new Dictionary<string, string> { ["@capacitor/core"] = "6.2.1", ["@capacitor/android"] = "6.2.1", ["@capacitor/ios"] = "6.2.1" },
            devDependencies = new Dictionary<string, string> { ["@capacitor/cli"] = "6.2.1", ["typescript"] = "5.9.3", ["vite"] = "6.4.1" },
        }, JsonOptions) + "\n");
        Write(directory, "package-lock.json", CapacitorLockFile(packageName));
        Write(directory, "capacitor.config.json", JsonSerializer.Serialize(new { appId = PackageName(name), appName = name, webDir = "dist" }, JsonOptions) + "\n");
        Write(directory, "tsconfig.json", """{ "compilerOptions": { "target": "ES2022", "module": "ESNext", "moduleResolution": "Bundler", "strict": true, "lib": ["ES2022", "DOM"], "noEmit": true }, "include": ["src"] }""");
        Write(directory, "index.html", """
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>My Capacitor App</title></head>
            <body><main><h1>Built on my tablet</h1><p>A TypeScript app for the web, Android and iOS.</p><button id="counter">Tap me</button></main><script type="module" src="/src/main.ts"></script></body></html>
            """);
        Write(directory, "src/main.ts", """
            import './style.css';
            let count = 0;
            const button = document.querySelector<HTMLButtonElement>('#counter')!;
            button.addEventListener('click', () => { button.textContent = `Tapped ${++count} times`; });
            """);
        Write(directory, "src/style.css", """
            :root { font-family: system-ui, sans-serif; color: #e8eef9; background: #101b2c; }
            main { max-width: 36rem; margin: 12vh auto; padding: 2rem; }
            h1 { font-size: 2.4rem; } p { line-height: 1.6; color: #b4c5df; }
            button { padding: 1rem 1.5rem; border: 0; border-radius: .75rem; background: #9bceff; color: #10243d; font: inherit; cursor: pointer; }
            """);
        Write(directory, ".gitignore", "node_modules/\ndist/\nandroid/.gradle/\nandroid/**/build/\nandroid/local.properties\nios/App/Pods/\n*.p12\n*.mobileprovision\nAuthKey_*.p8\n");
    }

    // The prefix also keeps Java/Kotlin keywords valid; the alphabet works for Apple bundle IDs as well.
    private static string PackageName(string name) => "com.example.app" + new string(name.Where(char.IsAsciiLetterOrDigit).ToArray()).ToLowerInvariant();

    private static void Write(string root, string relative, string text)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text.ReplaceLineEndings("\n").TrimEnd() + "\n");
    }
}
