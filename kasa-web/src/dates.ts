// Tarih yardımcıları — yerel saat dilimine göre çalışır (kasa günü = işletmenin günü).

function pad(n: number): string {
  return String(n).padStart(2, '0')
}

export function toISODate(d: Date): string {
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

export function todayISO(): string {
  return toISODate(new Date())
}

export function shiftDate(iso: string, days: number): string {
  const d = new Date(`${iso}T00:00:00`)
  d.setDate(d.getDate() + days)
  return toISODate(d)
}

/** İçinde bulunulan ay: "2026-07". */
export function currentMonthISO(): string {
  return todayISO().slice(0, 7)
}

export function shiftMonth(isoMonth: string, months: number): string {
  const [year, month] = isoMonth.split('-').map(Number)
  const d = new Date(year, month - 1 + months, 1)
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}`
}

/** Ay seçici başlığı: "Temmuz 2026". */
export function formatMonthLong(isoMonth: string): string {
  return new Date(`${isoMonth}-01T00:00:00`).toLocaleDateString('tr-TR', {
    month: 'long',
    year: 'numeric',
  })
}

/** "12.07.2026" — ay tablosundaki kısa tarih. */
export function formatDateShort(iso: string): string {
  return new Date(`${iso}T00:00:00`).toLocaleDateString('tr-TR')
}

export function formatDateLong(iso: string): string {
  return new Date(`${iso}T00:00:00`).toLocaleDateString('tr-TR', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  })
}

/** Rapor başlığındaki uzun tarih ("3 Ağustos 2026") — PDF ile aynı biçim, haftanın günü yok. */
export function formatDateReport(iso: string): string {
  return new Date(`${iso}T00:00:00`).toLocaleDateString('tr-TR', {
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  })
}
