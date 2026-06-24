namespace TransfersApp.Domain.Entities;

public class Transfer
{
    public Guid Id { get; set; }
    public Guid SourceAccountId { get; set; }
    public Guid DestinationAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime OperationDate { get; set; }
}
