namespace TabStudio.WindowsBridge.Windows.Tests;

/// <summary>A test of the bridge on real Windows: skipped on every other system.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WindowsFactAttribute : FactAttribute
{
    /// <inheritdoc />
    public override string? Skip
    {
        get => base.Skip ?? (OperatingSystem.IsWindows() ? null : "Needs Windows: the Windows CI job runs it.");
        set => base.Skip = value;
    }
}
