using System.Reflection;
using System.Text;
using TabStudio.TestRunner.Reflection;

namespace TabStudio.TestRunner.Execution;

/// <summary>
/// Decides what an exception thrown by a test means, by the names of the frameworks' exception types.
/// </summary>
/// <remarks>
/// Assertion failures are <see cref="TestOutcome.Failed"/>: any exception in the <c>Xunit.Sdk</c> namespace
/// or implementing <c>Xunit.Sdk.IAssertionException</c>, NUnit's <c>AssertionException</c> and
/// <c>MultipleAssertException</c>, and MSTest's <c>AssertFailedException</c>. NUnit's <c>Assert.Pass</c>
/// passes, its <c>Assert.Ignore</c> and <c>Assert.Inconclusive</c> and MSTest's <c>Assert.Inconclusive</c>
/// skip. Every other exception is <see cref="TestOutcome.Errored"/>.
/// </remarks>
internal static class OutcomeClassifier
{
    /// <summary>Unwraps the reflection wrapper around an exception thrown by invoked code.</summary>
    /// <param name="exception">The exception as caught.</param>
    public static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: { } inner })
        {
            exception = inner;
        }

        return exception;
    }

    /// <summary>Classifies an exception and describes it for the user.</summary>
    /// <param name="exception">The exception, already unwrapped.</param>
    /// <returns>The outcome and the message to show.</returns>
    public static (TestOutcome Outcome, string Message) Classify(Exception exception)
    {
        var type = exception.GetType();
        if (TypeMatching.Is(type, FrameworkNames.NUnitSuccess))
        {
            return (TestOutcome.Passed, exception.Message);
        }

        if (TypeMatching.Is(type, FrameworkNames.NUnitIgnoreException))
        {
            return (TestOutcome.Skipped, exception.Message);
        }

        if (TypeMatching.Is(type, FrameworkNames.NUnitInconclusive) || TypeMatching.Is(type, FrameworkNames.MSTestAssertInconclusive))
        {
            return (TestOutcome.Skipped, Prefix("Inconclusive", exception.Message));
        }

        if (exception.Message.StartsWith(FrameworkNames.XunitDynamicSkipPrefix, StringComparison.Ordinal))
        {
            return (TestOutcome.Skipped, exception.Message[FrameworkNames.XunitDynamicSkipPrefix.Length..]);
        }

        if (IsAssertionFailure(type))
        {
            return (TestOutcome.Failed, exception.Message);
        }

        return (TestOutcome.Errored, Describe(exception));
    }

    /// <summary>Whether an exception type is one of the frameworks' assertion failures.</summary>
    /// <param name="type">The exception type.</param>
    public static bool IsAssertionFailure(Type type)
    {
        if (TypeMatching.Is(type, FrameworkNames.NUnitAssertion) ||
            TypeMatching.Is(type, FrameworkNames.NUnitMultipleAssert) ||
            TypeMatching.Is(type, FrameworkNames.MSTestAssertFailed) ||
            TypeMatching.Is(type, FrameworkNames.MSTestUnitTestAssert) ||
            TypeMatching.FindInterface(type, FrameworkNames.XunitAssertionInterface) is not null)
        {
            return true;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (string.Equals(current.Namespace, FrameworkNames.XunitSdkNamespace, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Describes an unexpected exception and its inner exceptions, as in <c>System.InvalidOperationException: boom</c>.</summary>
    /// <param name="exception">The exception.</param>
    public static string Describe(Exception exception)
    {
        var builder = new StringBuilder();
        builder.Append(exception.GetType().FullName).Append(": ").Append(exception.Message);
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            builder.Append("\n ---> ").Append(inner.GetType().FullName).Append(": ").Append(inner.Message);
        }

        return builder.ToString();
    }

    private static string Prefix(string label, string message) =>
        string.IsNullOrWhiteSpace(message) ? label : $"{label}: {message}";
}
