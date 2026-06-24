using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        // 5 transfers of 10.00 from Alice to Bob, each with a unique key — all new requests
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

    [Fact]
    public async Task PostTransfer_SameKeySameBody_AllReturn201WithSameTransferId()
    {
        // Same key + same body sent 5 times concurrently — only one transfer processes,
        // all responses must be 201 and contain the same transfer id.
        const string key = "idempotent-same-body-key";
        var body = new
        {
            sourceAccountId = "11111111-1111-1111-1111-111111111111",
            destinationAccountId = "22222222-2222-2222-2222-222222222222",
            amount = 5.00m,
            currency = "USD"
        };

        var tasks = Enumerable.Range(0, 5).Select(_ =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.Add("Idempotency-Key", key);
            return _client.SendAsync(request);
        });

        var responses = await Task.WhenAll(tasks);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var ids = await Task.WhenAll(responses.Select(async r =>
        {
            var json = await r.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("id").GetString();
        }));

        Assert.All(ids, id => Assert.Equal(ids[0], id));
    }

    [Fact]
    public async Task PostTransfer_MissingIdempotencyKey_Returns400ProblemDetails()
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

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task PostTransfer_SameKeyDifferentBody_AtLeastOneCreatedRestConflict()
    {
        // Same key, 5 different amounts — one wins and returns 201, the rest return 409
        const string key = "idempotent-different-body-key";

        var tasks = Enumerable.Range(1, 5).Select(i =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/transfers")
            {
                Content = JsonContent.Create(new
                {
                    sourceAccountId = "11111111-1111-1111-1111-111111111111",
                    destinationAccountId = "22222222-2222-2222-2222-222222222222",
                    amount = 10m + i,
                    currency = "USD"
                })
            };
            request.Headers.Add("Idempotency-Key", key);
            return _client.SendAsync(request);
        });

        var responses = await Task.WhenAll(tasks);
        var statuses = responses.Select(r => r.StatusCode).ToList();

        Assert.Contains(HttpStatusCode.Created, statuses);
        Assert.Contains(HttpStatusCode.Conflict, statuses);
    }
}
