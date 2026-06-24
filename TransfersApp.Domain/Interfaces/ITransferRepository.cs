using TransfersApp.Domain.Entities;

namespace TransfersApp.Domain.Interfaces;

public interface ITransferRepository
{
    Task<Transfer> ApplyTransferAsync(Guid sourceId, Guid destinationId, decimal amount, string currency);
    Task<Transfer?> GetTransferByIdAsync(Guid id);
    Task<Account?> GetAccountByIdAsync(Guid id);
}
