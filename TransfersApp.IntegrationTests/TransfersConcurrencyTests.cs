using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TransfersApp.IntegrationTests;

public class TransfersConcurrencyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TransfersConcurrencyTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ConcurrentTransfers_ExhaustBalance_ConsistentFinalState()
    {
        // Alice starts with 1000. 50 concurrent requests of 100 each.
        // The lock in InMemoryTransferRepository ensures exactly 10 succeed and 40 fail.
        const int count = 50;
        const decimal amount = 100m;

        var barrier = new Barrier(count);
        var tasks = Enumerable.Range(0, count).Select(_ => Task.Run(async () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
            {
                Content = JsonContent.Create(new
                {
                    sourceAccountId = "11111111-1111-1111-1111-111111111111",
                    destinationAccountId = "22222222-2222-2222-2222-222222222222",
                    amount,
                    currency = "USD"
                })
            };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

            barrier.SignalAndWait();

            return await _client.SendAsync(request);
        }));

        var responses = await Task.WhenAll(tasks);
        var statuses = responses.Select(r => r.StatusCode).ToList();

        Assert.Equal(10, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.Equal(40, statuses.Count(s => s == HttpStatusCode.UnprocessableEntity));

        var accountResponse = await _client.GetAsync(
            "/api/accounts/11111111-1111-1111-1111-111111111111");
        Assert.Equal(HttpStatusCode.OK, accountResponse.StatusCode);

        var account = await accountResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0m, account.GetProperty("balance").GetDecimal());
    }
}
