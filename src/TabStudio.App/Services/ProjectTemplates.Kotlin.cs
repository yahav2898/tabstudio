namespace TabStudio.App.Services;

/// <summary>
/// Kotlin on the JVM: a Gradle (Kotlin DSL) console app with a JUnit test, and a Kotlin script. Both run with the
/// Kotlin compiler and the JDK from the Toolchain Store.
/// </summary>
public sealed partial class ProjectTemplates
{
    /// <summary>A Kotlin console app built with Gradle's Kotlin DSL.</summary>
    public const string KotlinId = "kotlin";

    /// <summary>A Kotlin script, <c>.main.kts</c>.</summary>
    public const string KotlinScriptId = "kotlin-script";

    /// <summary>The Kotlin version of new Kotlin projects: the compiler the Toolchain Store installs.</summary>
    public const string KotlinVersion = "2.4.21";

    private static ProjectTemplate[] KotlinTemplates =>
    [
        new(KotlinId, "Kotlin Console App (Gradle)", "A Kotlin/JVM app built with Gradle's Kotlin DSL, with a JUnit test, kotlin-lsp IntelliSense and JVM debugging.", ["Kotlin", "Gradle", "Console"]),
        new(KotlinScriptId, "Kotlin Script", "A .main.kts script run with the Kotlin compiler, line by line, without a build.", ["Kotlin", "Script"]),
    ];

    private static string CreateKotlinProject(string root, string name)
    {
        File.WriteAllText(Path.Combine(root, "settings.gradle.kts"), $"""
            rootProject.name = "{name}"

            """);
        File.WriteAllText(Path.Combine(root, "build.gradle.kts"), $$"""
            plugins {
                kotlin("jvm") version "{{KotlinVersion}}"
                application
            }

            repositories {
                mavenCentral()
            }

            dependencies {
                testImplementation(kotlin("test"))
            }

            application {
                mainClass.set("MainKt")
            }

            tasks.test {
                useJUnitPlatform()
            }

            """);
        var source = Path.Combine(root, "src", "main", "kotlin");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Main.kt"), """
            // Ctrl+F5 runs; F5 debugs with breakpoints, locals and the call stack. Answer in the Output panel.
            // Test Explorer runs the JUnit tests in src/test/kotlin.

            fun greet(name: String): String {
                val answer = 42
                val message = "Hello, $name! This is Kotlin ${KotlinVersion.CURRENT} on TabStudio. The answer is $answer"
                return message
            }

            fun main() {
                print("What is your name? ")
                val name = readlnOrNull()?.trim().orEmpty()
                println(greet(name.ifEmpty { "world" }))
            }

            """);
        var tests = Path.Combine(root, "src", "test", "kotlin");
        Directory.CreateDirectory(tests);
        File.WriteAllText(Path.Combine(tests, "GreetTest.kt"), """
            import kotlin.test.Test
            import kotlin.test.assertTrue

            class GreetTest {
                @Test
                fun `greets by name`() {
                    assertTrue(greet("Ada").startsWith("Hello, Ada!"))
                }
            }

            """);
        File.WriteAllText(Path.Combine(root, ".gitignore"), ".gradle/\nbuild/\n.kotlin/\n.tabstudio/\n");
        return root;
    }

    private static string CreateKotlinScript(string root, string name)
    {
        File.WriteAllText(Path.Combine(root, name + ".main.kts"), """
            // Press F5 to run this script with the Kotlin compiler. Scripts need no build file:
            // add dependencies with @file:DependsOn("group:artifact:version").

            import java.time.LocalDate

            val languages = listOf("Kotlin", "Java", "Swift", "PHP")
            println("Today is ${LocalDate.now()}.")
            for ((index, language) in languages.withIndex()) {
                println("${index + 1}. $language")
            }

            """);
        File.WriteAllText(Path.Combine(root, ".gitignore"), ".tabstudio/\n");
        return root;
    }
}
