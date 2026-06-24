using TransfersApp.Domain.Entities;
using TransfersApp.Domain.Exceptions;
using TransfersApp.Domain.Interfaces;

namespace TransfersApp.Application;

public class TransfersService : ITransfersService
{
    private readonly ITransferRepository _repository;

    public TransfersService(ITransferRepository repository)
    {
        _repository = repository;
    }

    public async Task<Transfer> ApplyTransferAsync(Guid sourceId, Guid destinationId, decimal amount, string currency)
    {
        if (amount <= 0)
            throw new InvalidTransferAmountException(amount);

        if (sourceId == destinationId)
            throw new SameAccountTransferException(sourceId);

        var source = await _repository.GetAccountByIdAsync(sourceId)
            ?? throw new AccountNotFoundException(sourceId);
        var destination = await _repository.GetAccountByIdAsync(destinationId)
            ?? throw new AccountNotFoundException(destinationId);

        if (source.Currency != destination.Currency)
            throw new CurrencyMismatchException(source.Currency, destination.Currency);

        if (currency != source.Currency)
            throw new CurrencyMismatchException(currency, source.Currency);

        return await _repository.ApplyTransferAsync(sourceId, destinationId, amount, currency);
    }

    public Task<Transfer?> GetTransferByIdAsync(Guid id)
        => _repository.GetTransferByIdAsync(id);

    public Task<Account?> GetAccountByIdAsync(Guid id)
        => _repository.GetAccountByIdAsync(id);
}
