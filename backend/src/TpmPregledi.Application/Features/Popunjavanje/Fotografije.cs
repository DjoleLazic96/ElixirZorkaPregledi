using TpmPregledi.Application.Common.Exceptions;
using TpmPregledi.Application.Common.Interfaces;
using TpmPregledi.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace TpmPregledi.Application.Features.Popunjavanje;

/// <summary>Skladište fajlova na volumenu (implementacija u Infrastructure).</summary>
public interface IFajlSkladiste
{
    /// <summary>Upisuje sadržaj i vraća zapis za tabelu <c>fajl</c> (još nesnimljen).</summary>
    Task<Fajl> SacuvajAsync(byte[] sadrzaj, string mime, CancellationToken ct);

    /// <summary>Otvara fajl za čitanje.</summary>
    Stream Otvori(Fajl fajl);

    /// <summary>Briše fajl sa diska (posle uspešnog brisanja zapisa iz baze); nepostojeći fajl nije greška.</summary>
    void Obrisi(Fajl fajl) { }
}

/// <summary>Slika uz stavku (R4.5).</summary>
/// <param name="KlijentId">ID sa klijenta (idempotentnost).</param>
/// <param name="PregledId">Pregled.</param>
/// <param name="StavkaId">Stavka.</param>
/// <param name="Izvor"><c>kamera</c> ili <c>galerija</c>.</param>
/// <param name="Sadrzaj">JPEG/PNG/WebP, već smanjen na klijentu.</param>
/// <param name="Mime">MIME tip.</param>
public sealed record SacuvajFotografijuCommand(Guid KlijentId, int PregledId, int StavkaId, string Izvor, byte[] Sadrzaj, string Mime) : IRequest<long>;

/// <summary>Slika za prikaz.</summary>
/// <param name="Id">ID slike.</param>
public sealed record GetFotografijaQuery(long Id) : IRequest<(Stream Sadrzaj, string Mime)>;

/// <summary>Sadržaj slika pregleda za PDF i Word (zamerka 72); slika čiji fajl nedostaje se preskače.</summary>
/// <param name="Ids">ID-jevi slika.</param>
public sealed record GetSlikeZaIzvestajQuery(IReadOnlyList<long> Ids) : IRequest<IReadOnlyDictionary<long, byte[]>>;

/// <summary>Čuvanje i čitanje slika.</summary>
public sealed class FotografijeHandler(IAppDbContext db, IFajlSkladiste skladiste, ICurrentUser currentUser, IDateTimeProvider clock) :
    IRequestHandler<SacuvajFotografijuCommand, long>,
    IRequestHandler<GetFotografijaQuery, (Stream Sadrzaj, string Mime)>,
    IRequestHandler<GetSlikeZaIzvestajQuery, IReadOnlyDictionary<long, byte[]>>
{
    /// <summary>Najviše slika po stavci (R4.5).</summary>
    public const int NajviseSlika = 5;

    /// <summary>Dozvoljeni tipovi.</summary>
    public static readonly string[] Tipovi = ["image/jpeg", "image/png", "image/webp"];

    /// <inheritdoc />
    public async Task<long> Handle(SacuvajFotografijuCommand req, CancellationToken ct)
    {
        var postojeca = await db.Fotografije.AsNoTracking().FirstOrDefaultAsync(f => f.KlijentId == req.KlijentId, ct);
        if (postojeca is not null) return postojeca.Id;   // ponovljeno slanje iste slike

        var p = await db.Pregledi.Include(x => x.Ucesnici).FirstOrDefaultAsync(x => x.Id == req.PregledId, ct)
                ?? throw new NotFoundException("Pregled ne postoji.");
        if (p.DodeljenId != currentUser.UserId && p.Ucesnici.All(u => u.KorisnikId != currentUser.UserId))
            throw new ForbiddenException("Ovaj pregled je dodeljen drugom inženjeru.");
        if (p.Status is not (StatusPregleda.URadu or StatusPregleda.VracenNaDoradu)) throw new BusinessRuleException("Predat pregled se menja samo kroz doradu.");
        var sekcija = await db.SablonStavke.Where(s => s.Id == req.StavkaId && s.VerzijaId == p.SablonVerzijaId)
            .Select(s => db.Sekcije.Where(x => x.Id == s.SekcijaId).Select(x => x.Sifra).First()).FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException("Stavka nije u ovom pregledu.");
        // PZ sa dva inženjera: slika ide samo u svoju sekciju, kao i odgovor (zamerka 16).
        var radi = p.Ucesnici.FirstOrDefault(u => u.Sekcije.Contains(sekcija))?.KorisnikId ?? p.DodeljenId;
        if (radi != currentUser.UserId) throw new BusinessRuleException($"Sekciju {sekcija} radi drugi inženjer.");
        if (!JeSlika(req.Sadrzaj)) throw new BusinessRuleException("Fajl nije slika (dozvoljeni su JPG, PNG i WebP).");
        if (await db.Fotografije.CountAsync(f => f.PregledId == p.Id && f.StavkaId == req.StavkaId, ct) >= NajviseSlika)
            throw new BusinessRuleException("Najviše 5 slika po stavci.");

        var fajl = await skladiste.SacuvajAsync(req.Sadrzaj, req.Mime, ct);
        db.Fajlovi.Add(fajl);
        var f = new Fotografija
        {
            PregledId = p.Id, StavkaId = req.StavkaId, Izvor = req.Izvor == "galerija" ? "galerija" : "kamera",
            KorisnikId = currentUser.UserId!, KlijentId = req.KlijentId, KreiranoAt = clock.UtcNow, Dorada = p.Dorada,
        };
        await db.SaveChangesAsync(ct);
        f.FajlId = fajl.Id;
        db.Fotografije.Add(f);
        await db.SaveChangesAsync(ct);
        return f.Id;
    }

    /// <summary>Stvarni format po prvim bajtovima (JPEG, PNG, WebP) — tip koji pošalje telefon se ne uzima na reč (zamerka 16).</summary>
    public static bool JeSlika(byte[] b) =>
        (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
        || (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
        || (b.Length >= 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 && b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50);

    /// <inheritdoc />
    public async Task<(Stream Sadrzaj, string Mime)> Handle(GetFotografijaQuery req, CancellationToken ct)
    {
        var f = await db.Fotografije.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Slika ne postoji.");
        var fajl = await db.Fajlovi.AsNoTracking().FirstAsync(x => x.Id == f.FajlId, ct);
        return (skladiste.Otvori(fajl), fajl.Mime);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<long, byte[]>> Handle(GetSlikeZaIzvestajQuery req, CancellationToken ct)
    {
        var zapisi = await (from f in db.Fotografije.AsNoTracking() join fa in db.Fajlovi.AsNoTracking() on f.FajlId equals fa.Id
            where req.Ids.Contains(f.Id) select new { f.Id, Fajl = fa }).ToListAsync(ct);
        var rez = new Dictionary<long, byte[]>();
        foreach (var z in zapisi)
        {
            try
            {
                await using var s = skladiste.Otvori(z.Fajl);
                using var ms = new MemoryStream();
                await s.CopyToAsync(ms, ct);
                rez[z.Id] = ms.ToArray();
            }
            catch (IOException)
            {
                // Fajl nedostaje na disku — izveštaj ide bez te slike.
            }
        }
        return rez;
    }
}
