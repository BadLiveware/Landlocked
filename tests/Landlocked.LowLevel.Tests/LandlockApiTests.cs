using System.Runtime.Versioning;
using Landlocked.LowLevel;

namespace Landlocked.LowLevel.Tests;

public sealed class LandlockApiTests
{
    [Fact]
    [SupportedOSPlatform("linux")]
    public void Path_rules_reject_embedded_null_characters_before_native_access()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var createResult = LandlockApi.CreateRuleset(
            LandlockFileSystemAccess.ReadFile,
            LandlockNetworkAccess.None,
            out var ruleset);
        if (!createResult.IsSuccess)
        {
            return;
        }

        var createdRuleset = Assert.IsType<LandlockRuleset>(ruleset);
        using (createdRuleset)
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                LandlockApi.AddPathBeneathRule(
                    createdRuleset,
                    "/tmp\0/other",
                    LandlockFileSystemAccess.ReadFile));

            Assert.Equal("path", exception.ParamName);
        }
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void Path_rules_reject_invalid_Unicode_before_native_access()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var createResult = LandlockApi.CreateRuleset(
            LandlockFileSystemAccess.ReadFile,
            LandlockNetworkAccess.None,
            out var ruleset);
        if (!createResult.IsSuccess)
        {
            return;
        }

        var createdRuleset = Assert.IsType<LandlockRuleset>(ruleset);
        using (createdRuleset)
        {
            var exception = Assert.Throws<ArgumentException>(() =>
                LandlockApi.AddPathBeneathRule(
                    createdRuleset,
                    "/tmp/\ud800",
                    LandlockFileSystemAccess.ReadFile));

            Assert.Equal("path", exception.ParamName);
        }
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public void Path_rules_accept_whitespace_only_relative_paths()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var originalDirectory = Environment.CurrentDirectory;
        var parent = Directory.CreateTempSubdirectory("landlocked-low-level-");
        try
        {
            Environment.CurrentDirectory = parent.FullName;
            Directory.CreateDirectory("   ");

            var createResult = LandlockApi.CreateRuleset(
                LandlockFileSystemAccess.ReadFile,
                LandlockNetworkAccess.None,
                out var ruleset);
            if (!createResult.IsSuccess)
            {
                return;
            }

            var createdRuleset = Assert.IsType<LandlockRuleset>(ruleset);
            using (createdRuleset)
            {
                var addResult = LandlockApi.AddPathBeneathRule(
                    createdRuleset,
                    "   ",
                    LandlockFileSystemAccess.ReadFile);

                Assert.True(addResult.IsSuccess, $"errno: {addResult.ErrorCode}");
            }
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
            parent.Delete(recursive: true);
        }
    }

    [Fact]
    public void Result_success_is_derived_from_the_native_return_value()
    {
        Assert.True(new LandlockResult(0, 0).IsSuccess);
        Assert.True(new LandlockResult(7, 0).IsSuccess);
        Assert.False(new LandlockResult(-1, 22).IsSuccess);
    }

    [Fact]
    public void EnsureSuccess_preserves_the_value_or_throws_a_typed_exception()
    {
        Assert.Equal(7, new LandlockResult(7, 0).EnsureSuccess("query ABI"));

        var exception = Assert.Throws<LandlockException>(() =>
            new LandlockResult(-1, 22).EnsureSuccess("create ruleset"));

        Assert.Equal("create ruleset", exception.Operation);
        Assert.Equal(22, exception.NativeErrorCode);
    }
}
