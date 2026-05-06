using PayFlow.Application.Health;

namespace PayFlow.Api.Services;

public sealed class ApplicationEnvironment(IWebHostEnvironment webHostEnvironment) : IApplicationEnvironment
{
    public string EnvironmentName => webHostEnvironment.EnvironmentName;
}
