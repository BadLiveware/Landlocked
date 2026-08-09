using Microsoft.Win32.SafeHandles;

namespace Landlocked.Internal;

internal sealed class SafeFileDescriptor : SafeHandleMinusOneIsInvalid
{
    private SafeFileDescriptor()
        : base(true)
    {
    }

    internal static SafeFileDescriptor Own(int descriptor)
    {
        var handle = new SafeFileDescriptor();
        handle.SetHandle(descriptor);
        return handle;
    }

    internal int Descriptor => checked((int)DangerousGetHandle());

    protected override bool ReleaseHandle()
    {
        return LinuxNative.Close(handle) == 0;
    }
}
