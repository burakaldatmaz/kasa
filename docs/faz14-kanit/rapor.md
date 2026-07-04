# Faz 14 Kanıt — Frontend: Depozito sekmesi

Desktop `/depozito` sayfası + mobil 5. sekme. Playwright ile izole API'de (5268, taze DB) uçtan uca sürüldü — **20 PASS / 0 FAIL** (`playwright-cikti.txt`, script `playwright-dogrulama.js`).

## Desktop (`a-desktop-form-1280.png`, `b-desktop-liste-1280.png`)

`/depozito` path + DayPage'de "Depozito" nav linki. Sol: yeni makbuz formu (tarih üst seçiciden, method=Nakit, yakıt=Full, limitler 150/150 düzenlenebilir, **beklenen iade = teslim + 30 gün** önerisi). Sağ: günün makbuz listesi (No · isim · plaka · method · tutar) + satır başına PDF butonu. **Kaydet ve Yazdır → PDF yeni sekmede açılır** (günlük-rapor PDF deseniyle aynı `window.open`). İkinci makbuzun numarası +1 arttı; PDF ucu `application/pdf` + `DEP-2026-00001.pdf` döndü.

## Mobil 390 (`c-mobil-depozito-390.png`, `d-mobil-sheet-390.png`, `e-mobil-liste-390.png`)

Tab bar **5 sekme**: Gün · Ay · [FAB] · Rapor · Filo · **Depozito** (kalkan ikonu). Depozito ekranı: "Yeni Depozito Makbuzu" butonu + günün makbuz listesi (indir ikonu → PDF). Buton `DepositSheet`'i açar (EntrySheet deseni); Kaydet → PDF yeni sekmede + `"Makbuz kesildi · DEP-… · ฿4,000.00"` toast'ı + liste güncellenir.

## Regresyon / temassızlık

- Desktop ve mobil: yatay taşma yok.
- **Mali temassızlık:** depozito makbuzu kesmek Gün ekranındaki kasayı etkilemedi (Kasada ฿0.00).
- Mobil listedeki makbuzlar API ile birebir; toast/tutar server yanıtından biçimlenir (I1).
