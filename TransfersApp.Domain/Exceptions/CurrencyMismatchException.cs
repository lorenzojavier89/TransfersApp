namespace TransfersApp.Domain.Exceptions;

public class CurrencyMismatchException : Exception
{
    public CurrencyMismatchException(string sourceCurrency, string destinationCurrency)
        : base($"Currency mismatch: '{sourceCurrency}' does not match '{destinationCurrency}'.") { }
}
