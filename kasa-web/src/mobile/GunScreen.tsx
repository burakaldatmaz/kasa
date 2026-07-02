import { useEffect, useState } from 'react'
import type { DailyReport, Transaction } from '../api'
import { errorMessage, getDailyReport, getTransactions, logout } from '../api'
import { formatDateLong, formatDayMonth, shiftDate, todayISO } from '../dates'
import { formatSatang } from '../format'
import { paymentShort } from '../labels'
import FleetRing from './FleetRing'
import TxnActionSheet from './TxnActionSheet'
import { IconArrowDown, IconArrowUp, IconLogout } from './icons'

/** "%3,5" gibi Türkçe yüzde metni; oran server'dan hazır gelir, burada yalnız biçimlenir (I1). */
const trNumber = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2 })

interface Props {
  date: string
  tick: number
  onDateChange: (date: string) => void
  onOpenFilo: () => void
  onChanged: () => void
  showToast: (message: string, color: string) => void
}

function metaLine(t: Transaction): string {
  return t.note ? `${paymentShort(t.paymentMethod)} · ${t.note}` : paymentShort(t.paymentMethod)
}

export default function GunScreen({ date, tick, onDateChange, onOpenFilo, onChanged, showToast }: Props) {
  const [report, setReport] = useState<DailyReport | null>(null)
  const [transactions, setTransactions] = useState<Transaction[]>([])
  const [loadError, setLoadError] = useState<string | null>(null)
  const [selected, setSelected] = useState<Transaction | null>(null)

  useEffect(() => {
    let cancelled = false
    Promise.all([getTransactions(date), getDailyReport(date)])
      .then(([txns, rep]) => {
        if (cancelled) return
        setTransactions(txns)
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

  async function handleLogout() {
    try {
      await logout()
    } catch {
      // Oturum zaten düşmüş olabilir; yine de login'e dön.
    }
    window.location.assign('/login')
  }

  const isToday = date === todayISO()
  const incomes = transactions.filter(t => t.type === 'Income')
  const expenses = transactions.filter(t => t.type === 'Expense')
  const negative = report !== null && report.closingBalance < 0
  const dayNetNegative = report !== null && report.dayNet < 0

  function txnRow(t: Transaction) {
    const income = t.type === 'Income'
    return (
      <button key={t.id} type="button" className="m-row" onClick={() => setSelected(t)}>
        <span className={`m-row-icon ${income ? 'm-row-icon-income' : 'm-row-icon-expense'}`}>
          {income ? <IconArrowUp /> : <IconArrowDown />}
        </span>
        <span className="m-row-main">
          <span className="m-row-cat">{t.categoryName}</span>
          <span className="m-row-meta">{metaLine(t)}</span>
        </span>
        <span className={`m-row-amt ${income ? 'm-income-text' : 'm-expense-text'}`}>
          {formatSatang(t.amountSatang)}
        </span>
      </button>
    )
  }

  return (
    <div className="m-screen">
      <header className="m-screen-head">
        <div>
          <h1 className="m-title">{isToday ? 'Bugün' : formatDayMonth(date)}</h1>
          <div className="m-subtitle">{formatDateLong(date)}</div>
        </div>
        <div className="m-head-actions">
          <button type="button" className="m-round-btn" onClick={() => onDateChange(shiftDate(date, -1))} aria-label="Önceki gün">
            ‹
          </button>
          <button type="button" className="m-round-btn" onClick={() => onDateChange(shiftDate(date, 1))} aria-label="Sonraki gün">
            ›
          </button>
          <button type="button" className="m-round-btn m-round-btn-muted" onClick={handleLogout} aria-label="Çıkış">
            <IconLogout />
          </button>
        </div>
      </header>

      {loadError && <div className="banner banner-error">{loadError}</div>}

      {report && (
        <>
          <section className="m-hero">
            <div className="m-hero-glow" />
            <div className="m-hero-label">Kasada</div>
            <div className={`m-hero-amount ${negative ? 'm-hero-amount-neg' : ''}`}>
              {formatSatang(report.closingBalance)}
            </div>
            <div className="m-hero-sub">
              <span className={dayNetNegative ? 'm-hero-net-neg' : 'm-hero-net'}>
                {report.dayNet > 0 ? '+' : ''}
                {formatSatang(report.dayNet)}
              </span>
              <span>
                {isToday ? 'bugün' : 'gün net'} · devir {formatSatang(report.previousBalance)}
              </span>
            </div>
            <div className="m-hero-boxes">
              <div className="m-hero-box">
                <div className="m-hero-box-label">GELİR</div>
                <div className="m-hero-box-value">{formatSatang(report.incomeTotal)}</div>
              </div>
              <div className="m-hero-box">
                <div className="m-hero-box-label">GİDER</div>
                <div className="m-hero-box-value">{formatSatang(report.expenseTotal)}</div>
              </div>
              <div className="m-hero-box">
                <div className="m-hero-box-label">POS %{trNumber.format(report.posFeeRatePercent)}</div>
                <div className="m-hero-box-value">{formatSatang(report.posFee)}</div>
              </div>
            </div>
          </section>

          {report.fleet && !report.fleetMissing ? (
            <button type="button" className="m-fleet-mini" onClick={onOpenFilo}>
              <FleetRing size={56} radius={24} strokeWidth={7} percent={report.fleet.rentalPercent}>
                <span className="m-ring-pct-small">
                  {report.fleet.rentalPercent != null ? `%${report.fleet.rentalPercent.toFixed(0)}` : '—'}
                </span>
              </FleetRing>
              <span className="m-fleet-mini-main">
                <span className="m-fleet-mini-title">Filo Durumu</span>
                <span className="m-fleet-mini-meta">
                  {report.fleet.rentedBikes} kirada · {report.fleet.idleBikes} boşta · {report.fleet.brokenBikes}{' '}
                  arızalı
                  {/* Rezervasyon sayaçları: ikisi de null ("girilmedi") ise bu kısım tamamen gizli (K2). */}
                  {(report.fleet.startedReservations != null || report.fleet.endedReservations != null) && (
                    <>
                      {' · '}
                      {report.fleet.startedReservations ?? '—'} başladı / {report.fleet.endedReservations ?? '—'} bitti
                    </>
                  )}
                </span>
              </span>
              <span className="m-chevron">›</span>
            </button>
          ) : (
            <button type="button" className="m-fleet-mini m-fleet-warn" onClick={onOpenFilo}>
              <span className="m-fleet-warn-text">⚠ Bugünün filo verisi girilmedi</span>
              <span className="m-chevron">›</span>
            </button>
          )}

          <h2 className="m-section-label">Gelirler</h2>
          <div className="m-list">
            {incomes.length === 0 ? <div className="m-empty">Henüz gelir yok</div> : incomes.map(txnRow)}
          </div>

          <h2 className="m-section-label">Giderler</h2>
          <div className="m-list">
            {expenses.length === 0 ? <div className="m-empty">Henüz gider yok</div> : expenses.map(txnRow)}
          </div>
        </>
      )}

      {selected && (
        <TxnActionSheet
          txn={selected}
          onClose={() => setSelected(null)}
          onChanged={onChanged}
          showToast={showToast}
        />
      )}
    </div>
  )
}
