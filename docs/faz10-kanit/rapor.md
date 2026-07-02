# Faz 10 — Mobil Arayüz (Claude Design Referanslı Yeniden Düzenleme) Kanıt Raporu

**Tarih:** 2026-07-02
**Görsel referans:** `Tasarım Sistemi/Kasa Mobil.dc.html` — yalnız görsel dil (renkler,
kart stilleri, köşe yarıçapları, tipografi, boşluklar), ekran düzenleri ve etkileşim
desenleri alındı. Prototipteki cihaz çerçevesi, sc-if/sc-for şablonları ve TÜM
JavaScript mantığı (derive(), client hesapları, Math.round + en-US format,
client-side clamp) ALINMADI.

> Not: Bu rapor önceki "responsive CSS geçişi" kanıtının yerini alır; eski kanıt
> git geçmişinde durur (commit `3f29c27`).

## Kurallara Uyum

- **R1 (backend dokunulmadı):** `git status`'ta Kasa.Api/Kasa.Domain/Kasa.Tests
  değişikliği yok; `dotnet test` **108/108**. Ay ekranındaki "Ay raporunu indir
  (Excel)" mevcut `/api/reports/month/xlsx` ucuna bağlı (Playwright'ta uç canlılığı
  doğrulandı).
- **R2 (I1 — client hesap yok):** Gelir/gider toplamı, POS, gün net, kapanış,
  devir, ortak payları, filo yüzdesi ekranlara `/api/reports/daily`,
  `/api/reports/month`, `/api/fleet/*`'ten hazır gelir. POS etiketi
  `posFeeRatePercent`'ten ("POS %3,5" hardcode değil), ortak adları/payları
  `distribution`'dan. Para biçimi yalnız `format.ts` (satang/100 → th-TH).
  Grep kanıtı: `src`'de `"0.035"`, `"reduce(("` ve `'en-US'` araması **boş**.
  Görsel ölçek istisnaları kod içinde işaretli: ring `dasharray`
  (rentalPercent/100 → çevre izdüşümü) ve dağılım bar genişliği (kategori/max) —
  para hesabı değil.
- **R3 (tek kırılım):** `App.tsx` 640px `matchMedia` ile ≤640px'te `MobileApp`
  render eder; 641px+ eski sayfa bileşenleri aynen. Desktop 1280×800 ekran
  görüntüleri önceki commit'teki referanslarla **%0.000 piksel farkı** (birebir).
- **R4:** `dotnet test` 108/108 · `npm run build` (tsc strict + vite) temiz ·
  `npm run lint` (oxlint) temiz.

## Yapı (≤640px)

1. **Tab bar:** Gün | Ay | [+ FAB] | Rapor | Filo — yarı saydam + blur, aktif
   `#1F3864`, safe-area dolgusu. Filo ayrı ekran; Çıkış Gün ekranı sağ üstünde ikon.
2. **Gün:** "Bugün" (başka gün seçiliyse tarih) + uzun tarih + ‹ › (mevcut `?date=`
   state'i, pushState/popstate). Lacivert gradient hero: KASADA (negatifte kırmızı
   ton), "+GünNet bugün · devir X" (negatif GünNet `#F2565B`), GELİR|GİDER|POS mini
   kutuları. Filo mini kartı (ring + "N kirada · N boşta · N arızalı") → Filo
   sekmesi; snapshot yoksa sarı "⚠ Bugünün filo verisi girilmedi" kartı (dokununca
   Filo). İkonlu Gelirler/Giderler listeleri; meta satırı "K.Kartı · not".
   Satıra dokununca alt eylem sheet'i: Düzenle (sheet içinde form) / Sil (confirm).
3. **Giriş sheet'i (FAB):** Gelir/Gider toggle (yeşil/kırmızı aksan), büyük tutar
   göstergesi (ham string, biçimleme yok), 12 tuşlu özel keypad (BahtParser ile
   aynı kural: en fazla 2 ondalık, ikinci nokta engelli), kategori chip'leri
   API'den SortOrder sırasıyla + sonda "＋ Yeni" (CategoryModal yeniden kullanıldı,
   eklenen chip seçili gelir), Nakit|K.Kartı|Havale segmenti (enum map:
   Cash/CreditCard/BankTransfer), "＋ Not ekle" satırı, "Ekle ve devam" (tutar+not
   sıfırlanır, kategori+ödeme korunur) ve "Kaydet". Başarıda toast
   ("Gelir eklendi · ฿1,234.56" — tutar server yanıtından) + `/api/reports/daily`
   yeniden çekilir; API 400 sheet içinde kırmızı gösterilir, sheet kapanmaz.
4. **Ay:** Başlık + ‹ › (`?month=`), hero (Ay Sonu Ana Kasa + Gelir/Gider),
   ortaklık kartları (`distribution`; finalBalance<0 → kırmızı zarar kartı),
   Günler listesi (gün rozeti + haftanın günü + "Kasa ฿X" + renkli net) → dokununca
   o günün Rapor sekmesi, Gelir/Gider dağılım bar listeleri, filo ay özeti
   (ortalama % / arızalı-gün / eksik gün), "Ay raporunu indir (Excel)".
5. **Rapor:** Devreden Kasa şeridi, noktalı Gelirler/Giderler satırları (kategori
   yanında kısaltmalı ödeme, not gri alt satır), POS satırı, Gün Net kutusu,
   lacivert Kapanış Kasası (negatifte kırmızı), Tür Dağılımı bölümü, "PDF olarak
   indir" → mevcut `/api/reports/daily/pdf`.
6. **Filo:** Büyük ring + % + Kirada/Boşta/Arızalı (GET `/api/fleet/{date}`; 404 →
   "Bugün için filo verisi yok"). Ring/yüzde her zaman son KAYITLI snapshot'tan
   (API); stepper'lar (±) + sayıya dokununca doğrudan yazma (`inputmode=numeric`).
   Client-side clamp yok — kısıt ihlali kaydet sonrası API 400 mesajıyla gösterilir.
   "Filo durumunu kaydet" → PUT + toast.
7. **Login:** mobilde tam genişlik alan + 48px buton (yalnız CSS).

Eski ≤640px CSS bloğu buduldu: desktop bileşenleri artık ≤640px'te hiç render
edilmediği için o kurallar ölüydü; yalnız ortak login/modal kuralları kaldı.
641px+ CSS'ine dokunulmadı.

## Doğrulama — Playwright (chromium, 390×844 @2x + 1280×800): **70/70 PASS**

Tam çıktı: `playwright-cikti.txt` · script: `playwright-dogrulama.js`

- **(a)** Tab bar geçişleri (4 sekme + aktif renk) + FAB sheet aç/kapa, 12 tuş.
- **(b)** Keypad ile "1234.56": gösterge `฿1234.56`, ikinci nokta ve 3. ondalık
  engellendi; Kaydet → toast "Gelir eklendi · ฿1,234.56"; satır listede; hero
  KASADA/GELİR/GİDER/POS ve devir satırı **API yanıtındaki değerlerle birebir**;
  POS etiketi `posFeeRatePercent`'ten.
- **(c)** "Ekle ve devam": sheet açık, tutar `฿0`, kategori + ödeme (Havale)
  korundu; kısaltma liste meta satırında.
- **(d)** Satır dokunma → Düzenle/Sil sheet'i; dokunma alanı ≥44px; Düzenle
  formu sheet içinde.
- **(e)** Gün/Ay/Rapor/Filo/giriş sheet'i/eylem sheet'inde yatay sayfa taşması yok
  (scrollWidth=390) + tüm tutar öğeleri boundingBox ile viewport içinde.
- **(f)** 1280×800: 6 özet hücresi yan yana, mobil katman gizli, tab bar/FAB
  DOM'da yok, formlar iki sütun, nav `display:contents`; gün + ay ekran
  görüntüleri önceki referanslarla piksel karşılaştırması **%0.000 fark**.
- **(g)** "+ Yeni" chip → CategoryModal (sheet üstünde) → eklenen chip şeritte ve
  seçili.
- **Ek:** Boş gün (başlık tarih gösterir, sarı filo uyarısı, boş listeler),
  Filo 404 boş durumu, xlsx/pdf uçlarının canlılığı, Ay hero/ortak kartlarının
  API değerleriyle eşleşmesi, Ay günü → Rapor geçişi.

Doğrulama izole ortamda yapıldı: API kopya DB ile 5268'de (webroot=dist, geçici
parola hash'i env üzerinden); üretim/geliştirme süreçlerine (5267/5173) ve
`kasa.db`'ye dokunulmadı.

## Kanıt Dosyaları

| Dosya | İçerik |
|---|---|
| `a-login-390.png` | Login, 390px (tam genişlik alan + büyük buton) |
| `b-gun-390.png` | Gün ekranı: hero + filo mini + ikonlu listeler + tab bar |
| `c-ay-390.png` | Ay ekranı: hero + ortaklık kartları + günler + dağılım barları |
| `d-rapor-390.png` | Rapor: devir şeridi, noktalı satırlar, Gün Net, kırmızı Kapanış (negatif) |
| `e-filo-390.png` | Filo: büyük ring + stepper'lar + kaydet |
| `f-giris-sheet-390.png` | Giriş sheet'i: keypad ile ฿1234.56 yazılmış hali |
| `g-toast-390.png` | Başarı toast'ı: "Gelir eklendi · ฿1,234.56" |
| `h-eylem-sheet-390.png` | İşlem eylem sheet'i (Düzenle/Sil) |
| `i-gun-bos-390.png` | Boş gün: başlık tarih, sarı filo uyarı kartı, boş listeler |
| `j-filo-bos-390.png` | Filo boş durumu (404): "Bugün için filo verisi yok" |
| `g-gun-desktop-1280.png` | Desktop gün (2026-08-03) — önceki referansla %0.000 fark |
| `h-ay-desktop-1280.png` | Desktop ay — önceki referansla %0.000 fark |
| `playwright-cikti.txt` | 70 kontrolün tam çıktısı |
| `playwright-dogrulama.js` | Doğrulama script'i (yeniden çalıştırılabilir) |
