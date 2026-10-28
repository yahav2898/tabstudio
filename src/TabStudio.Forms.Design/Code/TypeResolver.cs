using System.Collections.Concurrent;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// Finds the types designer code names, such as <c>Button</c>, <c>System.Drawing.Point</c> or <c>AnchorStyles</c>,
/// among the Forms layer's and .NET's, as the usings of a Windows Forms project see them. The app's own types are
/// not looked up: the designer never runs the app's code.
/// </summary>
internal static class TypeResolver
{
    /// <summary>The namespaces a designer file sees: those Windows Forms projects import, and those designer code names.</summary>
    private static readonly string[] Namespaces =
    [
        "System.Windows.Forms", "System.Drawing", "System.ComponentModel", "System", "System.Drawing.Drawing2D",
        "System.Drawing.Imaging", "System.Drawing.Text", "System.Collections.Generic",
    ];

    private static readonly Dictionary<string, Type> Keywords = new(StringComparer.Ordinal)
    {
        ["bool"] = typeof(bool), ["byte"] = typeof(byte), ["sbyte"] = typeof(sbyte), ["char"] = typeof(char),
        ["short"] = typeof(short), ["ushort"] = typeof(ushort), ["int"] = typeof(int), ["uint"] = typeof(uint),
        ["long"] = typeof(long), ["ulong"] = typeof(ulong), ["float"] = typeof(float), ["double"] = typeof(double),
        ["decimal"] = typeof(decimal), ["string"] = typeof(string), ["object"] = typeof(object),
    };

    /// <summary>Visual Basic's names of the types it has keywords for, which ignore case as Visual Basic does.</summary>
    private static readonly Dictionary<string, Type> VisualBasicKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Boolean"] = typeof(bool), ["Byte"] = typeof(byte), ["SByte"] = typeof(sbyte), ["Char"] = typeof(char),
        ["Short"] = typeof(short), ["UShort"] = typeof(ushort), ["Integer"] = typeof(int), ["UInteger"] = typeof(uint),
        ["Long"] = typeof(long), ["ULong"] = typeof(ulong), ["Single"] = typeof(float), ["Double"] = typeof(double),
        ["Decimal"] = typeof(decimal), ["String"] = typeof(string), ["Object"] = typeof(object), ["Date"] = typeof(DateTime),
    };

    private static readonly System.Reflection.Assembly[] Assemblies =
    [
        typeof(Form).Assembly,
        typeof(Point).Assembly,
        typeof(Component).Assembly,
        typeof(object).Assembly,
        typeof(ISupportInitialize).Assembly,
        typeof(TypeConverter).Assembly,
    ];

    private static readonly ConcurrentDictionary<string, Type?> Cache = new(StringComparer.Ordinal);

    /// <summary>The type a name means, or <see langword="null"/> when it is not one the designer knows.</summary>
    /// <param name="name">
    /// The name as the code writes it, with or without its namespace or <c>global::</c> (Visual Basic's <c>Global.</c>);
    /// a C# or Visual Basic keyword, such as <c>int</c> or <c>Integer</c>.
    /// </param>
    public static Type? Resolve(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Cache.GetOrAdd(name, static written =>
        {
            var clean = written.Replace("global::", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
            if (clean.StartsWith("Global.", StringComparison.OrdinalIgnoreCase))
            {
                clean = clean["Global.".Length..];
            }

            if (Keywords.TryGetValue(clean, out var keyword) || VisualBasicKeywords.TryGetValue(clean, out keyword))
            {
                return keyword;
            }

            if (Find(clean) is { } full)
            {
                return full;
            }

            foreach (var ns in Namespaces)
            {
                if (Find(ns + "." + clean) is { } found)
                {
                    return found;
                }
            }

            return null;
        });
    }

    /// <summary>The name to write a type with in a style: <c>Button</c>, or <c>System.Windows.Forms.Button</c>.</summary>
    /// <param name="type">The type.</param>
    /// <param name="fullNames">Whether types are written with their namespace.</param>
    public static string NameOf(Type type, bool fullNames)
    {
        ArgumentNullException.ThrowIfNull(type);
        foreach (var (keyword, keywordType) in Keywords)
        {
            if (keywordType == type)
            {
                return keyword;
            }
        }

        var name = (type.FullName ?? type.Name).Replace('+', '.');
        if (fullNames)
        {
            return name;
        }

        // A project with implicit usings imports System, System.Drawing and System.Windows.Forms.
        return type.Namespace is "System" or "System.Drawing" or "System.Windows.Forms" ? name[(type.Namespace.Length + 1)..] : name;
    }

    /// <summary>
    /// The name to write a type with in Visual Basic in a style: <c>Integer</c> for the types it has keywords for,
    /// otherwise <c>Button</c> or <c>System.Windows.Forms.Button</c>.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <param name="fullNames">Whether types are written with their namespace.</param>
    public static string VisualBasicNameOf(Type type, bool fullNames)
    {
        ArgumentNullException.ThrowIfNull(type);
        foreach (var (keyword, keywordType) in VisualBasicKeywords)
        {
            if (keywordType == type)
            {
                return keyword;
            }
        }

        return NameOf(type, fullNames);
    }

    private static Type? Find(string fullName)
    {
        foreach (var assembly in Assemblies)
        {
            if (assembly.GetType(fullName, throwOnError: false) is { } type)
            {
                return type;
            }
        }

        // A nested type, such as ListBox.ObjectCollection, has a plus in its runtime name.
        var dot = fullName.LastIndexOf('.');
        return dot > 0 && Find(fullName[..dot]) is { } outer ? outer.GetNestedType(fullName[(dot + 1)..]) : null;
    }
}
