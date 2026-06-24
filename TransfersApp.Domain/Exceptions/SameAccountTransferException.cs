namespace TransfersApp.Domain.Exceptions;

public class SameAccountTransferException : Exception
{
    public SameAccountTransferException(Guid accountId)
        : base($"Source and destination accounts must differ (both are '{accountId}').") { }
}
