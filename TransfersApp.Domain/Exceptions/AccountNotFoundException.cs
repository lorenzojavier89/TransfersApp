namespace TransfersApp.Domain.Exceptions;

public class AccountNotFoundException : Exception
{
    public AccountNotFoundException(Guid accountId)
        : base($"Account '{accountId}' was not found.") { }
}
