# Faz 11 — Günlük Rezervasyon Sayaçları (Başlayan / Biten) Kanıt Raporu

**Tarih:** 2026-07-02
**Amaç:** Filo günlük kaydına iki bağımsız operasyonel sayaç: `StartedReservations`
(o gün başlayan rezervasyon) ve `EndedReservations` (o gün biten rezervasyon).

## Kritik Kurallara Uyum

- **K1 (rentalPercent'e girmez):** `FleetCalculator.RentalPercent` formülü ve Faz 4
  testleri DEĞİŞMEDİ (`git diff` yalnız yeni saf `SumCounters` yardımcısını ekler).
  Kanıt: `ReservationCounterApiTests.Put_HugeCounters_DoNotAffectRentalPercent...`
  (started=999 ile %80.0 aynı) + Playwright "(j) K1: rentalPercent sayaçlardan
  ETKİLENMEDİ (33/59 → %55.9)". `Broken+Rented<=Total` tarzı çapraz kısıt sayaçlara
  UYGULANMAZ: 59 filo + 999 başlayan 200 döner (aynı motor aynı gün hem başlayıp hem
  bitebilir; sayılar filo adetleriyle matematiksel ilişki taşımaz).
- **K2 (nullable, null="girilmedi"):** Alanlar `int?`; migration
  `20260702160711_AddReservationCounters` yalnız iki nullable sütun ekler, mevcut
  satırlara 0 YAZMAZ (kanıt: kopya DB'de migration sonrası eski satırlar null —
  `curl-eski-govde.txt` GET çıktısı). UI/PDF/rapor null'ı "—" gösterir; Excel gün
  hücresi null'da BOŞ kalır. 0 ise gerçekten sıfır olarak yazılır ve döner
  (`Put_ZeroCounters_AreStoredAsZeroNotNull`).
- **K3 (validasyon):** Girilmişse `>= 0`; negatifte 400 + Türkçe mesaj
  ("Rezervasyon sayıları negatif olamaz."), kayıt oluşmaz
  (`Put_NegativeCounter_Returns400WithTurkishMessage`).
- **Geriye dönük uyum (Faz 6/7 deseni):** Tüm DTO ekleri SONA eklendi; eski PUT
  gövdesi (üç alan) 200 döner ve sayaçları null yazar. Kanıt: `curl-eski-govde.txt`
  (HTTP/1.1 200 + `"startedReservations":null`) + mevcut 108 test DEĞİŞMEDEN yeşil.
- **I1 (server hesaplar):** `totalStarted`/`totalEnded` toplamları
  `FleetCalculator.SumCounters` ile SERVER'da; null günler toplama katılmaz, TÜM
  günler null ise toplam da null (`Month_MixedDays_TotalsSkipNullDays`,
  `Month_AllDaysNull_TotalsAreNull`). UI hiçbir yerde toplamaz.

## Backend

- `FleetSnapshot` + iki `int?` alan; migration salt `AddColumn` (SQLite tablo
  yeniden kurulumu yok, DB check kısıtı bilinçli eklenmedi — tek kural endpoint
  validasyonundaki `>= 0`, K3).
- `PUT /api/fleet/{date}`: `SaveFleetSnapshotRequest`'e iki OPSİYONEL alan
  (`= null` varsayılanlı); gönderilmezse null yazılır.
- `GET /api/fleet/{date}`, `/api/fleet/month` gün satırları,
  `/api/fleet/month` summary (`totalStarted`/`totalEnded`) ve
  `/api/reports/daily` fleet nesnesi alanları döner.

## PDF (DailyReportPdf v2) / Excel (MonthReportExcel)

- PDF filo şeridi: `... | Kiralama %55.9 | Başlayan 7 | Biten 5`; null'da
  `Başlayan — | Biten —`. Tek sayfa kuralı ve MEVCUT metin assert'leri aynen
  (yeni içerik ekleme ile geldi; `Pdf_GoldenDay...` değişmeden geçiyor).
  Kanıt: `i-pdf-filo-degerli.pdf/.png` (7/5) ve `j-pdf-filo-null.pdf/.png` (—/—).
- Excel Sheet1 "Ay Özeti": "Kiralama %" sonrasına `Başlayan | Biten` sütunları
  (null → boş hücre); filo özet satırına `Toplam Başlayan X | Toplam Biten X`
  (tüm günler null ise "—"). Header/alternating/autofilter stilleri 9 sütuna
  genişletildi. Kanıt: `k-excel-ay-2026-07.xlsx` + `excel-dogrulama.txt`
  (openpyxl dökümü: 01.07 → None/None, 02.07 → 7/5, özet → 7/5).

## Frontend — Mobil (≤640px, `kasa-web/src/mobile/`)

- **Filo sekmesi:** üç stepper'ın altında AYRI kart "Bugünün Rezervasyonları":
  Başlayan (yeşil aksan) / Biten (kırmızı aksan) stepper'ları, aynı desen
  (± + sayıya dokununca doğrudan yazma, `inputmode=numeric`). Null durumda giriş
  0'dan DEĞİL boş "—" göstergeden (placeholder) başlar; kullanıcı dokunmadan
  kaydederse null gider (Playwright (b) adımı bunu API'den doğrular).
- **Gün ekranı** filo mini kart meta: `33 kirada · 26 boşta · 0 arızalı ·
  7 başladı / 5 bitti`; iki sayaç da null ise bu kısım tamamen gizli.
- **Ay ekranı:** filo özet satırına `Başlayan X · Biten X` (null → "—");
  Günler listesinde net tutarın altında küçük soluk trend satırı `↑7 ↓5`
  (yeşil ↑ / kırmızı ↓, `opacity .72`, 11px). Snapshot yoksa VEYA alanlar null
  ise gösterge tamamen gizli ("—" bile yazılmaz) — kanıt `d-mobil-ay-trend-390.png`
  aynı karede değerli (2 Tem) + null (1 Tem) günü gösterir.
- **Rapor ekranı:** filo bölümü (ay özeti satırı deseniyle) aynı bilgiyi gösterir.

## Frontend — Desktop (641px+)

- **FleetCard:** mevcut form desenine iki sayı alanı (Başlayan / Biten, boş
  bırakılabilir → null gider) + başlık rozetinde `Başlayan 7 · Biten 5`
  (ikisi de null ise rozet gizli).
- **/rapor** filo satırı: `FİLO: ... | Başlayan 7 | Biten 5` (null → "—") — PDF ile
  birebir. **/ay** gün tablosuna iki dar sütun `Başlayan | Biten` (null → "—"),
  tfoot boş; filo özet kartına `Toplam Başlayan X | Toplam Biten X`.
  Desktop düzeni bozulmadı (aynı kart/tablo desenleri).

## Testler ve Doğrulama

- `dotnet test`: **121/121** (108 mevcut DEĞİŞMEDEN + 13 yeni
  `ReservationCounterApiTests`: eski gövde uyumu, round-trip, eski gövdeyle
  null'a dönüş, negatif 400 ×2, sıfır≠null, K1, ay toplamı karışık/tümü-null,
  PDF değerli/null, Excel round-trip/tümü-null). GoldenDayTest ve Faz 4 yüzde
  testleri değişmeden yeşil.
- `npm run build` (tsc strict + vite): temiz.
- Playwright (izole API 5268 + kopya DB, `playwright-dogrulama.js`):
  **46 PASS / 0 FAIL** — 390×844 mobil VE 1280×800 desktop (`playwright-cikti.txt`).

## Kanıt Dosyaları

| Dosya | İçerik |
|---|---|
| `a-mobil-filo-rezervasyon-bos-390.png` | Rezervasyon kartı NULL durumda: boş "—" gösterge |
| `b-mobil-filo-rezervasyon-dolu-390.png` | Giriş akışı: Başlayan 7 (6 + stepper), Biten 5 |
| `c-mobil-gun-mini-kart-390.png` | Gün mini kartı: "… · 7 başladı / 5 bitti" |
| `d-mobil-ay-trend-390.png` | Ay Günler: 2 Tem `↑7 ↓5` trendli, 1 Tem (null) sade — aynı karede |
| `e-mobil-rapor-filo-390.png` | Mobil rapor filo bölümü |
| `f-desktop-fleetcard-1280.png` | FleetCard: iki yeni alan + rozet |
| `g-desktop-ay-sutunlar-1280.png` | /ay: Başlayan-Biten sütunları (7/5 ve —/—) + Toplam satırı |
| `h-desktop-rapor-filo-1280.png` | /rapor filo satırı |
| `i-pdf-filo-degerli.pdf/.png` | PDF şeridi: Başlayan 7 \| Biten 5 (tek sayfa) |
| `j-pdf-filo-null.pdf/.png` | PDF şeridi: Başlayan — \| Biten — (tek sayfa) |
| `k-excel-ay-2026-07.xlsx` + `excel-dogrulama.txt` | Yeni sütunlar + özet toplamları (openpyxl dökümü) |
| `curl-eski-govde.txt` | ESKİ gövdeyle PUT → 200 + alanlar null (geriye dönük uyum) |
| `playwright-dogrulama.js` / `playwright-cikti.txt` | Doğrulama scripti + 46/0 çıktısı |

## Not — dev veritabanı (kasa.db)

Kanıtlar kasa.db'nin **kopyası** üzerinde alındı (5267'deki çalışan dev API'ye
dokunulmadı). kasa.db'ye migration, dev API yeni build ile yeniden
başlatıldığında `dotnet ef database update` (veya `AUTO_MIGRATE=true`) ile
uygulanır; örnek sayaç verisi için:
`PUT /api/fleet/2026-07-02 {"totalBikes":59,"brokenBikes":0,"rentedBikes":33,"startedReservations":7,"endedReservations":5}`.
