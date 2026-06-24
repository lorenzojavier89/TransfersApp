using Moq;
using TransfersApp.Application;
using TransfersApp.Domain.Entities;
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
        mockRepo
            .Setup(r => r.ApplyTransferAsync(sourceId, destinationId, 100m, "USD"))
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
}
