namespace Vairas.UserManagement.Business;

/// <summary>The caller is not authenticated or the session is invalid.</summary>
public sealed class UnauthorizedException(string code, string message = BusinessException.DefaultMessage, Exception? innerException = null)
    : BusinessException(code, message, innerException);
