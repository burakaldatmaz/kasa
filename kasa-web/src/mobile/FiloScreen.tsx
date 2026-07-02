import { useEffect, useState } from 'react'
import type { FleetSnapshot } from '../api'
import { ApiError, errorMessage, getFleet, saveFleet } from '../api'
import { formatDateLong } from '../dates'
import FleetRing from './FleetRing'

interface Props {
  date: string
  onChanged: () => void
  showToast: (message: string, color: string) => void
}

interface StepperProps {
  label: string
  value: string
  accent?: 'income' | 'expense'
  /** Boş değerde göstergede ne görünür; rezervasyon stepper'ları "—" ile başlar (null = girilmedi). */
  emptyHint?: string
  onChange: (next: string) => void
}

/**
 * ± günlük düzeltme için, sayıya dokununca doğrudan yazma ilk kayıt için.
 * Client-side clamp yok: kısıt ihlali kaydet sonrası API 400 mesajıyla döner
 * (tek doğruluk kaynağı server).
 */
function Stepper({ label, value, accent, emptyHint, onChange }: StepperProps) {
  const n = Number(value || '0')
  return (
    <div className="m-stepper-row">
      <span className="m-stepper-label">{label}</span>
      <div className="m-stepper-controls">
        <button
          type="button"
          className={`m-stepper-btn m-stepper-btn-${accent ?? 'neutral'}`}
          onClick={() => onChange(String(n - 1))}
          disabled={value === '' || n <= 0}
          aria-label={`${label} azalt`}
        >
          −
        </button>
        <input
          className="m-stepper-value"
          value={value}
          placeholder={emptyHint}
          onChange={e => onChange(e.target.value.replace(/[^\d]/g, ''))}
          inputMode="numeric"
          autoComplete="off"
          aria-label={label}
        />
        <button
          type="button"
          className={`m-stepper-btn m-stepper-btn-${accent ?? 'neutral'}`}
          onClick={() => onChange(String(n + 1))}
          aria-label={`${label} artır`}
        >
          ＋
        </button>
      </div>
    </div>
  )
}

/** Filo ekranı: kayıtlı snapshot halkası + stepper'lı giriş. Yüzde API'den (I1). */
export default function FiloScreen({ date, onChanged, showToast }: Props) {
  const [snapshot, setSnapshot] = useState<FleetSnapshot | null>(null)
  const [loaded, setLoaded] = useState(false)
  const [total, setTotal] = useState('')
  const [rented, setRented] = useState('')
  const [broken, setBroken] = useState('')
  // Rezervasyon sayaçları: '' = "girilmedi" → kayıtta null gider (K2), stepper "—" gösterir.
  const [started, setStarted] = useState('')
  const [ended, setEnded] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  useEffect(() => {
    let cancelled = false
    setLoaded(false)
    getFleet(date)
      .then(snap => {
        if (cancelled) return
        setSnapshot(snap)
        setTotal(String(snap.totalBikes))
        setRented(String(snap.rentedBikes))
        setBroken(String(snap.brokenBikes))
        setStarted(snap.startedReservations != null ? String(snap.startedReservations) : '')
        setEnded(snap.endedReservations != null ? String(snap.endedReservations) : '')
        setError(null)
        setLoaded(true)
      })
      .catch(err => {
        if (cancelled) return
        // 404 = o güne kayıt yok: boş durum + ilk kayıt formu.
        setSnapshot(null)
        setTotal('')
        setRented('')
        setBroken('')
        setStarted('')
        setEnded('')
        setError(err instanceof ApiError && err.status === 404 ? null : errorMessage(err))
        setLoaded(true)
      })
    return () => {
      cancelled = true
    }
  }, [date])

  async function handleSave() {
    if (saving) return
    if (total.trim() === '' || rented.trim() === '' || broken.trim() === '') {
      setError('Üç alan da doldurulmalıdır.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      const saved = await saveFleet(date, {
        totalBikes: Number(total),
        brokenBikes: Number(broken),
        rentedBikes: Number(rented),
        // Boş bırakılan sayaç null gider: "girilmedi" 0'a çevrilmez (K2).
        startedReservations: started.trim() === '' ? null : Number(started),
        endedReservations: ended.trim() === '' ? null : Number(ended),
      })
      setSnapshot(saved)
      setStarted(saved.startedReservations != null ? String(saved.startedReservations) : '')
      setEnded(saved.endedReservations != null ? String(saved.endedReservations) : '')
      showToast('Filo durumu kaydedildi', '#34C77A')
      onChanged()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="m-screen">
      <header className="m-screen-head">
        <div>
          <h1 className="m-title">Filo</h1>
          <div className="m-subtitle">{formatDateLong(date)}</div>
        </div>
      </header>

      {loaded && (
        <>
          {snapshot ? (
            <section className="m-filo-card">
              <FleetRing size={150} radius={62} strokeWidth={15} percent={snapshot.rentalPercent}>
                <span className="m-ring-pct-big">
                  {snapshot.rentalPercent != null ? `%${snapshot.rentalPercent.toFixed(0)}` : '—'}
                </span>
                <span className="m-ring-caption">KİRALAMA</span>
              </FleetRing>
              <div className="m-filo-stats">
                <div className="m-filo-stat">
                  <div className="m-filo-stat-num m-income-text">{snapshot.rentedBikes}</div>
                  <div className="m-filo-stat-label">Kirada</div>
                </div>
                <div className="m-filo-stat">
                  <div className="m-filo-stat-num m-filo-stat-idle">{snapshot.idleBikes}</div>
                  <div className="m-filo-stat-label">Boşta</div>
                </div>
                <div className="m-filo-stat">
                  <div className="m-filo-stat-num m-expense-text">{snapshot.brokenBikes}</div>
                  <div className="m-filo-stat-label">Arızalı</div>
                </div>
              </div>
            </section>
          ) : (
            <section className="m-filo-card m-filo-empty">Bugün için filo verisi yok</section>
          )}

          <div className="m-list m-steppers">
            <Stepper label="Toplam bisiklet" value={total} onChange={setTotal} />
            <Stepper label="Kirada" value={rented} accent="income" onChange={setRented} />
            <Stepper label="Arızalı" value={broken} accent="expense" onChange={setBroken} />
          </div>

          <h2 className="m-section-label">Bugünün Rezervasyonları</h2>
          <div className="m-list m-steppers">
            <Stepper label="Başlayan" value={started} accent="income" emptyHint="—" onChange={setStarted} />
            <Stepper label="Biten" value={ended} accent="expense" emptyHint="—" onChange={setEnded} />
          </div>

          {error && <p className="m-error m-error-block">{error}</p>}

          <button type="button" className="m-primary-btn" onClick={handleSave} disabled={saving}>
            Filo durumunu kaydet
          </button>
        </>
      )}
    </div>
  )
}
