using Microsoft.AspNetCore.Mvc;
using TransfersApp.Application;
using TransfersApp.Models;

namespace TransfersApp.Controllers;

[ApiController]
[Route("api/transfers")]
public class TransfersController : ControllerBase
{
    private readonly ITransfersService _service;
    private readonly IIdempotencyService _idempotencyService;

    public TransfersController(ITransfersService service, IIdempotencyService idempotencyService)
    {
        _service = service;
        _idempotencyService = idempotencyService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransfer(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] CreateTransferRequest request)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Problem(
                detail: "The 'Idempotency-Key' header is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");

        var bodyHash = $"{request.SourceAccountId}|{request.DestinationAccountId}|{request.Amount}|{request.Currency}";

        var result = await _idempotencyService.ExecuteAsync(
            idempotencyKey,
            bodyHash,
            () => _service.ApplyTransferAsync(
                request.SourceAccountId,
                request.DestinationAccountId,
                request.Amount,
                request.Currency));

        return result switch
        {
            NewTransfer(var t)    => CreatedAtAction(nameof(GetTransfer), new { id = t.Id }, t),
            CachedTransfer(var t) => CreatedAtAction(nameof(GetTransfer), new { id = t.Id }, t),
            ConflictingBody       => Problem(
                detail: "A different transfer with the same Idempotency-Key already exists.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Conflict"),
            _                     => throw new InvalidOperationException("Unexpected idempotency result")
        };
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTransfer(Guid id)
    {
        var transfer = await _service.GetTransferByIdAsync(id);
        return transfer is null
            ? Problem(detail: $"Transfer '{id}' was not found.", statusCode: StatusCodes.Status404NotFound, title: "Not Found")
            : Ok(transfer);
    }
}
