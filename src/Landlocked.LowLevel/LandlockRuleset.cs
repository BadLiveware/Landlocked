using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace Landlocked.LowLevel;

/// <summary>Owns a Landlock ruleset file descriptor.</summary>
[SupportedOSPlatform("linux")]
public sealed class LandlockRuleset : IDisposable
{
    private readonly RulesetHandle _handle;

    private LandlockRuleset(int descriptor)
    {
        _handle = new RulesetHandle(descriptor);
    }

    internal static LandlockRuleset Own(int descriptor)
    {
        try
        {
            return new LandlockRuleset(descriptor);
        }
        catch
        {
            LandlockApi.Close(descriptor);
            throw;
        }
    }

    internal DescriptorLease BorrowDescriptor()
    {
        ObjectDisposedException.ThrowIf(_handle.IsClosed, this);
        return new DescriptorLease(_handle);
    }

    public void Dispose() => _handle.Dispose();

    internal sealed class RulesetHandle : SafeHandleMinusOneIsInvalid
    {
        internal RulesetHandle(int descriptor)
            : base(true)
        {
            SetHandle(descriptor);
        }

        protected override bool ReleaseHandle() => LandlockApi.Close(handle) == 0;
    }

    internal sealed class DescriptorLease : IDisposable
    {
        private RulesetHandle? _handle;
        private readonly bool _addedReference;

        internal DescriptorLease(RulesetHandle handle)
        {
            _handle = handle;
            var addedReference = false;
            try
            {
                handle.DangerousAddRef(ref addedReference);
                Descriptor = checked((int)handle.DangerousGetHandle());
                _addedReference = addedReference;
            }
            catch
            {
                if (addedReference)
                {
                    handle.DangerousRelease();
                }

                throw;
            }
        }

        internal int Descriptor { get; }

        public void Dispose()
        {
            var handle = Interlocked.Exchange(ref _handle, null);
            if (handle is not null && _addedReference)
            {
                handle.DangerousRelease();
            }
        }
    }
}
