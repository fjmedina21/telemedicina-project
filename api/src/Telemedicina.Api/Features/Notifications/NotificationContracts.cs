using System.ComponentModel.DataAnnotations;

namespace Telemedicina.Api.Features.Notifications;

public sealed record NotificationPreferencesRequest(bool EmailEnabled, [Range(0, int.MaxValue)] int Version);
