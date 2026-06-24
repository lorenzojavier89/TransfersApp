using Moq;
using TransfersApp.Application;
using TransfersApp.Domain.Entities;
using TransfersApp.Domain.Exceptions;
using TransfersApp.Domain.Interfaces;
using Xunit;

namespace TransfersApp.UnitTests;

public class TransfersServiceTests
{
    [Fact]
    public async Task ApplyTransferAsync_ReturnsTransfer_WithCorrectProperties()
    {
        // Arrange
        var sourceId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();
        var expected = new Transfer
        {
            Id = Guid.NewGuid(),
            SourceAccountId = sourceId,
            DestinationAccountId = destinationId,
            Amount = 100m,
            Currency = "USD",
            OperationDate = DateTime.UtcNow
        };

        var mockRepo = new Mock<ITransferRepository>();
        mockRepo.Setup(r => r.GetAccountByIdAsync(sourceId))
            .ReturnsAsync(new Account { Id = sourceId, Name = "A", Currency = "USD" });
        mockRepo.Setup(r => r.GetAccountByIdAsync(destinationId))
            .ReturnsAsync(new Account { Id = destinationId, Name = "B", Currency = "USD" });
        mockRepo.Setup(r => r.ApplyTransferAsync(sourceId, destinationId, 100m, "USD"))
            .ReturnsAsync(expected);

        var service = new TransfersService(mockRepo.Object);

        // Act
        var result = await service.ApplyTransferAsync(sourceId, destinationId, 100m, "USD");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expected.Id, result.Id);
        Assert.Equal(sourceId, result.SourceAccountId);
        Assert.Equal(destinationId, result.DestinationAccountId);
        Assert.Equal(100m, result.Amount);
        Assert.Equal("USD", result.Currency);
        mockRepo.Verify(r => r.ApplyTransferAsync(sourceId, destinationId, 100m, "USD"), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task ApplyTransferAsync_InvalidAmount_ThrowsInvalidTransferAmountException(decimal amount)
    {
        var service = new TransfersService(new Mock<ITransferRepository>().Object);

        await Assert.ThrowsAsync<InvalidTransferAmountException>(
            () => service.ApplyTransferAsync(Guid.NewGuid(), Guid.NewGuid(), amount, "USD"));
    }

    [Fact]
    public async Task ApplyTransferAsync_SameSourceAndDestination_ThrowsSameAccountTransferException()
    {
        var accountId = Guid.NewGuid();
        var service = new TransfersService(new Mock<ITransferRepository>().Object);

        await Assert.ThrowsAsync<SameAccountTransferException>(
            () => service.ApplyTransferAsync(accountId, accountId, 100m, "USD"));
    }

    [Fact]
    public async Task ApplyTransferAsync_CurrencyMismatch_ThrowsCurrencyMismatchException()
    {
        var sourceId = Guid.NewGuid();
        var destinationId = Guid.NewGuid();

        var mockRepo = new Mock<ITransferRepository>();
        mockRepo.Setup(r => r.GetAccountByIdAsync(sourceId))
            .ReturnsAsync(new Account { Id = sourceId, Currency = "USD" });
        mockRepo.Setup(r => r.GetAccountByIdAsync(destinationId))
            .ReturnsAsync(new Account { Id = destinationId, Currency = "ARS" });

        var service = new TransfersService(mockRepo.Object);

        await Assert.ThrowsAsync<CurrencyMismatchException>(
            () => service.ApplyTransferAsync(sourceId, destinationId, 100m, "USD"));
    }
}
