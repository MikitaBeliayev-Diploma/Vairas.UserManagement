using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;

namespace Vairas.UserManagement.Controllers;

/// <summary>Minimal identity binding. Email ownership is validated separately with Authentication.</summary>
public sealed record NotificationBindingResponse(Guid UserId, Guid IdentityId, bool Active, long Revision);
