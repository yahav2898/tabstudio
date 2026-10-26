using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Data.Sqlite;
using TabStudio.App.ViewModels.Database;
using TabStudio.Database;

namespace TabStudio.App.Views.Database;

public sealed partial class DatabaseExplorerView : UserControl
{
    public DatabaseExplorerView() => InitializeComponent();
    private async void OnPickSqlite(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DatabaseExplorerViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "SQLite database", AllowMultiple = false });
        if (files.FirstOrDefault() is not { } file) return;
        if (file.TryGetLocalPath() is not { } path)
        {
            // A content URI cannot be opened by SQLite. Import an app-private copy for read/write access.
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabStudio", "Databases");
            Directory.CreateDirectory(folder); path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".db");
            await using var source = await file.OpenReadAsync(); await using var target = File.Create(path); await source.CopyToAsync(target);
        }
        vm.Provider = DatabaseProvider.SQLite; vm.ConnectionName = file.Name;
        vm.ConnectionString = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite }.ConnectionString;
    }
}
