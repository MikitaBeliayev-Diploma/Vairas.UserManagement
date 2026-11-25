using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vairas.ConfirmedActions;

/// <summary>Frozen owner arguments and source basis, never model-supplied execution input.</summary>
public sealed record LessonAction(LessonActionBinding Binding, JsonElement Arguments, JsonElement SourceRevisions);
