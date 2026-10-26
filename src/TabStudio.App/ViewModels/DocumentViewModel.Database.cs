using System.Text;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels.Database;

namespace TabStudio.App.ViewModels;

public sealed partial class DocumentViewModel
{
    public DatabaseQueryViewModel? DatabaseQuery { get; private set; }
    public static DocumentViewModel ForDatabase(DatabaseQueryViewModel query, string path, DeviceProfile profile)
    {
        var document = new DocumentViewModel(path, new TextFileContent("-- " + query.Heading.Replace("\n", " ", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal) + "\nSELECT ", new UTF8Encoding(false)), null, profile)
        { DatabaseQuery = query };
        return document;
    }
}
