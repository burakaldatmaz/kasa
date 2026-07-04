import { useEffect, useState } from 'react'
import type { DepositReceipt } from '../api'
import { depositReceiptPdfUrl, errorMessage, getDepositReceipts, logout } from '../api'
import { formatDateLong, formatDayMonth, shiftDate, todayISO } from '../dates'
import { formatSatang } from '../format'
import { paymentShort } from '../labels'
import DepositSheet from './DepositSheet'
import { IconDownload, IconLogout, IconPlus } from './icons'

interface Props {
  date: string
  tick: number
  onDateChange: (date: string) => void
  onChanged: () => void
  showToast: (message: string, color: string) => void
}

/** Mobil depozito sekmesi: günün makbuz listesi + yeni makbuz sheet'i; PDF yeni sekmede açılır. */
export default function DepozitoScreen({ date, tick, onDateChange, onChanged, showToast }: Props) {
  const [receipts, setReceipts] = useState<DepositReceipt[]>([])
  const [loadError, setLoadError] = useState<string | null>(null)
  const [sheetOpen, setSheetOpen] = useState(false)

  useEffect(() => {
    let cancelled = false
    getDepositReceipts(date)
      .then(list => {
        if (cancelled) return
        setReceipts(list)
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

  return (
    <div className="m-screen">
      <header className="m-screen-head">
        <div>
          <h1 className="m-title">Depozito</h1>
          <div className="m-subtitle">{isToday ? formatDateLong(date) : formatDayMonth(date)}</div>
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

      <button type="button" className="m-dep-new" onClick={() => setSheetOpen(true)}>
        <IconPlus />
        <span>Yeni Depozito Makbuzu</span>
      </button>

      <h2 className="m-section-label">Günün Makbuzları</h2>
      <div className="m-list">
        {receipts.length === 0 ? (
          <div className="m-empty">Bu gün için makbuz yok</div>
        ) : (
          receipts.map(r => (
            <div key={r.id} className="m-row m-dep-item">
              <span className="m-row-main">
                <span className="m-row-cat">
                  {r.no} · {r.customerName}
                </span>
                <span className="m-row-meta">
                  {r.plate} · {paymentShort(r.paymentMethod)} · {formatSatang(r.amountSatang)}
                </span>
              </span>
              <button
                type="button"
                className="m-dep-pdf"
                onClick={() => window.open(depositReceiptPdfUrl(r.id))}
                aria-label={`${r.no} PDF`}
              >
                <IconDownload />
              </button>
            </div>
          ))
        )}
      </div>

      {sheetOpen && (
        <DepositSheet
          date={date}
          onSaved={onChanged}
          showToast={showToast}
          onClose={() => setSheetOpen(false)}
        />
      )}
    </div>
  )
}
