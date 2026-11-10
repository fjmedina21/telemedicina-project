using Microsoft.Extensions.Configuration;
using Telemedicina.Api.Features.Identity;

namespace Telemedicina.Tests;

public static class StaffInvitationUnitChecks
{
    public static void Register(Action<string, Action> test, Action<bool, string> assert)
    {
        foreach (var origin in new[] { "http://localhost:3000", "http://127.0.0.1:3000/", "http://[::1]:3000",
            "https://portal.example.invalid" })
            test("StaffInvitation_ValidPortal_" + origin, () =>
            {
                var config = new ConfigurationBuilder().AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Milestone:PortalOrigin"] = origin }).Build();
                var url = StaffInvitations.ActivationUrl(config);
                assert(url == new Uri(origin).GetLeftPart(UriPartial.Authority) + "/activar-personal",
                    "Activation uses a public page without token in URL.");
            });
        foreach (var origin in new[] { "", "/relative", "ftp://portal.example.invalid", "http://portal.example.invalid",
            "https://user:password@portal.example.invalid", "https://portal.example.invalid/?token=secret",
            "https://portal.example.invalid/#secret", "https://portal.example.invalid/subpath" })
            test("StaffInvitation_RejectPortal_" + origin, () =>
            {
                var config = new ConfigurationBuilder().AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Milestone:PortalOrigin"] = origin }).Build();
                var rejected = false;
                try { StaffInvitations.ActivationUrl(config); }
                catch (InvalidOperationException) { rejected = true; }
                assert(rejected, "Invalid activation origin must fail before persistence.");
            });
    }
}
