# build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# 1) Restore cache'i için çözüm ve merkezi nuget dosyaları
COPY CoHr.sln ./

# 2) Projenin csproj'u (grafiği değiştirmeden restore cache'i ısındırır)
COPY src/Comp.Api/Comp.Api.csproj src/Comp.Api/
RUN dotnet restore src/Comp.Api/Comp.Api.csproj

# 3) Tüm kaynaklar
COPY . .

# 4) Güvenli yol: tam grafikte tekrar restore (veya publish'ta --no-restore kullanmayın)
RUN dotnet restore src/Comp.Api/Comp.Api.csproj

# 5) Publish (no-restore kullanmıyoruz)
RUN dotnet publish src/Comp.Api/Comp.Api.csproj -c Release -o /app

# run
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet","Comp.Api.dll"]