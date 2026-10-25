using TabStudio.LiveTesting.Results;

namespace TabStudio.App.ViewModels;

/// <summary>What the editor's Live Unit Testing markers ask of Live Unit Testing. Implemented by <see cref="LiveTestingViewModel"/>.</summary>
public interface ILiveTestingEditor
{
    /// <summary>Runs tests now, whatever changed: live while Live Unit Testing runs, else from the Test Explorer.</summary>
    /// <param name="tests">The tests.</param>
    Task RunTestsAsync(IReadOnlyList<TestKey> tests);

    /// <summary>Opens a test's source and selects it in the Test Explorer.</summary>
    /// <param name="test">The test.</param>
    Task GoToTestAsync(TestKey test);
}
