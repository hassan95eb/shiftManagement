using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Projects.Dtos;

namespace ShiftFlow.Application.Features.Projects.Validators;

/// <summary>
/// Semantic validation for the project write DTOs, on top of the shape checks
/// the <c>[ApiController]</c> model binder already runs. Trims the name and
/// rejects a blank or over-long value with the uniform 400 error shape.
/// </summary>
public static class ProjectRequestValidator
{
    private const int NameMaxLength = 128;

    /// <returns>The trimmed project name.</returns>
    public static string ValidateAndNormalize(CreateProjectRequest request) =>
        NormalizeName(request.Name);

    /// <returns>The trimmed project name and the resolved active flag.</returns>
    public static (string Name, bool IsActive) ValidateAndNormalize(UpdateProjectRequest request)
    {
        var name = NormalizeName(request.Name);

        if (request.IsActive is null)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["isActive"] = ["The isActive field is required."],
            });
        }

        return (name, request.IsActive.Value);
    }

    private static string NormalizeName(string? rawName)
    {
        var name = (rawName ?? string.Empty).Trim();

        var errors = new Dictionary<string, string[]>();
        if (name.Length == 0)
        {
            errors["name"] = ["The name field is required."];
        }
        else if (name.Length > NameMaxLength)
        {
            errors["name"] = [$"The name must be at most {NameMaxLength} characters."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return name;
    }
}
