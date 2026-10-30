# syntax=docker/dockerfile:1
FROM --platform=$BUILDPLATFORM denoland/deno:2.9.7@sha256:fa335acdf6b72106eda2cb6a8cb5f4187e7630e357467489db4b2e7352d5e432 AS frontend
USER root
RUN apt-get update && apt-get install -y --no-install-recommends python3 && rm -rf /var/lib/apt/lists/*
WORKDIR /source
COPY web/deno.json web/deno.lock web/
RUN deno install --config web/deno.json --frozen
COPY web/ web/
COPY scripts/prepare-bundled-content.py scripts/content_bitmap.py scripts/content_xml.py scripts/content_recipes.py scripts/
COPY prototype/ prototype/
COPY content/bundled-v2/ content/bundled-v2/
COPY content/localization/ content/localization/
COPY doc/notices/StbImageSharp/ doc/notices/StbImageSharp/
COPY src/FreeTrain.Online.Content/reference-spatial.json src/FreeTrain.Online.Content/
COPY src/FreeTrain.Online.Content/bundled-v2.json src/FreeTrain.Online.Content/bundled-v2.sha256 src/FreeTrain.Online.Content/
RUN deno task --cwd web build

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0.402@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS backend
WORKDIR /source
COPY global.json ./
COPY eng/ eng/
COPY src/ src/
COPY scripts/prepare-optional-content.py scripts/prepare-bundled-content.py scripts/content_bitmap.py scripts/content_xml.py scripts/content_recipes.py scripts/
COPY content/bundled-v2/ content/bundled-v2/
COPY doc/notices/StbImageSharp/ doc/notices/StbImageSharp/
RUN dotnet restore src/FreeTrain.Online.Server/FreeTrain.Online.Server.csproj --locked-mode \
 && dotnet publish src/FreeTrain.Online.Server/FreeTrain.Online.Server.csproj -c Release --no-restore -p:UseAppHost=false -o /release

# Decoder is compiled from reviewed bounded managed source; no native image assets.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.13@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac AS release
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends python3 && rm -rf /var/lib/apt/lists/*
COPY --from=backend /release/ ./
COPY --from=frontend /source/web/dist/ wwwroot/
RUN mkdir -p /data/saves && chown -R app:app /data
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    FreeTrainOnline__Saves__Directory=/data/saves \
    DOTNET_EnableDiagnostics=0
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "FreeTrain.Online.Server.dll"]
