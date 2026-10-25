using Microsoft.CodeAnalysis.Text;
using TabStudio.LiveTesting.Impact;

namespace TabStudio.LiveTesting.Tests;

public sealed class FileChangeTests
{
    private const string Path = "/projects/Shop/Cart.cs";

    private const string Cart = """
        using System.Collections.Generic;

        namespace Shop;

        public class Cart
        {
            private readonly List<int> _items = new();

            public int Count => _items.Count;

            public void Add(int item)
            {
                _items.Add(item);
            }

            public int Total()
            {
                var total = 0;
                foreach (var item in _items) total += item;
                return total;
            }
        }
        """;

    [Fact]
    public void Whitespace_and_comments_are_not_changes()
    {
        var edited = "// The shopping cart.\n" + Cart.Replace("_items.Add(item);", "_items.Add(item);   // keep order", StringComparison.Ordinal);

        Assert.Null(Compare(Cart, edited));
    }

    [Fact]
    public void A_body_edit_changes_only_its_member()
    {
        var change = Compare(Cart, Cart.Replace("var total = 0;", "var total = 1;", StringComparison.Ordinal));

        Assert.NotNull(change);
        Assert.False(change.IsStructural);
        var member = Assert.Single(change.Members);
        Assert.Equal(("Shop.Cart.Total()", "Shop.Cart", "Total", MemberChangeKind.Changed), (member.Key, member.TypeName, member.Name, member.Kind));
    }

    [Fact]
    public void Added_and_removed_members_are_reported_with_their_type()
    {
        var edited = Cart
            .Replace("    public int Count => _items.Count;\n\n", string.Empty, StringComparison.Ordinal)
            .Replace("    public void Add(int item)", "    public void Clear() { _items.Clear(); }\n\n    public void Add(int item)", StringComparison.Ordinal);

        var change = Compare(Cart, edited);

        Assert.NotNull(change);
        Assert.False(change.IsStructural);
        Assert.Equal(
            new[] { ("Shop.Cart.Count", MemberChangeKind.Removed), ("Shop.Cart.Clear()", MemberChangeKind.Added) },
            change.Members.Select(m => (m.Key, m.Kind)));
    }

    [Fact]
    public void Fields_usings_and_signatures_outside_bodies_are_structural()
    {
        Assert.True(Compare(Cart, Cart.Replace("new();", "new() { 1 };", StringComparison.Ordinal))!.IsStructural);
        Assert.True(Compare(Cart, "using System;\n" + Cart)!.IsStructural);
        Assert.True(Compare(Cart, Cart.Replace("public class Cart", "public sealed class Cart", StringComparison.Ordinal))!.IsStructural);
    }

    [Fact]
    public void New_and_deleted_files_are_structural()
    {
        Assert.True(FileChange.Compare(Path, null, SourceText.From(Cart))!.IsStructural);
        Assert.True(FileChange.Compare(Path, SourceText.From(Cart), null)!.IsStructural);
    }

    private static FileChange? Compare(string before, string after) =>
        FileChange.Compare(Path, SourceText.From(before), SourceText.From(after));
}
