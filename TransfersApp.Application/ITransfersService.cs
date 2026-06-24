using TransfersApp.Domain.Entities;

namespace TransfersApp.Application;

public interface ITransfersService
{
    Task<Transfer> ApplyTransferAsync(Guid sourceId, Guid destinationId, decimal amount, string currency);
    Task<Transfer?> GetTransferByIdAsync(Guid id);
    Task<Account?> GetAccountByIdAsync(Guid id);
}
