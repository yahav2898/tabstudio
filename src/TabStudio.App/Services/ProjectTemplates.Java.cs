namespace TabStudio.App.Services;

public sealed partial class ProjectTemplates
{
    private static string CreateJavaProject(string root, string name)
    {
        File.WriteAllText(Path.Combine(root, "pom.xml"), $$"""
            <project xmlns="http://maven.apache.org/POM/4.0.0" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                     xsi:schemaLocation="http://maven.apache.org/POM/4.0.0 https://maven.apache.org/xsd/maven-4.0.0.xsd">
              <modelVersion>4.0.0</modelVersion>
              <groupId>com.example</groupId>
              <artifactId>{{name.ToLowerInvariant().Replace('.', '-')}}</artifactId>
              <version>1.0.0</version>
              <properties>
                <maven.compiler.release>21</maven.compiler.release>
                <project.build.sourceEncoding>UTF-8</project.build.sourceEncoding>
              </properties>
              <build><plugins><plugin>
                <groupId>org.apache.maven.plugins</groupId><artifactId>maven-compiler-plugin</artifactId><version>3.13.0</version>
                <configuration><debug>true</debug></configuration>
              </plugin></plugins></build>
            </project>

            """);
        var source = Path.Combine(root, "src", "main", "java", "com", "example");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "App.java"), """
            package com.example;

            import java.util.Scanner;

            // Ctrl+F5 runs; F5 debugs. Answer in the Output panel.
            public class App {
                static String greet(String name) {
                    int answer = 42;
                    String message = "Hello, " + name + "! This is Java on TabStudio. The answer is " + answer;
                    return message;
                }

                public static void main(String[] args) {
                    System.out.print("What is your name? ");
                    try (Scanner input = new Scanner(System.in)) {
                        String name = input.nextLine().trim();
                        System.out.println(greet(name.isEmpty() ? "world" : name));
                    }
                }
            }

            """);
        File.WriteAllText(Path.Combine(root, ".gitignore"), "target/\n.gradle/\nbuild/\n.tabstudio/jdtls/\n.tabstudio/java/\n.tabstudio/java-classpath.txt\n.tabstudio/java-runtime.init.gradle\n");
        return root;
    }
}
