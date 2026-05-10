using System.Text.RegularExpressions;
using FluentValidation;

namespace PayFlow.Application.Wallets.Commands;

public sealed class TopUpWalletCommandValidator : AbstractValidator<TopUpWalletCommand>
{
    private static readonly Regex CurrencyRegex = new("^[A-Z]{3}$", RegexOptions.Compiled);

    public TopUpWalletCommandValidator()
    {
        RuleFor(command => command.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(999999.99m);

        RuleFor(command => command.Currency)
            .NotEmpty()
            .Must(currency => CurrencyRegex.IsMatch(currency))
            .WithMessage("Currency must match [A-Z]{3}");

        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(command => command.WalletId)
            .NotEmpty();
    }
}
