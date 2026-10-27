FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Inspecta.Api/Inspecta.Api.csproj Inspecta.Api/
COPY Inspecta.Application/Inspecta.Application.csproj Inspecta.Application/
COPY Inspecta.Domain/Inspecta.Domain.csproj Inspecta.Domain/
COPY Inspecta.Infrastructure/Inspecta.Infrastructure.csproj Inspecta.Infrastructure/
RUN dotnet restore Inspecta.Api/Inspecta.Api.csproj

COPY . .
RUN dotnet publish Inspecta.Api/Inspecta.Api.csproj --configuration Release --no-restore --output /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER 1654
ENTRYPOINT ["dotnet", "Inspecta.Api.dll"]
