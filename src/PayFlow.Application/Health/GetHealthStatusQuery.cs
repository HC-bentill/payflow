using MediatR;

namespace PayFlow.Application.Health;

public sealed record GetHealthStatusQuery : IRequest<HealthStatusResponse>;
