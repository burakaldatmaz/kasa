# Faz 12 Kanıt — Domain: BahtText + DepositNumber

Saf fonksiyonlar; dizgi/HTTP'den bağımsız, golden testlerle sabitlendi (`Kasa.Tests/BahtTextTests.cs`, `DepositNumberTests.cs`). Bu faz kasa'nın mali akışına **dokunmaz**.

## BahtText — tutarın (satang) harflerle yazımı

Kritik Thai kuralları golden test'lerde ayrı ayrı sabit:

| satang | İngilizce | Thai |
|--------|-----------|------|
| 300000 | Three thousand baht only | สามพันบาทถ้วน |
| 100 | One baht only | หนึ่งบาทถ้วน (tek başına 1 → หนึ่ง) |
| 2100 | Twenty-one baht only | ยี่สิบเอ็ดบาทถ้วน (21 → ยี่สิบ + เอ็ด) |
| 1100 | — | สิบเอ็ดบาทถ้วน (11 → สิบเอ็ด) |
| 1000 | — | สิบบาทถ้วน (10 → สิบ, หนึ่งสิบ değil) |
| 10100 | — | หนึ่งร้อยเอ็ดบาทถ้วน (101 → เอ็ด) |
| 2000000000 | Twenty million baht only | ยี่สิบล้านบาทถ้วน (ล้าน katmanı) |
| 12550 | One hundred twenty-five baht and fifty satang | หนึ่งร้อยยี่สิบห้าบาทห้าสิบสตางค์ |
| 50 | Fifty satang | ห้าสิบสตางค์ (0.50 baht) |

Sınır: `0` ve negatif → `ArgumentOutOfRangeException` (makbuz sıfır tutar kesmez).

## DepositNumber

`Format(year, seq)` → `"DEP-YYYY-NNNNN"` (yıl 4 hane, sıra 5 hane sıfır dolgulu). Örn: `Format(2026, 418)` → `DEP-2026-00418`.

## Sonuç

`test-cikti.txt`: **26 test, 0 hata** (Domain golden).
