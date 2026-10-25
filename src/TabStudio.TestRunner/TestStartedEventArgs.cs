namespace TabStudio.TestRunner;

/// <summary>Data for <see cref="TestSession.TestStarted"/>.</summary>
public sealed class TestStartedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    /// <param name="test">The test that is starting.</param>
    public TestStartedEventArgs(TestCase test)
    {
        ArgumentNullException.ThrowIfNull(test);
        Test = test;
    }

    /// <summary>The test that is starting.</summary>
    public TestCase Test { get; }
}
