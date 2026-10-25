// Welcome to TabStudio!
//
// Press F5 (or tap Run) to build this solution and run it right here on your tablet.
// The program asks for your name: type it in the Output panel and press Enter.
//
// Try this next:
//   1. Change the greeting in Greeter.cs and press F5 again.
//   2. Misspell a variable below and watch the Error List update as you type.

using Greetings;

Console.Write("What's your name? ");
var name = Console.ReadLine();

var greeter = new Greeter(name);
Console.WriteLine(greeter.Greet());
Console.WriteLine();

var languages = new[]
{
    new Language("C#", 2000, IsStaticallyTyped: true),
    new Language("Python", 1991, IsStaticallyTyped: false),
    new Language("Rust", 2015, IsStaticallyTyped: true),
    new Language("JavaScript", 1995, IsStaticallyTyped: false),
};

Console.WriteLine("Statically typed languages, oldest first:");
foreach (var language in languages.Where(l => l.IsStaticallyTyped).OrderBy(l => l.Year))
{
    Console.WriteLine($"  {language.Name,-10} since {language.Year} ({language.Age} years)");
}

return 0;

internal sealed record Language(string Name, int Year, bool IsStaticallyTyped)
{
    public int Age => DateTime.Now.Year - Year;
}
