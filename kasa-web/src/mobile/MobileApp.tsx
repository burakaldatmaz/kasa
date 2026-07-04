import { useCallback, useEffect, useRef, useState } from 'react'
import type { Category, TransactionType } from '../api'
import { getCategories } from '../api'
import { todayISO } from '../dates'
import AyScreen from './AyScreen'
import DepozitoScreen from './DepozitoScreen'
import EntrySheet from './EntrySheet'
import FiloScreen from './FiloScreen'
import GunScreen from './GunScreen'
import RaporScreen from './RaporScreen'
import { IconAy, IconDepozito, IconFilo, IconGun, IconPlus, IconRapor } from './icons'

export type Tab = 'gun' | 'ay' | 'rapor' | 'filo' | 'depozito'

const DATE_RE = /^\d{4}-\d{2}-\d{2}$/
const MONTH_RE = /^\d{4}-\d{2}$/

interface NavState {
  tab: Tab
  date: string
  month: string
}

function stateFromUrl(): NavState {
  const q = new URLSearchParams(window.location.search)
  const dateParam = q.get('date') ?? ''
  const monthParam = q.get('month') ?? ''
  const date = DATE_RE.test(dateParam) ? dateParam : todayISO()
  const month = MONTH_RE.test(monthParam) ? monthParam : date.slice(0, 7)
  switch (window.location.pathname) {
    case '/ay':
      return { tab: 'ay', date, month }
    case '/rapor':
      return { tab: 'rapor', date, month }
    case '/filo':
      return { tab: 'filo', date, month }
    case '/depozito':
      return { tab: 'depozito', date, month }
    default:
      return { tab: 'gun', date, month }
  }
}

/** Desktop URL şemasıyla aynı kalır ki kırılım değişince sayfa karşılığı korunur. */
function urlFor(nav: NavState): string {
  switch (nav.tab) {
    case 'ay':
      return `/ay?month=${nav.month}`
    case 'rapor':
      return `/rapor?date=${nav.date}`
    case 'filo':
      return `/filo?date=${nav.date}`
    case 'depozito':
      return `/depozito?date=${nav.date}`
    default:
      return `/?date=${nav.date}`
  }
}

/**
 * ≤640px sekmeli mobil uygulama: Gün | Ay | [+] | Rapor | Filo.
 * Tüm tutar/yüzde değerleri API'den hazır gelir; burada yalnız gezinme,
 * kategori listeleri ve toast durumu tutulur (I1).
 */
export default function MobileApp() {
  const [nav, setNav] = useState(stateFromUrl)
  // Her işlem/silme/düzenleme/filo kaydı sonrası aktif ekran yeniden çeker.
  const [tick, setTick] = useState(0)
  const [sheetOpen, setSheetOpen] = useState(false)
  const [toast, setToast] = useState<{ message: string; color: string } | null>(null)
  const [incomeCategories, setIncomeCategories] = useState<Category[]>([])
  const [expenseCategories, setExpenseCategories] = useState<Category[]>([])
  const toastTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  const refresh = useCallback(() => setTick(t => t + 1), [])

  const navigate = useCallback((next: Partial<NavState>) => {
    setNav(prev => {
      const merged = { ...prev, ...next }
      window.history.pushState(null, '', urlFor(merged))
      return merged
    })
  }, [])

  // Tarayıcı geri/ileri okları URL'deki sekme+tarihe döndürür.
  useEffect(() => {
    const onPop = () => setNav(stateFromUrl())
    window.addEventListener('popstate', onPop)
    return () => window.removeEventListener('popstate', onPop)
  }, [])

  // İlk açılışta URL'i normalize et (bugünün tarihi yazılır, link paylaşımı korunur).
  useEffect(() => {
    window.history.replaceState(null, '', urlFor(stateFromUrl()))
  }, [])

  useEffect(() => {
    let cancelled = false
    Promise.all([getCategories('Income'), getCategories('Expense')])
      .then(([income, expense]) => {
        if (cancelled) return
        setIncomeCategories(income)
        setExpenseCategories(expense)
      })
      .catch(() => {
        // Kategori listesi alınamazsa sheet boş chip'lerle açılır; ekranlar kendi hatasını gösterir.
      })
    return () => {
      cancelled = true
    }
  }, [])

  const reloadCategories = useCallback(async (type: TransactionType) => {
    const list = await getCategories(type)
    if (type === 'Income') setIncomeCategories(list)
    else setExpenseCategories(list)
  }, [])

  const showToast = useCallback((message: string, color: string) => {
    clearTimeout(toastTimer.current)
    setToast({ message, color })
    toastTimer.current = setTimeout(() => setToast(null), 2200)
  }, [])

  useEffect(() => () => clearTimeout(toastTimer.current), [])

  const tabs: Array<{ key: Tab; label: string; icon: () => React.JSX.Element }> = [
    { key: 'gun', label: 'Gün', icon: IconGun },
    { key: 'ay', label: 'Ay', icon: IconAy },
    { key: 'rapor', label: 'Rapor', icon: IconRapor },
    { key: 'filo', label: 'Filo', icon: IconFilo },
    { key: 'depozito', label: 'Depozito', icon: IconDepozito },
  ]

  function tabButton(tab: { key: Tab; label: string; icon: () => React.JSX.Element }) {
    const Icon = tab.icon
    return (
      <button
        key={tab.key}
        type="button"
        className={`m-tab ${nav.tab === tab.key ? 'm-tab-active' : ''}`}
        onClick={() => navigate({ tab: tab.key })}
        aria-current={nav.tab === tab.key ? 'page' : undefined}
      >
        <Icon />
        <span>{tab.label}</span>
      </button>
    )
  }

  return (
    <div className="m-app">
      {nav.tab === 'gun' && (
        <GunScreen
          date={nav.date}
          tick={tick}
          onDateChange={date => navigate({ date, month: date.slice(0, 7) })}
          onOpenFilo={() => navigate({ tab: 'filo' })}
          onChanged={refresh}
          showToast={showToast}
        />
      )}
      {nav.tab === 'ay' && (
        <AyScreen
          month={nav.month}
          tick={tick}
          onMonthChange={month => navigate({ month })}
          onOpenDay={date => navigate({ tab: 'rapor', date, month: date.slice(0, 7) })}
        />
      )}
      {nav.tab === 'rapor' && <RaporScreen date={nav.date} tick={tick} />}
      {nav.tab === 'filo' && (
        <FiloScreen date={nav.date} onChanged={refresh} showToast={showToast} />
      )}
      {nav.tab === 'depozito' && (
        <DepozitoScreen
          date={nav.date}
          tick={tick}
          onDateChange={date => navigate({ date, month: date.slice(0, 7) })}
          onChanged={refresh}
          showToast={showToast}
        />
      )}

      <nav className="m-tabbar" aria-label="Ana gezinme">
        {tabButton(tabs[0])}
        {tabButton(tabs[1])}
        <div className="m-fab-slot">
          <button type="button" className="m-fab" aria-label="Yeni işlem ekle" onClick={() => setSheetOpen(true)}>
            <IconPlus />
          </button>
        </div>
        {tabButton(tabs[2])}
        {tabButton(tabs[3])}
        {tabButton(tabs[4])}
      </nav>

      {sheetOpen && (
        <EntrySheet
          date={nav.date}
          incomeCategories={incomeCategories}
          expenseCategories={expenseCategories}
          onCategoriesChanged={reloadCategories}
          onSaved={refresh}
          showToast={showToast}
          onClose={() => setSheetOpen(false)}
        />
      )}

      {toast && (
        <div className="m-toast" role="status">
          <span className="m-toast-dot" style={{ color: toast.color }}>
            ●
          </span>
          {toast.message}
        </div>
      )}
    </div>
  )
}
