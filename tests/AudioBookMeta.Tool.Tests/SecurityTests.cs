using AudiobookMeta.Tool.Common;
using AudiobookMeta.Tool.Configuration;
using AudiobookMeta.Tool.Providers;

namespace AudiobookMeta.Tool.Tests;

public sealed class SecurityTests
{
    [Fact]
    public void Terminal_markup_and_control_characters_are_sanitized()
    {
        var safe = AppConsole.Safe("[bold]unsafe[/]\u001b[31m\nnext");
        Assert.DoesNotContain('\u001b', safe);
        Assert.DoesNotContain('\n', safe);
        Assert.Contains("[[bold]]", safe, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostic_log_omits_arguments_and_inner_exception_secrets()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"audiobookmeta-security-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var config = Path.Combine(directory, "config.toml");
        try
        {
            var exception = new ProviderException("test", "http_client", "redacted provider failure", inner: new Exception("literal:inner-secret"));
            var path = new DiagnosticLogger(new ConfigPathResolver()).Write(exception, ["search", "literal:argument-secret", "--config", config]);
            var content = File.ReadAllText(path);
            Assert.DoesNotContain("argument-secret", content, StringComparison.Ordinal);
            Assert.DoesNotContain("inner-secret", content, StringComparison.Ordinal);
            Assert.Contains("redacted provider failure", content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
