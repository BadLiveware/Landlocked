namespace Landlocked.LowLevel;

/// <summary>An unsuccessful low-level Landlock or supporting Linux operation.</summary>
public sealed class LandlockException : Exception
{
    public LandlockException(string operation, int nativeErrorCode)
        : base($"Landlock operation '{operation}' failed with errno {nativeErrorCode}.")
    {
        Operation = operation;
        NativeErrorCode = nativeErrorCode;
    }

    public string Operation { get; }

    public int NativeErrorCode { get; }
}
