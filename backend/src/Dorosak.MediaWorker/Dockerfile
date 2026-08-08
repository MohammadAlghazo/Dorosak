# syntax=docker/dockerfile:1.7@sha256:a57df69d0ea827fb7266491f2813635de6f17269be881f696fbfdf2d83dda33e

FROM mcr.microsoft.com/dotnet/sdk:10.0.302-noble@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0 AS build
WORKDIR /source/backend
COPY .editorconfig /source/.editorconfig
COPY backend/global.json backend/Directory.Build.props backend/Directory.Packages.props backend/Dorosak.slnx ./
COPY backend/src/Dorosak.Domain/Dorosak.Domain.csproj backend/src/Dorosak.Domain/packages.lock.json src/Dorosak.Domain/
COPY backend/src/Dorosak.Application/Dorosak.Application.csproj backend/src/Dorosak.Application/packages.lock.json src/Dorosak.Application/
COPY backend/src/Dorosak.Infrastructure/Dorosak.Infrastructure.csproj backend/src/Dorosak.Infrastructure/packages.lock.json src/Dorosak.Infrastructure/
COPY backend/src/Dorosak.MediaWorker/Dorosak.MediaWorker.csproj backend/src/Dorosak.MediaWorker/packages.lock.json src/Dorosak.MediaWorker/
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked dotnet restore src/Dorosak.MediaWorker/Dorosak.MediaWorker.csproj --locked-mode
COPY backend/ ./
RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked dotnet publish src/Dorosak.MediaWorker/Dorosak.MediaWorker.csproj --configuration Release --no-restore --output /app/publish --property:UseAppHost=false --property:ContinuousIntegrationBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10-noble@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg=7:6.1.1-3ubuntu5 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
ENV DOTNET_EnableDiagnostics=0
COPY --from=build /app/publish ./
RUN mkdir -p /tmp/dorosak-media && chown -R $APP_UID:$APP_UID /app /tmp/dorosak-media
USER $APP_UID
ENTRYPOINT ["dotnet", "Dorosak.MediaWorker.dll"]
