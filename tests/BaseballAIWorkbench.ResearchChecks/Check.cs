namespace BaseballAIWorkbench.ResearchChecks;

internal static class Check
{
    public static void That(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("CHECK FAILED: " + message);
    }

    public static async Task ThrowsAsync(Func<Task> operation, string message)
    {
        try { await operation(); }
        catch (TimeoutException) { throw; }
        catch { return; }
        throw new InvalidOperationException("CHECK FAILED: " + message);
    }

    public static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
