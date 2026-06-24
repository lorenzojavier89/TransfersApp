namespace TransfersApp.Domain.Entities;

public class Account
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}
