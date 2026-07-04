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

/** Mobil Gün başlığı (bugün değilse): "1 Temmuz". */
export function formatDayMonth(iso: string): string {
  return new Date(`${iso}T00:00:00`).toLocaleDateString('tr-TR', {
    day: 'numeric',
    month: 'long',
  })
}

/** Mobil Ay listesi gün rozeti: "TEM" gibi kısa ay adı. */
export function formatMonthShort(iso: string): string {
  return new Date(`${iso}T00:00:00`)
    .toLocaleDateString('tr-TR', { month: 'short' })
    .toLocaleUpperCase('tr-TR')
}

/** Mobil Ay listesi satırı: "Çarşamba". */
export function formatWeekday(iso: string): string {
  return new Date(`${iso}T00:00:00`).toLocaleDateString('tr-TR', { weekday: 'long' })
}

/** "YYYY-MM-DDTHH:mm" — datetime-local input değeri (depozito teslim/iade alanları). */
export function dateTimeLocal(iso: string, time = '09:00'): string {
  return `${iso}T${time}`
}

/** datetime-local değerini gün olarak kaydırır ("...T09:00" → +30 gün). Saati korur. */
export function shiftDateTimeLocalDays(value: string, days: number): string {
  const [d, t] = value.split('T')
  return `${shiftDate(d, days)}T${t ?? '09:00'}`
}

/** datetime-local → "3 Tem 2026 · 09:00" (mobil makbuz listesi meta satırı). */
export function formatDateTimeShort(value: string): string {
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return value
  return d.toLocaleString('tr-TR', {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
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
