using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Experts.Dtos;

namespace ShiftFlow.Application.Features.Experts.Validators;

/// <summary>
/// Semantic validation for <see cref="CreateExpertRequest"/>, on top of the
/// model binder's shape checks. Trims the username and full name, rejects blanks,
/// and leaves the password untouched (it is only ever hashed, never stored raw).
/// </summary>
public static class CreateExpertRequestValidator
{
    private const int UsernameMaxLength = 64;
    private const int FullNameMaxLength = 128;
    private const int PasswordMaxLength = 128;

    /// <returns>The trimmed username and full name, and the password verbatim.</returns>
    public static (string Username, string Password, string FullName) ValidateAndNormalize(
        CreateExpertRequest request)
    {
        var username = (request.Username ?? string.Empty).Trim();
        var fullName = (request.FullName ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;

        var errors = new Dictionary<string, string[]>();

        if (username.Length == 0)
        {
            errors["username"] = ["The username field is required."];
        }
        else if (username.Length > UsernameMaxLength)
        {
            errors["username"] = [$"The username must be at most {UsernameMaxLength} characters."];
        }

        if (password.Length == 0)
        {
            errors["password"] = ["The password field is required."];
        }
        else if (password.Length > PasswordMaxLength)
        {
            errors["password"] = [$"The password must be at most {PasswordMaxLength} characters."];
        }

        if (fullName.Length == 0)
        {
            errors["fullName"] = ["The fullName field is required."];
        }
        else if (fullName.Length > FullNameMaxLength)
        {
            errors["fullName"] = [$"The fullName must be at most {FullNameMaxLength} characters."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return (username, password, fullName);
    }
}
