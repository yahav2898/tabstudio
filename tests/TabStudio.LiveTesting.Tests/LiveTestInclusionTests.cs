namespace TabStudio.LiveTesting.Tests;

public sealed class LiveTestInclusionTests
{
    private const string Project = "/projects/Shop.Tests/Shop.Tests.csproj";

    [Fact]
    public void Everything_is_included_until_excluded()
    {
        var inclusion = new LiveTestInclusion();

        Assert.True(inclusion.IsProjectIncluded(Project));
        Assert.True(inclusion.IsIncluded(Project, "Shop.Tests.CartTests.Adds"));
        Assert.Empty(inclusion.Rules);
    }

    [Fact]
    public void The_most_specific_entry_wins()
    {
        var inclusion = new LiveTestInclusion();

        inclusion.Set(Project, "Shop.Tests.CartTests", include: false);
        inclusion.Set(Project, "Shop.Tests.CartTests.Adds", include: true);

        Assert.False(inclusion.IsIncluded(Project, "Shop.Tests.CartTests.Removes"));
        Assert.False(inclusion.IsIncluded(Project, "Shop.Tests.CartTests.Totals(1, 2)"));
        Assert.True(inclusion.IsIncluded(Project, "Shop.Tests.CartTests.Adds"));
        Assert.True(inclusion.IsIncluded(Project, "Shop.Tests.CartTestsExtra.Adds"));
        Assert.True(inclusion.IsIncluded(Project, "Shop.Tests.OrderTests.Places"));
        Assert.True(inclusion.IsProjectIncluded(Project));
    }

    [Fact]
    public void Including_a_container_again_clears_the_entries_inside_it()
    {
        var inclusion = new LiveTestInclusion();
        inclusion.Set(Project, "Shop.Tests.CartTests.Adds", include: false);
        inclusion.Set(Project, "Shop.Tests.OrderTests+Nested.Places", include: false);

        inclusion.Set(Project, "Shop.Tests", include: true);

        Assert.Empty(inclusion.Rules);
        Assert.True(inclusion.IsIncluded(Project, "Shop.Tests.CartTests.Adds"));
    }

    [Fact]
    public void Excluding_a_project_excludes_its_tests_and_is_listed()
    {
        var inclusion = new LiveTestInclusion();
        var changes = 0;
        inclusion.Changed += (_, _) => changes++;

        inclusion.Set(Project, string.Empty, include: false);

        Assert.False(inclusion.IsProjectIncluded(Project));
        Assert.False(inclusion.IsIncluded(Project, "Shop.Tests.OrderTests+Nested.Places"));
        Assert.Equal(new[] { Project }, inclusion.ExcludedProjects);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Ancestors_go_from_the_data_row_to_the_project()
    {
        Assert.Equal(
            new[] { "Shop.Tests.Outer+Inner.Adds", "Shop.Tests.Outer+Inner", "Shop.Tests.Outer", "Shop.Tests", "Shop", string.Empty },
            LiveTestInclusion.Ancestors("Shop.Tests.Outer+Inner.Adds(1.5, \"a.b\")"));
    }
}
