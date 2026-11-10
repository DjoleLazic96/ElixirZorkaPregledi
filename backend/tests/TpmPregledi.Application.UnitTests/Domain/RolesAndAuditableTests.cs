using FluentAssertions;
using TpmPregledi.Domain.Common;
using TpmPregledi.Domain.Entities;
using TpmPregledi.Domain.Identity;
using System.Reflection;

namespace TpmPregledi.Application.UnitTests.Domain;

/// <summary>Fiksira konvencije: tačno dve role, audit-abilnost korisnika i kategorije, oblik baznih entiteta.</summary>
public class RolesAndAuditableTests
{
    [Fact]
    public void Roles_definise_tacno_cetiri_role()
    {
        Roles.All.Should().Equal("Inzenjer", "Supervizor", "Direktor", "Admin");
    }

    [Theory]
    [InlineData(typeof(ApplicationUser))]
    [InlineData(typeof(Uredjaj))]
    [InlineData(typeof(Aktivnost))]
    [InlineData(typeof(SablonVerzija))]
    public void Entitet_je_Auditable(Type t)
        => t.GetCustomAttribute<AuditableAttribute>().Should().NotBeNull($"{t.Name} ide u audit_log");

    [Theory]
    [InlineData(typeof(AuditLog))]
    [InlineData(typeof(RefreshToken))]
    public void Log_tabele_nisu_Auditable(Type t)
        => t.GetCustomAttribute<AuditableAttribute>().Should().BeNull($"{t.Name} je šum / rekurzija");

    [Fact]
    public void AuditableEntity_ima_created_i_updated_polja()
    {
        var t = typeof(AuditableEntity);
        t.GetProperty("CreatedAt")!.PropertyType.Should().Be(typeof(DateTime));
        t.GetProperty("CreatedBy")!.PropertyType.Should().Be(typeof(string));
        t.GetProperty("UpdatedAt")!.PropertyType.Should().Be(typeof(DateTime?));
        t.GetProperty("UpdatedBy")!.PropertyType.Should().Be(typeof(string));
    }

    [Fact]
    public void SoftDeletableEntity_je_podrazumevano_aktivan()
    {
        typeof(SoftDeletableEntity).Should().BeDerivedFrom<AuditableEntity>();
        new ProbniSifarnik().Aktivan.Should().BeTrue();
    }

    [Fact]
    public void ApplicationUser_i_ApplicationRole_dobijaju_GUID_Id_u_konstruktoru()
    {
        Guid.TryParse(new ApplicationUser().Id, out _).Should().BeTrue();
        Guid.TryParse(new ApplicationRole().Id, out _).Should().BeTrue();
    }

    [Fact]
    public void RefreshToken_JeVazeci_zavisi_od_revokacije_i_isteka()
    {
        var now = new DateTime(2026, 7, 24, 12, 0, 0, DateTimeKind.Utc);
        var t = new RefreshToken { ExpiresAt = now.AddDays(1) };
        t.JeVazeci(now).Should().BeTrue();
        t.JeVazeci(now.AddDays(2)).Should().BeFalse("istekao");
        t.RevokedAt = now;
        t.JeVazeci(now).Should().BeFalse("revokovan");
    }
}

/// <summary>Konkretna klasa samo za proveru podrazumevane vrednosti apstraktne baze.</summary>
internal sealed class ProbniSifarnik : SoftDeletableEntity;
