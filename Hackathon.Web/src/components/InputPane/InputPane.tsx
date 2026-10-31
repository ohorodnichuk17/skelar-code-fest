import type { BillingState } from '../../api/resolver'
import { TICKETS, type Scenario } from '../../scenarios'

interface InputPaneProps {
  scenario: Scenario
  ticketId: string
  message: string
  billing: BillingState | null
  changedChargeIds: string[]
  loading: boolean
  onTicketChange: (ticketId: string) => void
  onMessageChange: (message: string) => void
  onSubmit: () => void
}

const initials = (name: string) =>
  name
    .split(' ')
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase()

const money = (amount: number) => `₴${amount.toLocaleString('uk-UA')}`

export function InputPane({
  scenario,
  ticketId,
  message,
  billing,
  changedChargeIds,
  loading,
  onTicketChange,
  onMessageChange,
  onSubmit,
}: InputPaneProps) {
  return (
    <article className="case-panel">
      <div className="panel-topline">
        <span className="eyebrow">Вхідний тікет</span>
        <span className="ticket-id">{ticketId}</span>
      </div>

      <div className="customer">
        <span className="customer-avatar">{billing ? initials(billing.customerName) : '··'}</span>
        <div>
          <strong>{billing?.customerName ?? 'Завантаження…'}</strong>
          <span>{billing?.plan ?? ' '}</span>
        </div>
      </div>

      {scenario.editable ? (
        <div className="custom-message">
          <label className="field-label" htmlFor="ticket">Клієнт</label>
          <select id="ticket" value={ticketId} disabled={loading} onChange={(e) => onTicketChange(e.target.value)}>
            {TICKETS.map((t) => (
              <option key={t.id} value={t.id}>{t.label}</option>
            ))}
          </select>
          <label className="field-label" htmlFor="message">Повідомлення клієнта</label>
          <textarea
            id="message"
            rows={4}
            maxLength={2000}
            placeholder="Напр.: «Ви списали з мене двічі, поверніть гроші»"
            value={message}
            disabled={loading}
            onChange={(e) => onMessageChange(e.target.value)}
          />
        </div>
      ) : (
        <blockquote>«{message}»</blockquote>
      )}

      <button
        type="button"
        className="primary-button"
        disabled={loading || message.trim().length === 0}
        onClick={onSubmit}
      >
        {loading ? 'Gemma 4 аналізує…' : 'Обробити звернення'} <span aria-hidden="true">→</span>
      </button>

      <section className="billing" aria-label="Білінг клієнта">
        <div className="message-meta">
          <span>Білінг клієнта</span>
          <span>Live-стан</span>
        </div>
        <ul className="charges">
          {billing?.charges.map((charge) => (
            <li
              key={charge.id}
              className={[
                'charge',
                charge.status === 'refunded' ? 'charge--refunded' : '',
                changedChargeIds.includes(charge.id) ? 'charge--changed' : '',
              ].join(' ')}
            >
              <span className="charge-id">{charge.id}</span>
              <span className="charge-desc">{charge.description}</span>
              <span className="charge-amount">{money(charge.amount)}</span>
              <span className="charge-status">{charge.status === 'refunded' ? 'повернено' : 'оплачено'}</span>
            </li>
          ))}
        </ul>
      </section>
    </article>
  )
}
