# Faz 8 Kanıt Raporu — Tek Kullanıcı Auth + Production Deployment

Tarih: 2026-07-02 · Hedef: kasa.bkkbike.com

## Özet

| Kriter | Sonuç |
| --- | --- |
| dotnet test | **108/108 yeşil** (100 mevcut + 8 yeni auth testi) — `dotnet-test-cikti.txt` |
| GoldenDayTest.cs değişmedi | **Diff boş** (aşağıda) — golden 17750 testi aynen geçiyor |
| Smoke test (sıfır ortam) | **Tüm adımlar başarılı** — `smoke-test.log` |
| TZ kontrolü (Bangkok) | date `+07`, UTC 18:00 → **2026-07-03 01:00 +0700**, app zone `Asia/Bangkok` |
| Yedek servisi | Elle tetiklendi, `kasa-2026-07-02.db` üretildi, `integrity_check: ok` |
| İmaj boyutu | **630 MB** (aspnet:10.0 tabanı + fontlar + QuestPDF/Skia native) |

## Bölüm A — Auth tasarımı

- **Parola:** `PASSWORD_HASH` ortam değişkeninde bcrypt hash (BCrypt.Net-Next, work factor 12).
  appsettings'te parola YOK; dev fallback hash'i `appsettings.Development.json`'da
  (dev parolası: `kasa-dev`). Hash üretimi: `dotnet Kasa.Api.dll hash-password '<parola>'`
  (compose: `docker compose run --rm --no-deps api hash-password '<parola>'`).
- **Oturum:** `POST /api/auth/login` → HttpOnly + SameSite=Strict cookie (`kasa_auth`),
  30 gün sliding. Production'da `Secure` zorunlu (`CookieSecurePolicy.Always`);
  dev/test http'de çalışabilsin diye `SameAsRequest` (Production Set-Cookie'nin `secure`
  içerdiği testle kanıtlı: `ProductionEnvironment_LoginCookie_IsSecure`).
- **Logout gerçekten geçersiz kılar:** Cookie ticket'ı stateless olduğundan yalnız cookie
  silmek yetmez — Settings tablosunda tutulan **oturum damgası** login'de claim olarak
  gömülür, logout damgayı döndürür; eski cookie replay edilse bile 401
  (`Logout_OldCookie_Returns401_EvenIfReplayedRaw`). Damga DB'de durduğu için uygulama
  yeniden başlasa da geçerli oturumlar düşmez (30 gün sözü korunur).
- **Brute force:** 5 hatalı denemede 15 dk kilit (in-memory sayaç, tek kullanıcı).
  Kilitliyken doğru parola da 429; tüm denemeler IP ile loglanır. Süre `TimeProvider`
  üzerinden okunur — test kilidi gerçek bekleme olmadan simüle eder
  (`BruteForce_FiveFailures_Locks15Minutes_EvenCorrectPasswordGets429`: 14:59'da hâlâ 429,
  15:01'de giriş başarılı).
- **Kapsam:** `FallbackPolicy` ile TÜM endpoint'ler varsayılan auth ister; istisnalar tek tek
  `AllowAnonymous`: `/api/auth/login`, `/health`, SPA fallback. Yeni endpoint'ler otomatik
  korunur. Bilinmeyen `/api/*` path'i SPA'ya düşmez: auth'suz 401, auth'lu 404.
- **Frontend:** `/login` sayfası (aynı tasarım dili: card + lacivert başlık), 401 yakalama
  `api.ts`'teki tek `request()` wrapper'ında (sayfa sayfa kontrol yok, `?next=` ile dönüş,
  open-redirect koruması), çıkış butonu üç sayfanın üst barında (`LogoutButton`).
- **Mevcut 100 test değişmedi:** `KasaApiFactory.CreateClient` tek noktadan login olup
  cookie'yi istemciye ekler; test dosyalarına dokunulmadı.

## Bölüm B — Paketleme

- **Dockerfile (3 aşama):** `node:22-alpine` (vite build) → `dotnet/sdk:10.0` (publish) →
  `dotnet/aspnet:10.0` (runtime). Frontend dist `wwwroot`'a kopyalanır; Kasa.Api statik
  dosya + SPA fallback servis eder (`/rapor`, `/ay`, `/login` yenilemede çalışır — smoke
  adım 10).
- **PDF fontu:** QuestPDF/SkiaSharp için `libfontconfig1`; Tahoma'nın metrik uyumlu
  karşılığı `fonts-wine`'dan gelir. Wine fontları fontconfig'in taramadığı dizine düştüğü
  için Dockerfile'da `/usr/share/fonts`'a kopyalanıp `fc-cache` çalıştırılır. Kanıt:
  container PDF'inde `/BaseFont /WineTahoma` gömülü, Türkçe glifler ve ฿ simgesi doğru
  (`pdf-metin-container.txt`: "KASA İŞLEM", "GELİR TÜRÜ DAĞILIMI", "POS Kesintisi (%3,5)").
- **SQLite:** `/data/kasa.db` (named volume `kasa-data`), connection string env'den
  (`ConnectionStrings__Kasa`). `AUTO_MIGRATE=true` ile boş volume'de şema + seed
  migration'larla kurulur (sıfır kurulum smoke ile kanıtlı).
- **TZ=Asia/Bangkok:** api + backup servislerinde. "Bugün" hesapları `clock.GetLocalNow()`
  üzerinden Bangkok gününe göre. Kanıt (smoke adım 12): container `date` → `+07`;
  gece yarısı senaryosu `date -d '2026-07-02 18:00 UTC'` → `2026-07-03 01:00 +0700`
  (UTC 18:00 = BKK ertesi gün 01:00, doğru güne yazar); uygulama gözünden
  `dotnet Kasa.Api.dll now` → `zone = Asia/Bangkok`, `local = ...+07:00`.
- **Yedek:** alpine + crond, her gece 03:00 (BKK) `sqlite3 ".backup"` (cp DEĞİL — yazma
  anında tutarlı kopya) → `/backups/kasa-YYYY-MM-DD.db`, 30 günden eskiler `find -mtime +30
  -delete`. Kanıt (smoke adım 13): crontab girdisi, elle tetikleme, üretilen dosyada
  `PRAGMA integrity_check → ok`, 14 kategori + 1 işlem okunuyor.
- **Reverse proxy:** `deploy/nginx-kasa.bkkbike.com.conf` (80→443, HSTS, X-Forwarded-For/
  Proto; API tarafında `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`). Sertifika:
  `sudo certbot --nginx -d kasa.bkkbike.com`. Kurulum/geri dönüş adımları: `deploy/README.md`.

## Kabul kriterleri

### 1. dotnet test + golden kanıtı

`dotnet-test-cikti.txt`: **Başarısız: 0, Başarılı: 108, Toplam: 108**.

GoldenDayTest.cs kanıtı: proje Faz 8 başında git'e alındı (baseline commit
"Faz 1-7 durumu", Faz 8 öncesi hali). Faz 8 commit'ine göre:

```
$ git diff <baseline>..HEAD -- Kasa.Tests/GoldenDayTest.cs
(boş — dosyaya dokunulmadı)
git blob hash (baseline = HEAD): e9a3b92b08a7676bb0e497f4112418673704b067
dosya mtime: Jul 2 17:53 (Faz 8 çalışması 19:30'dan sonra başladı)
```

Not: Faz 1-7 git geçmişi yoktu (repo Faz 8'de oluşturuldu); "Faz 1'den beri" kanıtı
bu nedenle baseline snapshot + değişmeyen blob hash + golden değerin (17750) hâlâ
geçmesi üzerinden verildi.

### 2. Sıfır ortamda smoke test

`smoke-test.log` — ayrı compose projesi (`kasa-smoke`) + taze volume, sıralama:
hash üret → `up -d --wait` (healthcheck bekler) → `/health` 200 → auth'suz 401 →
yanlış parola 401 → login 204 + cookie → seed kategoriler → işlem POST 201
(2300 baht = 230000 satang) → PDF 200 (%PDF imzası) → Excel 200 (zip doğrulandı) →
filo PUT 200 (%71.4) → SPA fallback (4 path) → bilinmeyen api 401 → logout 204 →
eski cookie 401. **Sonuç: TÜM ADIMLAR BAŞARILI.**

### 3. TZ kontrolü — yukarıda (Bölüm B / TZ).

### 4. Yedek servisi — yukarıda (Bölüm B / Yedek).

### 5. Sayılar

- Toplam test: **108** (Faz 7'de 100 + 8 auth)
- İmaj boyutu: **630 MB**
- Smoke adım çıktıları: `smoke-test.log`; container üretimi örnek çıktılar:
  `kasa-islem-2026-07-02-container.pdf`, `kasa-2026-07-container.xlsx`,
  `pdf-metin-container.txt`

## Dosyalar

| Dosya | İçerik |
| --- | --- |
| `Dockerfile`, `.dockerignore` | 3 aşamalı imaj |
| `docker-compose.yml`, `.env.example` | api + backup servisleri, TZ, volume |
| `deploy/backup.sh` | sqlite3 .backup + 30 gün temizlik |
| `deploy/nginx-kasa.bkkbike.com.conf` | server block + certbot komutları |
| `deploy/README.md` | kurulum, parola değiştirme, yedekten dönme |
| `Kasa.Api/Auth/*` | LoginRateLimiter, SessionStamp, KasaCookieEvents |
| `Kasa.Api/Endpoints/AuthEndpoints.cs` | login/logout/me |
| `Kasa.Tests/AuthApiTests.cs` | 8 auth testi |
| `kasa-web/src/LoginPage.tsx`, `components/LogoutButton.tsx` | login UI + çıkış |
