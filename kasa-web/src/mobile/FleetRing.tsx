import type { ReactNode } from 'react'

interface Props {
  size: number
  radius: number
  strokeWidth: number
  /** API'den hazır gelen kiralama yüzdesi; null ise ring boş çizilir. */
  percent: number | null
  children?: ReactNode
}

/**
 * Kiralama oranı halkası. dasharray, rentalPercent/100'den türeyen GÖRSEL ölçek
 * dönüşümüdür (çevre uzunluğuna izdüşüm) — para/yüzde HESABI değildir (I1).
 */
export default function FleetRing({ size, radius, strokeWidth, percent, children }: Props) {
  const circumference = 2 * Math.PI * radius
  const filled = percent === null ? 0 : (circumference * percent) / 100
  const center = size / 2

  return (
    <div className="m-ring" style={{ width: size, height: size }}>
      <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
        <circle cx={center} cy={center} r={radius} fill="none" stroke="#EAECF2" strokeWidth={strokeWidth} />
        <circle
          cx={center}
          cy={center}
          r={radius}
          fill="none"
          stroke="#1F3864"
          strokeWidth={strokeWidth}
          strokeLinecap="round"
          strokeDasharray={`${filled.toFixed(1)} ${circumference.toFixed(1)}`}
          transform={`rotate(-90 ${center} ${center})`}
        />
      </svg>
      <div className="m-ring-center">{children}</div>
    </div>
  )
}
