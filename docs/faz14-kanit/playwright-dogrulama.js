/* Faz 14 depozito doğrulaması — izole API (5268, taze DB).
   Desktop: /depozito form → Kaydet ve Yazdır → liste satırı + PDF (window.open) + ucu application/pdf.
   Mobil 390: Depozito sekmesi (5.) → sheet → kaydet → toast + liste + PDF.
   Not: PDF ucu Content-Disposition: attachment (günlük-rapor deseniyle aynı) → window.open indirir;
   akış "popup açıldı" (window.open çağrıldı) + PDF ucunun application/pdf dönmesiyle doğrulanır. */
const { chromium } = require('playwright')
const fs = require('fs')
const path = require('path')

const BASE = 'http://localhost:5268'
const PASSWORD = 'kanit-test'
const OUT = '/Users/burakaldatmaz/Desktop/Projeler/günlük kasa/docs/faz14-kanit'

const pad = n => String(n).padStart(2, '0')
const d = new Date()
const TODAY = `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
const thb = new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' })

let pass = 0, fail = 0
const lines = []
function log(m) { lines.push(m); console.log(m) }
function check(name, cond, extra = '') {
  if (cond) { pass++; log(`  ✓ ${name}`) }
  else { fail++; log(`  ✗ ${name}${extra ? ' — ' + extra : ''}`) }
}
const closeQuiet = p => p.close().catch(() => {})

async function assertPdfEndpoint(reqCtx, id, no, tag) {
  const r = await reqCtx.get(`${BASE}/api/deposit-receipts/${id}/pdf`)
  const ct = r.headers()['content-type'] || ''
  const cd = r.headers()['content-disposition'] || ''
  check(`${tag}: PDF ucu 200 + application/pdf + "${no}.pdf"`,
    r.ok() && ct.includes('pdf') && cd.includes(`${no}.pdf`), `${r.status()} ${ct} ${cd}`)
}

async function main() {
  fs.mkdirSync(OUT, { recursive: true })
  const browser = await chromium.launch()

  /* ---------------- DESKTOP 1280 ---------------- */
  log('— Desktop 1280x900 —')
  const dctx = await browser.newContext({ viewport: { width: 1280, height: 900 } })
  const dpage = await dctx.newPage()
  await dpage.goto(`${BASE}/login`)
  await dpage.fill('input[type=password]', PASSWORD)
  await dpage.click('button[type=submit]')
  await dpage.waitForSelector('.page-header')

  check('Desktop: DayPage\'de "Depozito" nav linki', (await dpage.locator('.rapor-nav-link', { hasText: 'Depozito' }).count()) === 1)
  await dpage.goto(`${BASE}/depozito?date=${TODAY}`)
  await dpage.waitForSelector('.depozito-grid')
  check('Desktop: /depozito açıldı (form + liste)', await dpage.locator('.dep-list-card').isVisible())
  check('Desktop: başlangıçta makbuz yok', (await dpage.locator('.dep-empty').count()) === 1)

  await dpage.getByLabel('Müşteri adı').fill('Edward Penney Beaumont')
  await dpage.getByLabel('Tutar (฿)').fill('3000')
  await dpage.getByLabel('Araç modeli').fill('Honda Click 160')
  await dpage.getByLabel('Renk (opsiyonel)').fill('Matte Black')
  await dpage.getByLabel('Plaka').fill('8 ขผ 7250')
  await dpage.screenshot({ path: `${OUT}/a-desktop-form-1280.png` })

  const [popup] = await Promise.all([
    dpage.waitForEvent('popup'),
    dpage.locator('.btn-primary', { hasText: 'Kaydet ve Yazdır' }).click(),
  ])
  check('Desktop: Kaydet → PDF yeni sekme açıldı (window.open)', !!popup)
  await closeQuiet(popup)

  await dpage.waitForSelector('.dep-row')
  const rowText = (await dpage.locator('.dep-row').first().textContent()).replace(/\s+/g, ' ').trim()
  check('Desktop: makbuz listeye düştü (No + isim + tutar)',
    /DEP-\d{4}-\d{5}/.test(rowText) && rowText.includes('Edward Penney Beaumont') && rowText.includes('฿3,000.00'), rowText)

  const apiList = await (await dctx.request.get(`${BASE}/api/deposit-receipts?date=${TODAY}`)).json()
  check('Desktop: liste API ile birebir (1 makbuz)', apiList.length === 1 && rowText.includes(apiList[0].no), `api no: ${apiList[0] && apiList[0].no}`)
  const firstNo = apiList[0].no
  await assertPdfEndpoint(dctx.request, apiList[0].id, firstNo, 'Desktop')
  await dpage.screenshot({ path: `${OUT}/b-desktop-liste-1280.png` })

  // İkinci makbuz → numara +1
  await dpage.getByLabel('Müşteri adı').fill('Second Customer')
  await dpage.getByLabel('Tutar (฿)').fill('5000')
  await dpage.getByLabel('Araç modeli').fill('Yamaha NMAX')
  await dpage.getByLabel('Plaka').fill('1 กก 1111')
  const [popup2] = await Promise.all([
    dpage.waitForEvent('popup'),
    dpage.locator('.btn-primary', { hasText: 'Kaydet ve Yazdır' }).click(),
  ])
  await closeQuiet(popup2)
  await dpage.waitForFunction(() => document.querySelectorAll('.dep-row').length === 2)
  const nos = (await (await dctx.request.get(`${BASE}/api/deposit-receipts?date=${TODAY}`)).json()).map(r => r.no)
  check('Desktop: ikinci makbuz numarası +1 arttı',
    nos.length === 2 && nos[1].endsWith(String(Number(firstNo.slice(-5)) + 1).padStart(5, '0')), nos.join(', '))

  // Liste satırı PDF butonu (yeniden yazdırma)
  const [popup3] = await Promise.all([
    dpage.waitForEvent('popup'),
    dpage.locator('.dep-row .btn-secondary', { hasText: 'PDF' }).first().click(),
  ])
  check('Desktop: liste satırı PDF butonu makbuzu açtı (window.open)', !!popup3)
  await closeQuiet(popup3)

  const scrollW = await dpage.evaluate(() => document.scrollingElement.scrollWidth)
  check('Desktop: yatay taşma yok', scrollW <= 1280, `scrollWidth=${scrollW}`)
  await dctx.close()

  /* ---------------- MOBİL 390 ---------------- */
  log('— Mobil 390x844 —')
  const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true })
  const page = await ctx.newPage()
  await page.goto(`${BASE}/login`)
  await page.fill('input[type=password]', PASSWORD)
  await page.click('button[type=submit]')
  await page.waitForSelector('.m-tabbar')

  check('Mobil: tab barda 5 sekme', (await page.locator('.m-tab').count()) === 5)
  const depTab = page.locator('.m-tabbar .m-tab', { hasText: 'Depozito' })
  check('Mobil: "Depozito" sekmesi var', (await depTab.count()) === 1)
  await depTab.click()
  await page.waitForSelector('.m-dep-new')
  check('Mobil: Depozito ekranı açıldı', await page.locator('.m-dep-new').isVisible())
  await page.waitForSelector('.m-dep-item')
  check('Mobil: günün makbuzları listede (desktop 2 makbuz)', (await page.locator('.m-dep-item').count()) === 2)
  await page.screenshot({ path: `${OUT}/c-mobil-depozito-390.png` })

  await page.locator('.m-dep-new').click()
  await page.waitForSelector('.m-deposit-sheet')
  check('Mobil: DepositSheet açıldı', await page.locator('.m-deposit-sheet').isVisible())
  await page.getByLabel('Müşteri adı').fill('Mobil Müşteri')
  await page.getByLabel('Araç modeli').fill('Honda PCX')
  await page.getByLabel('Plaka').fill('9 ขง 3997')
  await page.getByLabel('Tutar (฿)').fill('4000')
  await page.locator('.m-seg-btn', { hasText: 'Havale' }).click()
  await page.screenshot({ path: `${OUT}/d-mobil-sheet-390.png` })

  const [mpopup] = await Promise.all([
    page.waitForEvent('popup'),
    page.locator('.m-dep-save', { hasText: 'Kaydet ve Yazdır' }).click(),
  ])
  check('Mobil: Kaydet → PDF yeni sekme açıldı (window.open)', !!mpopup)
  await closeQuiet(mpopup)
  await page.waitForSelector('.m-toast')
  const toast = (await page.locator('.m-toast').textContent()).trim()
  check('Mobil: "Makbuz kesildi" toast\'ı (No + tutar)', toast.includes('Makbuz kesildi') && /DEP-\d{4}-\d{5}/.test(toast) && toast.includes(thb.format(4000)), toast)
  await page.waitForFunction(() => document.querySelectorAll('.m-dep-item').length === 3)
  check('Mobil: yeni makbuz listeye düştü (3 makbuz)', (await page.locator('.m-dep-item').count()) === 3)

  const mScroll = await page.evaluate(() => document.scrollingElement.scrollWidth)
  check('Mobil: yatay taşma yok', mScroll <= 390, `scrollWidth=${mScroll}`)
  await page.locator('.m-toast').waitFor({ state: 'detached' }).catch(() => {})
  await page.screenshot({ path: `${OUT}/e-mobil-liste-390.png` })

  // Mali temassızlık: depozito Gün ekranındaki kasayı etkilemez
  await page.locator('.m-tabbar .m-tab', { hasText: 'Gün' }).click()
  await page.waitForSelector('.m-hero-amount')
  const kasa = (await page.locator('.m-hero-amount').textContent()).trim()
  check('Mali temassızlık: depozito kasayı etkilemedi (Kasada ฿0.00)', kasa === thb.format(0), kasa)

  await ctx.close()
  await browser.close()
  log('')
  log(`SONUÇ: ${pass} PASS / ${fail} FAIL`)
  fs.writeFileSync(path.join(OUT, 'playwright-cikti.txt'), lines.join('\n') + '\n')
  process.exit(fail > 0 ? 1 : 0)
}
main().catch(err => { log(`HATA: ${err.stack}`); fs.writeFileSync(path.join(OUT, 'playwright-cikti.txt'), lines.join('\n') + '\n'); process.exit(2) })
