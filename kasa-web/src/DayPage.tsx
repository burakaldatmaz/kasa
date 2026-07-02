import { useCallback, useEffect, useState } from 'react'
import type { Category, DailyReport, Transaction } from './api'
import { errorMessage, getCategories, getDailyReport, getTransactions } from './api'
import { formatDateLong, shiftDate, todayISO } from './dates'
import LogoutButton from './components/LogoutButton'
import TxnForm from './components/TxnForm'
import TxnList from './components/TxnList'
import SummaryBar from './components/SummaryBar'
import FleetCard from './components/FleetCard'

const DATE_RE = /^\d{4}-\d{2}-\d{2}$/

function dateFromUrl(): string {
  const param = new URLSearchParams(window.location.search).get('date')
  return param !== null && DATE_RE.test(param) ? param : todayISO()
}

export default function DayPage() {
  const [date, setDate] = useState(dateFromUrl)
  const [incomeCategories, setIncomeCategories] = useState<Category[]>([])
  const [expenseCategories, setExpenseCategories] = useState<Category[]>([])
  const [transactions, setTransactions] = useState<Transaction[]>([])
  const [report, setReport] = useState<DailyReport | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)

  /** Her işlem/silme/düzenleme/filo kaydı sonrası gün verisi yeniden çekilir. */
  const refresh = useCallback(() => setTick(t => t + 1), [])

  function changeDate(next: string) {
    if (!DATE_RE.test(next)) return
    setDate(next)
    const url = new URL(window.location.href)
    url.searchParams.set('date', next)
    window.history.pushState(null, '', url)
  }

  // Tarayıcı geri/ileri okları URL'deki tarihe döndürür.
  useEffect(() => {
    const onPop = () => setDate(dateFromUrl())
    window.addEventListener('popstate', onPop)
    return () => window.removeEventListener('popstate', onPop)
  }, [])

  // İlk açılışta URL'de tarih yoksa bugünü yaz: yenileme ve link paylaşımı korunur.
  useEffect(() => {
    const url = new URL(window.location.href)
    if (!DATE_RE.test(url.searchParams.get('date') ?? '')) {
      url.searchParams.set('date', dateFromUrl())
      window.history.replaceState(null, '', url)
    }
  }, [])

  useEffect(() => {
    let cancelled = false
    Promise.all([getCategories('Income'), getCategories('Expense')])
      .then(([income, expense]) => {
        if (cancelled) return
        setIncomeCategories(income)
        setExpenseCategories(expense)
      })
      .catch(err => {
        if (!cancelled) setLoadError(errorMessage(err))
      })
    return () => {
      cancelled = true
    }
  }, [])

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

  const reloadExpenseCategories = useCallback(async () => {
    setExpenseCategories(await getCategories('Expense'))
  }, [])

  return (
    <div className="page">
      <header className="page-header">
        <h1>Günlük Kasa</h1>
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
          <a className="rapor-nav-link" href={`/rapor?date=${date}`}>
            Rapor Görünümü
          </a>
          <a className="rapor-nav-link" href={`/ay?month=${date.slice(0, 7)}`}>
            Ay Görünümü
          </a>
          <button
            type="button"
            className="btn-primary btn-small"
            onClick={() => window.open(`/api/reports/daily/pdf?date=${date}`)}
          >
            PDF
          </button>
          <LogoutButton />
        </nav>
      </header>

      {report?.fleetMissing && <div className="banner banner-warn">⚠ Bugünün filo verisi girilmedi</div>}
      {loadError && <div className="banner banner-error">{loadError}</div>}

      <div className="forms-grid">
        <TxnForm title="Gelir Ekle" type="Income" date={date} categories={incomeCategories} onSaved={refresh} />
        <TxnForm
          title="Gider Ekle"
          type="Expense"
          date={date}
          categories={expenseCategories}
          onSaved={refresh}
          onCategoriesChanged={reloadExpenseCategories}
        />
      </div>

      <TxnList transactions={transactions} onChanged={refresh} />

      <FleetCard date={date} fleet={report?.fleet ?? null} onSaved={refresh} />

      <SummaryBar report={report} />
    </div>
  )
}
