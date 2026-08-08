# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0.302-noble AS build
WORKDIR /source/backend
COPY .editorconfig /source/.editorconfig
COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props backend/Dorosak.slnx ./
COPY backend/src/Dorosak.Domain/Dorosak.Domain.csproj src/Dorosak.Domain/
COPY backend/src/Dorosak.Application/Dorosak.Application.csproj src/Dorosak.Application/
COPY backend/src/Dorosak.Infrastructure/Dorosak.Infrastructure.csproj src/Dorosak.Infrastructure/
COPY backend/src/Dorosak.MediaWorker/Dorosak.MediaWorker.csproj src/Dorosak.MediaWorker/
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked dotnet restore src/Dorosak.MediaWorker/Dorosak.MediaWorker.csproj
COPY backend/ ./
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked dotnet publish src/Dorosak.MediaWorker/Dorosak.MediaWorker.csproj --configuration Release --no-restore --output /app/publish --property:UseAppHost=false --property:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/runtime:10.0.10-noble AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg ca-certificates && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish ./
RUN useradd --uid 1654 --create-home --shell /usr/sbin/nologin app && mkdir -p /tmp/dorosak-media && chown -R app:app /app /tmp/dorosak-media
USER app
ENTRYPOINT ["dotnet", "Dorosak.MediaWorker.dll"]
