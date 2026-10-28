FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY server.csproj ./
RUN dotnet restore server.csproj
COPY . .
RUN dotnet publish server.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
# x-release-please-start-version
ARG DRIA_VERSION=0.8.1
# x-release-please-end
ARG DRIA_REVISION=local
LABEL org.opencontainers.image.title="DrIA API" \
    org.opencontainers.image.version="${DRIA_VERSION}" \
    org.opencontainers.image.revision="${DRIA_REVISION}"
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/dria \
    && chown app:app /var/lib/dria
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    PathMainStorage=/var/lib/dria
USER app
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s \
    CMD curl --fail --silent http://127.0.0.1:8080/api/health > /dev/null || exit 1
ENTRYPOINT ["dotnet", "server.dll"]
