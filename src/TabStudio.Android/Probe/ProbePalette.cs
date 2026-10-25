using Avalonia.Media;
using Avalonia.Media.Immutable;
using Microsoft.CodeAnalysis.Classification;

namespace TabStudio.Android.Probe;

/// <summary>The probe page's colors and fonts, close to the dark theme the IDE will use.</summary>
internal static class ProbePalette
{
    public static readonly IBrush Window = Solid("#1B1D23");
    public static readonly IBrush Card = Solid("#23262E");
    public static readonly IBrush Code = Solid("#16181D");
    public static readonly IBrush Accent = Solid("#0F7A70");
    public static readonly IBrush Text = Solid("#E6E6E6");
    public static readonly IBrush Muted = Solid("#9AA0AA");
    public static readonly IBrush Passed = Solid("#4EC9A0");
    public static readonly IBrush Failed = Solid("#F1707B");
    public static readonly IBrush White = Solid("#FFFFFF");

    public static readonly FontFamily Monospace = new("monospace");

    private static readonly IBrush PlainCode = Solid("#DCDCDC");
    private static readonly IBrush Keyword = Solid("#569CD6");
    private static readonly IBrush ControlKeyword = Solid("#D8A0DF");
    private static readonly IBrush StringLiteral = Solid("#D69D85");
    private static readonly IBrush Comment = Solid("#6A9955");
    private static readonly IBrush Number = Solid("#B5CEA8");
    private static readonly IBrush TypeName = Solid("#4EC9B0");
    private static readonly IBrush MethodName = Solid("#DCDCAA");
    private static readonly IBrush LocalName = Solid("#9CDCFE");
    private static readonly IBrush Preprocessor = Solid("#9B9B9B");

    /// <summary>The color of a Roslyn classification, as Visual Studio's dark theme draws it.</summary>
    /// <param name="classification">A name from <see cref="ClassificationTypeNames"/>.</param>
    public static IBrush ForClassification(string classification) => classification switch
    {
        ClassificationTypeNames.Keyword => Keyword,
        ClassificationTypeNames.ControlKeyword => ControlKeyword,
        ClassificationTypeNames.StringLiteral or ClassificationTypeNames.VerbatimStringLiteral => StringLiteral,
        ClassificationTypeNames.Comment => Comment,
        ClassificationTypeNames.NumericLiteral => Number,
        ClassificationTypeNames.PreprocessorKeyword or ClassificationTypeNames.ExcludedCode => Preprocessor,
        ClassificationTypeNames.ClassName or ClassificationTypeNames.RecordClassName or ClassificationTypeNames.StructName
            or ClassificationTypeNames.RecordStructName or ClassificationTypeNames.InterfaceName or ClassificationTypeNames.EnumName
            or ClassificationTypeNames.DelegateName or ClassificationTypeNames.TypeParameterName => TypeName,
        ClassificationTypeNames.MethodName or ClassificationTypeNames.ExtensionMethodName => MethodName,
        ClassificationTypeNames.LocalName or ClassificationTypeNames.ParameterName => LocalName,
        _ => PlainCode,
    };

    /// <summary>The default color of code that has no classification.</summary>
    public static IBrush CodeText => PlainCode;

    private static ImmutableSolidColorBrush Solid(string color) => new(Color.Parse(color));
}
