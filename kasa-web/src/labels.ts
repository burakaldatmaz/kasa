import type { PaymentMethod } from './api'

export const PAYMENT_METHODS: ReadonlyArray<{ value: PaymentMethod; label: string; short: string }> = [
  { value: 'Cash', label: 'Nakit', short: 'Nakit' },
  { value: 'CreditCard', label: 'Kredi Kartı', short: 'K.Kartı' },
  { value: 'BankTransfer', label: 'Banka Transferi', short: 'Havale' },
]

export function paymentLabel(method: PaymentMethod): string {
  return PAYMENT_METHODS.find(m => m.value === method)?.label ?? method
}

/** Mobil kısaltmalar (Nakit | K.Kartı | Havale) — sheet segmenti ve liste meta satırında. */
export function paymentShort(method: PaymentMethod): string {
  return PAYMENT_METHODS.find(m => m.value === method)?.short ?? method
}
