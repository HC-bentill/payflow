FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PayFlow.sln ./
COPY src/PayFlow.Api/PayFlow.Api.csproj src/PayFlow.Api/
COPY src/PayFlow.Application/PayFlow.Application.csproj src/PayFlow.Application/
COPY src/PayFlow.Domain/PayFlow.Domain.csproj src/PayFlow.Domain/
COPY src/PayFlow.Infrastructure/PayFlow.Infrastructure.csproj src/PayFlow.Infrastructure/
COPY tests/PayFlow.Api.Tests/PayFlow.Api.Tests.csproj tests/PayFlow.Api.Tests/

RUN dotnet restore PayFlow.sln

COPY . .
RUN dotnet publish src/PayFlow.Api/PayFlow.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
LABEL org.opencontainers.image.title="PayFlow API"
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PayFlow.Api.dll"]
