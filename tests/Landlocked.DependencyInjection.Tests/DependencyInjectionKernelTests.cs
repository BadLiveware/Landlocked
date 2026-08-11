using System.Diagnostics;

namespace Landlocked.DependencyInjection.Tests;

public sealed class DependencyInjectionKernelTests
{
    [Fact]
    public async Task Contributors_receive_module_owned_claims_from_the_service_provider()
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
            "di-permission-contributors",
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
            Path = Directory.CreateTempSubdirectory("landlocked-di-tests-").FullName;
        }

        internal string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
