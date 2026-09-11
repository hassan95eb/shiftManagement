using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Auth.Dtos;

namespace ShiftFlow.Application.Features.Auth;

/// <summary>
/// The one use case of the auth phase: exchange a username and password for a
/// signed access token. Only <c>Users</c> is read; the Supervisor / CallAgent row is
/// pulled alongside so its id can go into the token claims.
/// </summary>
public sealed class AuthService
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _tokenService;
    private readonly IAttendanceRecorder _attendance;

    public AuthService(
        IAppDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenService tokenService,
        IAttendanceRecorder attendance)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _attendance = attendance;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .Include(u => u.Supervisor)
            .Include(u => u.CallAgent)
            .SingleOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user is null
            || !user.IsActive
            || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var supervisorId = user.Supervisor?.Id;
        var callAgentId = user.CallAgent?.Id;

        var token = _tokenService.CreateAccessToken(user, supervisorId, callAgentId);

        if (callAgentId is not null)
        {
            try
            {
                await _attendance.OpenForLoginAsync(callAgentId.Value, cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Presence is best-effort: a missed login record costs at most
                // one heartbeat interval, while rejecting valid credentials
                // here could cost the CallAgent the whole shift.
            }
        }

        return new LoginResponse(
            token.Token,
            token.ExpiresAtUtc,
            "Bearer",
            user.Id,
            user.Role.ToString(),
            supervisorId,
            callAgentId);
    }
}
