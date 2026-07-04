import { useCallback, useEffect, useState } from 'react'
import type { DepositReceipt } from './api'
import { depositReceiptPdfUrl, errorMessage, getDepositReceipts } from './api'
import { formatDateLong, shiftDate, todayISO } from './dates'
import { formatSatang } from './format'
import { paymentLabel } from './labels'
import DepositForm from './components/DepositForm'
import LogoutButton from './components/LogoutButton'

const DATE_RE = /^\d{4}-\d{2}-\d{2}$/

function dateFromUrl(): string {
  const param = new URLSearchParams(window.location.search).get('date')
  return param !== null && DATE_RE.test(param) ? param : todayISO()
}

/**
 * Depozito makbuzu sekmesi — kasa'nın mali akışıyla temassız. Sol: yeni makbuz formu
 * (kaydet → PDF yeni sekmede). Sağ: seçili günün makbuz listesi (yeniden yazdırma).
 */
export default function DepozitoPage() {
  const [date, setDate] = useState(dateFromUrl)
  const [receipts, setReceipts] = useState<DepositReceipt[]>([])
  const [loadError, setLoadError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)

  const refresh = useCallback(() => setTick(t => t + 1), [])

  function changeDate(next: string) {
    if (!DATE_RE.test(next)) return
    setDate(next)
    const url = new URL(window.location.href)
    url.searchParams.set('date', next)
    window.history.pushState(null, '', url)
  }

  useEffect(() => {
    const onPop = () => setDate(dateFromUrl())
    window.addEventListener('popstate', onPop)
    return () => window.removeEventListener('popstate', onPop)
  }, [])

  useEffect(() => {
    const url = new URL(window.location.href)
    if (!DATE_RE.test(url.searchParams.get('date') ?? '')) {
      url.searchParams.set('date', dateFromUrl())
      window.history.replaceState(null, '', url)
    }
  }, [])

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

  return (
    <div className="page">
      <header className="page-header">
        <h1>Depozito Makbuzu</h1>
        <div className="date-nav">
          <button type="button" onClick={() => changeDate(shiftDate(date, -1))} aria-label="Önceki gün">
            ◀
          </button>
          <input type="date" value={date} onChange={e => changeDate(e.target.value)} />
          <button type="button" onClick={() => changeDate(shiftDate(date, 1))} aria-label="Sonraki gün">
            ▶
          </button>
        </div>
        <span className="date-long">{formatDateLong(date)}</span>
        <nav className="page-nav">
          <a className="rapor-nav-link" href={`/?date=${date}`}>
            Kasa
          </a>
          <a className="rapor-nav-link" href={`/rapor?date=${date}`}>
            Rapor Görünümü
          </a>
          <LogoutButton />
        </nav>
      </header>

      {loadError && <div className="banner banner-error">{loadError}</div>}

      <div className="depozito-grid">
        <DepositForm date={date} onSaved={refresh} />

        <section className="card dep-list-card">
          <h2 className="card-header">
            Günün Makbuzları
            <span className="dep-count">{receipts.length}</span>
          </h2>
          <div className="card-body">
            {receipts.length === 0 ? (
              <p className="dep-empty">Bu gün için makbuz yok.</p>
            ) : (
              <ul className="dep-list">
                {receipts.map(r => (
                  <li key={r.id} className="dep-row">
                    <div className="dep-row-main">
                      <span className="dep-row-no">{r.no}</span>
                      <span className="dep-row-name">{r.customerName}</span>
                      <span className="dep-row-meta">
                        {r.plate} · {paymentLabel(r.paymentMethod)}
                      </span>
                    </div>
                    <span className="dep-row-amt">{formatSatang(r.amountSatang)}</span>
                    <button
                      type="button"
                      className="btn-secondary btn-small"
                      onClick={() => window.open(depositReceiptPdfUrl(r.id))}
                    >
                      PDF
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </section>
      </div>
    </div>
  )
}
