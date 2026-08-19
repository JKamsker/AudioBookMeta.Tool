namespace BookMeta.Common;

public sealed class BookMetaException : Exception
{
    public BookMetaException(string message, int exitCode, string? recovery = null, Exception? inner = null)
        : base(message, inner)
    {
        ExitCode = exitCode;
        Recovery = recovery;
    }

    public int ExitCode { get; }
    public string? Recovery { get; }
}
