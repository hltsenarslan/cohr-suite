# build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY CoHr.sln ./

COPY src/Perf.Api/Perf.Api.csproj src/Perf.Api/
RUN dotnet restore src/Perf.Api/Perf.Api.csproj

COPY . .
RUN dotnet restore src/Perf.Api/Perf.Api.csproj
RUN dotnet publish src/Perf.Api/Perf.Api.csproj -c Release -o /app

# run
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet","Perf.Api.dll"]