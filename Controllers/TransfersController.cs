using Microsoft.AspNetCore.Mvc;
using TransfersApp.Application;
using TransfersApp.Models;

namespace TransfersApp.Controllers;

[ApiController]
[Route("api/transfers")]
public class TransfersController : ControllerBase
{
    private readonly ITransfersService _service;

    public TransfersController(ITransfersService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTransfer(
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromBody] CreateTransferRequest request)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest("The 'Idempotency-Key' header is required.");

        var transfer = await _service.ApplyTransferAsync(
            request.SourceAccountId,
            request.DestinationAccountId,
            request.Amount,
            request.Currency);

        return CreatedAtAction(nameof(GetTransfer), new { id = transfer.Id }, transfer);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTransfer(Guid id)
    {
        var transfer = await _service.GetTransferByIdAsync(id);
        return transfer is null ? NotFound() : Ok(transfer);
    }
}
