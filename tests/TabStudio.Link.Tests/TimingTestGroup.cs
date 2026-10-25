namespace TabStudio.Link.Tests;

/// <summary>
/// Tests that depend on timing, such as idle timeouts kept off by pings. They run by themselves, after the others:
/// a test that compiles or decodes a thousand QR codes beside them can keep the thread pool from answering a ping
/// for more than a second.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TimingTestGroup
{
    public const string Name = "Timing";
}
