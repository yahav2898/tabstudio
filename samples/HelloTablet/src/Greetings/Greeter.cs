namespace Greetings;

/// <summary>
/// Builds a friendly greeting. This class lives in its own project to show
/// that TabStudio builds project references for you.
/// </summary>
public sealed class Greeter
{
    private readonly string _name;

    public Greeter(string? name)
    {
        _name = string.IsNullOrWhiteSpace(name) ? "developer" : name.Trim();
    }

    public string Greet()
    {
        var partOfDay = System.DateTime.Now.Hour switch
        {
            < 12 => "morning",
            < 18 => "afternoon",
            _ => "evening",
        };

        return $"Good {partOfDay}, {_name}! This program was compiled and run on your tablet.";
    }
}
