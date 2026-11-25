namespace Vairas.UserManagement.Business;

/// <summary>The resource does not exist or is outside the caller's scope.</summary>
public sealed class NotFoundException(string code, string message = BusinessException.DefaultMessage, Exception? innerException = null)
    : BusinessException(code, message, innerException);
