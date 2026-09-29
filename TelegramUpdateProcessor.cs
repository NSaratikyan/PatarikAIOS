namespace PatarikAIOS;

/// <summary>
/// Checkpoints each attempted update, including commands whose reply failed.
/// Replaying an entire batch can repeat tasks and financial entries that were
/// already saved before a later Telegram send failed.
/// </summary>
public static class TelegramUpdateProcessor
{
    public static async Task ProcessAsync<T>(
        IEnumerable<T> updates,
        Func<T, long> updateId,
        Func<T, Task> handle,
        Action<long> checkpoint,
        Action<T, Exception> onError)
    {
        foreach (var update in updates.OrderBy(updateId))
        {
            try
            {
                await handle(update);
            }
            catch (Exception exception)
            {
                onError(update, exception);
            }
            finally
            {
                // If saving this checkpoint fails, stop the batch. Advancing to
                // a later update would conceal that persistence failure.
                checkpoint(updateId(update));
            }
        }
    }
}
