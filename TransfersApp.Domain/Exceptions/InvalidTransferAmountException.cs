namespace TransfersApp.Domain.Exceptions;

public class InvalidTransferAmountException : Exception
{
    public InvalidTransferAmountException(decimal amount)
        : base($"Transfer amount must be positive (received: {amount}).") { }
}
