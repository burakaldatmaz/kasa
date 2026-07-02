# Faz 9 Kanıt Raporu — Günlük PDF Yeniden Tasarımı (v2) + Hızlı Erişim

Tarih: 2026-07-02 · Kapsam: `DailyReportPdf.cs` görsel yeniden yazım, veri sözleşmesi ve hesap yolu DEĞİŞMEDİ

## Özet

| Kriter | Sonuç |
| --- | --- |
| dotnet test | **108/108 yeşil** — `dotnet-test-cikti.txt` |
| PDF testleri | **Hiçbiri değiştirilmedi** (`git diff -- Kasa.Tests/` boş) — tüm metin assert'leri v2 tasarımda da geçiyor |
| Veri sözleşmesi | PDF hâlâ `BuildDailyReportAsync` DTO'sunu dizgiye döker; PDF katmanında tek toplama/yüzde yok (I1/I2/I3 aynen) |
| Tek sayfa kuralı | 65 kalem stres günü **tek sayfa ve okunabilir** (kanıt c) — yan yana bloklar sayesinde ScaleToFit'e gerek kalmadan sığdı |
| Container fontu | `/BaseFont /WineTahoma` + `/WineTahomaBold` gömülü; Türkçe glifler ve em-dash WineTahoma'dan — `pdf-metin-container.txt` |
| Dosya adı | Değişmedi: `kasa-islem-YYYY-MM-DD.pdf` |

## Tasarım v2 — neler değişti

- **Üst bant** (tam genişlik #1F3864): solda büyük beyaz **BKKBIKE** + "Günlük Kasa Raporu";
  sağda küçük "KASA İŞLEM —" öneki + büyük beyaz **gün adlı tarih** ("3 Ağustos 2026, Pazartesi") +
  altında gri-beyaz "Oluşturma: 02.07.2026 20:54" (Bangkok saati — endpoint `TimeProvider`'dan
  UTC+7 verir; Bangkok DST uygulamadığından sabit ofset birebir doğrudur).
  Küçük "KASA İŞLEM —" öneki tasarım gereği değil sözleşme gereğidir: mevcut test
  `KASAİŞLEM—3Ağustos2026` metnini arar, önek + tarih bu diziyi aynen üretir.
- **4 özet kartı**: GELİR (yeşil #E2EFDA/#375623) | GİDER (kırmızı ton) | GÜN NET (negatifse
  kırmızı ton, kanıt b) | ANA KASA (lacivert zemin beyaz, en vurgulu; negatifse zemin kırmızı).
- **Kalem tabloları yan yana**: sol GELİR, sağ GİDER; bloklar bağımsız uzar. Başlık şeridinde
  kalem sayısı, satırlar alternating #F5F8FF, not gri italik 7.5pt alt satır, blok altı koyu
  çizgi + bold "Toplam" (tutar DTO'dan). Ödeme kısaltmaları (N)/(KK)/(BT) + altta tek satır lejant.
- **Tür Dağılımı** (sol alt): gelir + gider kategorileri tek kompakt tabloda, Faz 7 Sheet3
  deseni (önce Gelir, sonra Gider); tür işareti **fontsuz çizilen** yeşil/kırmızı daire.
- **Gün Hesabı** (sağ alt, ince çerçeveli kutu): Gelirler/Giderler Toplamı, POS Kesintisi
  (%oran Settings'ten), Gün Net, "+ Devir (önceki günden):" (test sözleşmesindeki metin),
  çift çizgi, büyük punto **ANA KASA** (negatifse kırmızı — kanıt b).
- **Filo şeridi** (en alt, açık gri): `● FİLO: Toplam 59 | Arızalı 4 | Kirada 44 | Boşta 11 |
  Kiralama %80.0`; Arızalı >0 ise **turuncu bold** (kanıt e). Snapshot yoksa "Filo verisi
  girilmedi" (kanıt d). Boşta değeri DTO'daki `IdleBikes`'tan gelir, hesaplanmaz.
- **Alt bilgi**: ince çizgi + "BKKBIKE — BMA Tech Global Co., Ltd." / "kasa.bkkbike.com",
  sayfa numarası yok (tek sayfa garanti).
- **İkon glifi yok** (Faz 8 ⚠ dersi): 🏍/● yerine QuestPDF ile çizilen renkli daireler
  (`Width/Height/CornerRadius/Background`) — font zincirinden bağımsız, container'da garanti.
- **Tek sayfa**: kademeli font 10 → 9 → 8.5 (eşikler ≤30 / ≤52 / üzeri) + `ScaleToFit`
  güvenlik ağı aynen.

## Hızlı erişim

- `DayPage` üst barına doğrudan **PDF** butonu eklendi: `window.open('/api/reports/daily/pdf?date=...')`
  (rapor sayfasına gitmeden indirme). `/rapor` sayfasındaki "PDF İndir" butonu aynen durdu.
- Frontend `npm run build` temiz.

## Kod değişiklikleri

| Dosya | Değişiklik |
| --- | --- |
| `Kasa.Api/Pdf/DailyReportPdf.cs` | v2 dizgi (tamamen yeniden yazıldı; imza: `Render(r, generatedAt)`) |
| `Kasa.Api/Endpoints/ReportEndpoints.cs` | `/daily/pdf` endpoint'i `TimeProvider`'dan Bangkok (UTC+7) oluşturma damgası geçirir |
| `kasa-web/src/DayPage.tsx` | Üst bara PDF butonu |
| `Kasa.Tests/*` | **Dokunulmadı** |

## Kanıtlar (bu klasör)

| Dosya | İçerik |
| --- | --- |
| `a-golden-gun-v2.pdf/.png` | Golden gün (2026-08-03): ANA KASA ฿177.50, POS %3,5 → ฿122.50, filo 14/4/8 |
| `b-negatif-anakasa.pdf/.png` | Negatif gün (2026-09-05): GÜN NET ve ANA KASA kartları + Gün Hesabı satırı kırmızı (-฿5,500.00) |
| `c-stres-65-kalem.pdf/.png` | 65 kalem stres günü: **tek sayfa**, 8.5pt, yan yana bloklar 33/32 kalem |
| `d-islemsiz-gun.pdf/.png` | İşlemsiz gün (2026-11-12): "— Kayıt yok —", devir ฿400.00 taşındı, "Filo verisi girilmedi" |
| `e-filo-arizali-turuncu.pdf/.png` | Filo şeridi turuncu hali: Toplam 59 \| **Arızalı 4 (turuncu)** \| Kirada 44 \| Boşta 11 \| %80.0 |
| `kasa-islem-2026-08-03-container.pdf/.png` | **Container üretimi** golden gün PDF'i (docker imajı `kasa-faz9`, TZ=+07 doğrulandı) |
| `pdf-metin-container.txt` | Container PDF'inden PdfPig dökümü: sayfa=1, font→karakter haritası, düz metin |
| `dotnet-test-cikti.txt` | 108/108 |

Kanıt PDF'leri taze SQLite DB'ye API üzerinden veri girilerek üretildi (demo `kasa.db`'ye
dokunulmadı); PNG önizlemeler `pdftoppm -r 150` çıktısıdır.

## Container font notu (Faz 8 dersinin devamı)

`pdf-metin-container.txt` font→karakter haritası: Türkçe glifler (İ/ğ/ş/ı/Ü/Ö), rakamlar,
em-dash — hepsi **WineTahoma/WineTahomaBold**. Tek istisna **฿ simgesi**: WineTahoma'da
bulunmadığından SkiaSharp, QuestPDF'in kendi gömülü **Lato**'suna düşer ve glif PDF'e
Lato subset'i olarak gömülür — yani ฿ container çıktısında da doğru görünür (tofu yok,
`kasa-islem-2026-08-03-container.png` ile görsel teyitli). Bu, Faz 8'deki üretim
davranışının aynısıdır; v2'de ikon glifleri tamamen kaldırıldığı için font zinciri
riski yalnız ฿ ile sınırlıdır ve o da gömülüdür.
