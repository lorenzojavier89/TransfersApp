using System.Collections.Concurrent;
using TransfersApp.Domain.Entities;

namespace TransfersApp.Application;

public class InMemoryIdempotencyService : IIdempotencyService
{
    private record Entry(string BodyHash, TaskCompletionSource<Transfer> Tcs);

    private readonly ConcurrentDictionary<string, Entry> _store = new();

    public async Task<IdempotencyCheckResult> ExecuteAsync(
        string key, string bodyHash, Func<Task<Transfer>> processAsync)
    {
        var newEntry = new Entry(bodyHash, new TaskCompletionSource<Transfer>(TaskCreationOptions.RunContinuationsAsynchronously));
        var actual = _store.GetOrAdd(key, newEntry);

        if (actual.BodyHash != bodyHash)
            return new ConflictingBody();

        if (actual != newEntry)
        {
            var cached = await actual.Tcs.Task;
            return new CachedTransfer(cached);
        }

        try
        {
            var transfer = await processAsync();
            actual.Tcs.SetResult(transfer);
            return new NewTransfer(transfer);
        }
        catch (Exception ex)
        {
            _store.TryRemove(key, out _);
            actual.Tcs.SetException(ex);
            throw;
        }
    }
}
