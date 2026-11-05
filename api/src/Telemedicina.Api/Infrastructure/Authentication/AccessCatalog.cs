namespace Telemedicina.Api.Infrastructure.Authentication;

/// <summary>Closed server catalog. No inherited administrator/clinical permissions.</summary>
public static class AccessCatalog
{
    public static bool IsManagedStaff(string role) => role is "Admission" or "Practitioner" or "Support";
    public static string[] Permissions(string role) => role switch
    {
        "Patient" => ["identity.self", "catalog.read", "appointments.self", "profile.self", "notifications.self"],
        "Admission" => ["identity.self", "catalog.read", "admission.manage", "catalog.manage",
            "schedule.manage", "patient.state.change", "audit.read", "delivery.read"],
        "Practitioner" or "Support" => ["identity.self"],
        "Administrator" => ["identity.self", "staff.read", "staff.invite", "staff.role.change",
            "staff.state.change", "staff.sessions.revoke", "staff.audit.read"],
        _ => []
    };
    public static bool Allows(string role, string permission) =>
        Array.IndexOf(Permissions(role), permission) >= 0;
}
