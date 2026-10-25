using System.Text;

namespace TabStudio.SourceControl.Tests;

public sealed class GitObjectsTests
{
    [Theory]
    [InlineData("", "e69de29bb2d1d6434b8b29ae775ad8c2e48c5391")]
    [InlineData("hello\n", "ce013625030ba8dba906f756967f9e9ca394464a")]
    [InlineData("hello world", "95d09f2b10159347eece71399a7e2e907ea3df4f")]
    public void Blob_hashes_match_git(string content, string expected) =>
        Assert.Equal(expected, GitObjects.BlobHash(Encoding.UTF8.GetBytes(content)));
}
