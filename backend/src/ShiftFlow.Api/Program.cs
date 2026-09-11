using ShiftFlow.Api.Extensions;
using ShiftFlow.Api.Middleware;
using ShiftFlow.Api.Security;
using ShiftFlow.Application;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddUniformModelValidationErrors();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSwaggerWithBearer();
builder.Services.AddLocalDevCors();

var app = builder.Build();

// Development / AutoMigrate only: bring the schema up to date and insert the
// scenario seed. No-op in every other environment — production migrates as a
// separate deploy step (see DevelopmentDataExtensions).
await app.UseDevelopmentSeedAsync();

// First in the pipeline: everything downstream reports failures as one ApiError shape.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Only where Kestrel actually has an HTTPS endpoint to redirect to — a local
// `dotnet run` (launchSettings binds https://localhost:7194). In a container
// TLS terminates upstream (the compose port map now, the `web`/nginx service
// later), so there is no HTTPS port and this would only log a misleading
// "Failed to determine the https port for redirect." on startup.
if (!app.Configuration.GetValue<bool>("DOTNET_RUNNING_IN_CONTAINER"))
{
    app.UseHttpsRedirection();
}

app.UseCors(CorsExtensions.LocalDevPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>Exposed so integration tests can host the API with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
