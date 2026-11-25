using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.UserManagement.Business;
using Vairas.UserManagement.Domain.Entities;
namespace Vairas.UserManagement.Controllers;

/// <summary>Minimal current active profile; no contacts or cross-school permissions.</summary>
public sealed record AssistantActorResponse(Guid UserId, string Status, Guid? StudentId, Guid? TeacherId, Guid? AdministratorId);
