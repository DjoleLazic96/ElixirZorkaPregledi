using FluentAssertions;
using TpmPregledi.Infrastructure.Identity;

namespace TpmPregledi.Application.UnitTests.Identity;

/// <summary>Heš refresh tokena: SHA-256 hex (64 znaka), deterministički, različit za različite ulaze, nikad jednak sirovom tokenu.</summary>
public class RefreshTokenHasherTests
{
    [Fact]
    public void Hash_je_sha256_hex_od_64_znaka()
    {
        var h = RefreshTokenHasher.Hash("abc");
        h.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
        // Poznata SHA-256 vrednost za "abc".
        h.Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void Isti_ulaz_daje_isti_hash()
        => RefreshTokenHasher.Hash("token-1").Should().Be(RefreshTokenHasher.Hash("token-1"));

    [Fact]
    public void Razlicit_ulaz_daje_razlicit_hash_i_ne_sadrzi_sirovi_token()
    {
        var h = RefreshTokenHasher.Hash("token-1");
        h.Should().NotBe(RefreshTokenHasher.Hash("token-2"));
        h.Should().NotContain("token-1");
    }
}
