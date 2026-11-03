using System.Reflection;
using System.Windows.Forms;
using TabStudio.Forms.Design.Code;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Surface;

/// <summary>
/// The names a component's field cannot take: besides the other components', those of the members the form's class
/// declares in its code file and its designer file, such as a field or a method in Form1.cs, which would not compile
/// (CS0102), and those of the members it inherits, such as <c>Text</c> and <c>Size</c>, which the field would hide.
/// </summary>
internal sealed partial class DesignSurface
{
    /// <summary>The members every designer file declares, whichever way it is written.</summary>
    private static readonly string[] DesignerMembers = ["components", "InitializeComponent", "Dispose"];

    /// <summary>Whether a name is taken in the form's class, other than by a component that is allowed to keep it.</summary>
    /// <param name="document">The document as it is now.</param>
    /// <param name="name">The name.</param>
    /// <param name="except">A component that may keep its own name, such as the one being renamed, or <see langword="null"/>.</param>
    internal bool IsNameTaken(DesignDocument document, string name, string? except = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        return NameChecker(document, except)(name);
    }

    /// <summary>A name for a new component of a type that no member of the form's class has.</summary>
    /// <param name="document">The document as it is now.</param>
    /// <param name="type">The type.</param>
    internal string NewName(DesignDocument document, Type type) => document.NewName(type, NameChecker(document));

    /// <summary>
    /// What says whether a name is taken, with the files read once: a new name tries one name after another.
    /// </summary>
    /// <param name="document">The document as it is now.</param>
    /// <param name="except">A component that may keep its own name, or <see langword="null"/>.</param>
    internal Func<string, bool> NameChecker(DesignDocument document, string? except = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var comparer = _language == DesignerLanguage.VisualBasic ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var members = new HashSet<string>(DesignerMembers, comparer) { ClassName };

        // The designer file's other members, such as a field it declares that InitializeComponent does not create,
        // and the code file's: its fields, methods, properties and events.
        members.UnionWith(MemberNames(Text).Where(m => document.Find(m) is null));
        if (_codeText is { } code)
        {
            members.UnionWith(MemberNames(code));
        }

        members.UnionWith(InheritedMemberNames(document.Root.Type ?? (_rootTypeName.EndsWith("UserControl", StringComparison.Ordinal) ? typeof(UserControl) : typeof(Form))));
        if (except is not null)
        {
            members.Remove(except);
        }

        return name => (except is null || !comparer.Equals(name, except)) && (document.Find(name) is not null || members.Contains(name));
    }

    private IReadOnlySet<string> MemberNames(string text)
    {
        try
        {
            return _language == DesignerLanguage.VisualBasic ? VisualBasicCodeBehind.MemberNames(text, ClassName) : CodeBehind.MemberNames(text, ClassName);
        }
        catch (ArgumentException)
        {
            return new HashSet<string>();
        }
    }

    /// <summary>The public and protected members a type has, its own and those it inherits.</summary>
    private static IEnumerable<string> InheritedMemberNames(Type type) =>
        type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(IsVisibleToDerived)
            .Select(m => m.Name);

    private static bool IsVisibleToDerived(MemberInfo member) => member switch
    {
        FieldInfo field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly,
        MethodBase method => !method.IsSpecialName && (method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly),
        PropertyInfo property => (property.GetMethod ?? property.SetMethod) is { } accessor && (accessor.IsPublic || accessor.IsFamily || accessor.IsFamilyOrAssembly),
        EventInfo @event => @event.AddMethod is { } add && (add.IsPublic || add.IsFamily || add.IsFamilyOrAssembly),
        Type nested => nested.IsNestedPublic || nested.IsNestedFamily || nested.IsNestedFamORAssem,
        _ => false,
    };
}
