using System.Text.Json;
using TabStudio.Toolchains.Projects;

// CI builds exactly the template and command the IDE uses, including its generated debug logcat provider.
var root = Path.GetFullPath(args[0]);
MobileTemplates.CreateAndroid(root, "MobileSmoke");
using var relay = new AndroidLogcatRelay(Console.WriteLine);
var init = relay.PrepareBuild(root);
var plan = GradleAndroidProject.Build(root, linuxEnvironment: true);
plan = plan with { Arguments = [.. plan.Arguments, "--init-script", init] };
File.WriteAllText(Path.Combine(root, "build-plan.json"), JsonSerializer.Serialize(plan));
