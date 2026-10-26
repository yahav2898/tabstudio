using System.Text.Json;
using TabStudio.Toolchains.Projects;

// CI builds exactly the template and command the IDE uses, including its generated debug logcat provider.
var root = Path.GetFullPath(args[0]);
var capacitor = args.Length > 1 && args[1] == "capacitor";
if (capacitor) MobileTemplates.CreateCapacitor(root, "CapacitorSmoke");
else MobileTemplates.CreateAndroid(root, "MobileSmoke");
using var relay = new AndroidLogcatRelay(Console.WriteLine);
var init = relay.PrepareBuild(root);
var plan = GradleAndroidProject.Build(capacitor ? Path.Combine(root, "android") : root, linuxEnvironment: true);
// cap add creates its wrapper after this host-side planning step.
if (capacitor) plan = plan with { Program = "sh", Arguments = ["gradlew", .. plan.Arguments] };
plan = plan with { Arguments = [.. plan.Arguments, "--init-script", init] };
File.WriteAllText(Path.Combine(root, "build-plan.json"), JsonSerializer.Serialize(plan));
