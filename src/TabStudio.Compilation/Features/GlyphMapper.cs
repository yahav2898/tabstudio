using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Tags;

namespace TabStudio.Compilation.Features;

/// <summary>Chooses the <see cref="SymbolGlyph"/> for Roslyn tags and symbols.</summary>
internal static class GlyphMapper
{
    /// <summary>The glyph for a set of <see cref="WellKnownTags"/>, as completion items and Quick Info carry them.</summary>
    public static SymbolGlyph FromTags(ImmutableArray<string> tags)
    {
        if (tags.IsDefaultOrEmpty)
        {
            return SymbolGlyph.Other;
        }

        foreach (var tag in tags)
        {
            var glyph = FromTag(tag);
            if (glyph != SymbolGlyph.Other)
            {
                return glyph;
            }
        }

        return SymbolGlyph.Other;
    }

    /// <summary>The glyph for a symbol.</summary>
    public static SymbolGlyph FromSymbol(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol type => type.TypeKind switch
        {
            TypeKind.Class or TypeKind.Module => SymbolGlyph.Class,
            TypeKind.Struct => SymbolGlyph.Struct,
            TypeKind.Interface => SymbolGlyph.Interface,
            TypeKind.Enum => SymbolGlyph.Enum,
            TypeKind.Delegate => SymbolGlyph.Delegate,
            _ => SymbolGlyph.Other,
        },
        IMethodSymbol { IsExtensionMethod: true } => SymbolGlyph.ExtensionMethod,
        IMethodSymbol => SymbolGlyph.Method,
        IPropertySymbol => SymbolGlyph.Property,
        IFieldSymbol { ContainingType.TypeKind: TypeKind.Enum } => SymbolGlyph.EnumMember,
        IFieldSymbol { IsConst: true } or ILocalSymbol { IsConst: true } => SymbolGlyph.Constant,
        IFieldSymbol => SymbolGlyph.Field,
        IEventSymbol => SymbolGlyph.Event,
        ILocalSymbol or IRangeVariableSymbol => SymbolGlyph.Local,
        IParameterSymbol => SymbolGlyph.Parameter,
        INamespaceSymbol => SymbolGlyph.Namespace,
        ITypeParameterSymbol => SymbolGlyph.TypeParameter,
        ILabelSymbol => SymbolGlyph.Label,
        _ => SymbolGlyph.Other,
    };

    private static SymbolGlyph FromTag(string tag) => tag switch
    {
        WellKnownTags.Class or WellKnownTags.Module => SymbolGlyph.Class,
        WellKnownTags.Structure => SymbolGlyph.Struct,
        WellKnownTags.Interface => SymbolGlyph.Interface,
        WellKnownTags.Enum => SymbolGlyph.Enum,
        WellKnownTags.EnumMember => SymbolGlyph.EnumMember,
        WellKnownTags.Delegate => SymbolGlyph.Delegate,
        WellKnownTags.Method or WellKnownTags.Operator => SymbolGlyph.Method,
        WellKnownTags.ExtensionMethod => SymbolGlyph.ExtensionMethod,
        WellKnownTags.Property => SymbolGlyph.Property,
        WellKnownTags.Field => SymbolGlyph.Field,
        WellKnownTags.Event => SymbolGlyph.Event,
        WellKnownTags.Local or WellKnownTags.RangeVariable => SymbolGlyph.Local,
        WellKnownTags.Parameter => SymbolGlyph.Parameter,
        WellKnownTags.Constant => SymbolGlyph.Constant,
        WellKnownTags.Namespace => SymbolGlyph.Namespace,
        WellKnownTags.Keyword => SymbolGlyph.Keyword,
        WellKnownTags.Snippet => SymbolGlyph.Snippet,
        WellKnownTags.TypeParameter => SymbolGlyph.TypeParameter,
        WellKnownTags.Label => SymbolGlyph.Label,
        _ => SymbolGlyph.Other,
    };
}
