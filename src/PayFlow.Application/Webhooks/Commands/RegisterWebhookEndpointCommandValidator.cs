using FluentValidation;

namespace PayFlow.Application.Webhooks.Commands;

public sealed class RegisterWebhookEndpointCommandValidator : AbstractValidator<RegisterWebhookEndpointCommand>
{
    public RegisterWebhookEndpointCommandValidator()
    {
        RuleFor(command => command.Url)
            .NotEmpty()
            .Must(BeHttpsUrl)
            .WithMessage("Url must be a valid HTTPS URL");

        RuleFor(command => command.Secret)
            .NotEmpty()
            .MinimumLength(16);
    }

    private static bool BeHttpsUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            !string.IsNullOrWhiteSpace(uri.Host);
    }
}
