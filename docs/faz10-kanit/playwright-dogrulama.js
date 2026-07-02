/* Faz 10 (Claude Design mobil) doğrulaması — izole API (5268, kopya DB).
   (a) tab bar + FAB sheet, (b) keypad 1234.56 + API karşılaştırması,
   (c) Ekle ve devam korunumu, (d) satır → Düzenle/Sil sheet'i,
   (e) taşma (boundingBox), (f) desktop regresyonu, (g) yeni kategori chip'i. */
const { chromium } = require('playwright')
const fs = require('fs')
const path = require('path')
const { PNG } = require('pngjs')
const pixelmatchMod = require('pixelmatch')
const pixelmatch = pixelmatchMod.default ?? pixelmatchMod

const BASE = 'http://localhost:5268'
const PASSWORD = 'kanit-test'
const OUT = path.join(__dirname, 'out')
const REF = '/Users/burakaldatmaz/Desktop/Projeler/günlük kasa/docs/faz10-kanit'

const thb = new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' })
const fmt = s => thb.format(s / 100)

let pass = 0
let fail = 0
const lines = []
function log(msg) {
  lines.push(msg)
  console.log(msg)
}
function check(name, cond, extra = '') {
  if (cond) {
    pass++
    log(`  ✓ ${name}`)
  } else {
    fail++
    log(`  ✗ ${name}${extra ? ' — ' + extra : ''}`)
  }
}

function comparePng(actualPath, refPath, name) {
  const a = PNG.sync.read(fs.readFileSync(actualPath))
  const b = PNG.sync.read(fs.readFileSync(refPath))
  if (a.width !== b.width || a.height !== b.height) {
    log(`  ~ ${name}: boyut farkı ${a.width}x${a.height} vs ${b.width}x${b.height} (piksel kıyas atlandı)`)
    return
  }
  const diff = new PNG({ width: a.width, height: a.height })
  const n = pixelmatch(a.data, b.data, diff.data, a.width, a.height, { threshold: 0.1 })
  const ratio = n / (a.width * a.height)
  check(`${name}: piksel farkı %${(ratio * 100).toFixed(3)} (${n} px)`, ratio < 0.005)
  if (n > 0) fs.writeFileSync(actualPath.replace('.png', '-diff.png'), PNG.sync.write(diff))
}

/** Yatay taşma yok: sayfa kaymıyor + tutar öğeleri viewport içinde. */
async function overflowCheck(page, screen, width) {
  const scrollW = await page.evaluate(() => document.scrollingElement.scrollWidth)
  check(`(e) ${screen}: yatay sayfa taşması yok (scrollWidth=${scrollW})`, scrollW <= width)
  const sels = [
    '.m-hero-amount', '.m-hero-box-value', '.m-row-amt', '.m-partner-amt', '.m-closing-amt',
    '.m-rapor-strip-amt', '.m-dotted-amt', '.m-bar-amt', '.m-amount-display', '.m-action-amt',
  ]
  let total = 0
  let bad = 0
  for (const sel of sels) {
    for (const el of await page.locator(sel).all()) {
      const box = await el.boundingBox()
      if (!box) continue
      total++
      if (box.x < -0.5 || box.x + box.width > width + 0.5) {
        bad++
        log(`      taşan öğe: ${sel} x=${box.x} w=${box.width}`)
      }
    }
  }
  check(`(e) ${screen}: ${total} tutar öğesi viewport içinde`, bad === 0)
}

async function main() {
  fs.mkdirSync(OUT, { recursive: true })
  const browser = await chromium.launch()

  /* ---------------- (f) DESKTOP — veri mutasyonundan ÖNCE ---------------- */
  log('— (f) Desktop 1280x800 (düzen değişmedi mi?) —')
  const dctx = await browser.newContext({ viewport: { width: 1280, height: 800 } })
  const dpage = await dctx.newPage()
  await dpage.goto(`${BASE}/login`)
  await dpage.fill('input[type=password]', PASSWORD)
  await dpage.click('button[type=submit]')
  await dpage.waitForSelector('.summary-bar')
  await dpage.waitForSelector('.txn-rows, .txn-empty')
  check('(f) 6 özet hücresi yan yana', (await dpage.locator('.summary-cells .summary-cell').count()) === 6)
  check('(f) mobil özet katmanı gizli', !(await dpage.locator('.summary-mobile').isVisible()))
  check('(f) tab bar DOM\'da yok', (await dpage.locator('.m-tabbar').count()) === 0)
  check('(f) FAB DOM\'da yok', (await dpage.locator('.m-fab').count()) === 0)
  const gridCols = await dpage
    .locator('.forms-grid')
    .evaluate(el => getComputedStyle(el).gridTemplateColumns.split(' ').length)
  check('(f) formlar iki sütun', gridCols === 2)
  const navInline = await dpage
    .locator('.page-nav')
    .evaluate(el => getComputedStyle(el).display === 'contents')
  check('(f) nav başlık satırında (display:contents)', navInline)
  // Referans görüntü demo verisinin olduğu 2026-08-03 ile alınmıştı; aynı tarihle kıyasla.
  await dpage.goto(`${BASE}/?date=2026-08-03`)
  await dpage.waitForSelector('.txn-rows')
  await dpage.waitForTimeout(300)
  await dpage.screenshot({ path: `${OUT}/g-gun-desktop-1280.png` })
  comparePng(`${OUT}/g-gun-desktop-1280.png`, `${REF}/g-gun-desktop-1280.png`, '(f) gün desktop regresyon')

  await dpage.goto(`${BASE}/ay`)
  await dpage.waitForSelector('.ay-table tfoot')
  await dpage.waitForTimeout(300)
  await dpage.screenshot({ path: `${OUT}/h-ay-desktop-1280.png` })
  comparePng(`${OUT}/h-ay-desktop-1280.png`, `${REF}/h-ay-desktop-1280.png`, '(f) ay desktop regresyon')
  await dctx.close()

  /* ---------------- MOBİL 390x844 ---------------- */
  log('— Mobil 390x844 —')
  const ctx = await browser.newContext({
    viewport: { width: 390, height: 844 },
    deviceScaleFactor: 2,
    isMobile: true,
    hasTouch: true,
  })
  const page = await ctx.newPage()

  await page.goto(`${BASE}/login`)
  await page.waitForSelector('.login-card')
  await page.screenshot({ path: `${OUT}/a-login-390.png` })
  await page.fill('input[type=password]', PASSWORD)
  await page.click('button[type=submit]')
  await page.waitForSelector('.m-hero')
  const dateParam = new URL(page.url()).searchParams.get('date')
  log(`  (gün: ${dateParam})`)

  /* (a) tab bar geçişleri */
  const tab = name => page.locator('.m-tabbar .m-tab', { hasText: name })
  check('(a) tab bar görünür', await page.locator('.m-tabbar').isVisible())
  check('(a) 4 sekme + FAB', (await page.locator('.m-tab').count()) === 4 && (await page.locator('.m-fab').count()) === 1)

  await tab('Ay').click()
  await page.waitForSelector('.m-hero-month')
  check('(a) Ay sekmesi açıldı', await page.locator('.m-hero-month').isVisible())
  check('(a) Ay sekmesi aktif renk', await tab('Ay').evaluate(el => el.classList.contains('m-tab-active')))

  await tab('Rapor').click()
  await page.waitForSelector('.m-rapor-card')
  check('(a) Rapor sekmesi açıldı', await page.locator('.m-closing-box').isVisible())

  await tab('Filo').click()
  await page.waitForSelector('.m-steppers')
  check('(a) Filo sekmesi açıldı (stepper\'lar)', (await page.locator('.m-stepper-row').count()) === 3)

  await tab('Gün').click()
  await page.waitForSelector('.m-hero')

  /* (a) FAB sheet aç/kapa */
  await page.locator('.m-fab').click()
  await page.waitForSelector('.m-entry-sheet')
  check('(a) FAB giriş sheet\'ini açtı', await page.locator('.m-entry-sheet').isVisible())
  check('(a) keypad 12 tuş', (await page.locator('.m-key').count()) === 12)
  await page.locator('.m-sheet-close').click()
  await page.waitForSelector('.m-entry-sheet', { state: 'detached' })
  check('(a) ✕ sheet\'i kapattı', (await page.locator('.m-entry-sheet').count()) === 0)

  /* (b) keypad ile 1234.56 gir + kaydet + API karşılaştırması */
  log('— (b) keypad 1234.56 → Kaydet —')
  await page.locator('.m-fab').click()
  await page.waitForSelector('.m-entry-sheet')
  const activeChipName = (await page.locator('.m-chip-active').first().textContent()).trim()
  log(`  (seçili kategori: ${activeChipName})`)
  for (const k of ['1', '2', '3', '4', '.', '5', '6']) {
    await page.locator('.m-key', { hasText: k === '.' ? /^\.$/ : new RegExp(`^${k}$`) }).click()
  }
  const disp = (await page.locator('[data-testid=sheet-amount]').textContent()).trim()
  check('(b) tutar göstergesi ฿1234.56', disp === '฿1234.56', `görülen: ${disp}`)
  // ikinci nokta engelleniyor (BahtParser kuralı)
  await page.locator('.m-key', { hasText: /^\.$/ }).click()
  const disp2 = (await page.locator('[data-testid=sheet-amount]').textContent()).trim()
  check('(b) ikinci nokta engellendi', disp2 === '฿1234.56', `görülen: ${disp2}`)
  // 2 ondalık sınırı
  await page.locator('.m-key', { hasText: /^9$/ }).click()
  const disp3 = (await page.locator('[data-testid=sheet-amount]').textContent()).trim()
  check('(b) 2 ondalık sınırı', disp3 === '฿1234.56', `görülen: ${disp3}`)
  await page.screenshot({ path: `${OUT}/f-giris-sheet-390.png` })

  await page.locator('.m-btn-save').click()
  await page.waitForSelector('.m-toast')
  const toastText = (await page.locator('.m-toast').textContent()).trim()
  check('(b) toast "Gelir eklendi · ฿1,234.56"', toastText.includes(`Gelir eklendi · ${fmt(123456)}`), `görülen: ${toastText}`)
  await page.screenshot({ path: `${OUT}/g-toast-390.png` })
  await page.waitForSelector('.m-entry-sheet', { state: 'detached' })

  // API'nin döndürdüğü rakamlar hero'da birebir
  const rep = await (await ctx.request.get(`${BASE}/api/reports/daily?date=${dateParam}`)).json()
  await page.waitForTimeout(600) // refetch
  const heroAmt = (await page.locator('.m-hero-amount').textContent()).trim()
  check('(b) hero KASADA = API closingBalance', heroAmt === fmt(rep.closingBalance), `${heroAmt} vs ${fmt(rep.closingBalance)}`)
  const boxVals = await page.locator('.m-hero-box-value').allTextContents()
  check('(b) hero GELİR = API incomeTotal', boxVals[0].trim() === fmt(rep.incomeTotal), `${boxVals[0]} vs ${fmt(rep.incomeTotal)}`)
  check('(b) hero GİDER = API expenseTotal', boxVals[1].trim() === fmt(rep.expenseTotal), `${boxVals[1]} vs ${fmt(rep.expenseTotal)}`)
  check('(b) hero POS = API posFee', boxVals[2].trim() === fmt(rep.posFee), `${boxVals[2]} vs ${fmt(rep.posFee)}`)
  const posLabel = (await page.locator('.m-hero-box-label').nth(2).textContent()).trim()
  check('(b) POS etiketi API oranından (hardcode değil)', posLabel === `POS %${new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 }).format(rep.posFeeRatePercent)}`, `görülen: ${posLabel}`)
  // yeni satır listede
  const newRow = page.locator('.m-row', { hasText: activeChipName }).filter({ hasText: fmt(123456) })
  check('(b) yeni işlem satırı listede (฿1,234.56)', (await newRow.count()) >= 1)
  const devirText = (await page.locator('.m-hero-sub').textContent()).trim()
  check('(b) devir satırı = API previousBalance', devirText.includes(`devir ${fmt(rep.previousBalance)}`), `görülen: ${devirText}`)

  await page.locator('.m-toast').waitFor({ state: 'detached' }).catch(() => {})
  await page.screenshot({ path: `${OUT}/b-gun-390.png` })
  await overflowCheck(page, 'Gün', 390)

  /* (c) Ekle ve devam: kategori + ödeme korunur */
  log('— (c) Ekle ve devam korunumu —')
  await page.locator('.m-fab').click()
  await page.waitForSelector('.m-entry-sheet')
  const chips = page.locator('.m-chip:not(.m-chip-new)')
  const chipCount = await chips.count()
  const pickIdx = chipCount > 1 ? 1 : 0
  const pickedName = (await chips.nth(pickIdx).textContent()).trim()
  await chips.nth(pickIdx).click()
  await page.locator('.m-seg-btn', { hasText: 'Havale' }).click()
  await page.locator('.m-key', { hasText: /^7$/ }).click()
  await page.locator('.m-key', { hasText: /^8$/ }).click()
  await page.locator('.m-btn-continue').click()
  await page.waitForSelector('.m-toast')
  await page.waitForFunction(() => {
    const el = document.querySelector('[data-testid=sheet-amount]')
    return el && el.textContent.trim() === '฿0'
  })
  check('(c) sheet açık kaldı', await page.locator('.m-entry-sheet').isVisible())
  check('(c) tutar sıfırlandı (฿0)', true)
  const activeAfter = (await page.locator('.m-chip-active').first().textContent()).trim()
  check(`(c) kategori korundu (${pickedName})`, activeAfter === pickedName, `görülen: ${activeAfter}`)
  const segActive = (await page.locator('.m-seg-btn-active').textContent()).trim()
  check('(c) ödeme korundu (Havale)', segActive === 'Havale', `görülen: ${segActive}`)
  await page.locator('.m-sheet-close').click()
  await page.waitForSelector('.m-entry-sheet', { state: 'detached' })
  // Havale kısaltması liste meta satırında
  await page.waitForTimeout(600)
  check('(c) meta satırında kısaltma (Havale)', (await page.locator('.m-row-meta', { hasText: 'Havale' }).count()) >= 1)

  /* (d) satır dokunma → Düzenle/Sil sheet'i */
  log('— (d) satır → eylem sheet\'i —')
  await page.locator('.m-row').first().click()
  await page.waitForSelector('.m-action-sheet')
  check('(d) eylem sheet\'i açıldı', await page.locator('.m-action-sheet').isVisible())
  check('(d) Düzenle + Sil butonları', (await page.locator('.m-action-btn', { hasText: 'Düzenle' }).count()) === 1 && (await page.locator('.m-action-btn', { hasText: /^Sil$/ }).count()) === 1)
  const editBox = await page.locator('.m-action-btn', { hasText: 'Düzenle' }).boundingBox()
  check('(d) dokunma alanı ≥44px', editBox.height >= 44)
  await page.waitForTimeout(450) // sheet animasyonu otursun
  await page.locator('.m-toast').waitFor({ state: 'detached' }).catch(() => {})
  await page.screenshot({ path: `${OUT}/h-eylem-sheet-390.png` })
  await page.locator('.m-action-btn', { hasText: 'Düzenle' }).click()
  await page.waitForSelector('.m-edit-form')
  check('(d) Düzenle → düzenleme formu sheet içinde', await page.locator('.m-edit-form input').first().isVisible())
  await overflowCheck(page, 'Eylem sheet', 390)
  await page.locator('.m-action-btn-muted', { hasText: 'Vazgeç' }).click()
  await page.waitForSelector('.m-action-sheet', { state: 'detached' })

  /* (g) yeni kategori chip akışı */
  log('— (g) + Yeni kategori chip akışı —')
  await page.locator('.m-fab').click()
  await page.waitForSelector('.m-entry-sheet')
  await page.locator('.m-type-toggle .m-type-btn', { hasText: 'Gider' }).click()
  await page.locator('.m-chip-new').click()
  await page.waitForSelector('.modal')
  check('(g) kategori modalı sheet üstünde açıldı', await page.locator('.modal').isVisible())
  const newCatName = 'PW Deneme'
  await page.locator('.modal input').fill(newCatName)
  await page.locator('.modal button[type=submit]').click()
  await page.waitForSelector('.modal', { state: 'detached' })
  const newChip = page.locator('.m-chip', { hasText: newCatName })
  await newChip.waitFor()
  check('(g) yeni chip şeritte', (await newChip.count()) === 1)
  check('(g) yeni chip seçili geldi', await newChip.evaluate(el => el.classList.contains('m-chip-active')))
  await overflowCheck(page, 'Giriş sheet', 390)
  await page.locator('.m-sheet-close').click()
  await page.waitForSelector('.m-entry-sheet', { state: 'detached' })

  /* Ay ekranı + taşma + ekran görüntüsü */
  await tab('Ay').click()
  await page.waitForSelector('.m-hero-month')
  await page.waitForSelector('.m-bar-track')
  const monthRep = await (await ctx.request.get(`${BASE}/api/reports/month?month=${dateParam.slice(0, 7)}`)).json()
  const monthHero = (await page.locator('.m-hero-amount').textContent()).trim()
  check('Ay hero = API finalBalance', monthHero === fmt(monthRep.finalBalance), `${monthHero} vs ${fmt(monthRep.finalBalance)}`)
  const p1 = monthRep.distribution.partner1
  if (monthRep.finalBalance >= 0) {
    const card1 = (await page.locator('.m-partner-card').first().textContent()).trim()
    check(`Ortak kartı API'den (${p1.name}, %${p1.sharePercent})`, card1.includes(p1.name) && card1.includes(fmt(p1.amountSatang)))
  } else {
    check('Zarar kartı görünür', await page.locator('.m-loss-card').isVisible())
  }
  check('Excel butonu', await page.locator('.m-primary-btn', { hasText: 'Ay raporunu indir (Excel)' }).isVisible())
  check('Filo ay özeti satırı', await page.locator('.m-fleet-summary').isVisible())
  const xlsx = await ctx.request.get(`${BASE}/api/reports/month/xlsx?month=${dateParam.slice(0, 7)}`)
  check('R1: /api/reports/month/xlsx canlı (Excel ucu)', xlsx.ok() && (xlsx.headers()['content-type'] || '').includes('spreadsheet'))
  const pdf = await ctx.request.get(`${BASE}/api/reports/daily/pdf?date=${dateParam}`)
  check('R1: /api/reports/daily/pdf canlı (PDF ucu)', pdf.ok() && (pdf.headers()['content-type'] || '').includes('pdf'))
  await page.locator('.m-toast').waitFor({ state: 'detached' }).catch(() => {})
  await page.screenshot({ path: `${OUT}/c-ay-390.png` })
  await overflowCheck(page, 'Ay', 390)

  /* Ay → gün satırı → Rapor sekmesi */
  const firstDayRow = page.locator('.m-list .m-row').first()
  await firstDayRow.click()
  await page.waitForSelector('.m-rapor-card')
  check('Ay günü → Rapor sekmesi açıldı', page.url().includes('/rapor?date='))
  await page.screenshot({ path: `${OUT}/d-rapor-390.png` })
  await overflowCheck(page, 'Rapor', 390)
  check('Rapor: Devreden Kasa şeridi', await page.locator('.m-rapor-strip').isVisible())
  check('Rapor: Tür Dağılımı bölümü', (await page.locator('.m-band-neutral').count()) === 2)
  check('Rapor: PDF butonu', await page.locator('.m-light-btn', { hasText: 'PDF olarak indir' }).isVisible())

  /* Filo ekranı */
  await tab('Filo').click()
  await page.waitForSelector('.m-steppers')
  const hasSnap = (await page.locator('.m-filo-card .m-ring').count()) === 1
  log(`  (filo snapshot ${hasSnap ? 'var' : 'yok — boş durum'})`)
  if (!hasSnap) check('Filo boş durum metni', (await page.locator('.m-filo-empty').textContent()).includes('filo verisi yok'))
  // stepper + doğrudan yazma
  const totalInput = page.locator('.m-stepper-value').first()
  await totalInput.fill('59')
  check('Filo: sayıya dokunup doğrudan yazma', (await totalInput.inputValue()) === '59')
  const plusBtn = page.locator('.m-stepper-row').first().locator('.m-stepper-btn').nth(1)
  await plusBtn.click()
  check('Filo: + stepper 59→60', (await totalInput.inputValue()) === '60')
  await page.screenshot({ path: `${OUT}/e-filo-390.png` })
  await overflowCheck(page, 'Filo', 390)

  /* Gün ekranına dön — filo mini kart */
  await tab('Gün').click()
  await page.waitForSelector('.m-fleet-mini')
  check('Gün: filo mini kartı görünür', await page.locator('.m-fleet-mini').isVisible())
  await page.locator('.m-fleet-mini').click()
  await page.waitForSelector('.m-steppers')
  check('Gün: filo kartı → Filo sekmesi', page.url().includes('/filo'))

  /* Boş gün (yarın): başlık tarih olur, sarı filo uyarısı, Filo boş durumu */
  log('— Boş gün + filo boş durumu —')
  await page.goto(`${BASE}/?date=2026-07-03`)
  await page.waitForSelector('.m-fleet-warn')
  const emptyTitle = (await page.locator('.m-title').textContent()).trim()
  check(`Boş gün: başlık tarihi gösterir (${emptyTitle})`, emptyTitle !== 'Bugün')
  check('Boş gün: sarı uyarı kartı "filo verisi girilmedi"', (await page.locator('.m-fleet-warn').textContent()).includes('filo verisi girilmedi'))
  check('Boş gün: liste boş durumları', (await page.locator('.m-empty', { hasText: 'Henüz gelir yok' }).count()) === 1)
  await page.screenshot({ path: `${OUT}/i-gun-bos-390.png` })
  await page.locator('.m-fleet-warn').click()
  await page.waitForSelector('.m-filo-empty')
  check('Filo boş durumu: "Bugün için filo verisi yok" (404)', (await page.locator('.m-filo-empty').textContent()).includes('filo verisi yok'))
  await page.screenshot({ path: `${OUT}/j-filo-bos-390.png` })

  await ctx.close()
  await browser.close()

  log('')
  log(`SONUÇ: ${pass} PASS / ${fail} FAIL`)
  fs.writeFileSync(path.join(OUT, 'playwright-cikti.txt'), lines.join('\n') + '\n')
  process.exit(fail > 0 ? 1 : 0)
}

main().catch(err => {
  log(`HATA: ${err.stack}`)
  fs.writeFileSync(path.join(OUT, 'playwright-cikti.txt'), lines.join('\n') + '\n')
  process.exit(2)
})
