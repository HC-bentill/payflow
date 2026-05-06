using FluentValidation;

namespace PayFlow.Application.Auth.Commands;

public sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .Length(3, 100);
    }
}
