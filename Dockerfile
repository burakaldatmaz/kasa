# ---------- Aşama 1: frontend build (kasa-web → dist) ----------
FROM node:22-alpine AS web
WORKDIR /src
COPY kasa-web/package.json kasa-web/package-lock.json ./
RUN npm ci
COPY kasa-web/ ./
RUN npm run build

# ---------- Aşama 2: API publish ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Önce csproj'lar: restore katmanı kod değişikliğinde cache'ten gelir.
COPY Kasa.Domain/Kasa.Domain.csproj Kasa.Domain/
COPY Kasa.Api/Kasa.Api.csproj Kasa.Api/
RUN dotnet restore Kasa.Api/Kasa.Api.csproj
COPY Kasa.Domain/ Kasa.Domain/
COPY Kasa.Api/ Kasa.Api/
RUN dotnet publish Kasa.Api/Kasa.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- Aşama 3: runtime (statik dosya + SPA fallback'i Kasa.Api servis eder) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
# QuestPDF (SkiaSharp) fontconfig ister; PDF fontu Tahoma'nın metrik uyumlu karşılığı
# fonts-wine'dan gelir (Türkçe glifler dahil). Wine fontları /usr/share/wine/fonts'a düşer,
# fontconfig orayı taramaz — Tahoma'ları fontconfig yoluna kopyala. tzdata: TZ=Asia/Bangkok.
RUN apt-get update \
 && apt-get install -y --no-install-recommends libfontconfig1 fontconfig fonts-dejavu-core fonts-wine tzdata \
 && mkdir -p /usr/share/fonts/truetype/wine \
 && cp /usr/share/wine/fonts/tahoma*.ttf /usr/share/fonts/truetype/wine/ \
 && fc-cache -f \
 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish ./
COPY --from=web /src/dist ./wwwroot
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Kasa.Api.dll"]
