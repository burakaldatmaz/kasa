import { useState } from 'react'
import type { PaymentMethod } from '../api'
import { createDepositReceipt, depositReceiptPdfUrl, errorMessage } from '../api'
import { dateTimeLocal, shiftDateTimeLocalDays } from '../dates'
import { formatSatang } from '../format'
import { PAYMENT_METHODS } from '../labels'
import { EXCESS_KM_FEE_THB, VEHICLE_BY_PLATE } from '../vehicles'
import type { Vehicle } from '../vehicles'
import PlateSelect from '../components/PlateSelect'

interface Props {
  date: string
  onSaved: () => void
  showToast: (message: string, color: string) => void
  onClose: () => void
}

/** Seçili aracın km/yarıçap kuralını tek satır Türkçe özet (form güven satırı). */
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
 * Mobil depozito makbuzu sheet'i (EntrySheet deseni). Araç plaka dropdown'dan seçilir; seçim
 * model + plaka + depozito tutarını (override edilebilir) + km limitlerini otomatik doldurur.
 * Kaydet → makbuz oluşturulur → PDF yeni sekmede açılır → toast → kapanır. Tutar server'da satang.
 */
export default function DepositSheet({ date, onSaved, showToast, onClose }: Props) {
  const [customerName, setCustomerName] = useState('')
  const [phone, setPhone] = useState('')
  const [taxId, setTaxId] = useState('')
  const [plate, setPlate] = useState('')
  const [amount, setAmount] = useState('')
  const [referenceNo, setReferenceNo] = useState('')
  const [payment, setPayment] = useState<PaymentMethod>('Cash')
  const [fuelLevel, setFuelLevel] = useState('Full')
  const [handoverAt, setHandoverAt] = useState(() => dateTimeLocal(date))
  const [returnExpectedAt, setReturnExpectedAt] = useState(() => shiftDateTimeLocalDays(dateTimeLocal(date), 30))
  const [returnEdited, setReturnEdited] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const vehicle = plate === '' ? undefined : VEHICLE_BY_PLATE[plate]

  function selectVehicle(nextPlate: string) {
    setPlate(nextPlate)
    const v = VEHICLE_BY_PLATE[nextPlate]
    if (v) setAmount(String(v.deposit))
  }

  function changeHandover(next: string) {
    setHandoverAt(next)
    if (!returnEdited) setReturnExpectedAt(shiftDateTimeLocalDays(next, 30))
  }

  async function save() {
    if (saving) return
    if (!vehicle) {
      setError('Araç seçin.')
      return
    }
    setSaving(true)
    setError(null)
    try {
      const created = await createDepositReceipt({
        date,
        customerName: customerName.trim(),
        phone: phone.trim() === '' ? null : phone.trim(),
        taxId: taxId.trim() === '' ? null : taxId.trim(),
        vehicleModel: vehicle.model,
        plate: vehicle.plate,
        amount: amount.trim(),
        paymentMethod: payment,
        referenceNo: referenceNo.trim() === '' ? null : referenceNo.trim(),
        fuelLevel: fuelLevel.trim() === '' ? 'Full' : fuelLevel.trim(),
        handoverAt,
        returnExpectedAt,
        dailyKm: vehicle.dailyKm,
        radiusPolicy: vehicle.radiusPolicy,
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
          <div className="m-dep-row">
            <label className="m-dep-field m-dep-grow">
              <span>Müşteri adı</span>
              <input value={customerName} onChange={e => setCustomerName(e.target.value)} autoComplete="off" />
            </label>
            <label className="m-dep-field">
              <span>Telefon</span>
              <input value={phone} onChange={e => setPhone(e.target.value)} inputMode="tel" autoComplete="off" />
            </label>
          </div>

          <label className="m-dep-field">
            <span>Araç (plaka)</span>
            <PlateSelect value={plate} onChange={selectVehicle} variant="mobile" />
          </label>

          {vehicle && <p className="m-dep-hint">{usageHint(vehicle)}</p>}

          <div className="m-dep-row">
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
            <label className="m-dep-field">
              <span>Vergi No</span>
              <input value={taxId} onChange={e => setTaxId(e.target.value)} autoComplete="off" />
            </label>
          </div>

          <label className="m-dep-field">
            <span>Ref No (opsiyonel)</span>
            <input
              value={referenceNo}
              onChange={e => setReferenceNo(e.target.value)}
              placeholder="Kart: TRACE NO · Nakit/Transfer: fatura no"
              autoComplete="off"
            />
          </label>

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
            <label className="m-dep-field m-dep-grow">
              <span>Teslim</span>
              <input type="datetime-local" value={handoverAt} onChange={e => changeHandover(e.target.value)} />
            </label>
          </div>

          <label className="m-dep-field">
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

        {error && <p className="m-error">{error}</p>}

        <div className="m-sheet-actions">
          <button
            type="button"
            className="m-btn-save m-dep-save"
            onClick={save}
            disabled={saving || !vehicle || amount === ''}
          >
            Kaydet ve Yazdır
          </button>
        </div>
      </div>
    </>
  )
}
