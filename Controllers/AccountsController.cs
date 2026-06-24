using Microsoft.AspNetCore.Mvc;
using TransfersApp.Application;

namespace TransfersApp.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly ITransfersService _service;

    public AccountsController(ITransfersService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAccount(Guid id)
    {
        var account = await _service.GetAccountByIdAsync(id);
        return account is null ? NotFound() : Ok(account);
    }
}
