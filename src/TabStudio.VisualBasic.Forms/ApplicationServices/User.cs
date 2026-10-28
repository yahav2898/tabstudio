using System.ComponentModel;
using System.Security.Principal;

namespace Microsoft.VisualBasic.ApplicationServices;

/// <summary>
/// The current user (<c>My.User</c>): the principal of the current thread. Windows Forms apps that use Windows
/// authentication, and programs that set no principal, see the device's user, who is signed in.
/// </summary>
public class User
{
    /// <summary>Creates the user object, as the My namespace does.</summary>
    public User()
    {
    }

    /// <summary>The principal of the current thread, or the device's user when the program set none.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public IPrincipal CurrentPrincipal
    {
        get => InternalPrincipal;
        set => InternalPrincipal = value;
    }

    /// <summary>Whether the user is authenticated: the device's user is.</summary>
    public bool IsAuthenticated => InternalPrincipal.Identity?.IsAuthenticated ?? false;

    /// <summary>The user's name: the device's user name unless the program set a principal of its own.</summary>
    public string? Name => InternalPrincipal.Identity?.Name;

    /// <summary>The principal behind the other members: the current thread's.</summary>
    protected virtual IPrincipal InternalPrincipal
    {
        get => Thread.CurrentPrincipal ?? DevicePrincipal();
        set => Thread.CurrentPrincipal = value;
    }

    /// <summary>Whether the user is in a role of the program's principal. The device's user has no roles.</summary>
    /// <param name="role">The role.</param>
    public bool IsInRole(string role) => InternalPrincipal.IsInRole(role);

    /// <summary>The device's user, signed in and without roles: what Windows authentication stands for on the tablet.</summary>
    internal static IPrincipal DevicePrincipal() =>
        new GenericPrincipal(new GenericIdentity(Environment.UserName, "TabStudio"), []);
}
