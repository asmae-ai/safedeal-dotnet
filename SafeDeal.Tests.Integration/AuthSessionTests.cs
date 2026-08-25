using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace SafeDeal.Tests.Integration;

/// <summary>
/// Cycle de vie de la session : rafraîchissement, 2FA et vérification d'e-mail.
/// </summary>
[Collection(SafeDealCollection.Name)]
public class AuthSessionTests
{
    private readonly SafeDealFactory _factory;

    public AuthSessionTests(SafeDealFactory factory) => _factory = factory;

    private async Task<AuthBody> RegisterAsync(string email, bool verifyEmail = true, string? phone = null)
    {
        var client = _factory.Anonymous();
        var response = await client.PostAsJsonAsync("/api/v1/register", new
        {
            name = "Session Test",
            email,
            password = "password123",
            passwordConfirmation = "password123",
            role = "buyer",
            phone,
        });
        response.EnsureSuccessStatusCode();
        var body = (await response.Content.ReadFromJsonAsync<AuthBody>())!;

        if (verifyEmail)
        {
            var code = _factory.Emails.Sent.Last(e => e.To == email && e.Subject == "verification").Code;
            client.DefaultRequestHeaders.Authorization = new("Bearer", body.Token);
            (await client.PostAsJsonAsync("/api/v1/auth/email/verify", new { code })).EnsureSuccessStatusCode();
        }

        return body;
    }

    private static string NewEmail() => $"session{Guid.NewGuid():N}@safedeal.test";

    // ------------------------------------------------------------------ A-10

    [Fact]
    public async Task A10_La_connexion_delivre_un_jeton_de_rafraichissement()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var login = await _factory.Anonymous()
            .PostAsJsonAsync("/api/v1/login", new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<AuthBody>();

        body!.Token.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task A10_Un_jeton_de_rafraichissement_rend_une_session_utilisable()
    {
        var email = NewEmail();
        var registered = await RegisterAsync(email);

        var refreshed = await _factory.Anonymous()
            .PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = registered.RefreshToken });
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await refreshed.Content.ReadFromJsonAsync<AuthBody>();
        body!.Token.Should().NotBeNullOrEmpty();

        var client = _factory.Anonymous();
        client.DefaultRequestHeaders.Authorization = new("Bearer", body.Token);
        (await client.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A10_Un_jeton_de_rafraichissement_ne_sert_qu_une_fois()
    {
        var email = NewEmail();
        var registered = await RegisterAsync(email);

        var first = await _factory.Anonymous()
            .PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = registered.RefreshToken });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        // Rejouer le meme jeton est le signe d'un vol, pas un usage normal.
        var replay = await _factory.Anonymous()
            .PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = registered.RefreshToken });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A10_Le_rafraichissement_fait_tourner_le_jeton()
    {
        var email = NewEmail();
        var registered = await RegisterAsync(email);

        var refreshed = await _factory.Anonymous()
            .PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = registered.RefreshToken });
        var body = await refreshed.Content.ReadFromJsonAsync<AuthBody>();

        body!.RefreshToken.Should().NotBeNullOrEmpty().And.NotBe(registered.RefreshToken);
    }

    [Fact]
    public async Task A10_Un_jeton_inconnu_est_refuse()
    {
        var response = await _factory.Anonymous()
            .PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "inexistant" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------------ I-10

    [Fact]
    public async Task I10_Un_email_non_verifie_bloque_la_connexion_avec_un_drapeau_exploitable()
    {
        var email = NewEmail();
        await RegisterAsync(email, verifyEmail: false);

        var login = await _factory.Anonymous()
            .PostAsJsonAsync("/api/v1/login", new { email, password = "password123" });

        login.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await login.Content.ReadAsStringAsync()).Should().Contain("email_verified");
    }

    [Fact]
    public async Task I10_La_2FA_active_retient_le_jeton_jusqu_a_la_saisie_du_code()
    {
        var email = NewEmail();
        var registered = await RegisterAsync(email);

        var client = _factory.Anonymous();
        client.DefaultRequestHeaders.Authorization = new("Bearer", registered.Token);
        (await client.PostAsJsonAsync("/api/v1/me/two-factor", new { enabled = true })).EnsureSuccessStatusCode();

        var login = await _factory.Anonymous()
            .PostAsJsonAsync("/api/v1/login", new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<AuthBody>();

        body!.RequiresTwoFactor.Should().BeTrue();
        body.Token.Should().BeNull();

        var code = _factory.Emails.LastOtpFor(email);
        var verified = await _factory.Anonymous()
            .PostAsJsonAsync("/api/v1/verify-2fa", new { email, code });

        verified.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await verified.Content.ReadFromJsonAsync<AuthBody>();
        session!.Token.Should().NotBeNullOrEmpty();
        session.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task I10_Un_code_2FA_ne_sert_qu_une_fois()
    {
        var email = NewEmail();
        var registered = await RegisterAsync(email);

        var client = _factory.Anonymous();
        client.DefaultRequestHeaders.Authorization = new("Bearer", registered.Token);
        await client.PostAsJsonAsync("/api/v1/me/two-factor", new { enabled = true });
        await _factory.Anonymous().PostAsJsonAsync("/api/v1/login", new { email, password = "password123" });

        var code = _factory.Emails.LastOtpFor(email);
        await _factory.Anonymous().PostAsJsonAsync("/api/v1/verify-2fa", new { email, code });

        var replay = await _factory.Anonymous().PostAsJsonAsync("/api/v1/verify-2fa", new { email, code });
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------------ I-11

    [Fact]
    public async Task I11_Le_telephone_saisi_a_l_inscription_est_conserve()
    {
        var email = NewEmail();

        var body = await RegisterAsync(email, phone: "+212611223344");

        body.User!.Phone.Should().Be("+212611223344");
    }

    [Fact]
    public async Task I11_Un_telephone_invalide_est_refuse()
    {
        var response = await _factory.Anonymous().PostAsJsonAsync("/api/v1/register", new
        {
            name = "Bad Phone",
            email = NewEmail(),
            password = "password123",
            passwordConfirmation = "password123",
            role = "buyer",
            phone = "pas-un-numero",
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}

public record AuthBody(string? Token, UserBody? User, bool RequiresTwoFactor, string? RefreshToken);
