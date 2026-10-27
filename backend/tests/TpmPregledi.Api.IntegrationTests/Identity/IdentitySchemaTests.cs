using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TpmPregledi.Api.IntegrationTests.Infrastructure;

namespace TpmPregledi.Api.IntegrationTests.Identity;

/// <summary>
/// Migrirana šema odgovara blueprint-u 05: Identity tabele zadržavaju <c>AspNet*</c> imena sa snake_case
/// kolonama; sistemske i domenske tabele su snake_case jednina; <c>refresh_token</c> ima <c>token_hash</c>
/// (i nema sirovi token); <c>audit_log</c> ima <c>ip_address</c>; jedinstveni indeksi nose <c>ux_</c> imena.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class IdentitySchemaTests(CustomWebAppFactory factory)
{
    private Task<List<string>> UpitAsync(string sql) =>
        factory.SaBazomAsync(db => db.Database.SqlQueryRaw<string>(sql).ToListAsync());

    [Fact]
    public async Task Tabele_su_AspNet_za_Identity_i_snake_case_jednina_za_ostalo()
    {
        var tabele = await UpitAsync("SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'");

        tabele.Should().Contain(["AspNetUsers", "AspNetRoles", "AspNetUserRoles", "refresh_token", "audit_log", "kategorija"]);
        tabele.Should().NotContain(["refresh_tokens", "kategorije", "asp_net_users"]);
    }

    [Fact]
    public async Task AspNetUsers_ima_snake_case_kolone_ukljucujuci_nasa_polja()
    {
        var kolone = await UpitAsync("SELECT column_name FROM information_schema.columns WHERE table_name = 'AspNetUsers'");

        kolone.Should().Contain(["id", "user_name", "normalized_user_name", "password_hash", "full_name", "is_active", "last_login_at"]);
        kolone.Should().NotContain(["UserName", "NormalizedUserName"]);
    }

    [Fact]
    public async Task Refresh_token_cuva_samo_hash()
    {
        var kolone = await UpitAsync("SELECT column_name FROM information_schema.columns WHERE table_name = 'refresh_token'");

        kolone.Should().Contain(["id", "token_hash", "user_id", "expires_at", "created_at", "revoked_at", "created_by_ip"]);
        kolone.Should().NotContain("token");
    }

    [Fact]
    public async Task Audit_log_ima_engleske_kolone_i_ip_address()
    {
        var kolone = await UpitAsync("SELECT column_name FROM information_schema.columns WHERE table_name = 'audit_log'");

        kolone.Should().Contain(["id", "user_id", "user_name", "action", "entity", "entity_id", "old_values", "new_values", "created_at", "ip_address"]);
    }

    [Fact]
    public async Task Jedinstveni_indeksi_nose_ux_imena_a_sifra_je_filtrirana()
    {
        var indeksi = await UpitAsync("SELECT indexname || ' ' || indexdef FROM pg_indexes WHERE schemaname = 'public'");

        indeksi.Should().Contain(i => i.StartsWith("ux_refresh_token_token_hash ") && i.Contains("UNIQUE"));
        indeksi.Should().Contain(i => i.StartsWith("ux_kategorija_sifra ") && i.Contains("UNIQUE") && i.Contains("WHERE (aktivan = true)"));
    }
}
