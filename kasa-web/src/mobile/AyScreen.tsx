import { useEffect, useState } from 'react'
import type { CategoryTotal, FleetMonth, MonthReport } from '../api'
import { errorMessage, getFleetMonth, getMonthReport } from '../api'
import { formatMonthLong, formatMonthShort, formatWeekday, shiftMonth } from '../dates'
import { formatSatang } from '../format'
import { IconDownload } from './icons'

/** "%90" gibi Türkçe yüzde metni; sayı server'dan hazır gelir, burada yalnız biçimlenir (I1). */
const trNumber = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 })

function rentalText(percent: number | null | undefined): string {
  return percent != null ? `%${percent.toFixed(1)}` : '—'
}

interface Props {
  month: string
  tick: number
  onMonthChange: (month: string) => void
  onOpenDay: (date: string) => void
}

/**
 * Kategori dağılım listesi. Bar genişliği kategori/max oranıdır — GÖRSEL ölçek
 * dönüşümü, para HESABI değil (I1); tutar metinleri API'den hazır gelir.
 */
function DistBars({ title, totals, accent }: { title: string; totals: CategoryTotal[]; accent: 'income' | 'expense' }) {
  const max = Math.max(...totals.map(t => t.totalSatang), 1)
  return (
    <>
      <h2 className="m-section-label">{title}</h2>
      <div className="m-list m-bars">
        {totals.length === 0 ? (
          <div className="m-empty">— Kayıt yok —</div>
        ) : (
          totals.map(t => (
            <div key={t.category} className="m-bar-row">
              <div className="m-bar-head">
                <span className="m-bar-name">{t.category}</span>
                <span className="m-bar-amt">{formatSatang(t.totalSatang)}</span>
              </div>
              <div className="m-bar-track">
                <div
                  className={`m-bar-fill m-bar-fill-${accent}`}
                  style={{ width: `${(t.totalSatang / max) * 100}%` }}
                />
              </div>
            </div>
          ))
        )}
      </div>
    </>
  )
}

export default function AyScreen({ month, tick, onMonthChange, onOpenDay }: Props) {
  const [report, setReport] = useState<MonthReport | null>(null)
  const [fleet, setFleet] = useState<FleetMonth | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)

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
  }, [month, tick])

  const negativeFinal = report !== null && report.finalBalance < 0

  // Trend verisi /api/fleet/month'tan tarihe eşlenir (ay raporuyla aynı desen); UI toplamaz (I1).
  const fleetByDate = new Map(fleet?.days.map(d => [d.date, d]) ?? [])

  return (
    <div className="m-screen">
      <header className="m-screen-head">
        <h1 className="m-title m-title-month">{formatMonthLong(month)}</h1>
        <div className="m-head-actions">
          <button type="button" className="m-round-btn" onClick={() => onMonthChange(shiftMonth(month, -1))} aria-label="Önceki ay">
            ‹
          </button>
          <button type="button" className="m-round-btn" onClick={() => onMonthChange(shiftMonth(month, 1))} aria-label="Sonraki ay">
            ›
          </button>
        </div>
      </header>

      {loadError && <div className="banner banner-error">{loadError}</div>}

      {report && fleet && (
        <>
          <section className="m-hero m-hero-month">
            <div className="m-hero-glow" />
            <div className="m-hero-label">Ay Sonu Ana Kasa</div>
            <div className={`m-hero-amount ${negativeFinal ? 'm-hero-amount-neg' : ''}`}>
              {formatSatang(report.finalBalance)}
            </div>
            <div className="m-hero-sub m-hero-month-sub">
              <span>
                Gelir <strong>{formatSatang(report.totals.incomeTotal)}</strong>
              </span>
              <span>
                Gider <strong>{formatSatang(report.totals.expenseTotal)}</strong>
              </span>
            </div>
          </section>

          <h2 className="m-section-label">Ortaklık Dağıtımı</h2>
          {negativeFinal ? (
            <div className="m-loss-card">
              Dağıtılacak bakiye yok — zarar: {formatSatang(Math.abs(report.finalBalance))}
            </div>
          ) : (
            <div className="m-partner-grid">
              {[report.distribution.partner1, report.distribution.partner2].map(p => (
                <div key={p.name} className="m-partner-card">
                  <div className="m-partner-name">{p.name}</div>
                  <div className="m-partner-share">%{trNumber.format(p.sharePercent)} pay</div>
                  <div className="m-partner-amt">{formatSatang(p.amountSatang)}</div>
                </div>
              ))}
            </div>
          )}

          <h2 className="m-section-label">Günler</h2>
          <div className="m-list">
            {report.days.length === 0 ? (
              <div className="m-empty">— Bu ayda işlem yok —</div>
            ) : (
              report.days.map(d => {
                // Snapshot yoksa veya iki sayaç da null ise gösterge TAMAMEN gizli ("—" bile yok).
                const snap = fleetByDate.get(d.date)
                const showTrend = snap != null && (snap.startedReservations != null || snap.endedReservations != null)
                return (
                  <button key={d.date} type="button" className="m-row" onClick={() => onOpenDay(d.date)}>
                    <span className="m-day-badge">
                      <span className="m-day-badge-num">{Number(d.date.slice(8, 10))}</span>
                      <span className="m-day-badge-mon">{formatMonthShort(d.date)}</span>
                    </span>
                    <span className="m-row-main">
                      <span className="m-row-cat">{formatWeekday(d.date)}</span>
                      <span className="m-row-meta">Kasa {formatSatang(d.cumulativeBalance)}</span>
                    </span>
                    <span className="m-row-amt-wrap">
                      <span className={`m-row-amt ${d.dayNet < 0 ? 'm-expense-text' : 'm-income-text'}`}>
                        {d.dayNet > 0 ? '+' : ''}
                        {formatSatang(d.dayNet)}
                      </span>
                      {showTrend && (
                        <span className="m-trend">
                          {snap.startedReservations != null && (
                            <span className="m-trend-up">↑{snap.startedReservations}</span>
                          )}
                          {snap.endedReservations != null && (
                            <span className="m-trend-down">↓{snap.endedReservations}</span>
                          )}
                        </span>
                      )}
                    </span>
                    <span className="m-chevron">›</span>
                  </button>
                )
              })
            )}
          </div>

          <DistBars title="Gelir Dağılımı" totals={report.incomeByCategory} accent="income" />
          <DistBars title="Gider Dağılımı" totals={report.expenseByCategory} accent="expense" />

          <h2 className="m-section-label">Filo Ay Özeti</h2>
          <div className="m-list m-fleet-summary">
            <span>Ortalama Kiralama {rentalText(fleet.summary.avgRentalPercent)}</span>
            <span className="m-fleet-summary-sep">·</span>
            <span>Arızalı-Gün {fleet.summary.totalBrokenDays}</span>
            <span className="m-fleet-summary-sep">·</span>
            {fleet.summary.missingDays > 0 ? (
              <span className="m-fleet-summary-warn">Eksik Gün {fleet.summary.missingDays}</span>
            ) : (
              <span>Eksik Gün 0</span>
            )}
            <span className="m-fleet-summary-sep">·</span>
            <span>Başlayan {fleet.summary.totalStarted ?? '—'}</span>
            <span className="m-fleet-summary-sep">·</span>
            <span>Biten {fleet.summary.totalEnded ?? '—'}</span>
          </div>

          <button
            type="button"
            className="m-primary-btn"
            onClick={() => window.open(`/api/reports/month/xlsx?month=${month}`)}
          >
            <IconDownload />
            Ay raporunu indir (Excel)
          </button>
        </>
      )}
    </div>
  )
}
