namespace Landlocked.LowLevel;

/// <summary>Identifies which operation produced a path-rule result.</summary>
public enum LandlockRuleOperation
{
    OpenPath,
    AddRule,
}

/// <summary>The result of adding a path-beneath rule.</summary>
public readonly record struct LandlockRuleResult(
    LandlockResult Result,
    LandlockRuleOperation Operation)
{
    public int Value => Result.Value;

    public int ErrorCode => Result.ErrorCode;

    public bool IsSuccess => Result.IsSuccess;
}
