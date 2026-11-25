namespace Vairas.UserManagement.Business;

/// <summary>The request is well formed but violates a business rule.</summary>
public sealed class UnprocessableEntityException(string code, string message = BusinessException.DefaultMessage, Exception? innerException = null)
    : BusinessException(code, message, innerException);
