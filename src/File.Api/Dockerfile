FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY CoHr.sln ./ 
COPY Directory.Packages.props ./ 
COPY NuGet.config ./ 
COPY src/File.Api/File.Api.csproj src/File.Api/
RUN dotnet restore src/File.Api/File.Api.csproj

COPY . .
RUN dotnet publish src/File.Api/File.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
ENV OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:4317
EXPOSE 8080
ENTRYPOINT ["dotnet","File.Api.dll"]