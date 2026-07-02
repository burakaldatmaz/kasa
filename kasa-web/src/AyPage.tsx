import { useEffect, useState } from 'react'
import type { CategoryTotal, FleetMonth, MonthReport } from './api'
import { errorMessage, getFleetMonth, getMonthReport } from './api'
import { currentMonthISO, formatDateShort, formatMonthLong, shiftMonth } from './dates'
import { formatSatang } from './format'

const MONTH_RE = /^\d{4}-\d{2}$/

function monthFromUrl(): string {
  const param = new URLSearchParams(window.location.search).get('month')
  return param !== null && MONTH_RE.test(param) ? param : currentMonthISO()
}

/** "%90" gibi Türkçe yüzde metni; sayı server'dan hazır gelir, burada yalnız biçimlenir (I1). */
const trNumber = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 })

/** Kiralama yüzdesi metni; değer yoksa (snapshot girilmemiş/tanımsız) "—". */
function rentalText(percent: number | null | undefined): string {
  return percent != null ? `%${percent.toFixed(1)}` : '—'
}

function CategoryTable({ title, totals }: { title: string; totals: CategoryTotal[] }) {
  return (
    <div className="card">
      <h2 className="card-header">{title}</h2>
      {totals.length === 0 ? (
        <p className="ay-empty-block">— Kayıt yok —</p>
      ) : (
        <table className="ay-table">
          <tbody>
            {totals.map(t => (
              <tr key={t.category}>
                <td>{t.category}</td>
                <td className="ay-amount">{formatSatang(t.totalSatang)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

/** Ay görünümü: gün tablosu + kategori dağılımı + ortaklık dağıtımı + filo özeti.
 *  Veri /api/reports/month + /api/fleet/month'tan hazır gelir; UI hiçbir toplama yapmaz (I1). */
export default function AyPage() {
  const [month, setMonth] = useState(monthFromUrl)
  const [report, setReport] = useState<MonthReport | null>(null)
  const [fleet, setFleet] = useState<FleetMonth | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)

  function changeMonth(next: string) {
    setMonth(next)
    const url = new URL(window.location.href)
    url.searchParams.set('month', next)
    window.history.pushState(null, '', url)
  }

  // Tarayıcı geri/ileri okları URL'deki aya döndürür.
  useEffect(() => {
    const onPop = () => setMonth(monthFromUrl())
    window.addEventListener('popstate', onPop)
    return () => window.removeEventListener('popstate', onPop)
  }, [])

  // İlk açılışta URL'de ay yoksa bu ayı yaz: yenileme ve link paylaşımı korunur.
  useEffect(() => {
    const url = new URL(window.location.href)
    if (!MONTH_RE.test(url.searchParams.get('month') ?? '')) {
      url.searchParams.set('month', monthFromUrl())
      window.history.replaceState(null, '', url)
    }
  }, [])

  useEffect(() => {
    let cancelled = false
    Promise.all([getMonthReport(month), getFleetMonth(month)])
      .then(([rep, fl]) => {
        if (cancelled) return
        setReport(rep)
        setFleet(fl)
        setLoadError(null)
      })
      .catch(err => {
        if (!cancelled) setLoadError(errorMessage(err))
      })
    return () => {
      cancelled = true
    }
  }, [month])

  // Kiralama % gün tablosuna tarihe göre eşlenir; snapshot yoksa "—" (UI hesap yapmaz).
  const rentalByDate = new Map(fleet?.days.map(d => [d.date, d.rentalPercent]) ?? [])
  const negativeFinal = report !== null && report.finalBalance < 0

  return (
    <div className="page ay-page">
      <header className="page-header">
        <h1>Günlük Kasa</h1>
        <div className="date-nav">
          <button type="button" onClick={() => changeMonth(shiftMonth(month, -1))} aria-label="Önceki ay">
            ◀
          </button>
          <span className="month-label">{formatMonthLong(month)}</span>
          <button type="button" onClick={() => changeMonth(shiftMonth(month, 1))} aria-label="Sonraki ay">
            ▶
          </button>
        </div>
        <a className="rapor-nav-link" href="/">
          ← Gün Görünümü
        </a>
        <button
          type="button"
          className="btn-primary btn-small"
          onClick={() => window.open(`/api/reports/month/xlsx?month=${month}`)}
        >
          Excel İndir
        </button>
      </header>

      {loadError && <div className="banner banner-error">{loadError}</div>}

      {report && fleet && (
        <>
          <div className="card">
            <table className="ay-table">
              <thead>
                <tr>
                  <th>Tarih</th>
                  <th className="ay-amount">Gelir</th>
                  <th className="ay-amount">Gider</th>
                  <th className="ay-amount">POS</th>
                  <th className="ay-amount">Gün Net</th>
                  <th className="ay-amount">Kümülatif Kasa</th>
                  <th className="ay-amount">Kiralama %</th>
                </tr>
              </thead>
              <tbody>
                {report.days.length === 0 && (
                  <tr>
                    <td className="ay-empty" colSpan={7}>
                      — Bu ayda işlem yok —
                    </td>
                  </tr>
                )}
                {report.days.map(d => (
                  <tr
                    key={d.date}
                    className="ay-row-click"
                    title="Günün raporunu aç"
                    onClick={() => {
                      window.location.href = `/rapor?date=${d.date}`
                    }}
                  >
                    <td>{formatDateShort(d.date)}</td>
                    <td className="ay-amount">{formatSatang(d.incomeTotal)}</td>
                    <td className="ay-amount">{formatSatang(d.expenseTotal)}</td>
                    <td className="ay-amount">{formatSatang(d.posFee)}</td>
                    <td className={`ay-amount ${d.dayNet < 0 ? 'ay-negative' : ''}`}>
                      {formatSatang(d.dayNet)}
                    </td>
                    <td className={`ay-amount ${d.cumulativeBalance < 0 ? 'ay-negative' : ''}`}>
                      {formatSatang(d.cumulativeBalance)}
                    </td>
                    <td className="ay-amount">{rentalText(rentalByDate.get(d.date))}</td>
                  </tr>
                ))}
              </tbody>
              <tfoot>
                {/* Toplamlar API'nin totals/finalBalance alanlarından; UI toplamaz (I1). */}
                <tr>
                  <td>TOPLAM</td>
                  <td className="ay-amount">{formatSatang(report.totals.incomeTotal)}</td>
                  <td className="ay-amount">{formatSatang(report.totals.expenseTotal)}</td>
                  <td className="ay-amount">{formatSatang(report.totals.posFee)}</td>
                  <td className={`ay-amount ${report.totals.dayNet < 0 ? 'ay-negative' : ''}`}>
                    {formatSatang(report.totals.dayNet)}
                  </td>
                  <td className={`ay-amount ${negativeFinal ? 'ay-negative' : ''}`}>
                    {formatSatang(report.finalBalance)}
                  </td>
                  <td />
                </tr>
              </tfoot>
            </table>
          </div>

          <div className="ay-dist-grid">
            <CategoryTable title="Gelir Türü Dağılımı" totals={report.incomeByCategory} />
            <CategoryTable title="Gider Türü Dağılımı" totals={report.expenseByCategory} />
          </div>

          <div className="card">
            <h2 className="card-header">Ortaklık Dağıtımı</h2>
            <div className="card-body">
              {negativeFinal ? (
                <p className="ay-loss">
                  Dağıtılacak bakiye yok — zarar: {formatSatang(Math.abs(report.finalBalance))}
                </p>
              ) : (
                <div>
                  <div className="ay-dist-row ay-dist-main">
                    <span>Ay Sonu Ana Kasa</span>
                    <span className="ay-amount">{formatSatang(report.finalBalance)}</span>
                  </div>
                  <div className="ay-dist-row">
                    <span>
                      {report.distribution.partner1.name} (%
                      {trNumber.format(report.distribution.partner1.sharePercent)})
                    </span>
                    <span className="ay-amount">{formatSatang(report.distribution.partner1.amountSatang)}</span>
                  </div>
                  <div className="ay-dist-row">
                    <span>
                      {report.distribution.partner2.name} (%
                      {trNumber.format(report.distribution.partner2.sharePercent)})
                    </span>
                    <span className="ay-amount">{formatSatang(report.distribution.partner2.amountSatang)}</span>
                  </div>
                </div>
              )}
            </div>
          </div>

          <div className="card">
            <h2 className="card-header">Filo Ay Özeti</h2>
            <div className="card-body ay-fleet">
              <span>Ortalama Kiralama {rentalText(fleet.summary.avgRentalPercent)}</span>
              <span className="ay-fleet-sep">|</span>
              <span>Toplam Arızalı-Gün {fleet.summary.totalBrokenDays}</span>
              <span className="ay-fleet-sep">|</span>
              {fleet.summary.missingDays > 0 ? (
                <span className="badge badge-warn">Eksik Gün {fleet.summary.missingDays}</span>
              ) : (
                <span>Eksik Gün 0</span>
              )}
            </div>
          </div>
        </>
      )}
    </div>
  )
}
