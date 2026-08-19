namespace BookMeta.Common;

public static class ExitCodes
{
    public const int Success = 0;
    public const int General = 1;
    public const int Usage = 2;
    public const int Configuration = 3;
    public const int ProvidersFailed = 4;
    public const int StrictFailure = 5;
    public const int UnsupportedCapability = 6;
    public const int Cancelled = 10;
}
