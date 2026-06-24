using TransfersApp.Domain.Entities;

namespace TransfersApp.Application;

public abstract record IdempotencyCheckResult;
public sealed record NewTransfer(Transfer Transfer) : IdempotencyCheckResult;
public sealed record CachedTransfer(Transfer Transfer) : IdempotencyCheckResult;
public sealed record ConflictingBody : IdempotencyCheckResult;
