import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import type { Category, PaymentMethod, TransactionType } from '../api'
import { createTransaction, errorMessage } from '../api'
import { PAYMENT_METHODS } from '../labels'
import CategoryModal from './CategoryModal'

interface Props {
  title: string
  type: TransactionType
  date: string
  categories: Category[]
  onSaved: () => void
  /** Verildiğinde "＋ Yeni Kategori" düğmesi görünür (gider formu). */
  onCategoriesChanged?: () => Promise<void>
}

/**
 * Ortak gelir/gider formu. Hız kritik: Enter submit eder, başarıda tutar+not
 * sıfırlanır ve odak tutara döner; kategori ve ödeme yöntemi son seçimde kalır.
 */
export default function TxnForm({ title, type, date, categories, onSaved, onCategoriesChanged }: Props) {
  const [categoryId, setCategoryId] = useState<number | ''>('')
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('Cash')
  const [amount, setAmount] = useState('')
  const [note, setNote] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  const [modalOpen, setModalOpen] = useState(false)
  const amountRef = useRef<HTMLInputElement>(null)

  // Seçim listede yoksa (ilk yükleme, kategori listesi değişti) ilk kategoriye düş.
  useEffect(() => {
    if (categories.length === 0) {
      setCategoryId('')
    } else if (categoryId === '' || !categories.some(c => c.id === categoryId)) {
      setCategoryId(categories[0].id)
    }
  }, [categories, categoryId])

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (saving) return
    if (categoryId === '') {
      setError('Önce bir kategori seçin.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      await createTransaction({
        date,
        type,
        categoryId,
        paymentMethod,
        amount: amount.trim(),
        note: note.trim() === '' ? null : note.trim(),
      })
      setAmount('')
      setNote('')
      // preventScroll: iOS Safari'de odak dönerken sayfa zıplamasın, klavye açık kalsın.
      amountRef.current?.focus({ preventScroll: true })
      onSaved()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  async function handleCategoryCreated(created: Category) {
    await onCategoriesChanged?.()
    setCategoryId(created.id)
    setModalOpen(false)
  }

  return (
    <section className={`card txn-form txn-form-${type.toLowerCase()}`}>
      <h2 className="card-header">{title}</h2>
      <form onSubmit={handleSubmit} className="card-body">
        <div className="field-row">
          <label className="field field-grow">
            <span>Kategori</span>
            <div className="field-inline">
              <select
                value={categoryId}
                onChange={e => setCategoryId(Number(e.target.value))}
                disabled={categories.length === 0}
              >
                {categories.length === 0 && <option value="">Kategori yok</option>}
                {categories.map(c => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
              {onCategoriesChanged && (
                <button
                  type="button"
                  className="btn-secondary btn-new-category"
                  onClick={() => setModalOpen(true)}
                >
                  ＋ Yeni Kategori
                </button>
              )}
            </div>
          </label>
          <label className="field">
            <span>Ödeme yöntemi</span>
            <select value={paymentMethod} onChange={e => setPaymentMethod(e.target.value as PaymentMethod)}>
              {PAYMENT_METHODS.map(m => (
                <option key={m.value} value={m.value}>
                  {m.label}
                </option>
              ))}
            </select>
          </label>
        </div>
        <div className="field-row">
          <label className="field">
            <span>Tutar (฿)</span>
            <input
              ref={amountRef}
              value={amount}
              onChange={e => setAmount(e.target.value)}
              placeholder="örn: 1000 veya 1000.50"
              inputMode="decimal"
              autoComplete="off"
            />
          </label>
          <label className="field field-grow">
            <span>Not (opsiyonel)</span>
            <input
              value={note}
              onChange={e => setNote(e.target.value)}
              maxLength={500}
              autoComplete="off"
            />
          </label>
          <button type="submit" className="btn-primary btn-submit" disabled={saving}>
            Ekle
          </button>
        </div>
        {error && <p className="form-error">{error}</p>}
      </form>
      {modalOpen && (
        <CategoryModal type={type} onCreated={handleCategoryCreated} onClose={() => setModalOpen(false)} />
      )}
    </section>
  )
}
