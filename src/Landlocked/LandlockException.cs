using System.ComponentModel;

namespace Landlocked;

public sealed class LandlockException : IOException
{
    internal LandlockException(string operation, int nativeErrorCode)
        : base($"Landlock operation '{operation}' failed with errno {nativeErrorCode}.", new Win32Exception(nativeErrorCode))
    {
        Operation = operation;
        NativeErrorCode = nativeErrorCode;
    }

    public string Operation { get; }

    public int NativeErrorCode { get; }
}
