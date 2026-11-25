using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Business;

/// <summary>Factories for the common application errors; each returns the category exception for the intended HTTP semantics.</summary>
public static class ServiceErrors
{
    public static ForbiddenException Forbidden() => new("ACCESS_DENIED", "Access denied.");
    public static NotFoundException Missing() => new("NOT_FOUND", "Resource not found or not visible.");
    public static ServiceUnavailableException Dependency() => new("DEPENDENCY_UNAVAILABLE", "A required dependency is unavailable.");

    /// <summary>Rebuilds the category from a persisted outcome status (idempotent replay of a stored decision).</summary>
    public static BusinessException FromStatus(int status, string code) => status switch
    {
        400 => new BadRequestException(code), 401 => new UnauthorizedException(code), 403 => new ForbiddenException(code),
        404 => new NotFoundException(code), 409 => new ConflictException(code), 422 => new UnprocessableEntityException(code),
        503 => new ServiceUnavailableException(code),
        _ => throw new InvalidOperationException("A persisted outcome carries an unsupported status.")
    };
}
