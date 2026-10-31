import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react'
import type { Company } from '../../api/support'

const countryNames: Record<string, string> = { UA: 'Україна', DE: 'Німеччина' }

function ProductIcon() {
  return <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true">
    <rect x="3" y="3" width="5" height="5" rx="1" /><rect x="12" y="3" width="5" height="5" rx="1" />
    <rect x="3" y="12" width="5" height="5" rx="1" /><rect x="12" y="12" width="5" height="5" rx="1" />
  </svg>
}

export function ProductSelect({ companies, selectedId, disabled, onChange }: {
  companies: Company[]; selectedId?: string; disabled: boolean; onChange: (id: string) => void
}) {
  const id = useId()
  const root = useRef<HTMLDivElement>(null)
  const trigger = useRef<HTMLButtonElement>(null)
  const [open, setOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(0)
  const search = useRef({ value: '', at: 0 })
  const selected = companies.find((item) => item.id === selectedId)
  const selectedIndex = Math.max(0, companies.findIndex((item) => item.id === selectedId))
  const available = !disabled && companies.length > 0
  const expanded = open && available

  useEffect(() => {
    if (!expanded) return
    const closeOutside = (event: PointerEvent) => {
      if (event.target instanceof Node && !root.current?.contains(event.target)) setOpen(false)
    }
    document.addEventListener('pointerdown', closeOutside)
    return () => document.removeEventListener('pointerdown', closeOutside)
  }, [expanded])

  const choose = (index: number) => {
    const company = companies[index]
    if (!company) return
    onChange(company.id)
    setOpen(false)
    trigger.current?.focus()
  }

  const onKeyDown = (event: KeyboardEvent<HTMLButtonElement>) => {
    if (!available) return
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault()
      const direction = event.key === 'ArrowDown' ? 1 : -1
      setActiveIndex(expanded ? (activeIndex + direction + companies.length) % companies.length : selectedIndex)
      setOpen(true)
    } else if (event.key === 'Home' || event.key === 'End') {
      event.preventDefault()
      setActiveIndex(event.key === 'Home' ? 0 : companies.length - 1)
      setOpen(true)
    } else if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      if (expanded) choose(activeIndex)
      else { setActiveIndex(selectedIndex); setOpen(true) }
    } else if (event.key === 'Escape') {
      if (expanded) { event.preventDefault(); event.stopPropagation(); setOpen(false) }
    } else if (event.key === 'Tab') setOpen(false)
    else if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey) {
      const now = Date.now()
      search.current = { value: (now - search.current.at < 700 ? search.current.value : '') + event.key.toLowerCase(), at: now }
      const index = companies.findIndex((item) => item.name.toLowerCase().startsWith(search.current.value))
      if (index >= 0) { event.preventDefault(); setActiveIndex(index); setOpen(true) }
    }
  }

  return <div className="company-picker product-select" ref={root} onBlur={(event) => {
    if (!event.currentTarget.contains(event.relatedTarget)) setOpen(false)
  }}>
    <span className="product-select-label" id={`${id}-label`}>SaaS-продукт</span>
    <button className="product-select-trigger" ref={trigger} type="button" role="combobox"
      aria-labelledby={`${id}-label ${id}-value`} aria-haspopup="listbox" aria-expanded={expanded}
      aria-controls={`${id}-options`} aria-activedescendant={expanded ? `${id}-option-${activeIndex}` : undefined}
      disabled={!available} onKeyDown={onKeyDown} onClick={() => {
        if (!expanded) setActiveIndex(selectedIndex)
        setOpen(!expanded)
      }}>
      <span className="product-select-icon"><ProductIcon /></span>
      <span className="product-select-text"><strong id={`${id}-value`}>{selected?.name ?? (disabled ? 'Завантаження…' : 'Оберіть продукт')}</strong>
        <span>{selected ? countryNames[selected.country] ?? selected.country : 'Робочий простір'}</span>
      </span>
      <svg className="product-select-chevron" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.5" aria-hidden="true"><path d="m6 8 4 4 4-4" strokeLinecap="round" strokeLinejoin="round" /></svg>
    </button>
    {expanded && <ul className="product-select-menu" id={`${id}-options`} role="listbox" aria-labelledby={`${id}-label`}>
      {companies.map((item, index) => <li key={item.id} id={`${id}-option-${index}`} role="option"
        aria-selected={item.id === selectedId} className={index === activeIndex ? 'product-select-option is-highlighted' : 'product-select-option'}
        onPointerMove={() => setActiveIndex(index)} onMouseDown={(event) => event.preventDefault()} onClick={() => choose(index)}>
        <span className="product-select-initial" aria-hidden="true">{item.name.slice(0, 1)}</span>
        <span className="product-select-text"><strong>{item.name}</strong><span>{countryNames[item.country] ?? item.country}</span></span>
        {item.id === selectedId && <svg className="product-select-check" viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="1.75" aria-hidden="true"><path d="m5 10 3 3 7-7" strokeLinecap="round" strokeLinejoin="round" /></svg>}
      </li>)}
    </ul>}
  </div>
}
