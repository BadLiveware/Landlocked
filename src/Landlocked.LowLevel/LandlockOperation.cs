namespace Landlocked.LowLevel;

/// <summary>Helpers for callers that prefer exceptions at an imperative operation boundary.</summary>
public static class LandlockOperation
{
    public static int EnsureSuccess(this LandlockResult result, string operation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        if (!result.IsSuccess)
        {
            throw new LandlockException(operation, result.ErrorCode);
        }

        return result.Value;
    }
}
