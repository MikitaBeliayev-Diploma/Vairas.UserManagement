using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;
namespace Vairas.UserManagement.Controllers;

/// <summary>Exact identity/session supplied by a locally authenticated owner workload.</summary>
public sealed record AssistantActorRequest(Guid IdentityId, Guid SessionId);
