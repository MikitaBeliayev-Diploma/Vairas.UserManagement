namespace Vairas.UserManagement.Business;

/// <summary>A dependency or the database is temporarily unavailable.</summary>
public sealed class ServiceUnavailableException(string code, string message = BusinessException.DefaultMessage, Exception? innerException = null)
    : BusinessException(code, message, innerException);
