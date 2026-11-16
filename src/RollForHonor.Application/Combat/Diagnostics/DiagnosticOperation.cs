namespace RollForHonor.Application.Combat.Diagnostics;

/// <summary>Shares diagnostic failure isolation while preserving the operation's result or original exception.</summary>
/// <typeparam name="TResult">The reference-type operation result, absent when execution throws.</typeparam>
internal abstract class DiagnosticOperation<TResult> where TResult : class
{
    internal TResult Execute(Func<TResult> operation)
    {
        try
        {
            var result = operation();
            PublishSafely(result, null);
            return result;
        }
        catch (Exception error)
        {
            PublishSafely(null, error);
            throw;
        }
    }

    protected static bool IsEnabledSafely(Func<bool> check)
    {
        try { return check(); }
        catch { return false; }
    }

    protected abstract void Publish(TResult? result, Exception? error);

    private void PublishSafely(TResult? result, Exception? error)
    {
        try { Publish(result, error); }
        catch { }
    }
}
