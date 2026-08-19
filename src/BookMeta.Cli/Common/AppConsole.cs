using System.Text.Json;
using Spectre.Console;

namespace BookMeta.Common;

public sealed class AppConsole(IAnsiConsole stdout)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public IAnsiConsole Out { get; } = stdout;

    public void Json(object value) => Console.Out.WriteLine(JsonSerializer.Serialize(value, JsonOptions));

    public void JsonLine(object value)
    {
        var options = new JsonSerializerOptions(JsonOptions) { WriteIndented = false };
        Console.Out.WriteLine(JsonSerializer.Serialize(value, options));
    }

    public void Error(string message) => Console.Error.WriteLine(message);
    public void Warning(string message) => Console.Error.WriteLine($"warning: {message}");
    public void Verbose(bool enabled, string message)
    {
        if (enabled)
            Console.Error.WriteLine($"debug: {message}");
    }

    public static string Safe(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "—";

        var clean = new string(value.Where(c => !char.IsControl(c) || c is '\t' or '\n').ToArray())
            .Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        return Markup.Escape(clean);
    }
}
