using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vairas.ConfirmedActions;

/// <summary>Scheduling-only exact execution authorization or recovery-evidence query.</summary>
public sealed record LessonAuthorizationQuery(LessonAction Action, bool Reconcile = false);
