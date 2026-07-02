// UI'daki TEK para dönüşümü burada yaşar (I1): satang/100 → gösterim.
// Toplam/net/POS gibi hesapların hepsi server'dan hazır gelir.

const thb = new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB' })

/** Satang → "฿1,000.00" biçiminde para metni. */
export function formatSatang(satang: number): string {
  return thb.format(satang / 100)
}

/** Satang → düzenleme alanına yazılacak baht string'i ("1000.50"). Hesap değil, gösterim. */
export function satangToBahtInput(satang: number): string {
  return (satang / 100).toFixed(2)
}
