FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY MS.Cobroup.Backend.Domain/*.csproj MS.Cobroup.Backend.Domain/
COPY MS.Cobroup.Backend.Shared/*.csproj MS.Cobroup.Backend.Shared/
COPY MS.Cobroup.Backend.Core/*.csproj MS.Cobroup.Backend.Core/
COPY MS.Cobroup.Backend.Infrastructure/*.csproj MS.Cobroup.Backend.Infrastructure/
COPY MS.Cobroup.Backend.Api/*.csproj MS.Cobroup.Backend.Api/
COPY MS.Cobroup.Backend.slnx .

RUN dotnet restore MS.Cobroup.Backend.Api/MS.Cobroup.Backend.Api.csproj

COPY . .
RUN dotnet publish MS.Cobroup.Backend.Api/MS.Cobroup.Backend.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

COPY --from=build --chown=app:app /app/publish .

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "MS.Cobroup.Backend.Api.dll"]
