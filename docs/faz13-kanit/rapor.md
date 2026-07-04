# Faz 13 Kanıt — API: kayıt + PDF

`DepositReceipt` entity + migration + `/api/deposit-receipts` uçları + `DepositReceiptPdf`. Kasa'nın mali akışıyla **SIFIR temas**: Transaction/rapor/kasa sayımı değişmedi; mevcut 121 test aynen yeşil.

## Entity + migration

`AddDepositReceipt` migration: `No` **UNIQUE** index, `Date` index, `CK_DepositReceipt_AmountSatang > 0` check, `CreatedAt` UTC dönüşümü (Transaction deseni). Ödeme enum'u mevcut `PaymentMethod` (Cash/CreditCard/BankTransfer) aynen.

## Endpoints

- `POST /api/deposit-receipts` — numarayı **server** atar (yıl bazlı MAX+1). Süreç-geneli `SemaphoreSlim` kritik bölümü serialize eder; UNIQUE index son sigortadır. Tutar BAHT string gelir, satang'a çeviri `BahtParser` ile (I1).
- `GET /api/deposit-receipts?date=` — günün makbuzları, `No` sırasıyla (yeniden yazdırma).
- `GET /api/deposit-receipts/{id}/pdf` — `DEP-YYYY-NNNNN.pdf` (application/pdf).

## PDF — `DepositReceiptPdf` (QuestPDF)

Onaylı BKKBIKE tasarımı birebir (navy #1F3864 / blue #2E75B6 / tint #F5F8FF), tek A4'te iki nüsha (CUSTOMER + OFFICE), aralarında kesikli FOLD & CUT. **v4 eklentileri:** METHOD üç kutucuklu satır (seçili navy dolu + beyaz tik) ve üçüncü bilgi kutusu **Late return** (Refundable · Usage limits · Late return). İkonlar SVG ile çizilir (glif değil → font zincirinden bağımsız). En dışta `ScaleToFit` tek-sayfa sigortası.

**Sarabun assembly'ye gömülü** (`Kasa.Api/Fonts/*.ttf`, `EmbeddedResource` → `SarabunFonts.EnsureRegistered`): Thai dizgisi Docker'da ve test host'unda garanti; ฿ glifi de Sarabun'dan gelir.

- `DEP-2026-00001.pdf` — çalışan uygulamadan indirilen gerçek makbuz (Edward Penney Beaumont · Honda Click 160 · ฿3,000.00). `DEP-2026-00001.pdf.png` render'ı.

## Testler (`DepositReceiptApiTests`, PdfPig)

- Numara 1'den başlar ve artar; yıl sınırında sıfırlanır; **12 eşzamanlı POST çakışmaz** (tekil numaralar).
- PDF **tek sayfa**; `No`, isim, `฿3,000.00`, `Three thousand baht only` ve Thai `สามพันบาทถ้วน` metinleri PdfPig ile doğrulandı (Sarabun doğru gömülmüş).
- Zorunlu alan / geçersiz tutar → 400 Türkçe mesaj; olmayan makbuz PDF → 404.

`test-cikti.txt`: **11 test, 0 hata**. `tum-suit.txt`: **158 test, 0 hata** (121 mevcut + 47 yeni).
