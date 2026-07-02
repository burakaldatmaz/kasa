import { useState } from 'react'
import type { FormEvent } from 'react'
import type { PaymentMethod, Transaction } from '../api'
import { deleteTransaction, errorMessage, updateTransaction } from '../api'
import { formatSatang, satangToBahtInput } from '../format'
import { PAYMENT_METHODS } from '../labels'

interface Props {
  txn: Transaction
  onClose: () => void
  onChanged: () => void
  showToast: (message: string, color: string) => void
}

/**
 * İşlem satırına dokununca açılan alt eylem sheet'i: Düzenle / Sil.
 * Desktop'taki inline düzenleme mobilde burada yapılır; API 400 mesajı
 * sheet içinde gösterilir, sheet kapanmaz.
 */
export default function TxnActionSheet({ txn, onClose, onChanged, showToast }: Props) {
  const [mode, setMode] = useState<'menu' | 'edit'>('menu')
  const [amount, setAmount] = useState(() => satangToBahtInput(txn.amountSatang))
  const [note, setNote] = useState(txn.note ?? '')
  const [payment, setPayment] = useState<PaymentMethod>(txn.paymentMethod)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const accent = txn.type === 'Income' ? '#34C77A' : '#F2565B'

  async function remove() {
    if (busy) return
    if (!window.confirm(`"${txn.categoryName} — ${formatSatang(txn.amountSatang)}" işlemi silinsin mi?`)) return
    setBusy(true)
    setError(null)
    try {
      await deleteTransaction(txn.id)
      showToast(`İşlem silindi · ${formatSatang(txn.amountSatang)}`, '#9AA0AA')
      onChanged()
      onClose()
    } catch (err) {
      setError(errorMessage(err))
      setBusy(false)
    }
  }

  async function saveEdit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setError(null)
    try {
      const updated = await updateTransaction(txn.id, {
        date: txn.date,
        type: txn.type,
        categoryId: txn.categoryId,
        paymentMethod: payment,
        amount: amount.trim(),
        note: note.trim() === '' ? null : note.trim(),
      })
      showToast(`İşlem güncellendi · ${formatSatang(updated.amountSatang)}`, accent)
      onChanged()
      onClose()
    } catch (err) {
      setError(errorMessage(err))
      setBusy(false)
    }
  }

  return (
    <>
      <div className="m-backdrop" onClick={onClose} />
      <div className="m-sheet m-action-sheet" role="dialog" aria-modal="true" aria-label="İşlem eylemleri">
        <div className="m-grabber" />
        <div className="m-action-title">
          <span className="m-action-cat">{txn.categoryName}</span>
          <span className={`m-action-amt ${txn.type === 'Income' ? 'm-income-text' : 'm-expense-text'}`}>
            {formatSatang(txn.amountSatang)}
          </span>
        </div>

        {mode === 'menu' ? (
          <div className="m-action-buttons">
            <button type="button" className="m-action-btn" onClick={() => setMode('edit')}>
              Düzenle
            </button>
            <button type="button" className="m-action-btn m-action-btn-danger" onClick={remove} disabled={busy}>
              Sil
            </button>
            <button type="button" className="m-action-btn m-action-btn-muted" onClick={onClose}>
              Vazgeç
            </button>
          </div>
        ) : (
          <form className="m-edit-form" onSubmit={saveEdit}>
            <div className="m-seg">
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
            <label className="m-edit-field">
              <span>Tutar (฿)</span>
              <input
                value={amount}
                onChange={e => setAmount(e.target.value)}
                inputMode="decimal"
                autoComplete="off"
                autoFocus
              />
            </label>
            <label className="m-edit-field">
              <span>Not (opsiyonel)</span>
              <input value={note} onChange={e => setNote(e.target.value)} maxLength={500} autoComplete="off" />
            </label>
            <div className="m-action-buttons">
              <button type="submit" className="m-action-btn m-action-btn-primary" disabled={busy}>
                Kaydet
              </button>
              <button type="button" className="m-action-btn m-action-btn-muted" onClick={onClose} disabled={busy}>
                Vazgeç
              </button>
            </div>
          </form>
        )}

        {error && <p className="m-error">{error}</p>}
      </div>
    </>
  )
}
