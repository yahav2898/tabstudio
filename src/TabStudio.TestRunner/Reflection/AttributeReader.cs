using System.Reflection;

namespace TabStudio.TestRunner.Reflection;

/// <summary>
/// Reads one attribute of a user's test code by name, without referencing the framework that defines it.
/// </summary>
/// <remarks>
/// Values come from the attribute's metadata (<see cref="CustomAttributeData"/>) first, which never runs code.
/// When a value is not spelled out in the metadata, for example because a derived attribute sets
/// <c>Skip</c> in its constructor, the attribute is instantiated once and the property is read from the instance.
/// </remarks>
internal sealed class AttributeReader
{
    private readonly CustomAttributeData _data;
    private object? _instance;
    private bool _instantiated;

    private AttributeReader(CustomAttributeData data)
    {
        _data = data;
    }

    /// <summary>The attribute's type.</summary>
    public Type Type => _data.AttributeType;

    /// <summary>Reads every attribute applied directly to a type, method or other member.</summary>
    /// <param name="member">The member.</param>
    public static List<AttributeReader> Of(MemberInfo member)
    {
        try
        {
            return member.GetCustomAttributesData().Select(d => new AttributeReader(d)).ToList();
        }
        catch (Exception ex) when (ex is TypeLoadException or FileNotFoundException or FileLoadException or BadImageFormatException or CustomAttributeFormatException)
        {
            // An attribute whose assembly cannot be loaded cannot mark a test either.
            return [];
        }
    }

    /// <summary>Reads every attribute applied to an assembly.</summary>
    /// <param name="assembly">The assembly.</param>
    public static List<AttributeReader> Of(Assembly assembly)
    {
        try
        {
            return assembly.GetCustomAttributesData().Select(d => new AttributeReader(d)).ToList();
        }
        catch (Exception ex) when (ex is TypeLoadException or FileNotFoundException or FileLoadException or BadImageFormatException or CustomAttributeFormatException)
        {
            return [];
        }
    }

    /// <summary>Whether this attribute's type, or one of its base types, has the given full name.</summary>
    /// <param name="fullName">A full name such as <c>Xunit.FactAttribute</c>.</param>
    public bool Is(string fullName) => TypeMatching.Is(Type, fullName);

    /// <summary>Whether this attribute's type implements an interface with the given full name.</summary>
    /// <param name="fullName">The interface's full name.</param>
    public bool Implements(string fullName) => TypeMatching.FindInterface(Type, fullName) is not null;

    /// <summary>
    /// The constructor arguments as values, with a <c>params</c> array spread into its elements, the way
    /// <c>[InlineData(1, 2)]</c> lists two values. A <c>params</c> array passed as <see langword="null"/>
    /// counts as one <see langword="null"/> value, as every framework treats <c>[InlineData(null)]</c>.
    /// </summary>
    public object?[] ExpandedArguments()
    {
        var parameters = _data.Constructor.GetParameters();
        var values = new List<object?>();
        for (var i = 0; i < _data.ConstructorArguments.Count; i++)
        {
            var value = Normalize(_data.ConstructorArguments[i]);
            var isParams = i == parameters.Length - 1 && parameters[i].IsDefined(typeof(ParamArrayAttribute), inherit: false);
            if (!isParams)
            {
                values.Add(value);
            }
            else if (value is Array array)
            {
                foreach (var element in array)
                {
                    values.Add(element);
                }
            }
            else
            {
                values.Add(null);
            }
        }

        return [.. values];
    }

    /// <summary>The constructor arguments as values, without spreading a <c>params</c> array.</summary>
    public object?[] ConstructorArguments() => _data.ConstructorArguments.Select(Normalize).ToArray();

    /// <summary>The first constructor argument of the given type, or <see langword="null"/>.</summary>
    /// <typeparam name="T">The argument type, such as <see cref="string"/> or <see cref="System.Type"/>.</typeparam>
    public T? Argument<T>()
        where T : class
    {
        foreach (var value in ConstructorArguments())
        {
            if (value is T typed)
            {
                return typed;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads a named property or field: from the attribute's metadata when it was set there, otherwise from an
    /// instance of the attribute.
    /// </summary>
    /// <param name="name">The property or field name, such as <c>Skip</c>.</param>
    /// <returns>The value, or <see langword="null"/> when the attribute has no such member or it could not be read.</returns>
    public object? Named(string name)
    {
        foreach (var argument in _data.NamedArguments)
        {
            if (string.Equals(argument.MemberName, name, StringComparison.Ordinal))
            {
                return Normalize(argument.TypedValue);
            }
        }

        return Property(name);
    }

    /// <summary>Whether a named property or field was set explicitly where the attribute is applied.</summary>
    /// <param name="name">The property or field name.</param>
    public bool IsSet(string name) =>
        _data.NamedArguments.Any(a => string.Equals(a.MemberName, name, StringComparison.Ordinal));

    /// <summary>Reads a property of an instance of the attribute, creating it on first use.</summary>
    /// <param name="name">The property name.</param>
    /// <returns>The value, or <see langword="null"/> when there is no such property or reading it failed.</returns>
    public object? Property(string name)
    {
        if (Instance is not { } instance)
        {
            return null;
        }

        try
        {
            return Type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);
        }
        catch (Exception ex) when (ex is TargetInvocationException or MethodAccessException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// An instance of the attribute, created from its metadata the way the runtime does, or <see langword="null"/>
    /// when its constructor or a setter threw.
    /// </summary>
    public object? Instance
    {
        get
        {
            if (!_instantiated)
            {
                _instantiated = true;
                _instance = CreateInstance();
            }

            return _instance;
        }
    }

    private object? CreateInstance()
    {
        try
        {
            var instance = _data.Constructor.Invoke(ConstructorArguments());
            foreach (var argument in _data.NamedArguments)
            {
                var value = Normalize(argument.TypedValue);
                switch (argument.MemberInfo)
                {
                    case PropertyInfo property:
                        property.SetValue(instance, value);
                        break;
                    case FieldInfo field:
                        field.SetValue(instance, value);
                        break;
                }
            }

            return instance;
        }
        catch (Exception ex) when (ex is TargetInvocationException or MemberAccessException or ArgumentException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Turns a metadata value into the object the runtime would pass: arrays become typed arrays and enum values
    /// become enum objects.
    /// </summary>
    private static object? Normalize(CustomAttributeTypedArgument argument)
    {
        if (argument.Value is IReadOnlyList<CustomAttributeTypedArgument> elements)
        {
            var elementType = argument.ArgumentType.GetElementType() ?? typeof(object);
            var array = Array.CreateInstance(elementType, elements.Count);
            for (var i = 0; i < elements.Count; i++)
            {
                array.SetValue(Normalize(elements[i]), i);
            }

            return array;
        }

        if (argument.ArgumentType.IsEnum && argument.Value is not null)
        {
            return Enum.ToObject(argument.ArgumentType, argument.Value);
        }

        return argument.Value;
    }
}
