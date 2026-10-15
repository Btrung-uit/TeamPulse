var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");

app.MapGet("/api", () => Results.Ok(new
{
    name = "TeamPulse.Api",
    status = "running",
}))
    .WithName("GetApiInfo");

app.Run();

public partial class Program;
