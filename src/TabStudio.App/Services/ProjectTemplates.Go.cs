namespace TabStudio.App.Services;

public sealed partial class ProjectTemplates
{
    private static string CreateGoProject(string root, string name)
    {
        File.WriteAllText(Path.Combine(root, "go.mod"), $"module example.com/{name.ToLowerInvariant()}\n\ngo 1.24\n");
        File.WriteAllText(Path.Combine(root, "main.go"), """
            // Press Ctrl+F5 to run, or F5 to debug. Answer in the Output panel.
            package main

            import (
                "bufio"
                "fmt"
                "os"
                "strings"
            )

            func greet(name string) string {
                message := "Hello, " + name + "! This is Go on TabStudio."
                return message
            }

            func main() {
                fmt.Print("What is your name? ")
                name, _ := bufio.NewReader(os.Stdin).ReadString('\n')
                name = strings.TrimSpace(name)
                if name == "" {
                    name = "world"
                }
                fmt.Println(greet(name))
            }

            """);
        File.WriteAllText(Path.Combine(root, ".gitignore"), ".tabstudio/bin/\n");
        return root;
    }
}
