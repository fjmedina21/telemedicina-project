using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Telemedicina.Api.Infrastructure.Email;
using static Telemedicina.Api.Infrastructure.Authentication.Security;
using System;
using System.Collections.Generic;
using Telemedicina.Common.Contracts.Health;
using Telemedicina.Common.Models.ApiResponses;

namespace Telemedicina.Tests;

public class Program
{
    private static int _passedCount = 0;
    private static int _failedCount = 0;

    public static int Main(string[] args)
    {
        if (args.Contains("--demo-reset-db-tests", StringComparer.Ordinal)) return DemoResetDbChecks.Run();
        Console.WriteLine("=================================================");
        Console.WriteLine(" Running Telemedicina Automated Unit Tests");
        Console.WriteLine("=================================================");

        Test("BaseApiResponse_Success_TrueFor2xxAnd3xx", () =>
        {
            var r200 = new ApiResponse<string>("data");
            Assert(r200.Success, "200 should be successful");
            Assert(r200.Status == 200, "Status must match 200");

            var r201 = new ApiResponse<string>(201, "Created", "id-123");
            Assert(r201.Success, "201 should be successful");

            var r400 = new ApiResponse<string>(400);
            Assert(!r400.Success, "400 should not be successful");

            var r404 = new ApiResponse<string>(404);
            Assert(!r404.Success, "404 should not be successful");

            var r409 = new ApiResponse<string>(409);
            Assert(!r409.Success, "409 Conflict should not be successful");
            Assert(r409.Detail.Contains("conflict", StringComparison.OrdinalIgnoreCase), "Detail should explain conflict");
        });

        Test("ApiResponse_DataPayload_PreservesContent", () =>
        {
            var payload = new { PatientId = "pat-001", Name = "Juan Perez" };
            var response = new ApiResponse<object>(200, "Patient retrieved", payload);

            Assert(response.Data != null, "Data should not be null");
            Assert(response.Detail == "Patient retrieved", "Detail must match");
            Assert(response.Status == 200, "Status must be 200");
        });

        Test("ErrorApiResponse_RFC7807Properties_Populated", () =>
        {
            var errors = new List<string> { "Field 'email' is required.", "Invalid date of birth." };
            var errorResponse = new ErrorApiResponse(
                status: 400,
                title: "Validation failed",
                instance: "/api/v1/auth/register",
                errors: errors
            );

            Assert(!errorResponse.Success, "ErrorApiResponse should have Success=false");
            Assert(errorResponse.Status == 400, "Status must be 400");
            Assert(errorResponse.Title == "Validation failed", "Title must match");
            Assert(errorResponse.Instance == "/api/v1/auth/register", "Instance must match");
            Assert(errorResponse.Errors.Count == 2, "Should contain 2 error messages");
            Assert(errorResponse.Type.Contains("400"), "Type URI should reference 400");
        });

        Test("PagedResult_PaginationMath_CalculatesCorrectly", () =>
        {
            var items = new List<string> { "item1", "item2", "item3", "item4", "item5" };
            var paged = new PagedResult<string>(items, count: 23, pageNumber: 2, pageSize: 5);

            Assert(paged.TotalPages == 5, "Total pages for 23 items with pageSize 5 should be 5");
            Assert(paged.HasPrevious == true, "Page 2 should have previous page");
            Assert(paged.HasNext == true, "Page 2 of 5 should have next page");

            var pagedLast = new PagedResult<string>(items, count: 23, pageNumber: 5, pageSize: 5);
            Assert(pagedLast.HasNext == false, "Page 5 of 5 should not have next page");

            var paginatedResponse = new PaginatedApiResponse<string>(paged);
            Assert(paginatedResponse.Success, "PaginatedApiResponse must have Success=true");
            Assert(paginatedResponse.TotalCount == 23, "TotalCount must match");
            Assert(paginatedResponse.CurrentPage == 2, "CurrentPage must match");
            Assert(paginatedResponse.Data.Count == 5, "Data items count must be 5");
        });

        Test("HealthCheckResponse_Contract_ValidatesStructure", () =>
        {
            var now = DateTime.UtcNow;
            var health = new HealthCheckResponse("Healthy", "Telemedicina.Api", "Testing", now, "1.0.0");

            Assert(health.Status == "Healthy", "Status must be Healthy");
            Assert(health.Service == "Telemedicina.Api", "Service must match");
            Assert(health.Environment == "Testing", "Environment must match");
            Assert(health.Version == "1.0.0", "Version must match");
            Assert(health.TimestampUtc == now, "TimestampUtc must match");
        });

        Test("ErrorApiResponse_SerializedContract_PreservesClientFields", () =>
        {
            var response = new ErrorApiResponse(400, "Solicitud inválida", "/api/v1/auth/verify")
            {
                Code = "INVALID_CHALLENGE", TraceId = "synthetic-trace"
            };
            var json = System.Text.Json.JsonSerializer.Serialize(response,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            using var parsed = System.Text.Json.JsonDocument.Parse(json);
            var root = parsed.RootElement;
            Assert(!root.GetProperty("success").GetBoolean(), "Errors must set success=false.");
            Assert(root.GetProperty("status").GetInt32() == 400, "Body and HTTP status must match.");
            Assert(root.GetProperty("code").GetString() == "INVALID_CHALLENGE", "Application codes must remain stable.");
            Assert(root.GetProperty("traceId").GetString() == "synthetic-trace", "Trace metadata must remain available.");
            Assert(root.GetProperty("errors").GetArrayLength() == 0, "No submitted secrets may be echoed.");
        });

        Test("VerificationCode_SixAsciiAlphanumericCharacters", () =>
        {
            for (var i = 0; i < 1000; i++)
            {
                var code = NewVerificationCode();
                Assert(code.Length == 6 && System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z0-9]{6}$"),
                    "Registration verification must contain exactly six ASCII letters or digits.");
            }
            Assert(NewToken().Length == 64, "Session and password-reset tokens must retain their length.");
        });
        Test("VerificationHash_ScopedAndCaseInsensitive", () =>
        {
            Assert(VerificationHash("one", 123, "Ab12Cd") == VerificationHash("one", 123, "AB12CD"),
                "Code matching must accept lowercase.");
            Assert(VerificationHash("one", 123, "AB12CD") != VerificationHash("two", 123, "AB12CD"),
                "The same code must not cross account boundaries.");
            Assert(VerificationHash("one", 123, "AB12CD") != VerificationHash("one", 124, "AB12CD"),
                "Different challenges must have separate hashes.");
        });

        foreach (var (environment, configured, expected) in new[]
        {
            ("Development", (string?)null, true),
            ("Development", "false", false),
            ("Production", "false", true),
            ("Staging", "false", true)
        })
        {
            Test($"MailRevocation_{environment}_{configured ?? "default"}", () =>
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    EnvironmentName = environment
                });
                var config = new ConfigurationBuilder().AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Mail:CheckCertificateRevocation"] = configured }).Build();
                var settings = new MailSettings(config, builder.Environment);
                Assert(settings.CheckCertificateRevocation == expected,
                    "Revocation opt-out must only work in Development and remain opt-in.");
            });
        }

        Test("StaffPermissions_DenyByDefaultAndNoAdminInheritance", () =>
        {
            foreach (var role in new[] { "Practitioner", "Support", "Unknown", "" })
            {
                Assert(!Telemedicina.Api.Infrastructure.Authentication.AccessCatalog.Allows(role, "staff.read"),
                    "Only administrator reads staff.");
                Assert(!Telemedicina.Api.Infrastructure.Authentication.AccessCatalog.Allows(role, "catalog.read"),
                    "New staff roles receive no operational access.");
            }
            Assert(Telemedicina.Api.Infrastructure.Authentication.AccessCatalog.Allows("Administrator", "staff.invite"),
                "Administrator may invite managed staff.");
            Assert(!Telemedicina.Api.Infrastructure.Authentication.AccessCatalog.Allows("Administrator", "admission.manage"),
                "Administrator does not inherit admission.");
            Assert(!Telemedicina.Api.Infrastructure.Authentication.AccessCatalog.IsManagedStaff("Administrator"),
                "Administrator is not an HTTP-managed staff target.");
            Assert(Telemedicina.Api.Infrastructure.Authentication.AccessCatalog.Allows("Admission", "patient.state.change"),
                "Preserve admission's existing patient lifecycle permission.");
        });
        foreach (var environment in new[] { "Production", "Staging" })
        {
            Test("StaffSeed_Rejects_" + environment, () =>
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
                var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Telemedicina"] = "Host=127.0.0.1;Database=synthetic;Username=synthetic",
                    ["Milestone:Enabled"] = "true"
                }).Build();
                var rejected = false;
                try
                {
                    Telemedicina.Api.Infrastructure.Persistence.Seeding.StaffSyntheticSeeder.Seed(
                        new Telemedicina.Api.Infrastructure.Persistence.Database(config), config, builder.Environment);
                }
                catch (InvalidOperationException error)
                {
                    rejected = error.Message == "Synthetic accounts require explicit Development configuration.";
                }
                Assert(rejected, "Seed must reject before opening a database connection.");
            });
        }

        DemoResetUnitChecks.Register(Test, Assert);

        Console.WriteLine("-------------------------------------------------");
        Console.WriteLine($" Summary: {_passedCount} passed, {_failedCount} failed.");
        Console.WriteLine("=================================================");

        return _failedCount == 0 ? 0 : 1;
    }

    private static void Test(string name, Action assertion)
    {
        try
        {
            assertion();
            Console.WriteLine($" [PASS] {name}");
            _passedCount++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($" [FAIL] {name}: {ex.Message}");
            _failedCount++;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception(message);
        }
    }
}
