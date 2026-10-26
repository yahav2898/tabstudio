namespace TabStudio.App.Services;

public sealed partial class ProjectTemplates
{
    private static string CreateFlutterProject(string root, string name)
    {
        var package = System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9_]", "_");
        Directory.CreateDirectory(Path.Combine(root, "lib"));
        Directory.CreateDirectory(Path.Combine(root, "web"));
        File.WriteAllText(Path.Combine(root, "pubspec.yaml"), $$"""
            name: {{package}}
            description: A Flutter app made with TabStudio.
            publish_to: none
            version: 1.0.0+1
            environment:
              sdk: '>=3.9.0 <4.0.0'
            dependencies:
              flutter:
                sdk: flutter
            flutter:
              uses-material-design: true

            """);
        File.WriteAllText(Path.Combine(root, "lib/main.dart"), """
            import 'package:flutter/material.dart';

            void main() => runApp(const CounterApp());

            class CounterApp extends StatelessWidget {
              const CounterApp({super.key});
              @override
              Widget build(BuildContext context) => MaterialApp(
                title: 'TabStudio Counter',
                theme: ThemeData(colorSchemeSeed: Colors.indigo),
                home: const CounterPage(),
              );
            }

            class CounterPage extends StatefulWidget {
              const CounterPage({super.key});
              @override
              State<CounterPage> createState() => _CounterPageState();
            }

            class _CounterPageState extends State<CounterPage> {
              int count = 0;
              @override
              Widget build(BuildContext context) => Scaffold(
                appBar: AppBar(title: const Text('TabStudio Counter')),
                body: Center(child: Text('Count: $count', style: const TextStyle(fontSize: 32))),
                floatingActionButton: FloatingActionButton(
                  tooltip: 'Increment',
                  onPressed: () => setState(() => count++),
                  child: const Icon(Icons.add),
                ),
              );
            }

            """);
        File.WriteAllText(Path.Combine(root, "web/index.html"), """
            <!DOCTYPE html>
            <html><head><base href="$FLUTTER_BASE_HREF"><meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>TabStudio Counter</title></head>
            <body><script src="flutter_bootstrap.js" async></script></body></html>

            """);
        File.WriteAllText(Path.Combine(root, ".gitignore"), ".dart_tool/\n.packages\nbuild/\n.flutter-plugins-dependencies\n");
        return root;
    }
}
