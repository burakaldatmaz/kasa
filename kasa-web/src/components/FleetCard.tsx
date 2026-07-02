import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import type { DailyFleet } from '../api'
import { errorMessage, saveFleet } from '../api'

interface Props {
  date: string
  fleet: DailyFleet | null
  onSaved: () => void
}

export default function FleetCard({ date, fleet, onSaved }: Props) {
  const [total, setTotal] = useState('')
  const [broken, setBroken] = useState('')
  const [rented, setRented] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  // Kayıtlı değerleri doldur; aynı veri tekrar geldiğinde (alakasız bir rapor
  // yenilemesi) kullanıcının yazmakta olduğu değerleri ezme.
  const appliedRef = useRef('')
  useEffect(() => {
    const key = `${date}|${JSON.stringify(fleet)}`
    if (appliedRef.current === key) return
    appliedRef.current = key
    setTotal(fleet ? String(fleet.totalBikes) : '')
    setBroken(fleet ? String(fleet.brokenBikes) : '')
    setRented(fleet ? String(fleet.rentedBikes) : '')
    setError(null)
  }, [date, fleet])

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (saving) return
    if (total.trim() === '' || broken.trim() === '' || rented.trim() === '') {
      setError('Üç alan da doldurulmalıdır.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      await saveFleet(date, {
        totalBikes: Number(total),
        brokenBikes: Number(broken),
        rentedBikes: Number(rented),
      })
      onSaved()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="card fleet-card">
      <h2 className="card-header">
        <span>Filo</span>
        <span className="fleet-badges">
          {fleet?.rentalPercent != null && (
            <span className="badge badge-info">Kiralama %{fleet.rentalPercent.toFixed(1)}</span>
          )}
          {fleet?.brokenAlert && (
            <span className="badge badge-warn">{fleet.brokenBikes} arızalı motor</span>
          )}
        </span>
      </h2>
      <form onSubmit={handleSubmit} className="card-body">
        <div className="field-row">
          <label className="field">
            <span>Filo Toplamı</span>
            <input
              type="number"
              inputMode="numeric"
              min={0}
              step={1}
              value={total}
              onChange={e => setTotal(e.target.value)}
            />
          </label>
          <label className="field">
            <span>Arızalı</span>
            <input
              type="number"
              inputMode="numeric"
              min={0}
              step={1}
              value={broken}
              onChange={e => setBroken(e.target.value)}
            />
          </label>
          <label className="field">
            <span>Kirada</span>
            <input
              type="number"
              inputMode="numeric"
              min={0}
              step={1}
              value={rented}
              onChange={e => setRented(e.target.value)}
            />
          </label>
          <button type="submit" className="btn-primary btn-submit" disabled={saving}>
            Kaydet
          </button>
        </div>
        {error && <p className="form-error">{error}</p>}
      </form>
    </section>
  )
}
