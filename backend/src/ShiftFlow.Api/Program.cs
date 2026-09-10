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
// manual-testing login seed. No-op in every other environment.
await app.UseDevelopmentSeedAsync();

// First in the pipeline: everything downstream reports failures as one ApiError shape.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(CorsExtensions.LocalDevPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>Exposed so integration tests can host the API with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
