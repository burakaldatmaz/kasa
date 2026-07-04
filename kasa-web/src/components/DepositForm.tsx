import { useState } from 'react'
import type { FormEvent } from 'react'
import type { DepositReceipt, PaymentMethod } from '../api'
import { createDepositReceipt, depositReceiptPdfUrl, errorMessage } from '../api'
import { dateTimeLocal, shiftDateTimeLocalDays } from '../dates'
import { PAYMENT_METHODS } from '../labels'

interface Props {
  /** Makbuz tarihi (üst tarih seçicisinden gelir; numara yılı bundan türer). */
  date: string
  onSaved: () => void
}

/**
 * Depozito makbuzu formu. Kaydet → makbuz oluşturulur → PDF yeni sekmede açılır (Ctrl+P).
 * Tutar BAHT string gönderilir, satang'a çeviri server'da (I1). ReturnExpectedAt teslim + 30 gün
 * önerisiyle başlar; kullanıcı düzenlerse öneri geri gelmez.
 */
export default function DepositForm({ date, onSaved }: Props) {
  const [customerName, setCustomerName] = useState('')
  const [vehicleModel, setVehicleModel] = useState('')
  const [vehicleColor, setVehicleColor] = useState('')
  const [plate, setPlate] = useState('')
  const [amount, setAmount] = useState('')
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('Cash')
  const [fuelLevel, setFuelLevel] = useState('Full')
  const [handoverAt, setHandoverAt] = useState(() => dateTimeLocal(date))
  const [returnExpectedAt, setReturnExpectedAt] = useState(() => shiftDateTimeLocalDays(dateTimeLocal(date), 30))
  const [returnEdited, setReturnEdited] = useState(false)
  const [limitKmPerDay, setLimitKmPerDay] = useState('150')
  const [limitRadiusKm, setLimitRadiusKm] = useState('150')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  function changeHandover(next: string) {
    setHandoverAt(next)
    // İade tarihi kullanıcı elle değiştirmediyse teslim + 30 gün önerisiyle güncellenir.
    if (!returnEdited) setReturnExpectedAt(shiftDateTimeLocalDays(next, 30))
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (saving) return
    setSaving(true)
    setError(null)
    try {
      const created: DepositReceipt = await createDepositReceipt({
        date,
        customerName: customerName.trim(),
        vehicleModel: vehicleModel.trim(),
        vehicleColor: vehicleColor.trim() === '' ? null : vehicleColor.trim(),
        plate: plate.trim(),
        amount: amount.trim(),
        paymentMethod,
        fuelLevel: fuelLevel.trim() === '' ? 'Full' : fuelLevel.trim(),
        handoverAt,
        returnExpectedAt,
        limitKmPerDay: Number(limitKmPerDay),
        limitRadiusKm: Number(limitRadiusKm),
      })
      // PDF yeni sekmede açılır → kullanıcı Ctrl+P ile yazdırır (günlük rapor PDF deseni).
      window.open(depositReceiptPdfUrl(created.id))
      // Bir sonraki müşteri için kimlik alanları temizlenir; ödeme/yakıt/limitler korunur.
      setCustomerName('')
      setVehicleModel('')
      setVehicleColor('')
      setPlate('')
      setAmount('')
      onSaved()
    } catch (err) {
      setError(errorMessage(err))
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="card txn-form">
      <h2 className="card-header">Yeni Depozito Makbuzu</h2>
      <form onSubmit={handleSubmit} className="card-body">
        <div className="field-row">
          <label className="field field-grow">
            <span>Müşteri adı</span>
            <input value={customerName} onChange={e => setCustomerName(e.target.value)} autoComplete="off" />
          </label>
          <label className="field">
            <span>Tutar (฿)</span>
            <input
              value={amount}
              onChange={e => setAmount(e.target.value)}
              placeholder="örn: 3000"
              inputMode="decimal"
              autoComplete="off"
            />
          </label>
        </div>

        <div className="field-row">
          <label className="field field-grow">
            <span>Araç modeli</span>
            <input
              value={vehicleModel}
              onChange={e => setVehicleModel(e.target.value)}
              placeholder="örn: Honda Click 160"
              autoComplete="off"
            />
          </label>
          <label className="field">
            <span>Renk (opsiyonel)</span>
            <input value={vehicleColor} onChange={e => setVehicleColor(e.target.value)} autoComplete="off" />
          </label>
        </div>

        <div className="field-row">
          <label className="field">
            <span>Plaka</span>
            <input value={plate} onChange={e => setPlate(e.target.value)} autoComplete="off" />
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
          <label className="field">
            <span>Yakıt</span>
            <input value={fuelLevel} onChange={e => setFuelLevel(e.target.value)} autoComplete="off" />
          </label>
        </div>

        <div className="field-row">
          <label className="field field-grow">
            <span>Teslim</span>
            <input type="datetime-local" value={handoverAt} onChange={e => changeHandover(e.target.value)} />
          </label>
          <label className="field field-grow">
            <span>Beklenen iade</span>
            <input
              type="datetime-local"
              value={returnExpectedAt}
              onChange={e => {
                setReturnEdited(true)
                setReturnExpectedAt(e.target.value)
              }}
            />
          </label>
        </div>

        <div className="field-row">
          <label className="field">
            <span>Limit (km/gün)</span>
            <input
              value={limitKmPerDay}
              onChange={e => setLimitKmPerDay(e.target.value)}
              inputMode="numeric"
              autoComplete="off"
            />
          </label>
          <label className="field">
            <span>Limit (yarıçap km)</span>
            <input
              value={limitRadiusKm}
              onChange={e => setLimitRadiusKm(e.target.value)}
              inputMode="numeric"
              autoComplete="off"
            />
          </label>
          <button type="submit" className="btn-primary btn-submit" disabled={saving}>
            Kaydet ve Yazdır
          </button>
        </div>
        {error && <p className="form-error">{error}</p>}
      </form>
    </section>
  )
}
