using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;

namespace TabStudio.Compilation.Features;

/// <summary>Converts Roslyn's tagged text and symbol display parts into <see cref="TaggedRun"/>s.</summary>
internal static class TaggedTextMapper
{
    /// <summary>Converts tagged text, as Quick Info and completion descriptions produce it.</summary>
    public static List<TaggedRun> Map(ImmutableArray<TaggedText> parts)
    {
        var runs = new List<TaggedRun>(parts.IsDefault ? 0 : parts.Length);
        if (parts.IsDefault)
        {
            return runs;
        }

        foreach (var part in parts)
        {
            var kind = FromTag(part.Tag);
            var text = kind == TaggedTextKind.LineBreak ? "\n" : part.Text;
            if (!string.IsNullOrEmpty(text))
            {
                runs.Add(new TaggedRun(text, kind));
            }
        }

        return runs;
    }

    /// <summary>Converts symbol display parts, as <see cref="ISymbol.ToMinimalDisplayParts"/> produces them.</summary>
    public static List<TaggedRun> Map(IEnumerable<SymbolDisplayPart> parts)
    {
        var runs = new List<TaggedRun>();
        foreach (var part in parts)
        {
            var kind = FromPartKind(part.Kind);
            var text = kind == TaggedTextKind.LineBreak ? "\n" : part.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                runs.Add(new TaggedRun(text, kind));
            }
        }

        return runs;
    }

    /// <summary>The classification name that colors a kind of run in the editor, or <see langword="null"/> for plain text.</summary>
    public static string? ToClassification(TaggedTextKind kind) => kind switch
    {
        TaggedTextKind.Keyword => ClassificationTypeNames.Keyword,
        TaggedTextKind.Punctuation => ClassificationTypeNames.Punctuation,
        TaggedTextKind.Operator => ClassificationTypeNames.Operator,
        TaggedTextKind.StringLiteral => ClassificationTypeNames.StringLiteral,
        TaggedTextKind.NumericLiteral => ClassificationTypeNames.NumericLiteral,
        TaggedTextKind.Class => ClassificationTypeNames.ClassName,
        TaggedTextKind.Record => ClassificationTypeNames.RecordClassName,
        TaggedTextKind.Struct => ClassificationTypeNames.StructName,
        TaggedTextKind.RecordStruct => ClassificationTypeNames.RecordStructName,
        TaggedTextKind.Interface => ClassificationTypeNames.InterfaceName,
        TaggedTextKind.Enum => ClassificationTypeNames.EnumName,
        TaggedTextKind.EnumMember => ClassificationTypeNames.EnumMemberName,
        TaggedTextKind.Delegate => ClassificationTypeNames.DelegateName,
        TaggedTextKind.TypeParameter => ClassificationTypeNames.TypeParameterName,
        TaggedTextKind.Namespace => ClassificationTypeNames.NamespaceName,
        TaggedTextKind.Method => ClassificationTypeNames.MethodName,
        TaggedTextKind.ExtensionMethod => ClassificationTypeNames.ExtensionMethodName,
        TaggedTextKind.Property => ClassificationTypeNames.PropertyName,
        TaggedTextKind.Field => ClassificationTypeNames.FieldName,
        TaggedTextKind.Event => ClassificationTypeNames.EventName,
        TaggedTextKind.Constant => ClassificationTypeNames.ConstantName,
        TaggedTextKind.Local or TaggedTextKind.RangeVariable => ClassificationTypeNames.LocalName,
        TaggedTextKind.Parameter => ClassificationTypeNames.ParameterName,
        TaggedTextKind.Label => ClassificationTypeNames.LabelName,
        TaggedTextKind.Module => ClassificationTypeNames.ModuleName,
        TaggedTextKind.Alias or TaggedTextKind.ErrorType or TaggedTextKind.Assembly => ClassificationTypeNames.Identifier,
        _ => null,
    };

    private static TaggedTextKind FromTag(string tag) => tag switch
    {
        TextTags.Space => TaggedTextKind.Space,
        TextTags.LineBreak => TaggedTextKind.LineBreak,
        TextTags.Keyword => TaggedTextKind.Keyword,
        TextTags.Punctuation => TaggedTextKind.Punctuation,
        TextTags.Operator => TaggedTextKind.Operator,
        TextTags.StringLiteral => TaggedTextKind.StringLiteral,
        TextTags.NumericLiteral => TaggedTextKind.NumericLiteral,
        TextTags.Class => TaggedTextKind.Class,
        TextTags.Record => TaggedTextKind.Record,
        TextTags.Struct => TaggedTextKind.Struct,
        TextTags.RecordStruct => TaggedTextKind.RecordStruct,
        TextTags.Interface => TaggedTextKind.Interface,
        TextTags.Enum => TaggedTextKind.Enum,
        TextTags.EnumMember => TaggedTextKind.EnumMember,
        TextTags.Delegate => TaggedTextKind.Delegate,
        TextTags.TypeParameter => TaggedTextKind.TypeParameter,
        TextTags.Namespace => TaggedTextKind.Namespace,
        TextTags.Method => TaggedTextKind.Method,
        TextTags.ExtensionMethod => TaggedTextKind.ExtensionMethod,
        TextTags.Property => TaggedTextKind.Property,
        TextTags.Field => TaggedTextKind.Field,
        TextTags.Event => TaggedTextKind.Event,
        TextTags.Constant => TaggedTextKind.Constant,
        TextTags.Local => TaggedTextKind.Local,
        TextTags.Parameter => TaggedTextKind.Parameter,
        TextTags.Label => TaggedTextKind.Label,
        TextTags.RangeVariable => TaggedTextKind.RangeVariable,
        TextTags.Alias => TaggedTextKind.Alias,
        TextTags.ErrorType => TaggedTextKind.ErrorType,
        TextTags.Assembly => TaggedTextKind.Assembly,
        TextTags.Module => TaggedTextKind.Module,
        TextTags.AnonymousTypeIndicator => TaggedTextKind.AnonymousTypeIndicator,
        _ => TaggedTextKind.Text,
    };

    private static TaggedTextKind FromPartKind(SymbolDisplayPartKind kind) => kind switch
    {
        SymbolDisplayPartKind.Space => TaggedTextKind.Space,
        SymbolDisplayPartKind.LineBreak => TaggedTextKind.LineBreak,
        SymbolDisplayPartKind.Keyword => TaggedTextKind.Keyword,
        SymbolDisplayPartKind.Punctuation => TaggedTextKind.Punctuation,
        SymbolDisplayPartKind.Operator => TaggedTextKind.Operator,
        SymbolDisplayPartKind.StringLiteral => TaggedTextKind.StringLiteral,
        SymbolDisplayPartKind.NumericLiteral => TaggedTextKind.NumericLiteral,
        SymbolDisplayPartKind.ClassName => TaggedTextKind.Class,
        SymbolDisplayPartKind.RecordClassName => TaggedTextKind.Record,
        SymbolDisplayPartKind.StructName => TaggedTextKind.Struct,
        SymbolDisplayPartKind.RecordStructName => TaggedTextKind.RecordStruct,
        SymbolDisplayPartKind.InterfaceName => TaggedTextKind.Interface,
        SymbolDisplayPartKind.EnumName => TaggedTextKind.Enum,
        SymbolDisplayPartKind.EnumMemberName => TaggedTextKind.EnumMember,
        SymbolDisplayPartKind.DelegateName => TaggedTextKind.Delegate,
        SymbolDisplayPartKind.TypeParameterName => TaggedTextKind.TypeParameter,
        SymbolDisplayPartKind.NamespaceName => TaggedTextKind.Namespace,
        SymbolDisplayPartKind.MethodName => TaggedTextKind.Method,
        SymbolDisplayPartKind.ExtensionMethodName => TaggedTextKind.ExtensionMethod,
        SymbolDisplayPartKind.PropertyName => TaggedTextKind.Property,
        SymbolDisplayPartKind.FieldName => TaggedTextKind.Field,
        SymbolDisplayPartKind.EventName => TaggedTextKind.Event,
        SymbolDisplayPartKind.ConstantName => TaggedTextKind.Constant,
        SymbolDisplayPartKind.LocalName => TaggedTextKind.Local,
        SymbolDisplayPartKind.ParameterName => TaggedTextKind.Parameter,
        SymbolDisplayPartKind.LabelName => TaggedTextKind.Label,
        SymbolDisplayPartKind.RangeVariableName => TaggedTextKind.RangeVariable,
        SymbolDisplayPartKind.AliasName => TaggedTextKind.Alias,
        SymbolDisplayPartKind.ErrorTypeName => TaggedTextKind.ErrorType,
        SymbolDisplayPartKind.AssemblyName => TaggedTextKind.Assembly,
        SymbolDisplayPartKind.ModuleName => TaggedTextKind.Module,
        SymbolDisplayPartKind.AnonymousTypeIndicator => TaggedTextKind.AnonymousTypeIndicator,
        _ => TaggedTextKind.Text,
    };
}
