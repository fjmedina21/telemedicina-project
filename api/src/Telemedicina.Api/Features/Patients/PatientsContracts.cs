using System.ComponentModel.DataAnnotations;

namespace Telemedicina.Api.Features.Patients;

public sealed record ProfileRequest(
    [Required, MaxLength(80)] string FirstName,
    [Required, MaxLength(80)] string LastName,
    DateOnly BirthDate,
    [Required, Phone, MaxLength(30)] string Phone,
    [Required, MaxLength(250)] string Address,
    [Required, MaxLength(100)] string EmergencyName,
    [Required, Phone, MaxLength(30)] string EmergencyPhone,
    [MaxLength(20)] string[] Allergies,
    [MaxLength(20)] string[] Medications,
    [MaxLength(20)] string[] Conditions,
    [Range(0, int.MaxValue)] int Version);
