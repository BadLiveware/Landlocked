using System.Diagnostics;
using Landlocked;

namespace Landlocked.Tests;

public sealed class LandlockKernelTests
{
    [Fact]
    public async Task Current_directory_can_be_writable_while_sibling_file_is_read_only()
    {
        if (!Landlock.GetSupport().IsAvailable)
        {
            return;
        }

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
        if (!Landlock.GetSupport().IsAvailable)
        {
            return;
        }

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
        if (!Landlock.GetSupport().IsAvailable)
        {
            return;
        }

        using var fixture = new TemporaryDirectory();
        var file = Path.Combine(fixture.Path, "existing.txt");
        File.WriteAllText(file, string.Empty);

        await RunScenario("existing-descriptor", file);

        Assert.Equal("existing-descriptor\n", File.ReadAllText(file));
    }

    [Fact]
    public async Task Restriction_is_applied_to_threads_created_before_it()
    {
        if (!Landlock.GetSupport().IsAvailable)
        {
            return;
        }

        using var fixture = new TemporaryDirectory();
        var file = Path.Combine(fixture.Path, "thread.txt");
        File.WriteAllText(file, string.Empty);

        await RunScenario("thread-sync", file);

        Assert.Equal(string.Empty, File.ReadAllText(file));
    }

    [Fact]
    public async Task Failed_policy_application_does_not_report_or_apply_a_restriction()
    {
        if (!Landlock.GetSupport().IsAvailable)
        {
            return;
        }

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
        if (!Landlock.GetSupport().IsAvailable)
        {
            return;
        }

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
        if (!Landlock.GetSupport().IsAvailable)
        {
            return;
        }

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
        var repositoryRoot = Path.GetFullPath("../../../../..", AppContext.BaseDirectory);
        var host = Path.Combine(
            repositoryRoot,
            "tests",
            "Landlocked.TestHost",
            "bin",
            "Debug",
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
