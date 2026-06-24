namespace TransfersApp.Domain.Exceptions;

public class CurrencyMismatchException : Exception
{
    public CurrencyMismatchException(string sourceCurrency, string destinationCurrency)
        : base($"Cannot transfer between accounts with different currencies ('{sourceCurrency}' → '{destinationCurrency}').") { }
}
