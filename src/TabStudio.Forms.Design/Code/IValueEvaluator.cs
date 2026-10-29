namespace TabStudio.Forms.Design.Code;

/// <summary>
/// Works out the values designer code assigns, in the designer file's language: C# (<see cref="ExpressionEvaluator"/>)
/// or Visual Basic (<see cref="VisualBasicExpressionEvaluator"/>).
/// </summary>
internal interface IValueEvaluator
{
    /// <summary>Works out an expression's value, such as <c>new Point(12, 12)</c>.</summary>
    /// <param name="code">The expression.</param>
    /// <param name="value">Its value.</param>
    /// <returns>Whether it has a value the designer can work out.</returns>
    bool TryEvaluate(string code, out object? value);

    /// <summary>Works out the arguments of a collection add, such as <c>button1</c> or <c>label1, 0, 0</c>.</summary>
    /// <param name="arguments">The arguments, separated by commas.</param>
    /// <param name="values">Their values.</param>
    /// <returns>Whether all of them have values the designer can work out.</returns>
    bool TryEvaluateArguments(string arguments, out object?[] values);
}
