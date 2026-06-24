using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TransfersApp.IntegrationTests;

public class TransfersApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TransfersApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostTransfer_MultipleParallelRequests_AllReturn201()
    {
        // 5 transfers of 10.00 from Alice to Bob; total 50.00 < Alice's 1000.00 starting balance
        const int count = 5;
        var tasks = Enumerable.Range(0, count).Select(_ =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
            {
                Content = JsonContent.Create(new
                {
                    sourceAccountId = "11111111-1111-1111-1111-111111111111",
                    destinationAccountId = "22222222-2222-2222-2222-222222222222",
                    amount = 10.00m,
                    currency = "USD"
                })
            };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            return _client.SendAsync(request);
        });

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
    }
}
