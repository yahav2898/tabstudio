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

    private const string VisualBasicPath = "/projects/Shop/Cart.vb";

    private const string VisualBasicCart = """
        Imports System.Collections.Generic

        Namespace Shop
            Public Class Cart
                Private ReadOnly _items As New List(Of Integer)()

                Public ReadOnly Property Count As Integer
                    Get
                        Return _items.Count
                    End Get
                End Property

                Public Sub Add(item As Integer)
                    _items.Add(item)
                End Sub

                Public Function Total() As Integer
                    Dim sum = 0
                    For Each item In _items : sum += item : Next
                    Return sum
                End Function
            End Class
        End Namespace
        """;

    [Fact]
    public void Visual_basic_whitespace_comments_and_line_joins_are_not_changes()
    {
        var edited = "' The shopping cart.\n" + VisualBasicCart
            .Replace("_items.Add(item)", "_items.Add(item)   ' keep order", StringComparison.Ordinal)
            .Replace("Dim sum = 0\n            For Each", "Dim sum = 0 : For Each", StringComparison.Ordinal);

        Assert.Null(CompareVisualBasic(VisualBasicCart, edited));
    }

    [Fact]
    public void A_visual_basic_body_edit_changes_only_its_member_named_with_the_root_namespace()
    {
        var change = CompareVisualBasic(VisualBasicCart, VisualBasicCart.Replace("Dim sum = 0", "Dim sum = 1", StringComparison.Ordinal));

        Assert.NotNull(change);
        Assert.False(change.IsStructural);
        var member = Assert.Single(change.Members);
        Assert.Equal(("Root.Shop.Cart.Total()", "Root.Shop.Cart", "Total", MemberChangeKind.Changed), (member.Key, member.TypeName, member.Name, member.Kind));
    }

    [Fact]
    public void Visual_basic_members_added_and_removed_are_reported()
    {
        var count = "        Public ReadOnly Property Count As Integer\n            Get\n                Return _items.Count\n            End Get\n        End Property\n\n";
        var edited = VisualBasicCart
            .Replace(count, string.Empty, StringComparison.Ordinal)
            .Replace("        Public Sub Add(item As Integer)", "        Public Sub Clear()\n            _items.Clear()\n        End Sub\n\n        Public Sub Add(item As Integer)", StringComparison.Ordinal);

        var change = CompareVisualBasic(VisualBasicCart, edited);

        Assert.NotNull(change);
        Assert.False(change.IsStructural);
        Assert.Equal(
            new[] { ("Root.Shop.Cart.Count", MemberChangeKind.Removed), ("Root.Shop.Cart.Clear()", MemberChangeKind.Added) },
            change.Members.Select(m => (m.Key, m.Kind)));
    }

    [Fact]
    public void Visual_basic_fields_imports_and_type_modifiers_are_structural()
    {
        Assert.True(CompareVisualBasic(VisualBasicCart, VisualBasicCart.Replace("New List(Of Integer)()", "New List(Of Integer) From {1}", StringComparison.Ordinal))!.IsStructural);
        Assert.True(CompareVisualBasic(VisualBasicCart, "Imports System\n" + VisualBasicCart)!.IsStructural);
        Assert.True(CompareVisualBasic(VisualBasicCart, VisualBasicCart.Replace("Public Class Cart", "Public NotInheritable Class Cart", StringComparison.Ordinal))!.IsStructural);
    }

    private static FileChange? Compare(string before, string after) =>
        FileChange.Compare(Path, SourceText.From(before), SourceText.From(after));

    private static FileChange? CompareVisualBasic(string before, string after) =>
        FileChange.Compare(VisualBasicPath, SourceText.From(before), SourceText.From(after), "Root");
}
