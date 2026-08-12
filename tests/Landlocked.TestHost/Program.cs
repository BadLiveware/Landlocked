using Landlocked;
using Landlocked.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Sockets;

if (args.Length == 0)
{
    Console.Error.WriteLine("A scenario name is required.");
    return 2;
}

var support = Landlock.GetSupport();
if (!support.IsAvailable)
{
    Console.Error.WriteLine(support.Reason);
    return 77;
}

try
{
    switch (args[0])
    {
        case "write-boundary":
            RunWriteBoundary(args[1], args[2]);
            break;
        case "progressive":
            RunProgressiveRestriction(args[1], args[2], args[3], args[4]);
            break;
        case "existing-descriptor":
            RunExistingDescriptor(args[1]);
            break;
        case "thread-sync":
            RunThreadSynchronization(args[1]);
            break;
        case "failed-apply":
            RunFailedApplication(args[1], args[2]);
            break;
        case "symlink-rule":
            RunSymbolicLinkRule(args[1], args[2]);
            break;
        case "concurrent-layers":
            RunConcurrentLayers(args[1], args[2], args[3], args[4]);
            break;
        case "permission-claims":
            RunPermissionClaims(args[1], args[2], args[3], args[4], args[5], args[6]);
            break;
        case "redundant-claims":
            RunRedundantClaims(args[1], args[2]);
            break;
        case "concurrent-claim-release":
            RunConcurrentClaimRelease(args[1], args[2], args[3], args[4]);
            break;
        case "failed-claim-release":
            RunFailedClaimRelease(args[1], args[2]);
            break;
        case "di-permission-contributors":
            RunDependencyInjectionContributors(args[1], args[2], args[3], args[4], args[5], args[6]);
            break;
        case "tcp-network-policy":
            RunTcpNetworkPolicy();
            break;
        case "network-claims":
            RunNetworkClaims();
            break;
        case "combined-policy":
            RunCombinedPolicy(args[1], args[2]);
            break;
        case "udp-network-policy":
            RunUdpNetworkPolicy();
            break;
        default:
            Console.Error.WriteLine($"Unknown scenario: {args[0]}");
            return 2;
    }

    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

static void RunWriteBoundary(string writableDirectory, string readOnlyFile)
{
    var policy = LandlockPolicy
        .Handle(FileSystemAccess.ContentAndHierarchyMutation)
        .Allow(writableDirectory, FileSystemAccess.ContentAndHierarchyMutation);

    Landlock.Restrict(policy);

    File.WriteAllText(Path.Combine(writableDirectory, "created.txt"), "inside");
    ExpectAccessDenied(() => File.AppendAllText(readOnlyFile, "outside"));
}

static void RunProgressiveRestriction(
    string broadDirectory,
    string narrowDirectory,
    string broadFile,
    string narrowFile)
{
    var broadPolicy = LandlockPolicy
        .Handle(FileSystemAccess.ContentAndHierarchyMutation)
        .Allow(broadDirectory, FileSystemAccess.ContentAndHierarchyMutation);

    Landlock.Restrict(broadPolicy);
    File.AppendAllText(broadFile, "broad-1\n");
    File.AppendAllText(narrowFile, "narrow-1\n");

    var narrowPolicy = LandlockPolicy
        .Handle(FileSystemAccess.ContentAndHierarchyMutation)
        .Allow(narrowDirectory, FileSystemAccess.ContentAndHierarchyMutation);

    Landlock.Restrict(narrowPolicy);
    ExpectAccessDenied(() => File.AppendAllText(broadFile, "broad-2\n"));
    File.AppendAllText(narrowFile, "narrow-2\n");
}

static void RunExistingDescriptor(string file)
{
    using var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
    Landlock.Restrict(LandlockPolicy.Handle(FileSystemAccess.ContentAndHierarchyMutation));

    using (var writer = new StreamWriter(stream, leaveOpen: true))
    {
        writer.Write("existing-descriptor\n");
        writer.Flush();
    }

    ExpectAccessDenied(() => File.AppendAllText(file, "new-descriptor\n"));
}

static void RunThreadSynchronization(string file)
{
    using var attemptWrite = new ManualResetEventSlim();
    Exception? workerFailure = null;
    var denied = false;

    var worker = new Thread(() =>
    {
        attemptWrite.Wait();
        try
        {
            File.AppendAllText(file, "worker-write\n");
        }
        catch (UnauthorizedAccessException)
        {
            denied = true;
        }
        catch (Exception exception)
        {
            workerFailure = exception;
        }
    });

    worker.Start();
    Landlock.Restrict(LandlockPolicy.Handle(FileSystemAccess.ContentAndHierarchyMutation));
    attemptWrite.Set();
    worker.Join();

    if (workerFailure is not null)
    {
        throw new InvalidOperationException("The worker failed unexpectedly.", workerFailure);
    }

    if (!denied)
    {
        throw new InvalidOperationException("The pre-existing worker thread retained write access.");
    }
}

static void RunFailedApplication(string missingPath, string writableFile)
{
    var policy = LandlockPolicy
        .Handle(FileSystemAccess.ContentAndHierarchyMutation)
        .Allow(missingPath, FileSystemAccess.ContentAndHierarchyMutation);

    try
    {
        Landlock.Restrict(policy);
        throw new InvalidOperationException("The invalid policy unexpectedly succeeded.");
    }
    catch (LandlockException)
    {
    }

    File.AppendAllText(writableFile, "still-unrestricted\n");
}

static void RunSymbolicLinkRule(string symbolicLink, string writableFile)
{
    var policy = LandlockPolicy
        .Handle(FileSystemAccess.ContentAndHierarchyMutation)
        .Allow(symbolicLink, FileSystemAccess.ContentAndHierarchyMutation);

    try
    {
        Landlock.Restrict(policy);
        throw new InvalidOperationException("The symbolic-link policy unexpectedly succeeded.");
    }
    catch (LandlockException)
    {
    }

    File.AppendAllText(writableFile, "symlink-rejected\n");
}

static void RunConcurrentLayers(string firstDirectory, string secondDirectory, string firstFile, string secondFile)
{
    using var start = new ManualResetEventSlim();
    Exception? firstFailure = null;
    Exception? secondFailure = null;

    var firstThread = new Thread(() =>
    {
        start.Wait();
        try
        {
            var policy = LandlockPolicy
                .Handle(FileSystemAccess.ContentAndHierarchyMutation)
                .Allow(firstDirectory, FileSystemAccess.ContentAndHierarchyMutation);
            Landlock.Restrict(policy);
        }
        catch (Exception exception)
        {
            firstFailure = exception;
        }
    });

    var secondThread = new Thread(() =>
    {
        start.Wait();
        try
        {
            var policy = LandlockPolicy
                .Handle(FileSystemAccess.ContentAndHierarchyMutation)
                .Allow(secondDirectory, FileSystemAccess.ContentAndHierarchyMutation);
            Landlock.Restrict(policy);
        }
        catch (Exception exception)
        {
            secondFailure = exception;
        }
    });

    firstThread.Start();
    secondThread.Start();
    start.Set();
    firstThread.Join();
    secondThread.Join();

    if (firstFailure is not null || secondFailure is not null)
    {
        throw new AggregateException(
            "A concurrent restriction failed.",
            new[] { firstFailure, secondFailure }.OfType<Exception>());
    }

    ExpectAccessDenied(() => File.AppendAllText(firstFile, "first\n"));
    ExpectAccessDenied(() => File.AppendAllText(secondFile, "second\n"));
}

static void RunPermissionClaims(
    string firstDirectory,
    string secondDirectory,
    string sharedDirectory,
    string firstFile,
    string secondFile,
    string sharedFile)
{
    var permissions = LandlockPermissions.Handle(FileSystemAccess.ContentAndHierarchyMutation);
    var firstClaim = permissions
        .Claim("first")
        .Allow(firstDirectory, FileSystemAccess.ContentAndHierarchyMutation)
        .Allow(sharedDirectory, FileSystemAccess.ContentAndHierarchyMutation);
    var secondClaim = permissions
        .Claim("second")
        .Allow(secondDirectory, FileSystemAccess.ContentAndHierarchyMutation)
        .Allow(sharedDirectory, FileSystemAccess.ContentAndHierarchyMutation);

    permissions.Activate();

    File.AppendAllText(firstFile, "initial\n");
    File.AppendAllText(secondFile, "initial\n");
    File.AppendAllText(sharedFile, "initial\n");

    ExpectInvalidOperation(() => permissions.Claim("late"));
    ExpectInvalidOperation(() => firstClaim.Allow(firstDirectory, FileSystemAccess.WriteFile));

    firstClaim.Release();
    firstClaim.Release();
    if (!firstClaim.IsReleased)
    {
        throw new InvalidOperationException("The released claim remained active.");
    }

    ExpectAccessDenied(() => File.AppendAllText(firstFile, "after-first\n"));
    File.AppendAllText(secondFile, "after-first\n");
    File.AppendAllText(sharedFile, "after-first\n");

    secondClaim.Release();
    ExpectAccessDenied(() => File.AppendAllText(secondFile, "after-second\n"));
    ExpectAccessDenied(() => File.AppendAllText(sharedFile, "after-second\n"));
}

static void RunRedundantClaims(string sharedDirectory, string sharedFile)
{
    var permissions = LandlockPermissions.Handle(FileSystemAccess.ContentAndHierarchyMutation);
    var claims = Enumerable
        .Range(0, 32)
        .Select(index => permissions
            .Claim($"shared-{index}")
            .Allow(sharedDirectory, FileSystemAccess.ContentAndHierarchyMutation))
        .ToArray();

    permissions.Activate();

    foreach (var claim in claims[..^1])
    {
        claim.Release();
    }

    File.AppendAllText(sharedFile, "last-claim-retains-access\n");
    claims[^1].Release();
    ExpectAccessDenied(() => File.AppendAllText(sharedFile, "released\n"));
}

static void RunConcurrentClaimRelease(
    string firstDirectory,
    string secondDirectory,
    string firstFile,
    string secondFile)
{
    var permissions = LandlockPermissions.Handle(FileSystemAccess.ContentAndHierarchyMutation);
    var firstClaim = permissions
        .Claim("first")
        .Allow(firstDirectory, FileSystemAccess.ContentAndHierarchyMutation);
    var secondClaim = permissions
        .Claim("second")
        .Allow(secondDirectory, FileSystemAccess.ContentAndHierarchyMutation);
    permissions.Activate();

    Exception? firstFailure = null;
    Exception? secondFailure = null;
    using var start = new ManualResetEventSlim();
    var firstThread = new Thread(() => ReleaseClaim(firstClaim, start, exception => firstFailure = exception));
    var secondThread = new Thread(() => ReleaseClaim(secondClaim, start, exception => secondFailure = exception));

    firstThread.Start();
    secondThread.Start();
    start.Set();
    firstThread.Join();
    secondThread.Join();

    if (firstFailure is not null || secondFailure is not null)
    {
        throw new AggregateException(
            "A concurrent claim release failed.",
            new[] { firstFailure, secondFailure }.OfType<Exception>());
    }

    ExpectAccessDenied(() => File.AppendAllText(firstFile, "first\n"));
    ExpectAccessDenied(() => File.AppendAllText(secondFile, "second\n"));
}

static void RunFailedClaimRelease(string writableDirectory, string writableFile)
{
    var permissions = LandlockPermissions.Handle(FileSystemAccess.ContentAndHierarchyMutation);
    var claim = permissions
        .Claim("writable")
        .Allow(writableDirectory, FileSystemAccess.ContentAndHierarchyMutation);
    permissions.Activate();

    var retainingPolicy = LandlockPolicy
        .Handle(FileSystemAccess.ContentAndHierarchyMutation)
        .Allow(writableDirectory, FileSystemAccess.ContentAndHierarchyMutation);
    for (var layer = 1; layer < 16; layer++)
    {
        Landlock.Restrict(retainingPolicy);
    }

    try
    {
        claim.Release();
        throw new InvalidOperationException("The claim release unexpectedly succeeded at the kernel layer limit.");
    }
    catch (LandlockException exception) when (exception.NativeErrorCode == 7)
    {
    }

    if (claim.IsReleased)
    {
        throw new InvalidOperationException("A failed release committed the claim state.");
    }

    File.AppendAllText(writableFile, "claim-remains-active\n");
}

static void RunDependencyInjectionContributors(
    string firstDirectory,
    string secondDirectory,
    string sharedDirectory,
    string firstFile,
    string secondFile,
    string sharedFile)
{
    var services = new ServiceCollection();
    services.AddLandlocked(FileSystemAccess.ContentAndHierarchyMutation);
    services.AddSingleton(new ContributorPaths(firstDirectory, secondDirectory, sharedDirectory));
    services.AddLandlockPermissionContributor<FirstPermissionContributor>();
    services.AddLandlockPermissionContributor<SecondPermissionContributor>();

    using var provider = services.BuildServiceProvider();
    var first = provider.GetRequiredService<FirstPermissionContributor>();
    var second = provider.GetRequiredService<SecondPermissionContributor>();
    var registeredContributors = provider.GetServices<ILandlockPermissionContributor>().ToArray();
    if (!registeredContributors.Contains(first) || !registeredContributors.Contains(second))
    {
        throw new InvalidOperationException("DI did not preserve contributor singleton identity.");
    }

    ExpectInvalidOperation(first.Release);

    var permissions = provider.ActivateLandlock();
    if (!ReferenceEquals(permissions, provider.ActivateLandlock()))
    {
        throw new InvalidOperationException("Repeated DI activation returned another permission coordinator.");
    }

    var firstRegistrationCount = registeredContributors
        .OfType<FirstPermissionContributor>()
        .Sum(contributor => contributor.RegistrationCount);
    if (firstRegistrationCount != 1 || second.RegistrationCount != 1)
    {
        throw new InvalidOperationException("A contributor was invoked more than once.");
    }

    File.AppendAllText(firstFile, "initial\n");
    File.AppendAllText(secondFile, "initial\n");
    File.AppendAllText(sharedFile, "initial\n");

    first.Release();
    ExpectAccessDenied(() => File.AppendAllText(firstFile, "after-first\n"));
    File.AppendAllText(secondFile, "after-first\n");
    File.AppendAllText(sharedFile, "after-first\n");

    second.Release();
    ExpectAccessDenied(() => File.AppendAllText(secondFile, "after-second\n"));
    ExpectAccessDenied(() => File.AppendAllText(sharedFile, "after-second\n"));
}

static void RunTcpNetworkPolicy()
{
    using var allowedListener = CreateTcpListener();
    using var deniedListener = CreateTcpListener();
    var allowedPort = GetPort(allowedListener);
    var deniedPort = GetPort(deniedListener);

    var policy = LandlockPolicy
        .HandleNetwork(NetworkAccess.Tcp)
        .AllowPort(allowedPort, NetworkAccess.ConnectTcp)
        .AllowPort(0, NetworkAccess.BindTcp);
    Landlock.Restrict(policy);

    ConnectTcp(allowedPort);
    ExpectSocketAccessDenied(() => ConnectTcp(deniedPort));

    using var ephemeralListener = new TcpListener(IPAddress.Loopback, 0);
    ephemeralListener.Start();
    using var fixedPortListener = new TcpListener(IPAddress.Loopback, deniedPort);
    ExpectSocketAccessDenied(fixedPortListener.Start);
}

static void RunNetworkClaims()
{
    using var firstListener = CreateTcpListener();
    using var secondListener = CreateTcpListener();
    var firstPort = GetPort(firstListener);
    var secondPort = GetPort(secondListener);

    var permissions = LandlockPermissions.HandleNetwork(NetworkAccess.ConnectTcp);
    var first = permissions.Claim("first").AllowPort(firstPort, NetworkAccess.ConnectTcp);
    var second = permissions.Claim("second").AllowPort(secondPort, NetworkAccess.ConnectTcp);
    permissions.Activate();

    ConnectTcp(firstPort);
    ConnectTcp(secondPort);

    first.Release();
    ExpectSocketAccessDenied(() => ConnectTcp(firstPort));
    ConnectTcp(secondPort);

    second.Release();
    ExpectSocketAccessDenied(() => ConnectTcp(secondPort));
}

static void RunCombinedPolicy(string writableDirectory, string readOnlyFile)
{
    using var allowedListener = CreateTcpListener();
    using var deniedListener = CreateTcpListener();
    var allowedPort = GetPort(allowedListener);
    var deniedPort = GetPort(deniedListener);

    var policy = LandlockPolicy
        .Handle(
            FileSystemAccess.ContentAndHierarchyMutation,
            NetworkAccess.ConnectTcp)
        .Allow(writableDirectory, FileSystemAccess.ContentAndHierarchyMutation)
        .AllowPort(allowedPort, NetworkAccess.ConnectTcp);
    Landlock.Restrict(policy);

    File.WriteAllText(Path.Combine(writableDirectory, "combined.txt"), "combined");
    ExpectAccessDenied(() => File.AppendAllText(readOnlyFile, "outside"));
    ConnectTcp(allowedPort);
    ExpectSocketAccessDenied(() => ConnectTcp(deniedPort));
}

static void RunUdpNetworkPolicy()
{
    using var allowedReceiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
    using var deniedReceiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
    var allowedPort = checked((ushort)((IPEndPoint)allowedReceiver.Client.LocalEndPoint!).Port);
    var deniedPort = checked((ushort)((IPEndPoint)deniedReceiver.Client.LocalEndPoint!).Port);

    var policy = LandlockPolicy
        .HandleNetwork(NetworkAccess.Udp)
        .AllowPort(allowedPort, NetworkAccess.ConnectSendUdp)
        .AllowPort(0, NetworkAccess.BindUdp);
    Landlock.Restrict(policy);

    SendUdp(allowedPort);
    ExpectSocketAccessDenied(() => SendUdp(deniedPort));
}

static TcpListener CreateTcpListener()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    return listener;
}

static ushort GetPort(TcpListener listener) =>
    checked((ushort)((IPEndPoint)listener.LocalEndpoint).Port);

static void ConnectTcp(ushort port)
{
    using var client = new TcpClient();
    client.Connect(IPAddress.Loopback, port);
}

static void SendUdp(ushort port)
{
    using var client = new UdpClient();
    _ = client.Send([1], new IPEndPoint(IPAddress.Loopback, port));
}

static void ExpectSocketAccessDenied(Action action)
{
    try
    {
        action();
    }
    catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AccessDenied)
    {
        return;
    }

    throw new InvalidOperationException("The network operation unexpectedly succeeded.");
}

static void ReleaseClaim(
    LandlockPermissionClaim claim,
    ManualResetEventSlim start,
    Action<Exception> recordFailure)
{
    start.Wait();
    try
    {
        claim.Release();
    }
    catch (Exception exception)
    {
        recordFailure(exception);
    }
}

static void ExpectInvalidOperation(Action action)
{
    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException("The invalid state transition unexpectedly succeeded.");
}

static void ExpectAccessDenied(Action action)
{
    try
    {
        action();
    }
    catch (UnauthorizedAccessException)
    {
        return;
    }

    throw new InvalidOperationException("The operation unexpectedly succeeded.");
}

internal sealed record ContributorPaths(
    string FirstDirectory,
    string SecondDirectory,
    string SharedDirectory);

internal sealed class FirstPermissionContributor(ContributorPaths paths) : ILandlockPermissionContributor
{
    private LandlockPermissionClaim? _claim;

    internal int RegistrationCount { get; private set; }

    public void RegisterPermissions(ILandlockPermissionRegistry permissions)
    {
        RegistrationCount++;
        _claim = permissions
            .Claim("first")
            .Allow(paths.FirstDirectory, FileSystemAccess.ContentAndHierarchyMutation)
            .Allow(paths.SharedDirectory, FileSystemAccess.ContentAndHierarchyMutation);
    }

    internal void Release() =>
        (_claim ?? throw new InvalidOperationException("Landlock permissions have not been activated.")).Release();
}

internal sealed class SecondPermissionContributor(ContributorPaths paths) : ILandlockPermissionContributor
{
    private LandlockPermissionClaim? _claim;

    internal int RegistrationCount { get; private set; }

    public void RegisterPermissions(ILandlockPermissionRegistry permissions)
    {
        RegistrationCount++;
        _claim = permissions
            .Claim("second")
            .Allow(paths.SecondDirectory, FileSystemAccess.ContentAndHierarchyMutation)
            .Allow(paths.SharedDirectory, FileSystemAccess.ContentAndHierarchyMutation);
    }

    internal void Release() =>
        (_claim ?? throw new InvalidOperationException("Landlock permissions have not been activated.")).Release();
}
