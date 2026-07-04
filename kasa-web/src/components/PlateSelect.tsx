import { useEffect, useMemo, useRef, useState } from 'react'
import type { KeyboardEvent } from 'react'
import { VEHICLES, VEHICLE_BY_PLATE } from '../vehicles'

/** Arama normalizasyonu: küçük harf + boşlukları at → "9918" yazınca "8 ขศ 9918" eşleşir. */
const norm = (s: string) => s.toLowerCase().replace(/\s+/g, '')

interface Props {
  value: string
  onChange: (plate: string) => void
  /** Görsel varyant: masaüstü `.field` vs mobil `.m-dep-field` ölçüleri. */
  variant?: 'desktop' | 'mobile'
}

/**
 * Aranabilir araç (plaka) seçici — native <select>'in yerine. Kullanıcı plakanın herhangi bir
 * parçasını (özellikle sonundaki sayıyı) veya model adını yazarak listeyi filtreler. Ok tuşları +
 * Enter ile klavye, tıkla/dışarı-tıkla ile fare desteği. Sıralama vehicles.ts'ten (grup + plaka no).
 */
export default function PlateSelect({ value, onChange, variant = 'desktop' }: Props) {
  const [open, setOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [activeIndex, setActiveIndex] = useState(0)
  const rootRef = useRef<HTMLDivElement>(null)
  const inputRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLUListElement>(null)

  const selected = value === '' ? undefined : VEHICLE_BY_PLATE[value]

  const filtered = useMemo(() => {
    const q = norm(query)
    if (q === '') return VEHICLES
    return VEHICLES.filter(v => norm(v.plate).includes(q) || norm(v.model).includes(q))
  }, [query])

  // Sorgu değişince aktif satırı başa al (aralık dışı kalmasını engelle).
  useEffect(() => {
    setActiveIndex(0)
  }, [query])

  // Dışarı tıklama → kapat.
  useEffect(() => {
    if (!open) return
    function onDown(e: MouseEvent) {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) {
        setOpen(false)
        setQuery('')
      }
    }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [open])

  // Aktif satırı görünür alanda tut (klavye ile gezinirken).
  useEffect(() => {
    if (!open || !listRef.current) return
    const el = listRef.current.children[activeIndex] as HTMLElement | undefined
    el?.scrollIntoView({ block: 'nearest' })
  }, [activeIndex, open])

  function openMenu() {
    setQuery('')
    setActiveIndex(0)
    setOpen(true)
  }

  function close() {
    setOpen(false)
    setQuery('')
  }

  function pick(plate: string) {
    onChange(plate)
    close()
    inputRef.current?.blur()
  }

  function onKeyDown(e: KeyboardEvent<HTMLInputElement>) {
    if (!open) {
      if (e.key === 'ArrowDown' || e.key === 'Enter') {
        e.preventDefault()
        openMenu()
      }
      return
    }
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setActiveIndex(i => Math.min(i + 1, filtered.length - 1))
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setActiveIndex(i => Math.max(i - 1, 0))
    } else if (e.key === 'Enter') {
      e.preventDefault() // form submit'i engelle; seçim yap
      const v = filtered[activeIndex]
      if (v) pick(v.plate)
    } else if (e.key === 'Escape') {
      e.preventDefault()
      close()
      inputRef.current?.blur()
    }
  }

  // Kapalıyken seçili aracın etiketi görünür; açıkken kullanıcının yazdığı sorgu.
  const inputValue = open ? query : selected ? `${selected.plate} — ${selected.model}` : ''
  const placeholder = selected ? `${selected.plate} — ${selected.model}` : 'Araç ara (plaka / model)…'

  return (
    <div className={`plate-select${variant === 'mobile' ? ' plate-select--mobile' : ''}`} ref={rootRef}>
      <input
        ref={inputRef}
        className="plate-select__input"
        role="combobox"
        aria-expanded={open}
        aria-autocomplete="list"
        value={inputValue}
        placeholder={placeholder}
        onChange={e => {
          setOpen(true)
          setQuery(e.target.value)
        }}
        onFocus={openMenu}
        onKeyDown={onKeyDown}
        autoComplete="off"
      />
      {open && (
        <ul className="plate-select__menu" ref={listRef} role="listbox">
          {filtered.length === 0 ? (
            <li className="plate-select__empty">Eşleşen araç yok</li>
          ) : (
            filtered.map((v, i) => (
              <li
                key={v.plate}
                role="option"
                aria-selected={v.plate === value}
                className={
                  'plate-select__option' +
                  (i === activeIndex ? ' plate-select__option--active' : '') +
                  (v.plate === value ? ' plate-select__option--selected' : '')
                }
                // mousedown + preventDefault: input blur'dan (→ menü kapanışı) önce seçimi yakala.
                onMouseDown={e => {
                  e.preventDefault()
                  pick(v.plate)
                }}
                onMouseEnter={() => setActiveIndex(i)}
              >
                <span className="plate-select__plate">{v.plate}</span>
                <span className="plate-select__model">{v.model}</span>
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  )
}
