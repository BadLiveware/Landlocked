namespace Landlocked.LowLevel;

/// <summary>The result of a Landlock or supporting Linux operation.</summary>
public readonly record struct LandlockResult(int Value, int ErrorCode)
{
    public bool IsSuccess => Value >= 0;
}
