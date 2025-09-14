# build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY CoHr.sln ./

COPY src/Core.Api/Core.Api.csproj src/Core.Api/
RUN dotnet restore src/Core.Api/Core.Api.csproj

COPY . .
RUN dotnet restore src/Core.Api/Core.Api.csproj
RUN dotnet publish src/Core.Api/Core.Api.csproj -c Release -o /app

# run
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet","Core.Api.dll"]