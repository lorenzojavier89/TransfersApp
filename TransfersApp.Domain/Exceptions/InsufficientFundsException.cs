namespace TransfersApp.Domain.Exceptions;

public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(Guid accountId, decimal available, decimal requested)
        : base($"Account '{accountId}' has insufficient funds (available: {available}, requested: {requested}).") { }
}
