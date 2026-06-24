using System.Collections.Concurrent;
using TransfersApp.Domain.Entities;
using TransfersApp.Domain.Exceptions;
using TransfersApp.Domain.Interfaces;

namespace TransfersApp.Repositories;

public class InMemoryTransferRepository : ITransferRepository
{
    private readonly ConcurrentDictionary<Guid, Account> _accounts = new();
    private readonly ConcurrentDictionary<Guid, Transfer> _transfers = new();
    private readonly object _lock = new();

    public InMemoryTransferRepository()
    {
        var alice = new Account
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Alice",
            Balance = 1000.00m,
            Currency = "USD"
        };
        var bob = new Account
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Bob",
            Balance = 1000.00m,
            Currency = "USD"
        };
        var carlos = new Account
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Name = "Carlos",
            Balance = 1000.00m,
            Currency = "ARS"
        };
        var diana = new Account
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Name = "Diana",
            Balance = 1000.00m,
            Currency = "ARS"
        };
        _accounts[alice.Id] = alice;
        _accounts[bob.Id] = bob;
        _accounts[carlos.Id] = carlos;
        _accounts[diana.Id] = diana;
    }

    public Task<Transfer> ApplyTransferAsync(Guid sourceId, Guid destinationId, decimal amount, string currency)
    {
        if (!_accounts.TryGetValue(sourceId, out _))
            throw new AccountNotFoundException(sourceId);
        if (!_accounts.TryGetValue(destinationId, out _))
            throw new AccountNotFoundException(destinationId);

        Transfer transfer;
        lock (_lock)
        {
            var source = _accounts[sourceId];
            var destination = _accounts[destinationId];

            if (source.Balance < amount)
                throw new InsufficientFundsException(sourceId, source.Balance, amount);

            source.Balance -= amount;
            destination.Balance += amount;

            transfer = new Transfer
            {
                Id = Guid.NewGuid(),
                SourceAccountId = sourceId,
                DestinationAccountId = destinationId,
                Amount = amount,
                Currency = currency,
                OperationDate = DateTime.UtcNow
            };
            _transfers[transfer.Id] = transfer;
        }

        return Task.FromResult(transfer);
    }

    public Task<Transfer?> GetTransferByIdAsync(Guid id)
    {
        _transfers.TryGetValue(id, out var transfer);
        return Task.FromResult(transfer);
    }

    public Task<Account?> GetAccountByIdAsync(Guid id)
    {
        _accounts.TryGetValue(id, out var account);
        return Task.FromResult(account);
    }
}
