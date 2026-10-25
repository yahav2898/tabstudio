using TabStudio.Toolchains.Alpine;

namespace TabStudio.Toolchains.Tests.Alpine;

/// <summary>Alpine's version order, as <c>apk version -t</c> gives it.</summary>
public sealed class ApkVersionTests
{
    [Theory]
    [InlineData("1.2.10", "1.2.9")]
    [InlineData("1.2.1", "1.2")]
    [InlineData("1.0", "1.0_rc1")]
    [InlineData("1.0_rc1", "1.0_beta2")]
    [InlineData("1.0_beta2", "1.0_beta1")]
    [InlineData("1.0_beta1", "1.0_alpha9")]
    [InlineData("1.0_p1", "1.0")]
    [InlineData("1.0-r2", "1.0-r1")]
    [InlineData("1.0-r10", "1.0-r9")]
    [InlineData("1.0a", "1.0")]
    [InlineData("3.14.8-r0", "3.14.7-r3")]
    [InlineData("24.22.1-r0", "24.18.1-r0")]
    [InlineData("2026.06.08-r0", "2025.12.30-r1")]
    [InlineData("1.0_git20260101-r0", "1.0-r5")]
    public void Orders_versions_as_apk_does(string newer, string older)
    {
        Assert.True(ApkVersion.Compare(newer, older) > 0, $"{newer} > {older}");
        Assert.True(ApkVersion.Compare(older, newer) < 0, $"{older} < {newer}");
    }

    [Theory]
    [InlineData("1.2.3-r0", "1.2.3-r0")]
    [InlineData("24.22.1", "24.22.1")]
    public void Equal_versions_compare_equal(string left, string right)
    {
        Assert.Equal(0, ApkVersion.Compare(left, right));
    }

    [Theory]
    [InlineData("3.14.8-r0", ">=", "3.14", true)]
    [InlineData("3.13.1-r0", ">=", "3.14", false)]
    [InlineData("3.14.8-r0", "<", "3.15", true)]
    [InlineData("3.14.8-r0", "=", "3.14.8-r0", true)]
    [InlineData("3.14.8-r0", "~", "3.14", true)]
    [InlineData("3.140.0-r0", "~", "3.14", false)]
    [InlineData("1.0-r0", "<=", "1.0-r0", true)]
    [InlineData("1.0-r1", ">", "1.0-r0", true)]
    public void Checks_constraints(string version, string operation, string constraint, bool expected)
    {
        Assert.Equal(expected, ApkVersion.Satisfies(version, operation, constraint));
    }

    [Theory]
    [InlineData("3.14.8-r0", "3.14.8")]
    [InlineData("24.22.1-r12", "24.22.1")]
    [InlineData("1.0", "1.0")]
    public void Shows_versions_without_their_revision(string version, string display)
    {
        Assert.Equal(display, ApkVersion.Display(version));
    }
}
