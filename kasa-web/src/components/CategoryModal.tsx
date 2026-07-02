import { useState } from 'react'
import type { FormEvent } from 'react'
import type { Category, TransactionType } from '../api'
import { createCategory, errorMessage } from '../api'

interface Props {
  type: TransactionType
  onCreated: (category: Category) => Promise<void>
  onClose: () => void
}

export default function CategoryModal({ type, onCreated, onClose }: Props) {
  const [name, setName] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (saving) return
    setSaving(true)
    setError(null)
    try {
      const created = await createCategory(name.trim(), type)
      await onCreated(created)
    } catch (err) {
      setError(errorMessage(err))
      setSaving(false)
    }
  }

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div
        className="modal"
        role="dialog"
        aria-modal="true"
        aria-label="Yeni kategori"
        onClick={e => e.stopPropagation()}
      >
        <div className="modal-header">Yeni Kategori</div>
        <form onSubmit={handleSubmit}>
          <label className="field">
            <span>Kategori adı</span>
            <input
              autoFocus
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder="örn: Benzin"
              maxLength={100}
            />
          </label>
          {error && <p className="form-error">{error}</p>}
          <div className="modal-actions">
            <button type="button" className="btn-secondary" onClick={onClose} disabled={saving}>
              Vazgeç
            </button>
            <button type="submit" className="btn-primary" disabled={saving || name.trim() === ''}>
              Kaydet
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
