using FluentValidation;

namespace PayFlow.Application.Auth.Commands;

public sealed class IssueTokenCommandValidator : AbstractValidator<IssueTokenCommand>
{
    public IssueTokenCommandValidator()
    {
        RuleFor(command => command.ApiKey)
            .NotEmpty()
            .Must(apiKey => apiKey is not null && apiKey.StartsWith("pf_live_", StringComparison.Ordinal))
            .WithMessage("ApiKey must start with 'pf_live_'");
    }
}
