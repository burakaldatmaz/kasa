import type { DailyReport } from '../api'
import { formatSatang } from '../format'

interface Props {
  report: DailyReport | null
}

/** Alt sabit özet barı. Tüm rakamlar /api/reports/daily'den hazır gelir (I1). */
export default function SummaryBar({ report }: Props) {
  function cell(label: string, satang: number | undefined, options?: { title?: string; className?: string }) {
    return (
      <div className={`summary-cell ${options?.className ?? ''}`} title={options?.title}>
        <span className="summary-label">{label}</span>
        <span className="summary-value">{satang === undefined ? '—' : formatSatang(satang)}</span>
      </div>
    )
  }

  const negative = report !== null && report.closingBalance < 0

  return (
    <footer className="summary-bar">
      {cell('Devir', report?.previousBalance, { title: 'Önceki günden devreden' })}
      {cell('Gelir', report?.incomeTotal)}
      {cell('Gider', report?.expenseTotal)}
      {cell('POS Kesintisi', report?.posFee)}
      {cell('Gün Net', report?.dayNet)}
      {cell('ANA KASA', report?.closingBalance, {
        className: negative ? 'summary-main summary-main-negative' : 'summary-main',
      })}
    </footer>
  )
}
