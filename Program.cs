using Scalar.AspNetCore;
using TransfersApp;
using TransfersApp.Application;
using TransfersApp.Domain.Interfaces;
using TransfersApp.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<ITransferRepository, InMemoryTransferRepository>();
builder.Services.AddSingleton<IIdempotencyService, InMemoryIdempotencyService>();
builder.Services.AddScoped<ITransfersService, TransfersService>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
