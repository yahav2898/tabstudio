using System.ComponentModel;
using Microsoft.Win32;

namespace Microsoft.VisualBasic.MyServices;

/// <summary>
/// The Windows registry (<c>My.Computer.Registry</c>). The tablet has none: every member throws
/// <see cref="NotSupportedException"/>. Keep settings in <c>My.Settings</c>, or in a file in
/// <c>My.Computer.FileSystem.SpecialDirectories.CurrentUserApplicationData</c>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public class RegistryProxy
{
    internal RegistryProxy()
    {
    }

    /// <summary>HKEY_CLASSES_ROOT. Not supported: there is no registry on the tablet.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public RegistryKey ClassesRoot => throw NoRegistry(nameof(ClassesRoot));

    /// <summary>HKEY_CURRENT_CONFIG. Not supported: there is no registry on the tablet.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public RegistryKey CurrentConfig => throw NoRegistry(nameof(CurrentConfig));

    /// <summary>HKEY_CURRENT_USER. Not supported: there is no registry on the tablet.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public RegistryKey CurrentUser => throw NoRegistry(nameof(CurrentUser));

    /// <summary>HKEY_LOCAL_MACHINE. Not supported: there is no registry on the tablet.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public RegistryKey LocalMachine => throw NoRegistry(nameof(LocalMachine));

    /// <summary>HKEY_PERFORMANCE_DATA. Not supported: there is no registry on the tablet.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public RegistryKey PerformanceData => throw NoRegistry(nameof(PerformanceData));

    /// <summary>HKEY_USERS. Not supported: there is no registry on the tablet.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public RegistryKey Users => throw NoRegistry(nameof(Users));

    /// <summary>Reads a value. Not supported: there is no registry on the tablet.</summary>
    /// <param name="keyName">The key.</param>
    /// <param name="valueName">The value's name.</param>
    /// <param name="defaultValue">The value when there is none.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public object? GetValue(string keyName, string valueName, object defaultValue) => throw NoRegistry(nameof(GetValue));

    /// <summary>Writes a value. Not supported: there is no registry on the tablet.</summary>
    /// <param name="keyName">The key.</param>
    /// <param name="valueName">The value's name.</param>
    /// <param name="value">The value.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void SetValue(string keyName, string valueName, object value) => throw NoRegistry(nameof(SetValue));

    /// <summary>Writes a value of a kind. Not supported: there is no registry on the tablet.</summary>
    /// <param name="keyName">The key.</param>
    /// <param name="valueName">The value's name.</param>
    /// <param name="value">The value.</param>
    /// <param name="valueKind">The kind of value.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public void SetValue(string keyName, string valueName, object value, RegistryValueKind valueKind) => throw NoRegistry(nameof(SetValue));

    private static NotSupportedException NoRegistry(string member) =>
        Unsupported.Member(
            $"My.Computer.Registry.{member}",
            "there is no Windows registry on the tablet. Keep settings in My.Settings, or in a file in My.Computer.FileSystem.SpecialDirectories.CurrentUserApplicationData.");
}
