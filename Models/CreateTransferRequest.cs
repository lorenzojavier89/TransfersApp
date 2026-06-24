namespace TransfersApp.Models;

public class CreateTransferRequest
{
    public Guid SourceAccountId { get; set; }
    public Guid DestinationAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
}
