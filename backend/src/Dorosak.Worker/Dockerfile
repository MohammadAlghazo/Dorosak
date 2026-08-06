# syntax=docker/dockerfile:1.7@sha256:a57df69d0ea827fb7266491f2813635de6f17269be881f696fbfdf2d83dda33e

FROM mcr.microsoft.com/dotnet/sdk:10.0.302-noble@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0 AS build
WORKDIR /source/backend

COPY .editorconfig /source/.editorconfig
COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props backend/Dorosak.slnx ./
COPY backend/src/Dorosak.Domain/Dorosak.Domain.csproj backend/src/Dorosak.Domain/packages.lock.json src/Dorosak.Domain/
COPY backend/src/Dorosak.Application/Dorosak.Application.csproj backend/src/Dorosak.Application/packages.lock.json src/Dorosak.Application/
COPY backend/src/Dorosak.Infrastructure/Dorosak.Infrastructure.csproj backend/src/Dorosak.Infrastructure/packages.lock.json src/Dorosak.Infrastructure/
COPY backend/src/Dorosak.Worker/Dorosak.Worker.csproj backend/src/Dorosak.Worker/packages.lock.json src/Dorosak.Worker/

RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet restore src/Dorosak.Worker/Dorosak.Worker.csproj --locked-mode

COPY backend/ ./

RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    dotnet publish src/Dorosak.Worker/Dorosak.Worker.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        --property:UseAppHost=false \
        --property:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10-noble-chiseled-extra@sha256:f9bd6be9b5ab75b8196bff0f0972580edaea7fa8ca04e6ef530950e33caee5b0 AS runtime
WORKDIR /app

ENV DOTNET_EnableDiagnostics=0

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish ./

USER $APP_UID
ENTRYPOINT ["dotnet", "Dorosak.Worker.dll"]
