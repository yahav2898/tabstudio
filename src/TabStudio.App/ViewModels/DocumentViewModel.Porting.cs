using System.Text;
using TabStudio.App.Editing;
using TabStudio.App.Platform;
using TabStudio.App.Services;

namespace TabStudio.App.ViewModels;

public sealed partial class DocumentViewModel
{
    private DocumentViewModel(PortSummaryViewModel summary, string path, DeviceProfile profile)
        : this(path, new TextFileContent(string.Empty, new UTF8Encoding(false)), null, profile)
    {
        PortSummary = summary;
        FileType = new FileType("Port summary", "AV", BadgeKind.Markup, null, null, null);
    }

    public PortSummaryViewModel? PortSummary { get; }

    public static DocumentViewModel ForPortSummary(PortSummaryViewModel summary, string path, DeviceProfile profile) => new(summary, path, profile);
}
