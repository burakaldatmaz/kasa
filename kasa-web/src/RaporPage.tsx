import { useEffect, useState } from 'react'
import type { CategoryTotal, DailyReport, ReportLine } from './api'
import { errorMessage, getDailyReport } from './api'
import { formatDateReport, todayISO } from './dates'
import { formatSatang } from './format'
import { paymentLabel } from './labels'
import LogoutButton from './components/LogoutButton'

const DATE_RE = /^\d{4}-\d{2}-\d{2}$/

function dateFromUrl(): string {
  const param = new URLSearchParams(window.location.search).get('date')
  return param !== null && DATE_RE.test(param) ? param : todayISO()
}

/** "%3,5" gibi Türkçe yüzde metni; sayı server'dan hazır gelir, burada yalnız biçimlenir (I1). */
const trNumber = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 })

function LineRows({ lines }: { lines: ReportLine[] }) {
  if (lines.length === 0) return <p className="rapor-empty">— Kayıt yok —</p>
  return (
    <div className="rapor-lines">
      {lines.map(line => (
        <div key={line.id} className="rapor-line">
          <div className="rapor-line-main">
            <span className="rapor-line-label">
              {line.category} ({paymentLabel(line.paymentMethod)})
            </span>
            <span className="rapor-leader" />
            <span className="rapor-amount">{formatSatang(line.amountSatang)}</span>
          </div>
          {line.note && <div className="rapor-note">{line.note}</div>}
        </div>
      ))}
    </div>
  )
}

function DistributionTable({ title, totals }: { title: string; totals: CategoryTotal[] }) {
  return (
    <div className="rapor-dist">
      <h3 className="rapor-band">{title}</h3>
      {totals.length === 0 ? (
        <p className="rapor-empty">— Kayıt yok —</p>
      ) : (
        <div>
          {totals.map(t => (
            <div key={t.category} className="rapor-dist-row">
              <span>{t.category}</span>
              <span className="rapor-amount">{formatSatang(t.totalSatang)}</span>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

/** "KASA İŞLEM" günlük rapor görünümü — PDF çıktısıyla birebir aynı düzen (veri: /api/reports/daily, I1). */
export default function RaporPage() {
  const [date] = useState(dateFromUrl)
  const [report, setReport] = useState<DailyReport | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    getDailyReport(date)
      .then(rep => {
        if (cancelled) return
        setReport(rep)
        setLoadError(null)
      })
      .catch(err => {
        if (!cancelled) setLoadError(errorMessage(err))
      })
    return () => {
      cancelled = true
    }
  }, [date])

  const negative = report !== null && report.closingBalance < 0

  return (
    <div className="page rapor-page">
      <header className="page-header">
        <h1>Günlük Kasa</h1>
        <nav className="page-nav">
          <a className="rapor-nav-link" href={`/?date=${date}`}>
            ← Gün Görünümü
          </a>
          <a className="rapor-nav-link" href={`/ay?month=${date.slice(0, 7)}`}>
            Ay Görünümü
          </a>
          <button
            type="button"
            className="btn-primary btn-small"
            onClick={() => window.open(`/api/reports/daily/pdf?date=${date}`)}
          >
            PDF İndir
          </button>
          <LogoutButton />
        </nav>
      </header>

      {loadError && <div className="banner banner-error">{loadError}</div>}

      {report && (
        <article className="rapor-sheet">
          <h2 className="rapor-title">KASA İŞLEM — {formatDateReport(report.date)}</h2>

          <p className="rapor-devir">
            Devir (önceki günden): <strong>{formatSatang(report.previousBalance)}</strong>
          </p>

          <section>
            <h3 className="rapor-band">GELİR</h3>
            <LineRows lines={report.incomeLines} />
          </section>

          <section>
            <h3 className="rapor-band">GİDER</h3>
            <LineRows lines={report.expenseLines} />
          </section>

          <div className="rapor-dist-grid">
            <DistributionTable title="GELİR TÜRÜ DAĞILIMI" totals={report.incomeByCategory} />
            <DistributionTable title="GİDER TÜRÜ DAĞILIMI" totals={report.expenseByCategory} />
          </div>

          <section>
            <h3 className="rapor-band">GÜN NET</h3>
            <div className="rapor-net">
              <div className="rapor-net-row">
                <span>Gelirler Toplamı</span>
                <span className="rapor-amount">{formatSatang(report.incomeTotal)}</span>
              </div>
              <div className="rapor-net-row">
                <span>Giderler Toplamı</span>
                <span className="rapor-amount">{formatSatang(report.expenseTotal)}</span>
              </div>
              <div className="rapor-net-row">
                <span>POS Kesintisi (%{trNumber.format(report.posFeeRatePercent)})</span>
                <span className="rapor-amount">{formatSatang(report.posFee)}</span>
              </div>
              <div className="rapor-net-row">
                <span>Gün Net</span>
                <span className="rapor-amount">{formatSatang(report.dayNet)}</span>
              </div>
              <div className="rapor-net-row">
                <span>+ Devir</span>
                <span className="rapor-amount">{formatSatang(report.previousBalance)}</span>
              </div>
              <div className={`rapor-net-row rapor-anakasa ${negative ? 'rapor-anakasa-negative' : ''}`}>
                <span>ANA KASA</span>
                <span className="rapor-amount">{formatSatang(report.closingBalance)}</span>
              </div>
            </div>
          </section>

          {report.fleet ? (
            <p className="rapor-filo">
              FİLO: Toplam {report.fleet.totalBikes}
              {' | '}Arızalı {report.fleet.brokenBikes}
              {report.fleet.brokenAlert && ' ⚠'}
              {' | '}Kirada {report.fleet.rentedBikes}
              {report.fleet.rentalPercent != null && ` | Kiralama %${report.fleet.rentalPercent.toFixed(1)}`}
              {/* Rezervasyon sayaçları PDF filo şeridiyle aynı: null → "—" (K2). */}
              {` | Başlayan ${report.fleet.startedReservations ?? '—'}`}
              {` | Biten ${report.fleet.endedReservations ?? '—'}`}
            </p>
          ) : (
            <p className="rapor-filo rapor-filo-missing">Filo verisi girilmedi</p>
          )}
        </article>
      )}
    </div>
  )
}
