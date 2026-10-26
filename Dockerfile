FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY InspectaBackend.slnx ./
COPY Inspecta.Api/Inspecta.Api.csproj Inspecta.Api/
COPY Inspecta.Application/Inspecta.Application.csproj Inspecta.Application/
COPY Inspecta.Domain/Inspecta.Domain.csproj Inspecta.Domain/
COPY Inspecta.Infrastructure/Inspecta.Infrastructure.csproj Inspecta.Infrastructure/
COPY Inspecta.Domain.Tests/Inspecta.Domain.Tests.csproj Inspecta.Domain.Tests/
RUN dotnet restore InspectaBackend.slnx

COPY . .
RUN dotnet publish Inspecta.Api/Inspecta.Api.csproj --configuration Release --no-restore --output /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
RUN mkdir -p /data && chown 1654:1654 /data
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ConnectionStrings__Inspecta="Data Source=/data/inspecta.db"
EXPOSE 8080
USER 1654
ENTRYPOINT ["dotnet", "Inspecta.Api.dll"]
