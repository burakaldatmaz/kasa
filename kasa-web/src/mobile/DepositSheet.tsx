import { useState } from 'react'
import type { PaymentMethod } from '../api'
import { createDepositReceipt, depositReceiptPdfUrl, errorMessage } from '../api'
import { dateTimeLocal, shiftDateTimeLocalDays } from '../dates'
import { formatSatang } from '../format'
import { PAYMENT_METHODS } from '../labels'

interface Props {
  date: string
  onSaved: () => void
  showToast: (message: string, color: string) => void
  onClose: () => void
}

/**
 * Mobil depozito makbuzu sheet'i (EntrySheet deseni). Kaydet → makbuz oluşturulur →
 * PDF yeni sekmede açılır (GunScreen PDF davranışıyla aynı) → toast → kapanır.
 * Tutar BAHT string gönderilir; satang'a çeviri server'da (I1).
 */
export default function DepositSheet({ date, onSaved, showToast, onClose }: Props) {
  const [customerName, setCustomerName] = useState('')
  const [vehicleModel, setVehicleModel] = useState('')
  const [vehicleColor, setVehicleColor] = useState('')
  const [plate, setPlate] = useState('')
  const [amount, setAmount] = useState('')
  const [payment, setPayment] = useState<PaymentMethod>('Cash')
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
    if (!returnEdited) setReturnExpectedAt(shiftDateTimeLocalDays(next, 30))
  }

  async function save() {
    if (saving) return
    setSaving(true)
    setError(null)
    try {
      const created = await createDepositReceipt({
        date,
        customerName: customerName.trim(),
        vehicleModel: vehicleModel.trim(),
        vehicleColor: vehicleColor.trim() === '' ? null : vehicleColor.trim(),
        plate: plate.trim(),
        amount: amount.trim(),
        paymentMethod: payment,
        fuelLevel: fuelLevel.trim() === '' ? 'Full' : fuelLevel.trim(),
        handoverAt,
        returnExpectedAt,
        limitKmPerDay: Number(limitKmPerDay),
        limitRadiusKm: Number(limitRadiusKm),
      })
      window.open(depositReceiptPdfUrl(created.id))
      showToast(`Makbuz kesildi · ${created.no} · ${formatSatang(created.amountSatang)}`, '#1F3864')
      onSaved()
      onClose()
    } catch (err) {
      setError(errorMessage(err))
      setSaving(false)
    }
  }

  return (
    <>
      <div className="m-backdrop" onClick={onClose} />
      <div className="m-sheet m-deposit-sheet" role="dialog" aria-modal="true" aria-label="Yeni depozito makbuzu">
        <div className="m-grabber" />

        <div className="m-sheet-top">
          <h2 className="m-dep-sheet-title">Yeni Depozito Makbuzu</h2>
          <button type="button" className="m-sheet-close" onClick={onClose} aria-label="Kapat">
            ✕
          </button>
        </div>

        <div className="m-dep-form">
          <label className="m-dep-field">
            <span>Müşteri adı</span>
            <input value={customerName} onChange={e => setCustomerName(e.target.value)} autoComplete="off" />
          </label>

          <div className="m-dep-row">
            <label className="m-dep-field m-dep-grow">
              <span>Araç modeli</span>
              <input
                value={vehicleModel}
                onChange={e => setVehicleModel(e.target.value)}
                placeholder="Honda Click 160"
                autoComplete="off"
              />
            </label>
            <label className="m-dep-field">
              <span>Renk</span>
              <input value={vehicleColor} onChange={e => setVehicleColor(e.target.value)} autoComplete="off" />
            </label>
          </div>

          <div className="m-dep-row">
            <label className="m-dep-field m-dep-grow">
              <span>Plaka</span>
              <input value={plate} onChange={e => setPlate(e.target.value)} autoComplete="off" />
            </label>
            <label className="m-dep-field">
              <span>Tutar (฿)</span>
              <input
                value={amount}
                onChange={e => setAmount(e.target.value)}
                placeholder="3000"
                inputMode="decimal"
                autoComplete="off"
              />
            </label>
          </div>

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

          <div className="m-dep-row">
            <label className="m-dep-field">
              <span>Yakıt</span>
              <input value={fuelLevel} onChange={e => setFuelLevel(e.target.value)} autoComplete="off" />
            </label>
            <label className="m-dep-field">
              <span>Limit (km/gün)</span>
              <input
                value={limitKmPerDay}
                onChange={e => setLimitKmPerDay(e.target.value)}
                inputMode="numeric"
                autoComplete="off"
              />
            </label>
            <label className="m-dep-field">
              <span>Yarıçap (km)</span>
              <input
                value={limitRadiusKm}
                onChange={e => setLimitRadiusKm(e.target.value)}
                inputMode="numeric"
                autoComplete="off"
              />
            </label>
          </div>

          <div className="m-dep-row">
            <label className="m-dep-field m-dep-grow">
              <span>Teslim</span>
              <input type="datetime-local" value={handoverAt} onChange={e => changeHandover(e.target.value)} />
            </label>
            <label className="m-dep-field m-dep-grow">
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
        </div>

        {error && <p className="m-error">{error}</p>}

        <div className="m-sheet-actions">
          <button
            type="button"
            className="m-btn-save m-dep-save"
            onClick={save}
            disabled={saving || amount === ''}
          >
            Kaydet ve Yazdır
          </button>
        </div>
      </div>
    </>
  )
}
