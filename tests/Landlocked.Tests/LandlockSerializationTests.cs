using System.Reflection;
using System.Runtime.Loader;
using Landlocked;

namespace Landlocked.Tests;

public sealed class LandlockSerializationTests
{
    [Fact]
    public async Task Restriction_gate_is_shared_across_assembly_load_contexts()
    {
        var assemblyPath = typeof(Landlock).Assembly.Location;
        var firstContext = new AssemblyLoadContext("landlocked-first", isCollectible: true);
        var secondContext = new AssemblyLoadContext("landlocked-second", isCollectible: true);

        try
        {
            var firstGate = GetRestrictionGate(firstContext.LoadFromAssemblyPath(assemblyPath));
            var secondGate = GetRestrictionGate(secondContext.LoadFromAssemblyPath(assemblyPath));
            using var attempted = new ManualResetEventSlim();
            using var entered = new ManualResetEventSlim();
            var cancellationToken = TestContext.Current.CancellationToken;
            var firstLease = Enter(firstGate);

            try
            {
                var secondEntry = Task.Run(() =>
                {
                    attempted.Set();
                    using var secondLease = Enter(secondGate);
                    entered.Set();
                }, cancellationToken);

                Assert.True(attempted.Wait(TimeSpan.FromSeconds(5), cancellationToken));
                Assert.False(entered.Wait(TimeSpan.FromMilliseconds(100), cancellationToken));

                firstLease.Dispose();
                Assert.True(entered.Wait(TimeSpan.FromSeconds(5), cancellationToken));
                await secondEntry;
            }
            finally
            {
                firstLease.Dispose();
            }
        }
        finally
        {
            firstContext.Unload();
            secondContext.Unload();
        }
    }

    private static object GetRestrictionGate(Assembly assembly) =>
        assembly
            .GetType(typeof(Landlock).FullName!, throwOnError: true)!
            .GetField("RestrictionLock", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

    private static IDisposable Enter(object gate)
    {
        Monitor.Enter(gate);
        return new GateLease(() => Monitor.Exit(gate));
    }

    private sealed class GateLease(Action release) : IDisposable
    {
        private Action? _release = release;

        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }
}
