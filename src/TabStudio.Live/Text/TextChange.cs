namespace TabStudio.Live.Text;

/// <summary>
/// A change of the visible text, in offsets, as an editor applies it: what an operation from another device did to
/// this device's text. Changes of one operation apply in order, each to the text the one before it left.
/// </summary>
/// <param name="Offset">Where the change starts, in characters.</param>
/// <param name="RemovedLength">How many characters it removes there.</param>
/// <param name="InsertedText">What it inserts there.</param>
public readonly record struct TextChange(int Offset, int RemovedLength, string InsertedText);
