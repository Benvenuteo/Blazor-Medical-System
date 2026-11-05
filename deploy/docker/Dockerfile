# syntax=docker/dockerfile:1
# One file, four targets: `api`, `staff`, `web` (patient WebAssembly app served by Caddy)
# and `maintenance` (backup, verify, restore and administrator recovery).
# The SDK stage runs on the build machine's architecture and cross-publishes for TARGETARCH,
# so `docker buildx build --platform linux/arm64` on an x64 runner needs no emulation for .NET.

# MCR publishes the SDK under the floating 10.0 tag (patch-level SDK tags are not all available).
# global.json is not copied: inside the image the SDK image itself is the pinned toolchain.
ARG DOTNET_SDK=mcr.microsoft.com/dotnet/sdk:10.0
ARG DOTNET_RUNTIME=mcr.microsoft.com/dotnet/aspnet:10.0
ARG CADDY=caddy:2.10-alpine

FROM --platform=$BUILDPLATFORM ${DOTNET_SDK} AS restore
ARG TARGETARCH
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY MedicalBookingSystem/MedicalBookingSystem.Domain/*.csproj MedicalBookingSystem/MedicalBookingSystem.Domain/
COPY MedicalBookingSystem/MedicalBookingSystem.SharedKernel/*.csproj MedicalBookingSystem/MedicalBookingSystem.SharedKernel/
COPY MedicalBookingSystem/MedicalBookingSystem.Application/*.csproj MedicalBookingSystem/MedicalBookingSystem.Application/
COPY MedicalBookingSystem/MedicalBookingSystem.Infrastructure/*.csproj MedicalBookingSystem/MedicalBookingSystem.Infrastructure/
COPY MedicalBookingSystem/MedicalBookingSystem.WebAPI/*.csproj MedicalBookingSystem/MedicalBookingSystem.WebAPI/
COPY MedicalBookingSystem/MedicalBookingSystem.BlazorServer/*.csproj MedicalBookingSystem/MedicalBookingSystem.BlazorServer/
COPY MedicalBookingSystem/MedicalBookingSystem.BlazorWasm/*.csproj MedicalBookingSystem/MedicalBookingSystem.BlazorWasm/
COPY MedicalBookingSystem/MedicalBookingSystem.Maintenance/*.csproj MedicalBookingSystem/MedicalBookingSystem.Maintenance/
RUN dotnet restore MedicalBookingSystem/MedicalBookingSystem.WebAPI -a $TARGETARCH \
 && dotnet restore MedicalBookingSystem/MedicalBookingSystem.BlazorServer -a $TARGETARCH \
 && dotnet restore MedicalBookingSystem/MedicalBookingSystem.Maintenance -a $TARGETARCH \
 && dotnet restore MedicalBookingSystem/MedicalBookingSystem.BlazorWasm
COPY MedicalBookingSystem/ MedicalBookingSystem/

FROM restore AS publish-api
RUN dotnet publish MedicalBookingSystem/MedicalBookingSystem.WebAPI -c Release -a $TARGETARCH --no-restore -o /out

FROM restore AS publish-staff
RUN dotnet publish MedicalBookingSystem/MedicalBookingSystem.BlazorServer -c Release -a $TARGETARCH --no-restore -o /out

FROM restore AS publish-maintenance
RUN dotnet publish MedicalBookingSystem/MedicalBookingSystem.Maintenance -c Release -a $TARGETARCH --no-restore -o /out

FROM restore AS publish-web
RUN dotnet publish MedicalBookingSystem/MedicalBookingSystem.BlazorWasm -c Release --no-restore -o /out

# Shared runtime: non-root user, curl for the health check, writable data and log directories.
# Empty named volumes copy these directories' ownership, so the app user can write to them.
FROM ${DOTNET_RUNTIME} AS runtime
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/* \
 && mkdir -p /data/db /data/photos /data/keys /data/mail /app/logs \
 && chown -R $APP_UID:$APP_UID /data /app/logs
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
EXPOSE 8080
# AllowedHosts rejects unknown Host headers, so the probe sends the first configured host name.
HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD curl -fsS -o /dev/null -H "Host: ${AllowedHosts%%;*}" http://127.0.0.1:8080/health/ready || exit 1

FROM runtime AS api
COPY --from=publish-api --chown=$APP_UID:$APP_UID /out ./
USER $APP_UID
ENTRYPOINT ["dotnet", "MedicalBookingSystem.WebAPI.dll"]

FROM runtime AS staff
COPY --from=publish-staff --chown=$APP_UID:$APP_UID /out ./
USER $APP_UID
ENTRYPOINT ["dotnet", "MedicalBookingSystem.BlazorServer.dll"]

FROM runtime AS maintenance
COPY --from=publish-maintenance --chown=$APP_UID:$APP_UID /out ./
USER $APP_UID
HEALTHCHECK NONE
ENTRYPOINT ["dotnet", "MedicalBookingSystem.Maintenance.dll"]

FROM ${CADDY} AS web
COPY --from=publish-web /out/wwwroot /srv/patient
COPY deploy/docker/Caddyfile /etc/caddy/sites.caddy
COPY deploy/docker/web-entrypoint.sh /usr/local/bin/web-entrypoint.sh
RUN chmod +x /usr/local/bin/web-entrypoint.sh
ENTRYPOINT ["/usr/local/bin/web-entrypoint.sh"]
CMD ["caddy", "run", "--config", "/etc/caddy/Caddyfile", "--adapter", "caddyfile"]
