using System.Text.Json;
using System.Text.RegularExpressions;
using FluentValidation;

namespace PayFlow.Application.Payments.Commands;

public sealed class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    private static readonly Regex CurrencyRegex = new("^[A-Z]{3}$", RegexOptions.Compiled);

    public CreatePaymentCommandValidator()
    {
        RuleFor(command => command.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(999999.99m);

        RuleFor(command => command.Currency)
            .NotEmpty()
            .Must(currency => CurrencyRegex.IsMatch(currency))
            .WithMessage("Currency must match [A-Z]{3}");

        RuleFor(command => command.ReceiverTenantId)
            .NotEmpty();

        RuleFor(command => command.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(command => command.Description)
            .MaximumLength(500);

        RuleFor(command => command.Metadata)
            .Must(BeValidJson)
            .When(command => command.Metadata is not null)
            .WithMessage("Metadata must be valid JSON");
    }

    private static bool BeValidJson(string? metadata)
    {
        if (metadata is null)
        {
            return true;
        }

        try
        {
            using var _ = JsonDocument.Parse(metadata);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
