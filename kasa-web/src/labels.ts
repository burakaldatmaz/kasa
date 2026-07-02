import type { PaymentMethod } from './api'

export const PAYMENT_METHODS: ReadonlyArray<{ value: PaymentMethod; label: string }> = [
  { value: 'Cash', label: 'Nakit' },
  { value: 'CreditCard', label: 'Kredi Kartı' },
  { value: 'BankTransfer', label: 'Banka Transferi' },
]

export function paymentLabel(method: PaymentMethod): string {
  return PAYMENT_METHODS.find(m => m.value === method)?.label ?? method
}
