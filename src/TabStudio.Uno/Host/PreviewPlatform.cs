#nullable enable

using System;
using Uno.ApplicationModel.Core;
using Uno.ApplicationModel.DataTransfer;
using Uno.Foundation.Logging;
using Uno.Helpers.Theming;
using Windows.ApplicationModel.DataTransfer;

namespace TabStudio.UnoHost;

/// <summary>How serious a message from Uno Platform is, as TabStudio reports it.</summary>
public enum PreviewLogLevel
{
    /// <summary>Worth knowing.</summary>
    Information,

    /// <summary>Something did not work as the app meant.</summary>
    Warning,

    /// <summary>Something failed.</summary>
    Error,
}

/// <summary>
/// What the app asks of the platform: to exit, which ends its run in the preview rather than TabStudio, the system
/// theme, which follows TabStudio's, and the clipboard, which the preview keeps in memory.
/// </summary>
internal sealed class PreviewPlatform : ICoreApplicationExtension, ISystemThemeHelperExtension, IClipboardExtension
{
    private SystemTheme _theme;
    private DataPackage? _clipboard;

    public PreviewPlatform(bool dark) => _theme = dark ? SystemTheme.Dark : SystemTheme.Light;

    public event EventHandler? SystemThemeChanged;

    public event EventHandler<object>? ContentChanged;

    public bool CanExit => true;

    /// <summary>The host's own application exited, which only TabStudio does when it closes.</summary>
    public void Exit() => PreviewHost.Report(PreviewLogLevel.Information, "Uno Platform stopped.", null);

    public SystemTheme GetSystemTheme() => _theme;

    /// <summary>Follows TabStudio's theme.</summary>
    public void SetTheme(bool dark)
    {
        var theme = dark ? SystemTheme.Dark : SystemTheme.Light;
        if (theme != _theme)
        {
            _theme = theme;
            SystemThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void StartContentChanged()
    {
    }

    public void StopContentChanged()
    {
    }

    public void Clear()
    {
        _clipboard = null;
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Flush() => ContentChanged?.Invoke(this, EventArgs.Empty);

    public DataPackageView GetContent() => (_clipboard ?? new DataPackage()).GetView();

    public void SetContent(DataPackage content)
    {
        _clipboard = content ?? throw new ArgumentNullException(nameof(content));
        ContentChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Sends Uno Platform's warnings and errors to TabStudio's Output, and its dispatcher's unhandled exceptions to the run.</summary>
internal sealed class PreviewLoggerFactory : IExternalLoggerFactory
{
    public IExternalLogger CreateLogger(string categoryName) => new PreviewLogger(categoryName);

    private sealed class PreviewLogger : IExternalLogger
    {
        private readonly string _category;

        public PreviewLogger(string category) => _category = category;

        public LogLevel LogLevel => LogLevel.Warning;

        public void Log(LogLevel logLevel, string? message, Exception? exception = null)
        {
            if (logLevel < LogLevel.Warning)
            {
                return;
            }

            // Uno catches dispatcher and routed-input exceptions; both must reach the running app.
            if (exception is not null && (_category.EndsWith("NativeDispatcher", StringComparison.Ordinal)
                || (_category.EndsWith("UIElement", StringComparison.Ordinal) && message?.StartsWith("Failed to raise '", StringComparison.Ordinal) == true)))
            {
                PreviewHost.ReportUnhandled(exception);
                return;
            }

            var level = logLevel >= LogLevel.Error ? PreviewLogLevel.Error : PreviewLogLevel.Warning;
            PreviewHost.Report(level, $"{_category}: {message}", exception);
        }
    }
}
