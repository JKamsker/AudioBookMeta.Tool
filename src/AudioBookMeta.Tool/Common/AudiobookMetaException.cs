namespace AudiobookMeta.Tool.Common;

public sealed class AudiobookMetaException : Exception
{
    public AudiobookMetaException(string message, int exitCode, string? recovery = null, Exception? inner = null)
        : base(message, inner)
    {
        ExitCode = exitCode;
        Recovery = recovery;
    }

    public int ExitCode { get; }
    public string? Recovery { get; }
}
