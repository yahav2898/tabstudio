using System.Text.Json.Serialization;

namespace TabStudio.Link.Tests;

/// <summary>JSON for test bodies.</summary>
[JsonSerializable(typeof(string))]
internal sealed partial class TestJsonContext : JsonSerializerContext
{
}
