using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vairas.UserManagement.Business;
namespace Vairas.UserManagement.Controllers;

/// <summary>Minimal identity fields for a specifically allowlisted document workload.</summary>
public sealed record DocumentProfileResponse(Guid UserId, string FirstName, string LastName, long Revision);
