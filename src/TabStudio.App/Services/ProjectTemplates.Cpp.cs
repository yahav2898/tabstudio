namespace TabStudio.App.Services;

public sealed partial class ProjectTemplates
{
    private static string CreateCppProject(string root)
    {
        File.WriteAllText(Path.Combine(root, "CMakeLists.txt"), """
            cmake_minimum_required(VERSION 3.16)
            project(Greeter LANGUAGES CXX)
            set(CMAKE_CXX_STANDARD 20)
            set(CMAKE_CXX_STANDARD_REQUIRED ON)
            set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
            add_executable(greeter main.cpp)

            """);
        File.WriteAllText(Path.Combine(root, "main.cpp"), """
            // Ctrl+F5 runs; F5 debugs. Answer in the Output panel.
            #include <iostream>
            #include <string>

            std::string greet(const std::string& name) {
                int answer = 42;
                std::string message = "Hello, " + name + "! This is C++ on TabStudio. The answer is " + std::to_string(answer);
                return message;
            }

            int main() {
                std::cout << "What is your name? " << std::flush;
                std::string name;
                std::getline(std::cin, name);
                if (name.empty()) name = "world";
                std::cout << greet(name) << std::endl;
                return 0;
            }

            """);
        File.WriteAllText(Path.Combine(root, ".gitignore"), ".tabstudio/bin/\n.tabstudio/cmake/\ncompile_commands.json\n");
        return root;
    }
}
