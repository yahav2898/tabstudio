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
        if (files.Count == 0) return;
        var file = files[0];
        if (file.TryGetLocalPath() is not { } path)
        {
            // A content URI cannot be opened by SQLite. Import an app-private copy for read/write access.
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TabStudio", "Databases");
            path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".db");
            try
            {
                Directory.CreateDirectory(folder);
                await using var source = await file.OpenReadAsync();
                await using var target = File.Create(path);
                await source.CopyToAsync(target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // A cloud file that would not stream, or a full disk, ended the app from this handler.
                try { File.Delete(path); } catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
                vm.Status = $"Could not copy '{file.Name}': {ex.Message}";
                return;
            }
        }
        vm.Provider = DatabaseProvider.SQLite; vm.ConnectionName = file.Name;
        vm.ConnectionString = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite }.ConnectionString;
    }
}
