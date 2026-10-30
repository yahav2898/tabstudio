namespace TabStudio.TestRunner.Reflection;

/// <summary>
/// The full names of the framework types the runner recognizes. Matching by name, instead of by type, is what lets
/// one runner work with every version of every framework without referencing any of them.
/// </summary>
internal static class FrameworkNames
{
    // xUnit.net v2 (and the parts of v3 that kept their names).
    public const string XunitFact = "Xunit.FactAttribute";
    public const string XunitTheory = "Xunit.TheoryAttribute";
    public const string XunitData = "Xunit.Sdk.DataAttribute";
    public const string XunitInlineData = "Xunit.InlineDataAttribute";
    public const string XunitMemberDataBase = "Xunit.MemberDataAttributeBase";
    public const string XunitClassData = "Xunit.ClassDataAttribute";
    public const string XunitTrait = "Xunit.TraitAttribute";
    public const string XunitClassFixture = "Xunit.IClassFixture`1";
    public const string XunitCollectionFixture = "Xunit.ICollectionFixture`1";
    public const string XunitCollection = "Xunit.CollectionAttribute";
    public const string XunitCollectionDefinition = "Xunit.CollectionDefinitionAttribute";
    public const string XunitAsyncLifetime = "Xunit.IAsyncLifetime";
    public const string XunitOutputHelper = "Xunit.Abstractions.ITestOutputHelper";
    public const string XunitV3OutputHelper = "Xunit.ITestOutputHelper";
    public const string XunitAssertionInterface = "Xunit.Sdk.IAssertionException";
    public const string XunitSdkNamespace = "Xunit.Sdk";
    public const string XunitDynamicSkipPrefix = "$XunitDynamicSkip$";

    // NUnit 3 and 4.
    public const string NUnitTest = "NUnit.Framework.TestAttribute";
    public const string NUnitTestCase = "NUnit.Framework.TestCaseAttribute";
    public const string NUnitTestCaseSource = "NUnit.Framework.TestCaseSourceAttribute";
    public const string NUnitTestFixture = "NUnit.Framework.TestFixtureAttribute";
    public const string NUnitSetUp = "NUnit.Framework.SetUpAttribute";
    public const string NUnitTearDown = "NUnit.Framework.TearDownAttribute";
    public const string NUnitOneTimeSetUp = "NUnit.Framework.OneTimeSetUpAttribute";
    public const string NUnitOneTimeTearDown = "NUnit.Framework.OneTimeTearDownAttribute";
    public const string NUnitIgnore = "NUnit.Framework.IgnoreAttribute";
    public const string NUnitExplicit = "NUnit.Framework.ExplicitAttribute";
    public const string NUnitCategory = "NUnit.Framework.CategoryAttribute";
    public const string NUnitTimeout = "NUnit.Framework.TimeoutAttribute";
    public const string NUnitFixtureLifeCycle = "NUnit.Framework.FixtureLifeCycleAttribute";
    public const string NUnitTestParameters = "NUnit.Framework.Internal.TestParameters";
    public const string NUnitSuccess = "NUnit.Framework.SuccessException";
    public const string NUnitIgnoreException = "NUnit.Framework.IgnoreException";
    public const string NUnitInconclusive = "NUnit.Framework.InconclusiveException";
    public const string NUnitAssertion = "NUnit.Framework.AssertionException";
    public const string NUnitMultipleAssert = "NUnit.Framework.MultipleAssertException";

    // MSTest v2 and v3.
    private const string MSTestNamespace = "Microsoft.VisualStudio.TestTools.UnitTesting.";
    public const string MSTestTestClass = MSTestNamespace + "TestClassAttribute";
    public const string MSTestTestMethod = MSTestNamespace + "TestMethodAttribute";
    public const string MSTestDataSource = MSTestNamespace + "ITestDataSource";
    public const string MSTestDataRow = MSTestNamespace + "DataRowAttribute";
    public const string MSTestTestInitialize = MSTestNamespace + "TestInitializeAttribute";
    public const string MSTestTestCleanup = MSTestNamespace + "TestCleanupAttribute";
    public const string MSTestClassInitialize = MSTestNamespace + "ClassInitializeAttribute";
    public const string MSTestClassCleanup = MSTestNamespace + "ClassCleanupAttribute";
    public const string MSTestAssemblyInitialize = MSTestNamespace + "AssemblyInitializeAttribute";
    public const string MSTestAssemblyCleanup = MSTestNamespace + "AssemblyCleanupAttribute";
    public const string MSTestIgnore = MSTestNamespace + "IgnoreAttribute";
    public const string MSTestTimeout = MSTestNamespace + "TimeoutAttribute";
    public const string MSTestTestCategory = MSTestNamespace + "TestCategoryAttribute";
    public const string MSTestTestProperty = MSTestNamespace + "TestPropertyAttribute";
    public const string MSTestOwner = MSTestNamespace + "OwnerAttribute";
    public const string MSTestPriority = MSTestNamespace + "PriorityAttribute";
    public const string MSTestExpectedException = MSTestNamespace + "ExpectedExceptionAttribute";
    public const string MSTestAssertFailed = MSTestNamespace + "AssertFailedException";
    public const string MSTestAssertInconclusive = MSTestNamespace + "AssertInconclusiveException";
    public const string MSTestUnitTestAssert = MSTestNamespace + "UnitTestAssertException";

    // Visual Basic: a Module compiles to a sealed class with this attribute, whose members are all Shared.
    public const string VisualBasicStandardModule = "Microsoft.VisualBasic.CompilerServices.StandardModuleAttribute";

    /// <summary>The namespaces of the frameworks' own code, whose frames are trimmed from the top of stack traces.</summary>
    public static IReadOnlyList<string> FrameworkFramePrefixes { get; } =
    [
        "Xunit.",
        "NUnit.Framework.",
        "Microsoft.VisualStudio.TestTools.UnitTesting.",
    ];
}
