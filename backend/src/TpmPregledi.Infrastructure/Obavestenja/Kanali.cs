using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TpmPregledi.Application.Features.Obavestenja;
using TpmPregledi.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPush;

namespace TpmPregledi.Infrastructure.Obavestenja;

/// <summary>
/// Web Push sa VAPID ključevima. Ključevi su iz konfiguracije (<c>Push:JavniKljuc</c>, <c>Push:PrivatniKljuc</c>); ako ih nema,
/// prvi start ih napravi i sačuva u <c>Push:Putanja</c> (podrazumevano <c>data/vapid.json</c>, na istom volumenu kao fotografije),
/// pa push radi bez ručnog koraka, a pretplate ostaju važeće posle restarta.
/// </summary>
public sealed class WebPushSlanje : IPushSlanje
{
    private readonly WebPushClient _klijent = new();
    private readonly VapidDetails _vapid;

    /// <summary>Učitava ili pravi ključeve.</summary>
    public WebPushSlanje(IConfiguration config, ILogger<WebPushSlanje> log)
    {
        var javni = config["Push:JavniKljuc"];
        var privatni = config["Push:PrivatniKljuc"];
        if (string.IsNullOrWhiteSpace(javni) || string.IsNullOrWhiteSpace(privatni))
        {
            var putanja = Path.GetFullPath(config["Push:Putanja"] ?? Path.Combine("data", "vapid.json"));
            if (File.Exists(putanja))
            {
                var k = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(putanja))!;
                (javni, privatni) = (k["javni"], k["privatni"]);
            }
            else
            {
                var novi = VapidHelper.GenerateVapidKeys();
                (javni, privatni) = (novi.PublicKey, novi.PrivateKey);
                Directory.CreateDirectory(Path.GetDirectoryName(putanja)!);
                File.WriteAllText(putanja, JsonSerializer.Serialize(new Dictionary<string, string> { ["javni"] = javni, ["privatni"] = privatni }));
                log.LogInformation("Napravljeni su novi VAPID ključevi u {Putanja}.", putanja);
            }
        }
        _vapid = new VapidDetails(config["Push:Subject"] ?? "https://tpm-pregledi.elixirgroup.rs", javni, privatni);
        JavniKljuc = javni;
    }

    /// <inheritdoc />
    public string? JavniKljuc { get; }

    /// <inheritdoc />
    public async Task<PushIshod> PosaljiAsync(PushPretplata pretplata, string poruka, CancellationToken ct)
    {
        try
        {
            await _klijent.SendNotificationAsync(new PushSubscription(pretplata.Endpoint, pretplata.P256dh, pretplata.Auth), poruka, _vapid, ct);
            return PushIshod.Poslato;
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
        {
            return PushIshod.Istekla;
        }
    }
}

/// <summary>
/// Mejl kroz Microsoft Graph (<c>/users/{posiljalac}/sendMail</c>, aplikacijska dozvola Mail.Send). Bez podešenog naloga
/// (<c>Graph:TenantId</c>, <c>Graph:ClientId</c>, <c>Graph:ClientSecret</c>, <c>Graph:Posiljalac</c>) mejl se samo upisuje u log —
/// dok IT ne otvori nalog, obaveštenja i dalje rade kroz zvono i push.
/// </summary>
public sealed class GraphMejlSlanje(IHttpClientFactory http, IConfiguration config, ILogger<GraphMejlSlanje> log) : IMejlSlanje
{
    private string? _token;
    private DateTime _tokenIstice;

    private bool Podeseno => new[] { "TenantId", "ClientId", "ClientSecret", "Posiljalac" }.All(k => !string.IsNullOrWhiteSpace(config[$"Graph:{k}"]));

    /// <inheritdoc />
    public async Task PosaljiAsync(string adresa, string naslov, string tekst, string? link, CancellationToken ct)
    {
        var adresaAplikacije = (config["App:JavnaAdresa"] ?? "https://tpm-pregledi.elixirgroup.rs").TrimEnd('/');
        var telo = link is null ? tekst : $"{tekst}\n\nOtvori u aplikaciji: {adresaAplikacije}{link}";
        if (!Podeseno)
        {
            log.LogInformation("Mejl (Graph nije podešen) za {Adresa}: {Naslov}", adresa, naslov);
            return;
        }

        var klijent = http.CreateClient("graph");
        klijent.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync(klijent, ct));
        var poruka = new
        {
            message = new
            {
                subject = $"TPM pregledi: {naslov}",
                body = new { contentType = "Text", content = telo },
                toRecipients = new[] { new { emailAddress = new { address = adresa } } },
            },
            saveToSentItems = false,
        };
        var odgovor = await klijent.PostAsJsonAsync($"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(config["Graph:Posiljalac"]!)}/sendMail", poruka, ct);
        odgovor.EnsureSuccessStatusCode();
    }

    private async Task<string> TokenAsync(HttpClient klijent, CancellationToken ct)
    {
        if (_token is not null && DateTime.UtcNow < _tokenIstice) return _token;
        var odgovor = await klijent.PostAsync($"https://login.microsoftonline.com/{config["Graph:TenantId"]}/oauth2/v2.0/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = config["Graph:ClientId"]!,
            ["client_secret"] = config["Graph:ClientSecret"]!,
            ["scope"] = "https://graph.microsoft.com/.default",
            ["grant_type"] = "client_credentials",
        }), ct);
        odgovor.EnsureSuccessStatusCode();
        var t = await odgovor.Content.ReadFromJsonAsync<JsonElement>(ct);
        _token = t.GetProperty("access_token").GetString();
        _tokenIstice = DateTime.UtcNow.AddSeconds(t.GetProperty("expires_in").GetInt32() - 120);
        return _token!;
    }
}
