namespace Vairas.UserManagement.Business;

/// <summary>The caller is authenticated but not allowed to perform the operation.</summary>
public sealed class ForbiddenException(string code, string message = BusinessException.DefaultMessage, Exception? innerException = null)
    : BusinessException(code, message, innerException);
