import { useEffect, useState } from 'react'
import {
  getBilling,
  resolveTicket,
  reviewAction,
  type BillingState,
  type ResolveTicketResponse,
  type ReviewActionResponse,
} from './api/resolver'
import { CorePreview, DecisionTrail } from './components/CorePreview/CorePreview'
import { InputPane } from './components/InputPane/InputPane'
import { SCENARIOS } from './scenarios'
import './App.css'

function StatusDot({ tone = 'neutral' }: { tone?: 'neutral' | 'success' | 'danger' }) {
  return <span className={`status-dot status-dot--${tone}`} aria-hidden="true" />
}

const changedIds = (before: BillingState | null, after: BillingState) => {
  const previous = new Map(before?.charges.map((c) => [c.id, c.status]))
  return after.charges.filter((c) => previous.get(c.id) !== c.status).map((c) => c.id)
}

function App() {
  const [scenarioKey, setScenarioKey] = useState(SCENARIOS[0].key)
  const scenario = SCENARIOS.find((s) => s.key === scenarioKey)!
  const [ticketId, setTicketId] = useState(scenario.ticketId)
  const [message, setMessage] = useState(scenario.message)
  const [billing, setBilling] = useState<BillingState | null>(null)
  const [changedChargeIds, setChangedChargeIds] = useState<string[]>([])
  const [result, setResult] = useState<ResolveTicketResponse | null>(null)
  const [review, setReview] = useState<ReviewActionResponse | null>(null)
  const [elapsed, setElapsed] = useState<number | null>(null)
  const [loading, setLoading] = useState(false)
  const [reviewing, setReviewing] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    getBilling(ticketId)
      .then((state) => !cancelled && setBilling(state))
      .catch((e: Error) => !cancelled && setError(e.message))
    return () => {
      cancelled = true
    }
  }, [ticketId])

  const resetDecision = () => {
    setResult(null)
    setReview(null)
    setChangedChargeIds([])
    setError(null)
  }

  const changeScenario = (key: string) => {
    const next = SCENARIOS.find((s) => s.key === key)!
    setScenarioKey(key)
    setTicketId(next.ticketId)
    setMessage(next.message)
    resetDecision()
  }

  const submit = async () => {
    resetDecision()
    setLoading(true)
    const started = performance.now()
    try {
      const response = await resolveTicket(ticketId, message.trim())
      setElapsed((performance.now() - started) / 1000)
      setChangedChargeIds(changedIds(billing, response.billing))
      setBilling(response.billing)
      setResult(response)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Невідома помилка')
    } finally {
      setLoading(false)
    }
  }

  const submitReview = async (approve: boolean) => {
    if (!result?.pendingAction) return
    setReviewing(true)
    setError(null)
    try {
      const response = await reviewAction(result.pendingAction.id, approve)
      setChangedChargeIds(changedIds(billing, response.billing))
      setBilling(response.billing)
      setReview(response)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Невідома помилка')
    } finally {
      setReviewing(false)
    }
  }

  const events = result ? [...result.timeline, ...(review ? [review.event] : [])] : []

  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">Перейти до основного вмісту</a>

      <header className="topbar">
        <a className="brand" href="#main-content" aria-label="Resolver home"><span className="brand-mark" aria-hidden="true">R</span><span>resolver</span></a>
        <div className="topbar-right"><span className="live-state"><StatusDot tone="success" /> Система правил активна</span><span className="agent-chip"><span className="agent-avatar">AK</span> Олексій К.</span></div>
      </header>

      <main id="main-content" className="workspace">
        <section className="page-intro" aria-labelledby="page-title">
          <div><p className="eyebrow">Автоматизація підтримки</p><h1 id="page-title">Автоматизована підтримка.</h1><p className="lede">Gemma 4 аналізує звернення, правила дозволяють безпечні дії, а ризикові запити погоджує агент.</p></div>
          <div className="eval-summary">
            <span className="eyebrow">Останнє рішення</span>
            <strong>{elapsed != null ? `${elapsed.toFixed(1).replace('.', ',')} с` : '—'}</strong>
            <span>{result ? (result.source === 'gemma' ? 'Gemma 4 · жива модель' : 'кешований тріаж · Gemma недоступна') : 'ще не запускали'}</span>
          </div>
        </section>

        <nav className="scenario-switcher" aria-label="Demo scenarios">
          {SCENARIOS.map((s) => (
            <button
              key={s.key}
              type="button"
              className={s.key === scenarioKey ? 'scenario-button is-active' : 'scenario-button'}
              disabled={loading || reviewing}
              onClick={() => changeScenario(s.key)}
            >
              <span>Сценарій {s.number}</span><strong>{s.title}</strong><small>{s.hint}</small>
            </button>
          ))}
        </nav>

        <section className="case-layout" aria-label="Ticket decision">
          <InputPane
            scenario={scenario}
            ticketId={ticketId}
            message={message}
            billing={billing}
            changedChargeIds={changedChargeIds}
            loading={loading}
            onTicketChange={(id) => {
              setTicketId(id)
              resetDecision()
            }}
            onMessageChange={setMessage}
            onSubmit={submit}
          />
          <CorePreview
            result={result}
            review={review}
            loading={loading}
            reviewing={reviewing}
            error={error}
            onReview={submitReview}
          />
        </section>

        <DecisionTrail events={events} />
      </main>

      <footer className="footer"><span>Resolver / Безпечна автоматизація підтримки</span><span>AI пропонує · Правила вирішують</span></footer>
    </div>
  )
}

export default App
