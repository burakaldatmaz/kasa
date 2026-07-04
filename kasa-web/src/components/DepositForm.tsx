import { useState } from 'react'
import type { FormEvent } from 'react'
import type { DepositReceipt, PaymentMethod } from '../api'
import { createDepositReceipt, depositReceiptPdfUrl, errorMessage } from '../api'
import { dateTimeLocal, shiftDateTimeLocalDays } from '../dates'
import { PAYMENT_METHODS } from '../labels'
import { EXCESS_KM_FEE_THB, VEHICLE_BY_PLATE } from '../vehicles'
import type { Vehicle } from '../vehicles'
import PlateSelect from './PlateSelect'

interface Props {
  /** Makbuz tarihi (üst tarih seçicisinden gelir; numara yılı bundan türer). */
  date: string
  onSaved: () => void
}

/** Seçili aracın km/yarıçap kuralını tek satır Türkçe özet (form güven satırı; PDF'te İng/Thai basılır). */
function usageHint(v: Vehicle): string {
  const radius =
    v.radiusPolicy === 'bangkok-only'
      ? 'Bangkok dışına çıkış yok'
      : v.radiusPolicy === 'within-150'
        ? "Bangkok'tan max 150 km"
        : 'Mesafe limiti yok'
  return `${v.dailyKm} km/gün · ${radius} · aşım ${EXCESS_KM_FEE_THB} ฿/km`
}

/**
 * Depozito makbuzu formu. Araç plaka bazlı dropdown'dan seçilir; seçim model + plaka + depozito
 * tutarını (override edilebilir) + km limitlerini otomatik doldurur. Kaydet → makbuz oluşturulur →
 * PDF yeni sekmede açılır (Ctrl+P). Tutar BAHT string gönderilir, satang'a çeviri server'da (I1).
 */
export default function DepositForm({ date, onSaved }: Props) {
  const [customerName, setCustomerName] = useState('')
  const [phone, setPhone] = useState('')
  const [taxId, setTaxId] = useState('')
  const [plate, setPlate] = useState('')
  const [amount, setAmount] = useState('')
  const [referenceNo, setReferenceNo] = useState('')
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('Cash')
  const [fuelLevel, setFuelLevel] = useState('Full')
  const [handoverAt, setHandoverAt] = useState(() => dateTimeLocal(date))
  const [returnExpectedAt, setReturnExpectedAt] = useState(() => shiftDateTimeLocalDays(dateTimeLocal(date), 30))
  const [returnEdited, setReturnEdited] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const vehicle = plate === '' ? undefined : VEHICLE_BY_PLATE[plate]

  function selectVehicle(nextPlate: string) {
    setPlate(nextPlate)
    // Depozito tutarı seçilen araçtan önerilir; kullanıcı sonra elle değiştirebilir.
    const v = VEHICLE_BY_PLATE[nextPlate]
    if (v) setAmount(String(v.deposit))
  }

  function changeHandover(next: string) {
    setHandoverAt(next)
    // İade tarihi kullanıcı elle değiştirmediyse teslim + 30 gün önerisiyle güncellenir.
    if (!returnEdited) setReturnExpectedAt(shiftDateTimeLocalDays(next, 30))
  }

  async function handleSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (saving) return
    if (!vehicle) {
      setError('Araç seçin.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      const created: DepositReceipt = await createDepositReceipt({
        date,
        customerName: customerName.trim(),
        phone: phone.trim() === '' ? null : phone.trim(),
        taxId: taxId.trim() === '' ? null : taxId.trim(),
        vehicleModel: vehicle.model,
        plate: vehicle.plate,
        amount: amount.trim(),
        paymentMethod,
        referenceNo: referenceNo.trim() === '' ? null : referenceNo.trim(),
        fuelLevel: fuelLevel.trim() === '' ? 'Full' : fuelLevel.trim(),
        handoverAt,
        returnExpectedAt,
        dailyKm: vehicle.dailyKm,
        radiusPolicy: vehicle.radiusPolicy,
      })
      // PDF yeni sekmede açılır → kullanıcı Ctrl+P ile yazdırır (günlük rapor PDF deseni).
      window.open(depositReceiptPdfUrl(created.id))
      // Bir sonraki müşteri için kimlik alanları temizlenir; ödeme/yakıt korunur.
      setCustomerName('')
      setPhone('')
      setTaxId('')
      setPlate('')
      setAmount('')
      setReferenceNo('')
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
            <span>Telefon (opsiyonel)</span>
            <input value={phone} onChange={e => setPhone(e.target.value)} inputMode="tel" autoComplete="off" />
          </label>
        </div>

        <div className="field-row">
          <label className="field field-grow">
            <span>Araç (plaka)</span>
            <PlateSelect value={plate} onChange={selectVehicle} />
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

        {vehicle && <p className="dep-usage-hint">{usageHint(vehicle)}</p>}

        <div className="field-row">
          <label className="field">
            <span>Vergi No (opsiyonel)</span>
            <input value={taxId} onChange={e => setTaxId(e.target.value)} autoComplete="off" />
          </label>
          <label className="field field-grow">
            <span>Ref No (opsiyonel)</span>
            <input
              value={referenceNo}
              onChange={e => setReferenceNo(e.target.value)}
              placeholder="Kart: TRACE NO · Nakit/Transfer: fatura no"
              autoComplete="off"
            />
          </label>
        </div>

        <div className="field-row">
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
          <button type="submit" className="btn-primary btn-submit" disabled={saving || !vehicle}>
            Kaydet ve Yazdır
          </button>
        </div>
        {error && <p className="form-error">{error}</p>}
      </form>
    </section>
  )
}
