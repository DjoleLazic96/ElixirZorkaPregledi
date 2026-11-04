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

/// <summary>Čuvanje i čitanje slika.</summary>
public sealed class FotografijeHandler(IAppDbContext db, IFajlSkladiste skladiste, ICurrentUser currentUser, IDateTimeProvider clock) :
    IRequestHandler<SacuvajFotografijuCommand, long>,
    IRequestHandler<GetFotografijaQuery, (Stream Sadrzaj, string Mime)>
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
        if (p.Status != StatusPregleda.URadu) throw new BusinessRuleException("Predat pregled se menja samo kroz doradu.");
        if (!await db.SablonStavke.AnyAsync(s => s.Id == req.StavkaId && s.VerzijaId == p.SablonVerzijaId, ct))
            throw new BusinessRuleException("Stavka nije u ovom pregledu.");
        if (await db.Fotografije.CountAsync(f => f.PregledId == p.Id && f.StavkaId == req.StavkaId, ct) >= NajviseSlika)
            throw new BusinessRuleException("Najviše 5 slika po stavci.");

        var fajl = await skladiste.SacuvajAsync(req.Sadrzaj, req.Mime, ct);
        db.Fajlovi.Add(fajl);
        var f = new Fotografija
        {
            PregledId = p.Id, StavkaId = req.StavkaId, Izvor = req.Izvor == "galerija" ? "galerija" : "kamera",
            KorisnikId = currentUser.UserId!, KlijentId = req.KlijentId, KreiranoAt = clock.UtcNow,
        };
        await db.SaveChangesAsync(ct);
        f.FajlId = fajl.Id;
        db.Fotografije.Add(f);
        await db.SaveChangesAsync(ct);
        return f.Id;
    }

    /// <inheritdoc />
    public async Task<(Stream Sadrzaj, string Mime)> Handle(GetFotografijaQuery req, CancellationToken ct)
    {
        var f = await db.Fotografije.AsNoTracking().FirstOrDefaultAsync(x => x.Id == req.Id, ct) ?? throw new NotFoundException("Slika ne postoji.");
        var fajl = await db.Fajlovi.AsNoTracking().FirstAsync(x => x.Id == f.FajlId, ct);
        return (skladiste.Otvori(fajl), fajl.Mime);
    }
}
