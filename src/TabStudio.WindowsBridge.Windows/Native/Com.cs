namespace TabStudio.WindowsBridge.Windows.Native;

/// <summary>
/// Calls into COM and WinRT objects by their virtual tables, as the Windows SDK's C++ headers lay them out. Slots
/// count from IUnknown's three methods; WinRT interfaces add IInspectable's three, so their own start at slot 6.
/// </summary>
internal static unsafe class Com
{
    /// <summary>The first slot of a WinRT interface's own methods, after IUnknown and IInspectable.</summary>
    public const int InspectableSlots = 6;

    /// <summary>A method of an object's virtual table.</summary>
    public static void* Slot(nint instance, int slot) => (*(void***)instance)[slot];

    /// <summary>Asks an object for another of its interfaces.</summary>
    /// <returns>The interface, or 0 when the object does not have it.</returns>
    public static nint QueryInterface(nint instance, Guid iid)
    {
        nint result;
        var hr = ((delegate* unmanaged<nint, Guid*, nint*, int>)Slot(instance, 0))(instance, &iid, &result);
        return hr < 0 ? 0 : result;
    }

    /// <summary>Releases an object, if there is one, and forgets it.</summary>
    public static void Release(ref nint instance)
    {
        var current = instance;
        instance = 0;
        if (current != 0)
        {
            _ = ((delegate* unmanaged<nint, uint>)Slot(current, 2))(current);
        }
    }

    /// <summary>Calls a method that takes nothing and returns an HRESULT.</summary>
    public static int Call(nint instance, int slot) => ((delegate* unmanaged<nint, int>)Slot(instance, slot))(instance);

    /// <summary>Calls a method that returns one object.</summary>
    public static int Call(nint instance, int slot, out nint result)
    {
        nint value;
        var hr = ((delegate* unmanaged<nint, nint*, int>)Slot(instance, slot))(instance, &value);
        result = hr < 0 ? 0 : value;
        return hr;
    }

    /// <summary>Calls a method that takes one object and returns another.</summary>
    public static int Call(nint instance, int slot, nint argument, out nint result)
    {
        nint value;
        var hr = ((delegate* unmanaged<nint, nint, nint*, int>)Slot(instance, slot))(instance, argument, &value);
        result = hr < 0 ? 0 : value;
        return hr;
    }

    /// <summary>Throws for a failed HRESULT, with what was being done.</summary>
    /// <exception cref="NativeCallException">The HRESULT is a failure.</exception>
    public static void Check(int hr, string what)
    {
        if (hr < 0)
        {
            throw new NativeCallException($"{what} failed (0x{hr:X8}).", hr);
        }
    }

    /// <summary>The activation factory of a WinRT class, with one of its interfaces.</summary>
    public static nint ActivationFactory(string className, in Guid iid)
    {
        nint name;
        fixed (char* text = className)
        {
            Check(NativeMethods.WindowsCreateString(text, (uint)className.Length, out name), $"Naming {className}");
        }

        try
        {
            Check(NativeMethods.RoGetActivationFactory(name, iid, out var factory), $"Activating {className}");
            return factory;
        }
        finally
        {
            _ = NativeMethods.WindowsDeleteString(name);
        }
    }
}

/// <summary>A call into Windows failed, with its HRESULT.</summary>
internal sealed class NativeCallException : Exception
{
    public NativeCallException()
    {
    }

    public NativeCallException(string message)
        : base(message)
    {
    }

    public NativeCallException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public NativeCallException(string message, int hresult)
        : base(message)
    {
        HResult = hresult;
    }
}
