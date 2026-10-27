using Android.App;
using Android.Content;
using Android.Database;
using Android.OS;
using TabStudio.App.Platform;
using Uri = Android.Net.Uri;

namespace TabStudio.Android;

/// <summary>
/// Images on Android's clipboard. Android's clipboard holds images as content addresses: a copied image is written
/// to the app's cache and offered through <see cref="ClipboardImageProvider"/>, which other apps may read, and a
/// pasted one is read from whichever app's provider offers it.
/// </summary>
/// <param name="context">The app's context.</param>
public sealed class AndroidImageClipboard(Context context) : IImageClipboard
{
    /// <summary>The authority of the provider that offers copied images.</summary>
    public const string Authority = "com.tabstudio.ide.clipboard";

    private const int KeptImages = 4;

    /// <summary>The folder copied images are written to.</summary>
    internal static string Folder(Context context) => Path.Combine(context.CacheDir!.AbsolutePath, "clipboard");

    /// <inheritdoc />
    public Task SetImageAsync(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        var folder = Folder(context);
        Directory.CreateDirectory(folder);

        // Keep the last few, so an app still reading an earlier copy finds it.
        foreach (var old in new DirectoryInfo(folder).GetFiles("image-*.png").OrderByDescending(f => f.LastWriteTimeUtc).Skip(KeptImages - 1))
        {
            old.Delete();
        }

        var name = $"image-{DateTime.UtcNow.Ticks}.png";
        File.WriteAllBytes(Path.Combine(folder, name), png);
        var uri = Uri.Parse($"content://{Authority}/{name}")!;
        var manager = (ClipboardManager)context.GetSystemService(Context.ClipboardService)!;
        manager.PrimaryClip = ClipData.NewUri(context.ContentResolver, "Image", uri);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetImageAsync()
    {
        var manager = (ClipboardManager)context.GetSystemService(Context.ClipboardService)!;
        if (manager.PrimaryClip is not { } clip)
        {
            return null;
        }

        for (var i = 0; i < clip.ItemCount; i++)
        {
            if (clip.GetItemAt(i)?.Uri is not { } uri)
            {
                continue;
            }

            var type = context.ContentResolver?.GetType(uri);
            if (type is not null && !type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var input = context.ContentResolver?.OpenInputStream(uri);
            if (input is null)
            {
                continue;
            }

            using var bytes = new MemoryStream();
            await input.CopyToAsync(bytes);
            return bytes.ToArray();
        }

        return null;
    }
}

/// <summary>Offers the images TabStudio's apps copy to other apps, read-only, from the app's cache.</summary>
[ContentProvider(new[] { AndroidImageClipboard.Authority }, Exported = false, GrantUriPermissions = true)]
public sealed class ClipboardImageProvider : ContentProvider
{
    /// <inheritdoc />
    public override bool OnCreate() => true;

    /// <inheritdoc />
    public override string? GetType(Uri uri) => "image/png";

    /// <inheritdoc />
    public override ParcelFileDescriptor? OpenFile(Uri uri, string mode)
    {
        var file = FileOf(uri) ?? throw new Java.IO.FileNotFoundException(uri.ToString());
        return ParcelFileDescriptor.Open(new Java.IO.File(file), ParcelFileMode.ReadOnly);
    }

    /// <inheritdoc />
    public override ICursor? Query(Uri uri, string[]? projection, string? selection, string[]? selectionArgs, string? sortOrder)
    {
        if (FileOf(uri) is not { } file)
        {
            return null;
        }

        // OpenableColumns: the display name and size any app may ask a content provider for.
        var cursor = new MatrixCursor(["_display_name", "_size"]);
        cursor.AddRow(new Java.Lang.Object[] { new Java.Lang.String(Path.GetFileName(file)), Java.Lang.Long.ValueOf(new FileInfo(file).Length) });
        return cursor;
    }

    /// <inheritdoc />
    public override Uri? Insert(Uri uri, ContentValues? values) => null;

    /// <inheritdoc />
    public override int Delete(Uri uri, string? selection, string[]? selectionArgs) => 0;

    /// <inheritdoc />
    public override int Update(Uri uri, ContentValues? values, string? selection, string[]? selectionArgs) => 0;

    private string? FileOf(Uri uri)
    {
        if (Context is not { } context || uri.LastPathSegment is not { } name || name.Contains('/', StringComparison.Ordinal) || name.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var path = Path.Combine(AndroidImageClipboard.Folder(context), name);
        return File.Exists(path) ? path : null;
    }
}
