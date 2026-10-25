namespace TabStudio.App.Services;

public sealed partial class ProjectTemplates
{
    private static string CreateRustProject(string root, string name)
    {
        File.WriteAllText(Path.Combine(root, "Cargo.toml"), $"[package]\nname = \"{name.ToLowerInvariant().Replace('.', '-')}\"\nversion = \"0.1.0\"\nedition = \"2024\"\n");
        Directory.CreateDirectory(Path.Combine(root, "src"));
        File.WriteAllText(Path.Combine(root, "src", "main.rs"), """
            // Ctrl+F5 runs; F5 debugs. Type your answer in the Output panel.
            use std::io::{self, Write};

            fn greet(name: &str) {
                let answer: i32 = 42;
                println!("Hello, {}! This is Rust on TabStudio. The answer is {}.", name, answer);
            }

            fn main() {
                print!("What is your name? ");
                io::stdout().flush().unwrap();
                let mut name = String::new();
                io::stdin().read_line(&mut name).unwrap();
                let name = name.trim();
                greet(if name.is_empty() { "world" } else { name });
            }

            """);
        File.WriteAllText(Path.Combine(root, ".gitignore"), "target/\n");
        return root;
    }
}
