using TabStudio.App.Editing;
using TabStudio.App.Platform;

namespace TabStudio.App.Tests.Editing;

public sealed class FileTypesTests
{
    [Theory]
    [InlineData("Program.cs", "C#", true)]
    [InlineData("appsettings.json", "JSON", false)]
    [InlineData("App.csproj", "MSBuild", false)]
    [InlineData(".gitignore", "Ignore", false)]
    [InlineData("Dockerfile", "Dockerfile", false)]
    [InlineData("notes.unknown", "Plain Text", false)]
    public void Recognizes_files_by_name_and_extension(string name, string displayName, bool isCSharp)
    {
        var type = FileTypes.For(name);

        Assert.Equal(displayName, type.DisplayName);
        Assert.Equal(isCSharp, type.IsCSharp);
    }

    [Theory]
    [InlineData(2, false, DeviceClass.Compact)]
    [InlineData(4, false, DeviceClass.Standard)]
    [InlineData(4, true, DeviceClass.Compact)]
    [InlineData(6, false, DeviceClass.Standard)]
    [InlineData(8, false, DeviceClass.Large)]
    [InlineData(12, false, DeviceClass.Large)]
    public void Device_profiles_follow_memory(int gigabytes, bool lowRam, DeviceClass expected)
    {
        // Devices report a little less memory than their marketing size.
        var reported = (long)(gigabytes * 0.93 * 1024 * 1024 * 1024);

        var profile = DeviceProfile.For(new DeviceInfo(reported, 8, lowRam));

        Assert.Equal(expected, profile.Class);
        Assert.Equal(expected != DeviceClass.Compact, profile.AnalyzeWholeSolution);
    }
}
