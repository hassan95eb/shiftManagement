var builder = WebApplication.CreateBuilder(args);

// Service registration (DI wiring for Application and Infrastructure) is added
// in later phases. See CLAUDE.md build order.
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
