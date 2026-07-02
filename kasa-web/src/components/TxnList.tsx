import { useState } from 'react'
import type { FormEvent } from 'react'
import type { PaymentMethod, Transaction } from '../api'
import { deleteTransaction, errorMessage, updateTransaction } from '../api'
import { formatSatang, satangToBahtInput } from '../format'
import { PAYMENT_METHODS, paymentLabel } from '../labels'

interface Props {
  transactions: Transaction[]
  onChanged: () => void
}

export default function TxnList({ transactions, onChanged }: Props) {
  const [editingId, setEditingId] = useState<number | null>(null)
  const [editAmount, setEditAmount] = useState('')
  const [editNote, setEditNote] = useState('')
  const [editPayment, setEditPayment] = useState<PaymentMethod>('Cash')
  const [editError, setEditError] = useState<string | null>(null)
  const [listError, setListError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const incomes = transactions.filter(t => t.type === 'Income')
  const expenses = transactions.filter(t => t.type === 'Expense')

  function startEdit(t: Transaction) {
    setEditingId(t.id)
    setEditAmount(satangToBahtInput(t.amountSatang))
    setEditNote(t.note ?? '')
    setEditPayment(t.paymentMethod)
    setEditError(null)
    setListError(null)
  }

  async function saveEdit(e: FormEvent<HTMLFormElement>, t: Transaction) {
    e.preventDefault()
    if (busy) return
    setBusy(true)
    setEditError(null)
    try {
      await updateTransaction(t.id, {
        date: t.date,
        type: t.type,
        categoryId: t.categoryId,
        paymentMethod: editPayment,
        amount: editAmount.trim(),
        note: editNote.trim() === '' ? null : editNote.trim(),
      })
      setEditingId(null)
      onChanged()
    } catch (err) {
      setEditError(errorMessage(err))
    } finally {
      setBusy(false)
    }
  }

  async function remove(t: Transaction) {
    if (!window.confirm(`"${t.categoryName} — ${formatSatang(t.amountSatang)}" işlemi silinsin mi?`)) return
    setListError(null)
    try {
      await deleteTransaction(t.id)
      onChanged()
    } catch (err) {
      setListError(errorMessage(err))
    }
  }

  function renderRow(t: Transaction) {
    if (editingId === t.id) {
      return (
        <li key={t.id} className="txn-row txn-row-edit">
          <form className="txn-edit-form" onSubmit={e => saveEdit(e, t)}>
            <span className="txn-edit-category">{t.categoryName}</span>
            <select value={editPayment} onChange={e => setEditPayment(e.target.value as PaymentMethod)}>
              {PAYMENT_METHODS.map(m => (
                <option key={m.value} value={m.value}>
                  {m.label}
                </option>
              ))}
            </select>
            <input
              className="txn-edit-amount"
              value={editAmount}
              onChange={e => setEditAmount(e.target.value)}
              inputMode="decimal"
              autoFocus
              aria-label="Tutar"
            />
            <input
              className="txn-edit-note"
              value={editNote}
              onChange={e => setEditNote(e.target.value)}
              placeholder="Not"
              maxLength={500}
              aria-label="Not"
            />
            <button type="submit" className="btn-primary btn-small" disabled={busy}>
              Kaydet
            </button>
            <button
              type="button"
              className="btn-secondary btn-small"
              onClick={() => setEditingId(null)}
              disabled={busy}
            >
              Vazgeç
            </button>
            {editError && <p className="form-error txn-edit-error">{editError}</p>}
          </form>
        </li>
      )
    }
    return (
      <li key={t.id} className="txn-row">
        <div className="txn-main">
          <span className="txn-text">
            {t.categoryName} ({paymentLabel(t.paymentMethod)}) — {formatSatang(t.amountSatang)}
          </span>
          <span className="txn-actions">
            <button type="button" className="btn-link" onClick={() => startEdit(t)}>
              Düzenle
            </button>
            <button type="button" className="btn-link btn-link-danger" onClick={() => remove(t)}>
              Sil
            </button>
          </span>
        </div>
        {t.note && <div className="txn-note">{t.note}</div>}
      </li>
    )
  }

  function renderBlock(title: string, rows: Transaction[], accent: 'income' | 'expense') {
    return (
      <div className={`txn-block txn-block-${accent}`}>
        <h3 className="txn-block-title">{title}</h3>
        {rows.length === 0 ? (
          <p className="txn-empty">Kayıt yok.</p>
        ) : (
          <ul className="txn-rows">{rows.map(renderRow)}</ul>
        )}
      </div>
    )
  }

  return (
    <section className="card">
      <h2 className="card-header">Günün İşlemleri</h2>
      <div className="card-body txn-blocks">
        {renderBlock('Gelir', incomes, 'income')}
        {renderBlock('Gider', expenses, 'expense')}
      </div>
      {listError && <p className="form-error txn-list-error">{listError}</p>}
    </section>
  )
}
