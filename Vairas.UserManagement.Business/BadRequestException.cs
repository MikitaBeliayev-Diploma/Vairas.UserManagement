namespace Vairas.UserManagement.Business;

/// <summary>The request is malformed or fails validation.</summary>
public sealed class BadRequestException(string code, string message = BusinessException.DefaultMessage, Exception? innerException = null)
    : BusinessException(code, message, innerException);
