namespace TabStudio.Compilation.Wpf;

/// <summary>The ids of the problems TabStudio reports for WPF projects, which build and run on OpenSilver.</summary>
public static class WpfDiagnosticIds
{
    /// <summary>OpenSilver's XAML compiler could not be loaded or crashed.</summary>
    public const string XamlCompilerFailed = "TS7001";

    /// <summary>OpenSilver, which a WPF project compiles against, is not restored, so its XAML cannot be compiled.</summary>
    public const string OpenSilverMissing = "TS7002";

    /// <summary>An error OpenSilver's XAML compiler reported in a XAML file without a code of its own.</summary>
    public const string XamlError = "TS7003";

    /// <summary>Code uses a WPF type or member that OpenSilver does not have.</summary>
    public const string UnsupportedApi = "TS7004";

    /// <summary>Code uses a WPF member OpenSilver has but marks as not implemented: it compiles and does nothing.</summary>
    public const string NotImplementedApi = "TS7005";

    /// <summary>A window's XAML sets something that has no meaning in the web preview, where the window fills the page.</summary>
    public const string WindowSettingIgnored = "TS7006";

    /// <summary>A XAML file is not well-formed, or names a type or member that does not exist.</summary>
    public const string InvalidXaml = "TS7007";

    /// <summary>A XAML event attribute names a method the class does not have.</summary>
    public const string MissingEventHandler = "TS7008";

    /// <summary>An error in the C# that OpenSilver's XAML compiler generated, placed in the XAML file it came from.</summary>
    public const string GeneratedCodeError = "TS7009";
}
