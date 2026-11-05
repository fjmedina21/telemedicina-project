using System.ComponentModel.DataAnnotations;

namespace Telemedicina.Api.Shared.Contracts;

public sealed record VersionRequest([Range(1, int.MaxValue)] int Version);
