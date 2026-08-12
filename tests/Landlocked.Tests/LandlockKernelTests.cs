using System.Diagnostics;
using Landlocked;

namespace Landlocked.Tests;

public sealed class LandlockKernelTests
{
    [Fact]
    public async Task Current_directory_can_be_writable_while_sibling_file_is_read_only()
    {
        RequireLandlock();

        using var fixture = new TemporaryDirectory();
        var writable = Directory.CreateDirectory(Path.Combine(fixture.Path, "writable")).FullName;
        var outside = Path.Combine(fixture.Path, "outside.txt");
        File.WriteAllText(outside, "original");

        await RunScenario("write-boundary", writable, outside);

        Assert.Equal("inside", File.ReadAllText(Path.Combine(writable, "created.txt")));
        Assert.Equal("original", File.ReadAllText(outside));
    }

    [Fact]
    public async Task Later_policy_layer_can_only_tighten_access()
    {
        RequireLandlock();

        using var fixture = new TemporaryDirectory();
        var broadDirectory = Directory.CreateDirectory(Path.Combine(fixture.Path, "broad")).FullName;
        var narrowDirectory = Directory.CreateDirectory(Path.Combine(broadDirectory, "narrow")).FullName;
        var broadFile = Path.Combine(broadDirectory, "broad.txt");
        var narrowFile = Path.Combine(narrowDirectory, "narrow.txt");
        File.WriteAllText(broadFile, string.Empty);
        File.WriteAllText(narrowFile, string.Empty);

        await RunScenario("progressive", broadDirectory, narrowDirectory, broadFile, narrowFile);

        Assert.Equal("broad-1\n", File.ReadAllText(broadFile));
        Assert.Equal("narrow-1\nnarrow-2\n", File.ReadAllText(narrowFile));
    }

    [Fact]
    public async Task Descriptor_opened_before_restriction_retains_its_access()
    {
        RequireLandlock();

        using var fixture = new TemporaryDirectory();
        var file = Path.Combine(fixture.Path, "existing.txt");
        File.WriteAllText(file, string.Empty);

        await RunScenario("existing-descriptor", file);

        Assert.Equal("existing-descriptor\n", File.ReadAllText(file));
    }

    [Fact]
    public async Task Restriction_is_applied_to_threads_created_before_it()
    {
        RequireLandlock();

        using var fixture = new TemporaryDirectory();
        var file = Path.Combine(fixture.Path, "thread.txt");
        File.WriteAllText(file, string.Empty);

        await RunScenario("thread-sync", file);

        Assert.Equal(string.Empty, File.ReadAllText(file));
    }

    [Fact]
    public async Task Failed_policy_application_does_not_report_or_apply_a_restriction()
    {
        RequireLandlock();

        using var fixture = new TemporaryDirectory();
        var missingPath = Path.Combine(fixture.Path, "missing");
        var writableFile = Path.Combine(fixture.Path, "writable.txt");
        File.WriteAllText(writableFile, string.Empty);

        await RunScenario("failed-apply", missingPath, writableFile);

        Assert.Equal("still-unrestricted\n", File.ReadAllText(writableFile));
    }

    [Fact]
    public async Task Symbolic_link_rule_is_rejected_without_installing_a_policy()
    {
        RequireLandlock();

        using var fixture = new TemporaryDirectory();
        var target = Directory.CreateDirectory(Path.Combine(fixture.Path, "target")).FullName;
        var symbolicLink = Path.Combine(fixture.Path, "link");
        Directory.CreateSymbolicLink(symbolicLink, target);
        var writableFile = Path.Combine(target, "writable.txt");
        File.WriteAllText(writableFile, string.Empty);

        await RunScenario("symlink-rule", symbolicLink, writableFile);

        Assert.Equal("symlink-rejected\n", File.ReadAllText(writableFile));
    }

    [Fact]
    public async Task Concurrent_policy_layers_are_serialized_and_intersected()
    {
        RequireLandlock();

        using var fixture = new TemporaryDirectory();
        var firstDirectory = Directory.CreateDirectory(Path.Combine(fixture.Path, "first")).FullName;
        var secondDirectory = Directory.CreateDirectory(Path.Combine(fixture.Path, "second")).FullName;
        var firstFile = Path.Combine(firstDirectory, "first.txt");
        var secondFile = Path.Combine(secondDirectory, "second.txt");
        File.WriteAllText(firstFile, string.Empty);
        File.WriteAllText(secondFile, string.Empty);

        await RunScenario("concurrent-layers", firstDirectory, secondDirectory, firstFile, secondFile);

        Assert.Equal(string.Empty, File.ReadAllText(firstFile));
        Assert.Equal(string.Empty, File.ReadAllText(secondFile));
    }

    [Fact]
    public async Task Releasing_a_claim_preserves_permissions_required_by_other_modules()
    {
        RequireLandlock();

        using var fixture = new TemporaryDirectory();
        var firstDirectory = Directory.CreateDirectory(Path.Combine(fixture.Path, "first")).FullName;
        var secondDirectory = Directory.CreateDirectory(Path.Combine(fixture.Path, "second")).FullName;
        var sharedDirectory = Directory.CreateDirectory(Path.Combine(fixture.Path, "shared")).FullName;
        var firstFile = Path.Combine(firstDirectory, "first.txt");
        var secondFile = Path.Combine(secondDirectory, "second.txt");
        var sharedFile = Path.Combine(sharedDirectory, "shared.txt");
        File.WriteAllText(firstFile, string.Empty);
        File.WriteAllText(secondFile, string.Empty);
        File.WriteAllText(sharedFile, string.Empty);

        await RunScenario(
            "permission-claims",
            firstDirectory,
            secondDirectory,
            sharedDirectory,
            firstFile,
            secondFile,
            sharedFile);

        Assert.Equal("initial\n", File.ReadAllText(firstFile));
        Assert.Equal("initial\nafter-first\n", File.ReadAllText(secondFile));
        Assert.Equal("initial\nafter-first\n", File.ReadAllText(sharedFile));
    }

    [Fact]
    public async Task Redundant_claim_releases_do_not_consume_kernel_policy_layers()
    {
        RequireLandlock();

        using var fixture = new TemporaryDirectory();
        var sharedDirectory = Directory.CreateDirectory(Path.Combine(fixture.Path, "shared")).FullName;
        var sharedFile = Path.Combine(sharedDirectory, "shared.txt");
        File.WriteAllText(sharedFile, string.Empty);

        await RunScenario("redundant-claims", sharedDirectory, sharedFile);

        Assert.Equal("last-claim-retains-access\n", File.ReadAllText(sharedFile));
    }

    [Fact]
    public async Task Concurrent_claim_releases_are_serialized()
    {
        RequireLandlock();

        using var fixture = new TemporaryDirectory();
        var firstDirectory = Directory.CreateDirectory(Path.Combine(fixture.Path, "first")).FullName;
        var secondDirectory = Directory.CreateDirectory(Path.Combine(fixture.Path, "second")).FullName;
        var firstFile = Path.Combine(firstDirectory, "first.txt");
        var secondFile = Path.Combine(secondDirectory, "second.txt");
        File.WriteAllText(firstFile, string.Empty);
        File.WriteAllText(secondFile, string.Empty);

        await RunScenario("concurrent-claim-release", firstDirectory, secondDirectory, firstFile, secondFile);

        Assert.Equal(string.Empty, File.ReadAllText(firstFile));
        Assert.Equal(string.Empty, File.ReadAllText(secondFile));
    }

    [Fact]
    public async Task Failed_claim_release_remains_active_and_retryable()
    {
        RequireLandlock();

        using var fixture = new TemporaryDirectory();
        var writableDirectory = Directory.CreateDirectory(Path.Combine(fixture.Path, "writable")).FullName;
        var writableFile = Path.Combine(writableDirectory, "writable.txt");
        File.WriteAllText(writableFile, string.Empty);

        await RunScenario("failed-claim-release", writableDirectory, writableFile);

        Assert.Equal("claim-remains-active\n", File.ReadAllText(writableFile));
    }

    [Fact]
    public async Task Tcp_policy_allows_only_configured_connect_and_bind_ports()
    {
        RequireLandlock();

        await RunScenario("tcp-network-policy");
    }

    [Fact]
    public async Task Releasing_network_claims_removes_only_their_ports()
    {
        RequireLandlock();

        await RunScenario("network-claims");
    }

    [Fact]
    public async Task Combined_policy_enforces_filesystem_and_network_rights()
    {
        RequireLandlock();

        using var fixture = new TemporaryDirectory();
        var writableDirectory = Directory.CreateDirectory(Path.Combine(fixture.Path, "writable")).FullName;
        var readOnlyFile = Path.Combine(fixture.Path, "outside.txt");
        File.WriteAllText(readOnlyFile, "original");

        await RunScenario("combined-policy", writableDirectory, readOnlyFile);

        Assert.Equal("combined", File.ReadAllText(Path.Combine(writableDirectory, "combined.txt")));
        Assert.Equal("original", File.ReadAllText(readOnlyFile));
    }

    [Fact]
    public async Task Empty_claims_activate_a_deny_all_policy_and_cannot_reactivate()
    {
        RequireLandlock();

        using var fixture = new TemporaryDirectory();
        var deniedFile = Path.Combine(fixture.Path, "denied.txt");
        File.WriteAllText(deniedFile, "original");

        await RunScenario("empty-permission-claims", deniedFile);

        Assert.Equal("original", File.ReadAllText(deniedFile));
    }

    [Fact]
    public async Task Udp_policy_allows_only_configured_destination_ports()
    {
        RequireNetworkAccess(NetworkAccess.Udp);

        await RunScenario("udp-network-policy");
    }

    private static void RequireNetworkAccess(NetworkAccess requiredAccess)
    {
        var support = Landlock.GetSupport();
        if (!support.IsAvailable)
        {
            Assert.Skip(support.Reason);
        }

        var unsupported = requiredAccess & ~support.SupportedNetworkAccess;
        if (unsupported != NetworkAccess.None)
        {
            Assert.Skip($"Landlock ABI {support.AbiVersion} does not support '{unsupported}'.");
        }
    }

    private static void RequireLandlock()
    {
        var support = Landlock.GetSupport();
        if (!support.IsAvailable)
        {
            Assert.Skip(support.Reason);
        }
    }

    private static async Task RunScenario(string scenario, params string[] arguments)
    {
        var host = FindTestHost();
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(host);
        startInfo.ArgumentList.Add(scenario);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var cancellationToken = TestContext.Current.CancellationToken;
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = await standardOutput;
        var error = await standardError;
        Assert.True(
            process.ExitCode == 0,
            $"Scenario '{scenario}' exited with {process.ExitCode}.\nstdout:\n{output}\nstderr:\n{error}");
    }

    private static string FindTestHost()
    {
        var targetFrameworkDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var configurationDirectory = targetFrameworkDirectory.Parent;
        Assert.NotNull(configurationDirectory);

        var repositoryRoot = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var host = Path.Combine(
            repositoryRoot,
            "tests",
            "Landlocked.TestHost",
            "bin",
            configurationDirectory.Name,
            "net10.0",
            "Landlocked.TestHost.dll");
        Assert.True(File.Exists(host), $"Test host not found: {host}");
        return host;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = Directory.CreateTempSubdirectory("landlocked-tests-").FullName;
        }

        internal string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
