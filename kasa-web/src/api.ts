// Tek veri katmanı: tüm fetch çağrıları buradan geçer.
// Tutarlar server'dan SATANG (number) gelir; UI hiçbir para hesabı yapmaz (I1).

export type TransactionType = 'Income' | 'Expense'
export type PaymentMethod = 'Cash' | 'CreditCard' | 'BankTransfer'

export interface Category {
  id: number
  name: string
  type: TransactionType
  isActive: boolean
  sortOrder: number
}

export interface Transaction {
  id: number
  date: string
  type: TransactionType
  categoryId: number
  categoryName: string
  paymentMethod: PaymentMethod
  amountSatang: number
  note: string | null
  createdAt: string
}

/** Amount BAHT string'dir ("2300" / "2300.50"); satang'a çeviri server'da (I1). */
export interface SaveTransactionRequest {
  date: string
  type: TransactionType
  categoryId: number
  paymentMethod: PaymentMethod
  amount: string
  note: string | null
}

export interface ReportLine {
  id: number
  category: string
  paymentMethod: PaymentMethod
  note: string | null
  amountSatang: number
}

export interface CategoryTotal {
  category: string
  totalSatang: number
}

export interface DailyFleet {
  totalBikes: number
  brokenBikes: number
  rentedBikes: number
  rentalPercent: number | null
  idleBikes: number
  brokenAlert: boolean
}

export interface DailyReport {
  date: string
  previousBalance: number
  incomeLines: ReportLine[]
  expenseLines: ReportLine[]
  incomeByCategory: CategoryTotal[]
  expenseByCategory: CategoryTotal[]
  incomeTotal: number
  expenseTotal: number
  posFee: number
  /** "POS Kesintisi (%3,5)" etiketi için; oran Settings'ten hazır yüzde olarak gelir (I1). */
  posFeeRatePercent: number
  dayNet: number
  closingBalance: number
  fleetMissing: boolean
  fleet: DailyFleet | null
}

export interface FleetSnapshot extends DailyFleet {
  date: string
}

export interface MonthDay {
  date: string
  incomeTotal: number
  expenseTotal: number
  posFee: number
  dayNet: number
  cumulativeBalance: number
}

export interface PartnerShare {
  name: string
  /** Yüzde hazır gelir (ör. 90); Settings kaynağı, UI'da hardcode yok (I1). */
  sharePercent: number
  amountSatang: number
}

export interface Distribution {
  partner1: PartnerShare
  partner2: PartnerShare
}

/** Ay alt toplam satırı — server toplar, UI yalnızca gösterir (I1). */
export interface MonthTotals {
  incomeTotal: number
  expenseTotal: number
  posFee: number
  dayNet: number
}

export interface MonthReport {
  month: string
  days: MonthDay[]
  incomeByCategory: CategoryTotal[]
  expenseByCategory: CategoryTotal[]
  finalBalance: number
  distribution: Distribution
  totals: MonthTotals
}

export interface FleetMonthSummary {
  avgRentalPercent: number | null
  totalBrokenDays: number
  missingDays: number
}

export interface FleetMonth {
  month: string
  days: FleetSnapshot[]
  summary: FleetMonthSummary
}

export interface SaveFleetRequest {
  totalBikes: number
  brokenBikes: number
  rentedBikes: number
}

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.status = status
  }
}

/** Hata gövdesindeki Türkçe mesajı ({ error: "..." }) ayıklar; yoksa genel mesaj. */
async function request<T>(url: string, init?: RequestInit): Promise<T> {
  let response: Response
  try {
    response = await fetch(url, init)
  } catch {
    throw new ApiError('Sunucuya ulaşılamadı. API çalışıyor mu?', 0)
  }

  // Oturum yok/düşmüş: tek noktadan login'e yönlendir (sayfa sayfa 401 kontrolü yok).
  // Auth uçları hariç: login'in kendi 401'i "parola hatalı" mesajıdır.
  if (response.status === 401 && !url.startsWith('/api/auth/') && window.location.pathname !== '/login') {
    const next = window.location.pathname + window.location.search
    window.location.assign(`/login?next=${encodeURIComponent(next)}`)
    throw new ApiError('Oturum açmanız gerekiyor.', 401)
  }

  if (!response.ok) {
    let message = `İstek başarısız oldu (HTTP ${response.status}).`
    try {
      const body = (await response.json()) as { error?: string }
      if (typeof body?.error === 'string' && body.error.length > 0) message = body.error
    } catch {
      // gövde JSON değilse genel mesaj kalır
    }
    throw new ApiError(message, response.status)
  }

  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

function jsonInit(method: string, body: unknown): RequestInit {
  return {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  }
}

export function errorMessage(err: unknown): string {
  return err instanceof Error ? err.message : 'Beklenmeyen bir hata oluştu.'
}

export function login(password: string): Promise<void> {
  return request('/api/auth/login', jsonInit('POST', { password }))
}

export function logout(): Promise<void> {
  return request('/api/auth/logout', { method: 'POST' })
}

export function getCategories(type: TransactionType): Promise<Category[]> {
  return request(`/api/categories?type=${type}`)
}

export function createCategory(name: string, type: TransactionType): Promise<Category> {
  return request('/api/categories', jsonInit('POST', { name, type }))
}

export function getTransactions(date: string): Promise<Transaction[]> {
  return request(`/api/transactions?date=${date}`)
}

export function createTransaction(req: SaveTransactionRequest): Promise<Transaction> {
  return request('/api/transactions', jsonInit('POST', req))
}

export function updateTransaction(id: number, req: SaveTransactionRequest): Promise<Transaction> {
  return request(`/api/transactions/${id}`, jsonInit('PUT', req))
}

export function deleteTransaction(id: number): Promise<void> {
  return request(`/api/transactions/${id}`, { method: 'DELETE' })
}

export function getDailyReport(date: string): Promise<DailyReport> {
  return request(`/api/reports/daily?date=${date}`)
}

export function saveFleet(date: string, req: SaveFleetRequest): Promise<FleetSnapshot> {
  return request(`/api/fleet/${date}`, jsonInit('PUT', req))
}

export function getMonthReport(month: string): Promise<MonthReport> {
  return request(`/api/reports/month?month=${month}`)
}

export function getFleetMonth(month: string): Promise<FleetMonth> {
  return request(`/api/fleet/month?month=${month}`)
}
