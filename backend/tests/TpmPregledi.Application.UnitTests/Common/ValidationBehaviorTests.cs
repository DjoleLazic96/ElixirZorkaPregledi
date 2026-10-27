using FluentAssertions;
using FluentValidation;
using TpmPregledi.Application.Common.Behaviors;
using TpmPregledi.Application.Common.Exceptions;

namespace TpmPregledi.Application.UnitTests.Common;

/// <summary>ValidationBehavior baca 400 izuzetak sa camelCase ključevima kad validacija padne, inače propušta.</summary>
public class ValidationBehaviorTests
{
    private sealed record Cmd(string Naziv);

    private sealed class Val : AbstractValidator<Cmd>
    {
        public Val() => RuleFor(x => x.Naziv).NotEmpty().WithMessage("Naziv je obavezan.");
    }

    [Fact]
    public async Task Pad_validacije_baca_ValidationFailedException_sa_camelCase_kljucem()
    {
        var behavior = new ValidationBehavior<Cmd, string>([new Val()]);
        var act = async () => await behavior.Handle(new Cmd(""), _ => Task.FromResult("ok"), default);

        var ex = await act.Should().ThrowAsync<ValidationFailedException>();
        ex.Which.Errors.Should().ContainKey("naziv").WhoseValue.Should().ContainSingle("Naziv je obavezan.");
    }

    [Fact]
    public async Task Validan_ulaz_prolazi_do_handlera()
    {
        var behavior = new ValidationBehavior<Cmd, string>([new Val()]);
        var rezultat = await behavior.Handle(new Cmd("K1"), _ => Task.FromResult("ok"), default);
        rezultat.Should().Be("ok");
    }
}
