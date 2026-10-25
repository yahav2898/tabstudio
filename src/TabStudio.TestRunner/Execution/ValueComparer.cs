using System.Collections;
using System.Globalization;

namespace TabStudio.TestRunner.Execution;

/// <summary>
/// Compares an expected and an actual value the way NUnit compares <c>ExpectedResult</c>: numbers by value
/// whatever their types, sequences element by element, and everything else with <see cref="object.Equals(object, object)"/>.
/// </summary>
internal static class ValueComparer
{
    /// <summary>Whether two values are equal.</summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    public static bool AreEqual(object? expected, object? actual)
    {
        if (Equals(expected, actual))
        {
            return true;
        }

        if (expected is null || actual is null)
        {
            return false;
        }

        if (IsNumber(expected) && IsNumber(actual))
        {
            return NumbersEqual(expected, actual);
        }

        if (expected is IEnumerable expectedItems and not string && actual is IEnumerable actualItems and not string)
        {
            var left = expectedItems.Cast<object?>().ToList();
            var right = actualItems.Cast<object?>().ToList();
            return left.Count == right.Count && left.Zip(right).All(pair => AreEqual(pair.First, pair.Second));
        }

        return false;
    }

    private static bool IsNumber(object value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static bool NumbersEqual(object expected, object actual)
    {
        if (expected is float or double || actual is float or double)
        {
            return System.Convert.ToDouble(expected, CultureInfo.InvariantCulture)
                .Equals(System.Convert.ToDouble(actual, CultureInfo.InvariantCulture));
        }

        return System.Convert.ToDecimal(expected, CultureInfo.InvariantCulture) == System.Convert.ToDecimal(actual, CultureInfo.InvariantCulture);
    }
}
