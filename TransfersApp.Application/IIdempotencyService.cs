using TransfersApp.Domain.Entities;

namespace TransfersApp.Application;

public interface IIdempotencyService
{
    Task<IdempotencyCheckResult> ExecuteAsync(
        string key, string bodyHash, Func<Task<Transfer>> processAsync);
}
