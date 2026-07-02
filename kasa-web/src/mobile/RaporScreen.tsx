import { useEffect, useState } from 'react'
import type { CategoryTotal, DailyReport, ReportLine } from '../api'
import { errorMessage, getDailyReport } from '../api'
import { formatDateLong } from '../dates'
import { formatSatang } from '../format'
import { paymentShort } from '../labels'
import { IconDownload } from './icons'

/** "%3,5" gibi Türkçe yüzde metni; oran server'dan hazır gelir, burada yalnız biçimlenir (I1). */
const trNumber = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 })

function DottedLines({ lines }: { lines: ReportLine[] }) {
  if (lines.length === 0) return <p className="m-rapor-empty">— Kayıt yok —</p>
  return (
    <>
      {lines.map(line => (
        <div key={line.id} className="m-dotted">
          <div className="m-dotted-main">
            <span className="m-dotted-label">
              {line.category} ({paymentShort(line.paymentMethod)})
            </span>
            <span className="m-leader" />
            <span className="m-dotted-amt">{formatSatang(line.amountSatang)}</span>
          </div>
          {line.note && <div className="m-dotted-note">{line.note}</div>}
        </div>
      ))}
    </>
  )
}

function DistBlock({ title, totals }: { title: string; totals: CategoryTotal[] }) {
  return (
    <div className="m-rapor-dist">
      <div className="m-band m-band-neutral">{title}</div>
      {totals.length === 0 ? (
        <p className="m-rapor-empty">— Kayıt yok —</p>
      ) : (
        totals.map(t => (
          <div key={t.category} className="m-dotted-main m-dist-row">
            <span className="m-dotted-label">{t.category}</span>
            <span className="m-leader" />
            <span className="m-dotted-amt">{formatSatang(t.totalSatang)}</span>
          </div>
        ))
      )}
    </div>
  )
}

interface Props {
  date: string
  tick: number
}

/** Gün Raporu ekranı — veriler /api/reports/daily'den hazır gelir (I1). */
export default function RaporScreen({ date, tick }: Props) {
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
  }, [date, tick])

  const negative = report !== null && report.closingBalance < 0

  return (
    <div className="m-screen">
      <header className="m-screen-head">
        <div>
          <h1 className="m-title">Gün Raporu</h1>
          <div className="m-subtitle">{formatDateLong(date)}</div>
        </div>
      </header>

      {loadError && <div className="banner banner-error">{loadError}</div>}

      {report && (
        <>
          <article className="m-rapor-card">
            <div className="m-rapor-strip">
              <span>Devreden Kasa</span>
              <span className="m-rapor-strip-amt">{formatSatang(report.previousBalance)}</span>
            </div>

            <div className="m-band m-band-income">Gelirler</div>
            <DottedLines lines={report.incomeLines} />

            <div className="m-band m-band-expense">Giderler</div>
            <DottedLines lines={report.expenseLines} />

            <div className="m-dotted-main m-pos-row">
              <span className="m-dotted-label">POS Kesintisi (%{trNumber.format(report.posFeeRatePercent)})</span>
              <span className="m-leader" />
              <span className="m-dotted-amt m-expense-text">{formatSatang(report.posFee)}</span>
            </div>

            <div className="m-net-box">
              <span>Gün Net</span>
              <span className={report.dayNet < 0 ? 'm-expense-text' : 'm-income-text'}>
                {formatSatang(report.dayNet)}
              </span>
            </div>

            <div className={`m-closing-box ${negative ? 'm-closing-box-neg' : ''}`}>
              <span>Kapanış Kasası</span>
              <span className="m-closing-amt">{formatSatang(report.closingBalance)}</span>
            </div>
          </article>

          <h2 className="m-section-label">Tür Dağılımı</h2>
          <article className="m-rapor-card">
            <DistBlock title="Gelir Türü Dağılımı" totals={report.incomeByCategory} />
            <DistBlock title="Gider Türü Dağılımı" totals={report.expenseByCategory} />
          </article>

          {/* Filo bölümü: PDF filo şeridiyle aynı bilgi; sayaçlarda null → "—" (K2). */}
          {report.fleet && (
            <>
              <h2 className="m-section-label">Filo</h2>
              <div className="m-list m-fleet-summary">
                <span>Toplam {report.fleet.totalBikes}</span>
                <span className="m-fleet-summary-sep">·</span>
                <span>Kirada {report.fleet.rentedBikes}</span>
                <span className="m-fleet-summary-sep">·</span>
                <span>Boşta {report.fleet.idleBikes}</span>
                <span className="m-fleet-summary-sep">·</span>
                {report.fleet.brokenAlert ? (
                  <span className="m-fleet-summary-warn">Arızalı {report.fleet.brokenBikes}</span>
                ) : (
                  <span>Arızalı {report.fleet.brokenBikes}</span>
                )}
                {report.fleet.rentalPercent != null && (
                  <>
                    <span className="m-fleet-summary-sep">·</span>
                    <span>Kiralama %{report.fleet.rentalPercent.toFixed(1)}</span>
                  </>
                )}
                <span className="m-fleet-summary-sep">·</span>
                <span>Başlayan {report.fleet.startedReservations ?? '—'}</span>
                <span className="m-fleet-summary-sep">·</span>
                <span>Biten {report.fleet.endedReservations ?? '—'}</span>
              </div>
            </>
          )}

          <button
            type="button"
            className="m-light-btn"
            onClick={() => window.open(`/api/reports/daily/pdf?date=${date}`)}
          >
            <IconDownload />
            PDF olarak indir
          </button>
        </>
      )}
    </div>
  )
}
