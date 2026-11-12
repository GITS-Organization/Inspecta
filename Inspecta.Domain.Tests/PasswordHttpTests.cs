using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Inspecta.Domain.Enums;

namespace Inspecta.Domain.Tests;

/// <summary>
/// Changing a password or an email, over HTTP, the way the apps call it. Every test uses its own accounts and its own caller address
/// (X-Forwarded-For), so the sign-in limits of one test never reach another.
/// </summary>
public sealed class PasswordHttpTests : IClassFixture<ApiHttpTests.Api>
{
    private const string Fresh = "Brand-New-Passw0rd!";
    private static int caller = 10;
    private readonly ApiHttpTests.Api api;

    public PasswordHttpTests(ApiHttpTests.Api api)
    {
        this.api = api;
        api.EnsureSeeded(); // the teams exist before a test names one of them
    }

    private HttpClient Client()
    {
        api.EnsureSeeded();
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "10.20.30." + Interlocked.Increment(ref caller));
        return client;
    }

    private static async Task<(string Access, string Refresh, HttpStatusCode Status)> SignIn(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        if (!response.IsSuccessStatusCode) return ("", "", response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (json.RootElement.GetProperty("accessToken").GetString()!, json.RootElement.GetProperty("refreshToken").GetString()!, response.StatusCode);
    }

    private static void As(HttpClient client, string access) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);

    private static async Task<string> CodeOf(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString()! : string.Empty;
    }

    private static Task<HttpResponseMessage> Refresh(HttpClient client, string token) =>
        client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = token });

    // ---- a user changing their own password ----

    [Fact]
    public async Task AWrongCurrentPasswordIsA400NotA401AndChangesNothing()
    {
        // A 401 would make the app think its session ended and sign the person out.
        var user = api.AddUser("own-wrong@example.com", UserRole.Coordinator, api.ManagerA);
        var client = Client();
        var session = await SignIn(client, user.Email, ApiHttpTests.Password);
        As(client, session.Access);

        var response = await client.PostAsJsonAsync("/api/v1/auth/change-my-password", new { currentPassword = "not-my-password", newPassword = Fresh });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("current_password_incorrect", await CodeOf(response));
        Assert.Equal(HttpStatusCode.OK, (await SignIn(Client(), user.Email, ApiHttpTests.Password)).Status);
    }

    [Fact]
    public async Task ARightCurrentPasswordChangesItAndTheOldOneStopsWorking()
    {
        var user = api.AddUser("own-right@example.com", UserRole.Inspector, api.ManagerA);
        var client = Client();
        var session = await SignIn(client, user.Email, ApiHttpTests.Password);
        As(client, session.Access);

        var response = await client.PostAsJsonAsync("/api/v1/auth/change-my-password", new { currentPassword = ApiHttpTests.Password, newPassword = Fresh });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignIn(Client(), user.Email, ApiHttpTests.Password)).Status);
        Assert.Equal(HttpStatusCode.OK, (await SignIn(Client(), user.Email, Fresh)).Status);
    }

    [Fact]
    public async Task TheNewPasswordMustFollowThePolicyAndDifferFromTheOldOne()
    {
        var user = api.AddUser("own-policy@example.com", UserRole.Coordinator, api.ManagerA);
        var client = Client();
        As(client, (await SignIn(client, user.Email, ApiHttpTests.Password)).Access);

        var weak = await client.PostAsJsonAsync("/api/v1/auth/change-my-password", new { currentPassword = ApiHttpTests.Password, newPassword = "short" });
        var same = await client.PostAsJsonAsync("/api/v1/auth/change-my-password", new { currentPassword = ApiHttpTests.Password, newPassword = ApiHttpTests.Password });

        Assert.Equal(("password_policy_failed", HttpStatusCode.BadRequest), (await CodeOf(weak), weak.StatusCode));
        Assert.Equal(("password_unchanged", HttpStatusCode.BadRequest), (await CodeOf(same), same.StatusCode));
        Assert.Equal(HttpStatusCode.OK, (await SignIn(Client(), user.Email, ApiHttpTests.Password)).Status);
    }

    [Fact]
    public async Task ChangingItSignsOutTheOtherDevicesButNotThisOne()
    {
        var user = api.AddUser("own-devices@example.com", UserRole.Coordinator, api.ManagerA);
        var phone = Client();
        var laptop = Client();
        var onPhone = await SignIn(phone, user.Email, ApiHttpTests.Password);
        var onLaptop = await SignIn(laptop, user.Email, ApiHttpTests.Password);
        As(phone, onPhone.Access);

        var response = await phone.PostAsJsonAsync("/api/v1/auth/change-my-password",
            new { currentPassword = ApiHttpTests.Password, newPassword = Fresh, refreshToken = onPhone.Refresh });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await Refresh(phone, onPhone.Refresh)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(laptop, onLaptop.Refresh)).StatusCode);
    }

    [Fact]
    public async Task GuessingTheCurrentPasswordIsStoppedLikeGuessingAtSignIn()
    {
        var user = api.AddUser("own-guess@example.com", UserRole.Coordinator, api.ManagerA);
        var client = Client();
        As(client, (await SignIn(client, user.Email, ApiHttpTests.Password)).Access);

        HttpStatusCode last = default;
        for (var attempt = 0; attempt < 7; attempt++)
            last = (await client.PostAsJsonAsync("/api/v1/auth/change-my-password", new { currentPassword = "guess-" + attempt, newPassword = Fresh })).StatusCode;

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task ChangingItNeedsASignedInUser()
    {
        var response = await Client().PostAsJsonAsync("/api/v1/auth/change-my-password", new { currentPassword = "x", newPassword = Fresh });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- a technical manager and their own team ----

    [Fact]
    public async Task AManagerSetsATeamMembersPasswordAndTheirOldSessionsEnd()
    {
        var member = api.AddUser("team-reset@example.com", UserRole.Inspector, api.ManagerA);
        var memberClient = Client();
        var old = await SignIn(memberClient, member.Email, ApiHttpTests.Password);
        var manager = Client();
        As(manager, (await SignIn(manager, "company-admin-a@example.com", ApiHttpTests.Password)).Access);

        var response = await manager.PutAsJsonAsync($"/api/v1/manager/team/{member.Id}/password", new { newPassword = Fresh });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Refresh(memberClient, old.Refresh)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignIn(Client(), member.Email, ApiHttpTests.Password)).Status);
        Assert.Equal(HttpStatusCode.OK, (await SignIn(Client(), member.Email, Fresh)).Status);
    }

    [Fact]
    public async Task AResetAlsoLiftsALockCausedByWrongGuesses()
    {
        var member = api.AddUser("team-locked@example.com", UserRole.Coordinator, api.ManagerA);
        for (var attempt = 0; attempt < 6; attempt++) await SignIn(Client(), member.Email, "wrong-" + attempt);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await SignIn(Client(), member.Email, ApiHttpTests.Password)).Status);
        var manager = Client();
        As(manager, (await SignIn(manager, "company-admin-a@example.com", ApiHttpTests.Password)).Access);

        await manager.PutAsJsonAsync($"/api/v1/manager/team/{member.Id}/password", new { newPassword = Fresh });

        Assert.Equal(HttpStatusCode.OK, (await SignIn(Client(), member.Email, Fresh)).Status);
    }

    [Fact]
    public async Task AManagerCannotTouchAnotherTeamsMemberAnotherManagerOrAnAdmin()
    {
        var other = api.AddUser("team-other@example.com", UserRole.Inspector, api.ManagerB);
        var admin = api.AddUser("team-admin@example.com", UserRole.Admin);
        var manager = Client();
        As(manager, (await SignIn(manager, "company-admin-a@example.com", ApiHttpTests.Password)).Access);

        foreach (var target in new[] { other.Id, api.ManagerB, admin.Id })
        {
            var password = await manager.PutAsJsonAsync($"/api/v1/manager/team/{target}/password", new { newPassword = Fresh });
            var email = await manager.PutAsJsonAsync($"/api/v1/manager/team/{target}/email", new { email = "stolen@example.com" });
            Assert.Equal(HttpStatusCode.NotFound, password.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, email.StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await SignIn(Client(), other.Email, ApiHttpTests.Password)).Status);
    }

    [Fact]
    public async Task AStaffMemberCannotUseTheManagersEndpoints()
    {
        var coordinator = api.AddUser("team-staff@example.com", UserRole.Coordinator, api.ManagerA);
        var victim = api.AddUser("team-victim@example.com", UserRole.Inspector, api.ManagerA);
        var client = Client();
        As(client, (await SignIn(client, coordinator.Email, ApiHttpTests.Password)).Access);

        var response = await client.PutAsJsonAsync($"/api/v1/manager/team/{victim.Id}/password", new { newPassword = Fresh });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AManagerChangesATeamMembersEmailAndTheOldAddressNoLongerSignsIn()
    {
        var member = api.AddUser("team-email-old@example.com", UserRole.Coordinator, api.ManagerA);
        var manager = Client();
        As(manager, (await SignIn(manager, "company-admin-a@example.com", ApiHttpTests.Password)).Access);

        var response = await manager.PutAsJsonAsync($"/api/v1/manager/team/{member.Id}/email", new { email = "  Team-Email-New@Example.com " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("team-email-new@example.com", json.RootElement.GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignIn(Client(), "team-email-old@example.com", ApiHttpTests.Password)).Status);
        Assert.Equal(HttpStatusCode.OK, (await SignIn(Client(), "team-email-new@example.com", ApiHttpTests.Password)).Status);
    }

    [Fact]
    public async Task AnEmailAlreadyInUseOrNotAnEmailIsRefused()
    {
        var member = api.AddUser("team-email-dup@example.com", UserRole.Coordinator, api.ManagerA);
        var manager = Client();
        As(manager, (await SignIn(manager, "company-admin-a@example.com", ApiHttpTests.Password)).Access);

        var taken = await manager.PutAsJsonAsync($"/api/v1/manager/team/{member.Id}/email", new { email = "manager-b@example.com" });
        var invalid = await manager.PutAsJsonAsync($"/api/v1/manager/team/{member.Id}/email", new { email = "not-an-email" });

        Assert.Equal(("account_exists", HttpStatusCode.Conflict), (await CodeOf(taken), taken.StatusCode));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    // ---- an admin ----

    [Fact]
    public async Task AnAdminSetsAManagersPasswordAndEmailButAManagerCannotUseTheAdminEndpoints()
    {
        var admin = api.AddUser("admin-resets@example.com", UserRole.Admin);
        var target = api.AddUser("manager-target@example.com", UserRole.TechnicalManager);
        var adminClient = Client();
        As(adminClient, (await SignIn(adminClient, admin.Email, ApiHttpTests.Password)).Access);

        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}/password", new { newPassword = Fresh })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.PutAsJsonAsync($"/api/v1/users/{target.Id}/email", new { email = "manager-target-new@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SignIn(Client(), "manager-target-new@example.com", Fresh)).Status);

        var manager = Client();
        As(manager, (await SignIn(manager, "manager-a@example.com", ApiHttpTests.Password)).Access);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"/api/v1/users/{target.Id}/password", new { newPassword = Fresh })).StatusCode);
    }
}
