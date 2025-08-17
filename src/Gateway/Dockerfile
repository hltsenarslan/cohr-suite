# build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish -c Release -o /app

# run
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
# OTEL endpoint (compose ile verilecek)
ENV OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317
EXPOSE 8080
#HEALTHCHECK --interval=10s --timeout=3s --start-period=10s CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet","Gateway.dll"]