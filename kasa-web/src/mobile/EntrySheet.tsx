import { useEffect, useState } from 'react'
import type { Category, PaymentMethod, TransactionType } from '../api'
import { createTransaction, errorMessage } from '../api'
import { formatSatang } from '../format'
import { PAYMENT_METHODS } from '../labels'
import CategoryModal from '../components/CategoryModal'

interface Props {
  date: string
  incomeCategories: Category[]
  expenseCategories: Category[]
  onCategoriesChanged: (type: TransactionType) => Promise<void>
  onSaved: () => void
  showToast: (message: string, color: string) => void
  onClose: () => void
}

const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '0', 'del'] as const

/**
 * Keypad tuşu tutar STRING'ini düzenler; sayısal çeviri ve doğrulama server'da
 * (BahtParser, I1). Kurallar BahtParser ile aynı: en fazla 2 ondalık hane,
 * ikinci nokta engellenir.
 */
function pressKey(prev: string, key: string): string {
  if (key === 'del') return prev.slice(0, -1)
  if (key === '.') {
    if (prev.includes('.')) return prev
    return prev === '' ? '0.' : `${prev}.`
  }
  const dot = prev.indexOf('.')
  if (dot !== -1 && prev.length - dot - 1 >= 2) return prev
  if (prev === '0') return key
  if (prev.replace('.', '').length >= 9) return prev
  return `${prev}${key}`
}

/** FAB'dan açılan giriş sheet'i: Gelir/Gider + keypad + kategori chip'leri + ödeme + not. */
export default function EntrySheet({
  date,
  incomeCategories,
  expenseCategories,
  onCategoriesChanged,
  onSaved,
  showToast,
  onClose,
}: Props) {
  const [type, setType] = useState<TransactionType>('Income')
  const [categoryId, setCategoryId] = useState<number | ''>('')
  const [payment, setPayment] = useState<PaymentMethod>('Cash')
  const [amount, setAmount] = useState('')
  const [note, setNote] = useState('')
  const [noteOpen, setNoteOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [modalOpen, setModalOpen] = useState(false)

  const categories = type === 'Income' ? incomeCategories : expenseCategories

  // Seçim listede yoksa (ilk açılış, tür değişti, liste yenilendi) ilk kategoriye düş.
  useEffect(() => {
    if (categories.length === 0) {
      setCategoryId('')
    } else if (categoryId === '' || !categories.some(c => c.id === categoryId)) {
      setCategoryId(categories[0].id)
    }
  }, [categories, categoryId])

  function switchType(next: TransactionType) {
    if (next === type) return
    setType(next)
    setCategoryId('')
    setError(null)
  }

  async function save(keepOpen: boolean) {
    if (saving) return
    if (categoryId === '') {
      setError('Önce bir kategori seçin.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      const created = await createTransaction({
        date,
        type,
        categoryId,
        paymentMethod: payment,
        amount: amount.trim(),
        note: note.trim() === '' ? null : note.trim(),
      })
      // Toast tutarı server yanıtından biçimlenir; client parse yok (I1).
      showToast(
        `${type === 'Income' ? 'Gelir' : 'Gider'} eklendi · ${formatSatang(created.amountSatang)}`,
        type === 'Income' ? '#34C77A' : '#F2565B'
      )
      onSaved()
      if (keepOpen) {
        // Ardışık giriş: tutar+not sıfırlanır, kategori ve ödeme KORUNUR.
        setAmount('')
        setNote('')
        setNoteOpen(false)
        setSaving(false)
      } else {
        onClose()
      }
    } catch (err) {
      setError(errorMessage(err))
      setSaving(false)
    }
  }

  async function handleCategoryCreated(created: Category) {
    await onCategoriesChanged(type)
    setCategoryId(created.id)
    setModalOpen(false)
  }

  const accentClass = type === 'Income' ? 'm-accent-income' : 'm-accent-expense'

  return (
    <>
      <div className="m-backdrop" onClick={onClose} />
      <div className="m-sheet m-entry-sheet" role="dialog" aria-modal="true" aria-label="Yeni işlem">
        <div className="m-grabber" />

        <div className="m-sheet-top">
          <div className="m-type-toggle">
            <button
              type="button"
              className={`m-type-btn ${type === 'Income' ? 'm-type-btn-income' : ''}`}
              onClick={() => switchType('Income')}
            >
              Gelir
            </button>
            <button
              type="button"
              className={`m-type-btn ${type === 'Expense' ? 'm-type-btn-expense' : ''}`}
              onClick={() => switchType('Expense')}
            >
              Gider
            </button>
          </div>
          <button type="button" className="m-sheet-close" onClick={onClose} aria-label="Kapat">
            ✕
          </button>
        </div>

        <div className="m-amount-wrap">
          <div className="m-amount-label">Tutar</div>
          <div className={`m-amount-display ${accentClass}`} data-testid="sheet-amount">
            ฿{amount === '' ? '0' : amount}
          </div>
        </div>

        <div className="m-chips">
          {categories.map(c => (
            <button
              key={c.id}
              type="button"
              className={`m-chip ${categoryId === c.id ? `m-chip-active ${accentClass}` : ''}`}
              onClick={() => setCategoryId(c.id)}
            >
              {c.name}
            </button>
          ))}
          <button type="button" className="m-chip m-chip-new" onClick={() => setModalOpen(true)}>
            ＋ Yeni
          </button>
        </div>

        <div className="m-seg m-sheet-pay">
          {PAYMENT_METHODS.map(m => (
            <button
              key={m.value}
              type="button"
              className={`m-seg-btn ${payment === m.value ? 'm-seg-btn-active' : ''}`}
              onClick={() => setPayment(m.value)}
            >
              {m.short}
            </button>
          ))}
        </div>

        <div className="m-note-row">
          {noteOpen ? (
            <input
              className="m-note-input"
              value={note}
              onChange={e => setNote(e.target.value)}
              placeholder="Not"
              maxLength={500}
              autoFocus
            />
          ) : (
            <button type="button" className="m-note-toggle" onClick={() => setNoteOpen(true)}>
              ＋ Not ekle{note.trim() !== '' ? ` · ${note}` : ''}
            </button>
          )}
        </div>

        <div className="m-keypad">
          {KEYS.map(k => (
            <button
              key={k}
              type="button"
              className="m-key"
              onClick={() => setAmount(a => pressKey(a, k))}
              aria-label={k === 'del' ? 'Sil' : k}
            >
              {k === 'del' ? '⌫' : k}
            </button>
          ))}
        </div>

        {error && <p className="m-error">{error}</p>}

        <div className="m-sheet-actions">
          <button
            type="button"
            className={`m-btn-continue ${accentClass}`}
            onClick={() => save(true)}
            disabled={saving || amount === ''}
          >
            Ekle ve devam
          </button>
          <button
            type="button"
            className={`m-btn-save ${accentClass}`}
            onClick={() => save(false)}
            disabled={saving || amount === ''}
          >
            Kaydet
          </button>
        </div>
      </div>

      {modalOpen && (
        <CategoryModal type={type} onCreated={handleCategoryCreated} onClose={() => setModalOpen(false)} />
      )}
    </>
  )
}
