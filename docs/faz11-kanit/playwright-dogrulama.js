/* Faz 11 (rezervasyon sayaçları) doğrulaması — izole API (5268, kopya DB).
   (a) mobil Filo rezervasyon kartı: null → boş "—" gösterge, giriş + kaydet akışı,
   (b) dokunmadan kaydet → null gider (K2), (c) gün mini kart meta satırı,
   (d) ay Günler listesi trend göstergesi (değerli günde var, null günde TAMAMEN gizli),
   (e) mobil rapor filo bölümü, (f) taşma denetimi, (g) desktop FleetCard alanları + rozet,
   (h) desktop /ay yeni sütunlar + filo özeti, (i) desktop /rapor filo satırı,
   (j) API çapraz denetimleri (rentalPercent K1, ay toplamları I1). */
const { chromium } = require('playwright')
const fs = require('fs')
const path = require('path')

const BASE = 'http://localhost:5268'
const PASSWORD = 'kanit-test'
const OUT = '/Users/burakaldatmaz/Desktop/Projeler/günlük kasa/docs/faz11-kanit'

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

/** Yatay taşma yok: sayfa kaymıyor. */
async function overflowCheck(page, screen, width) {
  const scrollW = await page.evaluate(() => document.scrollingElement.scrollWidth)
  check(`(f) ${screen}: yatay sayfa taşması yok (scrollWidth=${scrollW})`, scrollW <= width)
}

async function main() {
  fs.mkdirSync(OUT, { recursive: true })
  const browser = await chromium.launch()

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
  await page.fill('input[type=password]', PASSWORD)
  await page.click('button[type=submit]')
  await page.waitForSelector('.m-hero')
  const today = new URL(page.url()).searchParams.get('date')
  log(`  (bugün: ${today})`)

  /* (a) Filo sekmesi: rezervasyon kartı, null → boş "—" gösterge */
  log('— (a) Filo rezervasyon kartı (bugün, null durumda) —')
  await page.locator('.m-tabbar .m-tab', { hasText: 'Filo' }).click()
  await page.waitForSelector('.m-steppers')
  check('(a) "Bugünün Rezervasyonları" bölüm başlığı', await page.locator('.m-section-label', { hasText: 'Bugünün Rezervasyonları' }).isVisible())
  check('(a) toplam 5 stepper satırı (3 filo + 2 rezervasyon)', (await page.locator('.m-stepper-row').count()) === 5)
  const startedInput = page.locator('input[aria-label="Başlayan"]')
  const endedInput = page.locator('input[aria-label="Biten"]')
  check('(a) Başlayan null → giriş BOŞ (0 değil)', (await startedInput.inputValue()) === '')
  check('(a) Başlayan boş göstergesi "—" (placeholder)', (await startedInput.getAttribute('placeholder')) === '—')
  check('(a) Biten null → giriş BOŞ + "—"', (await endedInput.inputValue()) === '' && (await endedInput.getAttribute('placeholder')) === '—')
  await page.screenshot({ path: `${OUT}/a-mobil-filo-rezervasyon-bos-390.png` })
  await overflowCheck(page, 'Filo (boş sayaç)', 390)

  /* (a) giriş + kaydet akışı: doğrudan yazma + ± stepper */
  log('— (a) rezervasyon giriş/kaydet akışı —')
  await startedInput.fill('6')
  const startedPlus = page.locator('.m-stepper-row', { hasText: 'Başlayan' }).locator('.m-stepper-btn').nth(1)
  await startedPlus.click()
  check('(a) Başlayan: doğrudan yazma 6 + stepper → 7', (await startedInput.inputValue()) === '7')
  await endedInput.fill('5')
  await page.screenshot({ path: `${OUT}/b-mobil-filo-rezervasyon-dolu-390.png` })
  await page.locator('.m-primary-btn', { hasText: 'Filo durumunu kaydet' }).click()
  await page.waitForSelector('.m-toast')
  check('(a) kaydet → toast', (await page.locator('.m-toast').textContent()).includes('Filo durumu kaydedildi'))
  const savedToday = await (await ctx.request.get(`${BASE}/api/fleet/${today}`)).json()
  check('(a) API: startedReservations=7', savedToday.startedReservations === 7)
  check('(a) API: endedReservations=5', savedToday.endedReservations === 5)
  check('(j) K1: rentalPercent sayaçlardan ETKİLENMEDİ (33/59 → %55.9)', savedToday.rentalPercent === 55.9, `görülen: ${savedToday.rentalPercent}`)

  /* (b) null günde dokunmadan kaydet → null gider (K2) */
  log('— (b) 2026-07-01: dokunmadan kaydet → null —')
  await page.goto(`${BASE}/filo?date=2026-07-01`)
  await page.waitForSelector('.m-steppers')
  check('(b) 07-01 sayaç girişleri boş (null)', (await startedInput.inputValue()) === '' && (await endedInput.inputValue()) === '')
  await page.locator('.m-toast').waitFor({ state: 'detached' }).catch(() => {})
  await page.locator('.m-primary-btn', { hasText: 'Filo durumunu kaydet' }).click()
  await page.waitForSelector('.m-toast')
  const savedNull = await (await ctx.request.get(`${BASE}/api/fleet/2026-07-01`)).json()
  check('(b) sayaçlara dokunulmadan kaydedildi → API null (0 DEĞİL)', savedNull.startedReservations === null && savedNull.endedReservations === null)

  /* (c) Gün ekranı mini kart meta satırı */
  log('— (c) gün mini kart meta —')
  await page.goto(`${BASE}/?date=${today}`)
  await page.waitForSelector('.m-fleet-mini')
  const metaToday = (await page.locator('.m-fleet-mini-meta').textContent()).trim()
  check('(c) bugün meta: "… · 7 başladı / 5 bitti"', metaToday.includes('7 başladı / 5 bitti'), `görülen: ${metaToday}`)
  await page.screenshot({ path: `${OUT}/c-mobil-gun-mini-kart-390.png` })
  await overflowCheck(page, 'Gün', 390)

  await page.goto(`${BASE}/?date=2026-07-01`)
  await page.waitForSelector('.m-fleet-mini')
  const metaNull = (await page.locator('.m-fleet-mini-meta').textContent()).trim()
  check('(c) null günde meta kısmı TAMAMEN gizli', !metaNull.includes('başladı') && !metaNull.includes('—'), `görülen: ${metaNull}`)

  /* (d) Ay ekranı: trend göstergesi + filo özeti */
  log('— (d) ay Günler listesi trend göstergesi —')
  await page.locator('.m-tabbar .m-tab', { hasText: 'Ay' }).click()
  await page.waitForSelector('.m-hero-month')
  await page.waitForSelector('.m-row')
  const rowDay2 = page.locator('.m-row').filter({ has: page.locator('.m-day-badge-num', { hasText: /^2$/ }) })
  const rowDay1 = page.locator('.m-row').filter({ has: page.locator('.m-day-badge-num', { hasText: /^1$/ }) })
  check('(d) 2 Temmuz: trend satırı görünür', (await rowDay2.locator('.m-trend').count()) === 1)
  const trendText = (await rowDay2.locator('.m-trend').textContent()).trim()
  check('(d) trend "↑7 ↓5"', trendText.includes('↑7') && trendText.includes('↓5'), `görülen: ${trendText}`)
  const upColor = await rowDay2.locator('.m-trend-up').evaluate(el => getComputedStyle(el).color)
  const downColor = await rowDay2.locator('.m-trend-down').evaluate(el => getComputedStyle(el).color)
  check('(d) ↑ yeşil / ↓ kırmızı', upColor === 'rgb(31, 158, 90)' && downColor === 'rgb(210, 54, 59)', `${upColor} / ${downColor}`)
  check('(d) 1 Temmuz (null): trend TAMAMEN gizli (— bile yok)', (await rowDay1.locator('.m-trend').count()) === 0)
  const row1Text = (await rowDay1.textContent()).trim()
  check('(d) 1 Temmuz satırı sade (↑/↓ içermiyor)', !row1Text.includes('↑') && !row1Text.includes('↓'))
  const summaryText = (await page.locator('.m-fleet-summary').textContent()).trim()
  check('(d) filo özeti: "Başlayan 7 · Biten 5"', summaryText.includes('Başlayan 7') && summaryText.includes('Biten 5'), `görülen: ${summaryText}`)
  await page.locator('.m-toast').waitFor({ state: 'detached' }).catch(() => {})
  await page.screenshot({ path: `${OUT}/d-mobil-ay-trend-390.png` })
  await overflowCheck(page, 'Ay', 390)

  /* (e) mobil Rapor filo bölümü */
  log('— (e) mobil rapor filo bölümü —')
  await page.goto(`${BASE}/rapor?date=${today}`)
  await page.waitForSelector('.m-rapor-card')
  const raporFilo = (await page.locator('.m-fleet-summary').textContent()).trim()
  check('(e) rapor filo: Başlayan 7 · Biten 5', raporFilo.includes('Başlayan 7') && raporFilo.includes('Biten 5'), `görülen: ${raporFilo}`)
  await page.screenshot({ path: `${OUT}/e-mobil-rapor-filo-390.png` })
  await overflowCheck(page, 'Rapor', 390)

  await page.goto(`${BASE}/rapor?date=2026-07-01`)
  await page.waitForSelector('.m-fleet-summary')
  const raporFiloNull = (await page.locator('.m-fleet-summary').textContent()).trim()
  check('(e) null günde rapor filo: "Başlayan — · Biten —"', raporFiloNull.includes('Başlayan —') && raporFiloNull.includes('Biten —'), `görülen: ${raporFiloNull}`)

  /* (j) API: ay özeti toplamları server'dan (I1) */
  const fleetMonth = await (await ctx.request.get(`${BASE}/api/fleet/month?month=2026-07`)).json()
  check('(j) /api/fleet/month: totalStarted=7 (null gün katılmadı)', fleetMonth.summary.totalStarted === 7)
  check('(j) /api/fleet/month: totalEnded=5', fleetMonth.summary.totalEnded === 5)
  const daily = await (await ctx.request.get(`${BASE}/api/reports/daily?date=${today}`)).json()
  check('(j) /api/reports/daily fleet: started/ended alanları', daily.fleet.startedReservations === 7 && daily.fleet.endedReservations === 5)
  await ctx.close()

  /* ---------------- DESKTOP 1280x800 ---------------- */
  log('— Desktop 1280x800 —')
  const dctx = await browser.newContext({ viewport: { width: 1280, height: 800 } })
  const dpage = await dctx.newPage()
  await dpage.goto(`${BASE}/login`)
  await dpage.fill('input[type=password]', PASSWORD)
  await dpage.click('button[type=submit]')
  await dpage.waitForSelector('.summary-bar')

  /* (g) FleetCard alanları + rozet */
  log('— (g) FleetCard —')
  await dpage.goto(`${BASE}/?date=${today}`)
  await dpage.waitForSelector('.fleet-card')
  const dStarted = dpage.locator('.fleet-card label', { hasText: 'Başlayan' }).locator('input')
  const dEnded = dpage.locator('.fleet-card label', { hasText: 'Biten' }).locator('input')
  check('(g) Başlayan alanı kayıtlı değerle dolu (7)', (await dStarted.inputValue()) === '7')
  check('(g) Biten alanı kayıtlı değerle dolu (5)', (await dEnded.inputValue()) === '5')
  const badge = dpage.locator('.fleet-badges .badge', { hasText: 'Başlayan' })
  check('(g) rozet "Başlayan 7 · Biten 5"', (await badge.textContent()).trim() === 'Başlayan 7 · Biten 5')
  await dpage.locator('.fleet-card').scrollIntoViewIfNeeded()
  await dpage.screenshot({ path: `${OUT}/f-desktop-fleetcard-1280.png` })

  await dpage.goto(`${BASE}/?date=2026-07-01`)
  await dpage.waitForSelector('.fleet-card')
  check('(g) null günde alanlar boş + "—" placeholder', (await dStarted.inputValue()) === '' && (await dStarted.getAttribute('placeholder')) === '—')
  check('(g) null günde rezervasyon rozeti gizli', (await dpage.locator('.fleet-badges .badge', { hasText: 'Başlayan' }).count()) === 0)

  /* (g) desktop kaydet akışı: 07-01'e 3/1 yaz → rozet; sonra eski gövdeyle null'a döndür */
  await dStarted.fill('3')
  await dEnded.fill('1')
  await dpage.locator('.fleet-card button[type=submit]').click()
  await dpage.waitForSelector('.fleet-badges .badge:has-text("Başlayan 3 · Biten 1")')
  check('(g) desktop kaydet → rozet "Başlayan 3 · Biten 1"', true)
  const dSaved = await (await dctx.request.get(`${BASE}/api/fleet/2026-07-01`)).json()
  check('(g) API round-trip 3/1', dSaved.startedReservations === 3 && dSaved.endedReservations === 1)
  // eski gövdeyle null'a geri döndür (geriye dönük uyumun bir kez daha kanıtı)
  const resetResp = await dctx.request.put(`${BASE}/api/fleet/2026-07-01`, {
    data: { totalBikes: 59, brokenBikes: 0, rentedBikes: 36 },
  })
  const resetJson = await resetResp.json()
  check('(g) eski gövdeyle PUT → sayaçlar tekrar null', resetResp.ok() && resetJson.startedReservations === null && resetJson.endedReservations === null)

  /* (h) /ay: yeni sütunlar + filo özeti */
  log('— (h) /ay sütunları —')
  await dpage.goto(`${BASE}/ay?month=2026-07`)
  await dpage.waitForSelector('.ay-table tfoot')
  const headers = await dpage.locator('.ay-table thead th').allTextContents()
  check('(h) başlıklar: … Kiralama % | Başlayan | Biten', headers.join('|').includes('Kiralama %|Başlayan|Biten'), headers.join('|'))
  const row02 = dpage.locator('.ay-table tbody tr', { hasText: '02.07.2026' })
  const cells02 = await row02.locator('td').allTextContents()
  check('(h) 02.07 satırı: Başlayan 7, Biten 5', cells02[7] === '7' && cells02[8] === '5', cells02.join(' | '))
  const row01 = dpage.locator('.ay-table tbody tr', { hasText: '01.07.2026' })
  const cells01 = await row01.locator('td').allTextContents()
  check('(h) 01.07 satırı (null): "—" / "—"', cells01[7] === '—' && cells01[8] === '—', cells01.join(' | '))
  const ayFleet = (await dpage.locator('.ay-fleet').textContent()).trim()
  check('(h) filo özeti: Toplam Başlayan 7 | Toplam Biten 5', ayFleet.includes('Toplam Başlayan 7') && ayFleet.includes('Toplam Biten 5'), ayFleet)
  await dpage.screenshot({ path: `${OUT}/g-desktop-ay-sutunlar-1280.png` })

  /* (i) /rapor filo satırı */
  log('— (i) /rapor filo satırı —')
  await dpage.goto(`${BASE}/rapor?date=${today}`)
  await dpage.waitForSelector('.rapor-filo')
  const raporLine = (await dpage.locator('.rapor-filo').textContent()).trim()
  check('(i) FİLO satırı: "| Başlayan 7 | Biten 5"', raporLine.includes('| Başlayan 7') && raporLine.includes('| Biten 5'), raporLine)
  await dpage.locator('.rapor-filo').scrollIntoViewIfNeeded()
  await dpage.screenshot({ path: `${OUT}/h-desktop-rapor-filo-1280.png` })

  await dpage.goto(`${BASE}/rapor?date=2026-07-01`)
  await dpage.waitForSelector('.rapor-filo')
  const raporLineNull = (await dpage.locator('.rapor-filo').textContent()).trim()
  check('(i) null günde: "Başlayan — | Biten —"', raporLineNull.includes('Başlayan —') && raporLineNull.includes('Biten —'), raporLineNull)

  /* PDF + Excel dosyaları (kanıt) */
  log('— PDF/Excel kanıt dosyaları —')
  const pdfFull = await dctx.request.get(`${BASE}/api/reports/daily/pdf?date=${today}`)
  fs.writeFileSync(`${OUT}/i-pdf-filo-degerli.pdf`, await pdfFull.body())
  check('PDF (değerli sayaçlar) üretildi', pdfFull.ok())
  const pdfNull = await dctx.request.get(`${BASE}/api/reports/daily/pdf?date=2026-07-01`)
  fs.writeFileSync(`${OUT}/j-pdf-filo-null.pdf`, await pdfNull.body())
  check('PDF (null sayaçlar) üretildi', pdfNull.ok())
  const xlsx = await dctx.request.get(`${BASE}/api/reports/month/xlsx?month=2026-07`)
  fs.writeFileSync(`${OUT}/k-excel-ay-2026-07.xlsx`, await xlsx.body())
  check('Excel (2026-07) üretildi', xlsx.ok())

  await dctx.close()
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
