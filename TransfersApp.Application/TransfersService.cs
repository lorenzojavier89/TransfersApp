using TransfersApp.Domain.Entities;
using TransfersApp.Domain.Interfaces;

namespace TransfersApp.Application;

public class TransfersService : ITransfersService
{
    private readonly ITransferRepository _repository;

    public TransfersService(ITransferRepository repository)
    {
        _repository = repository;
    }

    public Task<Transfer> ApplyTransferAsync(Guid sourceId, Guid destinationId, decimal amount, string currency)
        => _repository.ApplyTransferAsync(sourceId, destinationId, amount, currency);

    public Task<Transfer?> GetTransferByIdAsync(Guid id)
        => _repository.GetTransferByIdAsync(id);

    public Task<Account?> GetAccountByIdAsync(Guid id)
        => _repository.GetAccountByIdAsync(id);
}
