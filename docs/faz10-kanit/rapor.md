# Faz 10 — Mobil Uyumlu Arayüz (Responsive) Kanıt Raporu

**Tarih:** 2026-07-02
**Kapsam:** Yalnızca kasa-web CSS/layout. Backend'e dokunulmadı; API sözleşmesi,
DTO'lar ve bileşen davranışları (Enter-submit, odak yönetimi, form korunumu,
401 yönlendirme) değişmedi. I1 aynen: mobilde de tek para hesabı yok, tüm
rakamlar server'dan hazır gelir.

## Yaklaşım

Tek kırılım: `@media (max-width: 640px)`. 641px+ desktop düzeni pikseli
pikseline aynı kaldı — mobil için eklenen sarmalayıcılar (`.page-nav`,
`.summary-cells`) desktop'ta `display: contents` ile kutu üretmez, çocuklar
eskisi gibi üst konteynerin doğrudan flex öğesi olarak dizilir.

## Değişiklikler

1. **Viewport:** `index.html`'de `width=device-width, initial-scale=1` zaten
   vardı; doğrulandı, değişiklik gerekmedi.

2. **Alt özet barı (`SummaryBar.tsx`):** Mobilde iki katman.
   - Üst satır (her zaman görünür, alta sabit): **Gün Net** ve **ANA KASA**
     yan yana, büyük punto. Ana Kasa negatifse kırmızı zemin kuralı aynen.
   - Detay: bara dokununca yukarı açılan panel — Devir | Gelir | Gider |
     POS Kesintisi 2×2 grid. Tekrar dokununca kapanır; varsayılan kapalı.
     Açık/kapalı durumu Gün Net etiketindeki ▲/▼ oku gösterir
     (`aria-expanded` ile erişilebilir).
   - Tutarlar kırpılmaz/binmez: `tabular-nums` + `nowrap` + `clamp()` ile
     dar ekranda punto kademeli düşer. iPhone home çubuğu için
     `env(safe-area-inset-bottom)`.

3. **Formlar (`TxnForm`):** Mobilde tek sütun: Kategori → Ödeme → Tutar →
   Not → Ekle (tam genişlik). Dropdown'lar %100 genişlik — "Lastik Se..."
   kırpılması bitti. "+ Yeni Kategori" kategori altına tam genişlik geçer.
   Tutar `inputmode="decimal"` (zaten vardı). Ekle sonrası odak tutara
   `focus({ preventScroll: true })` ile döner — iOS'ta klavye kapanmaz,
   sayfa zıplamaz. 16px input fontu ile iOS'un odakta otomatik zoom'u
   engellendi.

4. **İşlem listesi (`TxnList`):** Satır iki katlı — üstte "Kategori (Yöntem)",
   altta tutar sağa dayalı. Düzenle/Sil sağda, dokunma alanı ≥44×44px.
   Inline düzenleme mobilde dikey açılır. Desktop görünümü değişmedi
   (aynı satır içi metin; ayrım yalnızca span'lerle).

5. **Filo kartı:** Üç sayı girişi mobilde tek sütun, `inputmode="numeric"`.

6. **Nav:** Üst bar linkleri (`.page-nav`) mobilde yatay kaydırılabilir şerit
   (scrollbar gizli). Hamburger menü yapılmadı.

7. **Ay tablosu:** `.table-scroll` konteyneri ile yatay kaydırma; Tarih kolonu
   sticky-left (satır zebra arka planı korunarak). Kolon gizlenmedi.

8. **Rapor sayfası:** Dağılım blokları 700px altında zaten tek sütundu;
   mobilde sheet dolgusu daraltıldı.

## Doğrulama

- `npm run build` (tsc strict + vite): temiz. `npm run lint` (oxlint): temiz.
- `dotnet test`: **108/108** — backend'e dokunulmadığının teyidi.
- Playwright (chromium, viewport 390×844 iPhone 14 + 1280×800 desktop):
  **29/29 PASS** — bkz. `playwright-cikti.txt`.
  - (a) özet barında hiçbir tutar taşmıyor/binmiyor (boundingBox karşılaştırması)
  - (b) detay paneli aç/kapa çalışıyor, 4 hücre, varsayılan kapalı
  - (c) dropdown'lar/butonlar tam genişlik; Düzenle/Sil ≥44×44
  - (d) Ekle sonrası odak tutar alanına dönüyor, alan sıfırlanıyor
  - (e) ay tablosu konteyner içinde kaydırılıyor, Tarih kolonu sabit,
    gövdede yatay taşma yok
  - (f) 1280px'de desktop düzeni değişmedi: 6 hücre yan yana, mobil katman
    gizli, formlar iki sütun, nav başlıkla aynı satırda

Doğrulama izole ortamda yapıldı (API kopya DB ile 5268'de, geçici parola
hash'i; üretim/geliştirme süreçlerine ve `kasa.db`'ye dokunulmadı).
Not: `appsettings.Development.json`'daki hash, Faz 8 raporunda yazan
`kasa-dev` parolasıyla eşleşmiyor (doküman eskimiş olabilir).

## Kanıt Dosyaları

| Dosya | İçerik |
|---|---|
| `a-login-390.png` | Login, 390px |
| `b-gun-390.png` | Gün sayfası, 390px — özet barı kapalı (Gün Net + Ana Kasa) |
| `c-gun-390-detay-acik.png` | Özet detay paneli açık (2×2 grid) |
| `d-rapor-390.png` | Rapor görünümü, 390px |
| `e-ay-390.png` | Ay tablosu, 390px — kaydırma öncesi |
| `f-ay-390-kaydirilmis.png` | Ay tablosu kaydırılmış — Tarih kolonu sabit |
| `i-gun-390-islem-listesi.png` | İşlem listesi iki katlı satırlar, 390px |
| `g-gun-desktop-1280.png` | Desktop karşılaştırma — gün sayfası (değişmedi) |
| `h-ay-desktop-1280.png` | Desktop karşılaştırma — ay sayfası (değişmedi) |
| `playwright-cikti.txt` | 29 kontrolün tam çıktısı |
