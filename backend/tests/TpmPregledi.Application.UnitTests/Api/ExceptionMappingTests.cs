using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TpmPregledi.Api.Middleware;
using TpmPregledi.Application.Common.Exceptions;

namespace TpmPregledi.Application.UnitTests.Api;

/// <summary>
/// Tabela izuzetak → HTTP status (<see cref="ExceptionHandlingMiddleware.MapStatus"/>), oblik
/// problem+json tela (errors/greske), maskiranje 500 van Development-a i traceId iz Activity-ja.
/// </summary>
public class ExceptionMappingTests
{
    public static IEnumerable<object[]> Tabela =>
    [
        [new ValidationFailedException(new Dictionary<string, string[]>()), 400],
        [new AuthenticationFailedException(),                                401],
        [new ForbiddenException(),                                           403],
        [new NotFoundException("Kategorija ne postoji."),                    404],
        [new NotFoundException("Korisnik", "x"),                             404],
        [new ConflictException("Već postoji."),                              409],
        [new BusinessRuleException("Pravilo."),                              422],
        [new InvalidOperationException("boom"),                              500],
    ];

    [Theory]
    [MemberData(nameof(Tabela))]
    public void MapStatus_mapira_izuzetak_na_status(Exception ex, int ocekivano)
        => ((int)ExceptionHandlingMiddleware.MapStatus(ex).Status).Should().Be(ocekivano);

    [Fact]
    public void BusinessRule_params_konstruktor_puni_listu_gresaka()
    {
        var ex = new BusinessRuleException("Prva.", "Druga.");
        ex.Greske.Should().Equal("Prva.", "Druga.");
    }

    // ─── Telo odgovora ──────────────────────────────────────────────────────

    private sealed class Env(string ime) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = ime;
        public string ApplicationName { get; set; } = "TpmPregledi.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static async Task<(int Status, JsonElement Telo)> PokreniAsync(Exception ex, string okruzenje)
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw ex, NullLogger<ExceptionHandlingMiddleware>.Instance, new Env(okruzenje));

        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(ctx);

        ctx.Response.Body.Position = 0;
        var telo = await JsonDocument.ParseAsync(ctx.Response.Body);
        return (ctx.Response.StatusCode, telo.RootElement);
    }

    [Fact]
    public async Task Validacija_vraca_400_sa_errors_mapom()
    {
        var ex = new ValidationFailedException(new Dictionary<string, string[]> { ["sifra"] = ["Šifra je obavezna."] });

        var (status, telo) = await PokreniAsync(ex, Environments.Production);

        status.Should().Be(400);
        telo.GetProperty("errors").GetProperty("sifra")[0].GetString().Should().Be("Šifra je obavezna.");
        telo.GetProperty("title").GetString().Should().Be("Validacija nije prošla");
    }

    [Fact]
    public async Task Poslovno_pravilo_vraca_422_sa_greske_nizom()
    {
        var (status, telo) = await PokreniAsync(new BusinessRuleException("Kategorija sa ovom šifrom već postoji."), Environments.Production);

        status.Should().Be(422);
        telo.GetProperty("greske")[0].GetString().Should().Be("Kategorija sa ovom šifrom već postoji.");
    }

    [Fact]
    public async Task Neocekivana_greska_u_produkciji_maskira_poruku_i_navodi_traceId()
    {
        var (status, telo) = await PokreniAsync(new InvalidOperationException("connection string tajna"), Environments.Production);

        status.Should().Be(500);
        var traceId = telo.GetProperty("traceId").GetString();
        telo.GetProperty("detail").GetString()
            .Should().Be($"Došlo je do neočekivane greške. Navedite ovaj kod podršci: {traceId}");
        telo.TryGetProperty("stack", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Neocekivana_greska_u_developmentu_vraca_poruku_i_stack()
    {
        var (_, telo) = await PokreniAsync(new InvalidOperationException("boom"), Environments.Development);

        telo.GetProperty("detail").GetString().Should().Be("boom");
        telo.TryGetProperty("stack", out _).Should().BeTrue();
    }

    [Fact]
    public async Task TraceId_u_odgovoru_je_W3C_TraceId_tekuceg_Activity_ja()
    {
        // ASP.NET pravi Activity po zahtevu; Serilog {TraceId} token čita isti Activity.Current.TraceId,
        // pa odgovor i log moraju da nose identičan string.
        using var activity = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();

        var (_, telo) = await PokreniAsync(new NotFoundException("Nema."), Environments.Production);

        telo.GetProperty("traceId").GetString().Should().Be(activity.TraceId.ToString());
    }
}
