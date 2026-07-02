import { useState } from 'react'
import type { DailyReport } from '../api'
import { formatSatang } from '../format'

interface Props {
  report: DailyReport | null
}

/**
 * Alt sabit özet barı. Tüm rakamlar /api/reports/daily'den hazır gelir (I1).
 * Desktop (641px+): 6 hücre yan yana. Mobil (≤640px): Gün Net + Ana Kasa her
 * zaman görünür; Devir/Gelir/Gider/POS bara dokununca açılan detay panelinde.
 */
export default function SummaryBar({ report }: Props) {
  const [detailOpen, setDetailOpen] = useState(false)

  function cell(label: string, satang: number | undefined, options?: { title?: string; className?: string }) {
    return (
      <div className={`summary-cell ${options?.className ?? ''}`} title={options?.title}>
        <span className="summary-label">{label}</span>
        <span className="summary-value">{satang === undefined ? '—' : formatSatang(satang)}</span>
      </div>
    )
  }

  const negative = report !== null && report.closingBalance < 0
  const mainClass = negative ? 'summary-main summary-main-negative' : 'summary-main'

  return (
    <footer className="summary-bar">
      <div className="summary-cells">
        {cell('Devir', report?.previousBalance, { title: 'Önceki günden devreden' })}
        {cell('Gelir', report?.incomeTotal)}
        {cell('Gider', report?.expenseTotal)}
        {cell('POS Kesintisi', report?.posFee)}
        {cell('Gün Net', report?.dayNet)}
        {cell('ANA KASA', report?.closingBalance, { className: mainClass })}
      </div>
      <div className="summary-mobile">
        <div className="summary-detail" hidden={!detailOpen}>
          {cell('Devir', report?.previousBalance, { title: 'Önceki günden devreden' })}
          {cell('Gelir', report?.incomeTotal)}
          {cell('Gider', report?.expenseTotal)}
          {cell('POS Kesintisi', report?.posFee)}
        </div>
        <button
          type="button"
          className="summary-primary"
          onClick={() => setDetailOpen(o => !o)}
          aria-expanded={detailOpen}
        >
          {cell('Gün Net', report?.dayNet)}
          {cell('ANA KASA', report?.closingBalance, { className: mainClass })}
        </button>
      </div>
    </footer>
  )
}
