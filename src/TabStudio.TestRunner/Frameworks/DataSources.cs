using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using TabStudio.TestRunner.Execution;

namespace TabStudio.TestRunner.Frameworks;

/// <summary>
/// Reads data sources that name a member: xUnit's <c>[MemberData]</c>, NUnit's <c>[TestCaseSource]</c> and
/// MSTest's <c>[DynamicData]</c>.
/// </summary>
internal static class DataSources
{
    private const BindingFlags StaticMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Reads a static property, field or method by name, searching the type and then its base types, and returns
    /// the sequence it produces.
    /// </summary>
    /// <param name="declaringType">The type that declares the member, or a type derived from it.</param>
    /// <param name="memberName">The member's name.</param>
    /// <param name="arguments">Arguments for a method, or <see langword="null"/>.</param>
    /// <exception cref="InvalidOperationException">There is no such member, or it does not return a sequence.</exception>
    public static IEnumerable Read(Type declaringType, string memberName, IReadOnlyList<object?>? arguments)
    {
        for (var type = declaringType; type is not null; type = type.BaseType)
        {
            if (type.GetProperty(memberName, StaticMembers) is { } property && property.GetIndexParameters().Length == 0)
            {
                return AsSequence(property.GetValue(null), memberName, declaringType);
            }

            if (type.GetField(memberName, StaticMembers) is { } field)
            {
                return AsSequence(field.GetValue(null), memberName, declaringType);
            }

            var methods = type.GetMethods(StaticMembers).Where(m => m.Name == memberName).ToList();
            if (methods.Count > 0)
            {
                var values = arguments ?? [];
                var method = methods.Find(m => m.GetParameters().Length == values.Count) ?? methods[0];
                var bound = ArgumentBinder.Bind(method, values, out var closed);
                return AsSequence(UserCode.Invoke(closed, null, bound), memberName, declaringType);
            }
        }

        throw new InvalidOperationException(
            $"Could not find a static property, field or method named '{memberName}' on '{declaringType.FullName}'.");
    }

    /// <summary>Spreads a tuple into its elements, for data sources that yield tuples.</summary>
    /// <param name="item">An item of a data source.</param>
    /// <returns>The tuple's elements, or <see langword="null"/> when the item is not a tuple.</returns>
    public static object?[]? SpreadTuple(object? item)
    {
        if (item is not ITuple tuple)
        {
            return null;
        }

        var values = new object?[tuple.Length];
        for (var i = 0; i < tuple.Length; i++)
        {
            values[i] = tuple[i];
        }

        return values;
    }

    private static IEnumerable AsSequence(object? value, string memberName, Type type) =>
        value is IEnumerable sequence and not string
            ? sequence
            : throw new InvalidOperationException($"'{memberName}' on '{type.FullName}' must return a sequence of rows.");
}
