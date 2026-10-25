namespace TabStudio.Notebooks.Tests;

/// <summary>
/// Kernel tests redirect the process-wide console and measure time, so they run one class at a time.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class KernelCollection
{
    public const string Name = "Kernel";
}
