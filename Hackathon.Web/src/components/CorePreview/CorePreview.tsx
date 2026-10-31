import type { DecisionStatus, ResolveTicketResponse, ReviewActionResponse, TimelineEvent } from '../../api/resolver'

const INTENTS: Record<string, string> = {
  refund_duplicate_charge: 'Подвійне списання',
  refund_request: 'Запит на повернення',
  cancel_subscription: 'Скасування підписки',
  change_plan: 'Зміна плану',
  reset_password: 'Доступ до акаунта',
  question: 'Питання',
  other: 'Інше',
}

const TONES: Record<string, string> = {
  angry: 'злість',
  frustrated: 'роздратування',
  neutral: 'нейтральний',
  satisfied: 'задоволення',
  confused: 'розгубленість',
  positive: 'позитивний',
  negative: 'негативний',
}

const LEVELS: Record<string, string> = { low: 'Низька', medium: 'Середня', high: 'Висока' }

const ACTIONS: Record<string, string> = {
  none: 'Без дії',
  refund: 'Повернути списання',
  cancel_subscription: 'Скасувати підписку',
  change_plan: 'Змінити план',
  reset_password: 'Надіслати скидання пароля',
}

const DECISIONS: Record<DecisionStatus, string> = {
  auto: 'Автодозвіл',
  needs_approval: 'Потрібне погодження',
  blocked: 'Заблоковано',
  no_action: 'Дія не потрібна',
}

const STEPS: Record<string, string> = {
  message: 'Звернення отримано',
  triage: 'Намір і тон визначено',
  policy: 'Політику перевірено',
  action: 'Дія',
  audit: 'Audit записано',
  review: 'Рішення агента',
}

const money = (amount: number) => `₴${amount.toLocaleString('uk-UA')}`

interface CorePreviewProps {
  result: ResolveTicketResponse | null
  review: ReviewActionResponse | null
  loading: boolean
  reviewing: boolean
  error: string | null
  onReview: (approve: boolean) => void
}

export function CorePreview({ result, review, loading, reviewing, error, onReview }: CorePreviewProps) {
  return (
    <article className="decision-panel" aria-live="polite">
      <div className="panel-topline">
        <span className="eyebrow">Рішення агента</span>
        {result && !loading && <StatusLabel result={result} review={review} />}
      </div>

      {error && <p className="error-message" role="alert">Не вдалося обробити: {error}</p>}

      {loading && (
        <div className="loading-state" aria-busy="true">
          <span className="status-dot status-dot--success pulse" aria-hidden="true" />
          Gemma 4 читає звернення, визначає намір і тон…
        </div>
      )}

      {!loading && !result && !error && (
        <p className="idle-state">Оберіть сценарій і натисніть «Обробити звернення». Рішення з'явиться тут.</p>
      )}

      {!loading && result && <Decision result={result} review={review} reviewing={reviewing} onReview={onReview} />}
    </article>
  )
}

function StatusLabel({ result, review }: { result: ResolveTicketResponse; review: ReviewActionResponse | null }) {
  const [text, success] = review
    ? review.status === 'approved' ? ['Погоджено агентом', true] : ['Відхилено агентом', false]
    : result.decision.status === 'auto' ? ['Вирішено автоматично', true]
    : result.decision.status === 'no_action' ? ['Відповідь надіслано', true]
    : result.decision.status === 'blocked' ? ['Заблоковано правилами', false]
    : ['Потрібен ваш перегляд', false]

  return (
    <span className={success ? 'status-label status-label--success' : 'status-label status-label--warning'}>
      <span className={`status-dot status-dot--${success ? 'success' : 'danger'}`} aria-hidden="true" /> {text}
    </span>
  )
}

function Decision({
  result,
  review,
  reviewing,
  onReview,
}: {
  result: ResolveTicketResponse
  review: ReviewActionResponse | null
  reviewing: boolean
  onReview: (approve: boolean) => void
}) {
  const { triage, decision, action, pendingAction } = result
  const proposed = triage.proposedAction
  const toneChanged = triage.literalTone !== triage.actualTone

  return (
    <>
      {result.source === 'fallback' && (
        <p className="fallback-note">Gemma 4 недоступна — показано кешований тріаж, автоматичні дії вимкнено.</p>
      )}

      <div className="decision-intro">
        <span className="decision-kicker">Клієнт хоче</span>
        <h2>{triage.trueGoal}</h2>
      </div>

      <dl className="facts">
        <div><dt>Намір</dt><dd>{INTENTS[triage.intent] ?? triage.intent}</dd></div>
        <div>
          <dt>Тон</dt>
          <dd>
            {toneChanged && <s className="literal-tone">{TONES[triage.literalTone] ?? triage.literalTone}</s>}{' '}
            {TONES[triage.actualTone] ?? triage.actualTone}
          </dd>
        </div>
        <div><dt>Сарказм</dt><dd className={triage.sarcasm ? 'accent-text' : ''}>{triage.sarcasm ? 'Виявлено' : 'Не виявлено'}</dd></div>
        <div><dt>Терміновість</dt><dd>{LEVELS[triage.urgency] ?? triage.urgency}</dd></div>
        <div><dt>Ризик</dt><dd>{LEVELS[triage.risk] ?? triage.risk}</dd></div>
        <div>
          <dt>Маніпуляція</dt>
          <dd className={triage.manipulationAttempt ? 'accent-text' : ''}>{triage.manipulationAttempt ? 'Виявлено' : 'Ні'}</dd>
        </div>
      </dl>

      {triage.facts.length > 0 && (
        <ul className="found-facts" aria-label="Знайдені факти">
          {triage.facts.map((fact) => <li key={fact}>{fact}</li>)}
        </ul>
      )}

      <div className="action-box">
        <span className="decision-kicker">Запропонована дія</span>
        <div className="action-row">
          <strong>{ACTIONS[proposed.type] ?? proposed.type}{proposed.chargeId && ` · ${proposed.chargeId}`}</strong>
          <strong>{proposed.amount != null ? money(proposed.amount) : '—'}</strong>
        </div>
        <span className="policy-note">{decision.reason}</span>
      </div>

      <div className="policy-result">
        <span>Результат політики · <code>{decision.rule}</code></span>
        <strong>{DECISIONS[decision.status]}</strong>
      </div>

      {action.executed && <Outcome success title={action.summary} note="Виконано автоматично, без участі людини" />}

      {pendingAction && !review && (
        <div className="review-box">
          {pendingAction.canApprove ? (
            <button type="button" className="primary-button" disabled={reviewing} onClick={() => onReview(true)}>
              {reviewing ? 'Виконуємо…' : 'Погодити дію'} <span aria-hidden="true">→</span>
            </button>
          ) : (
            <p className="policy-note">Погодити неможливо: {pendingAction.cannotApproveReason}</p>
          )}
          <button type="button" className="secondary-button" disabled={reviewing} onClick={() => onReview(false)}>
            Відхилити
          </button>
        </div>
      )}

      {review && (
        <Outcome
          success={review.status === 'approved'}
          title={review.action.summary}
          note={review.status === 'approved' ? 'Погоджено агентом · білінг оновлено' : 'Білінг не змінено'}
        />
      )}

      <div className="reply-box">
        <span className="decision-kicker">Відповідь клієнту</span>
        <p>{review?.reply ?? result.reply}</p>
      </div>
    </>
  )
}

function Outcome({ success, title, note }: { success: boolean; title: string; note: string }) {
  return (
    <div className={success ? 'success-message' : 'success-message success-message--muted'}>
      <span className={`status-dot status-dot--${success ? 'success' : 'danger'}`} aria-hidden="true" />
      <div><strong>{title}</strong><span>{note}</span></div>
    </div>
  )
}

export function DecisionTrail({ events }: { events: TimelineEvent[] }) {
  return (
    <section className="trail" aria-labelledby="trail-title">
      <div className="trail-heading">
        <div><span className="eyebrow">Що відбулося</span><h2 id="trail-title">Історія рішення</h2></div>
        <span className="ticket-id">Audit log</span>
      </div>
      {events.length === 0 ? (
        <p className="idle-state">Кроки message → triage → policy → action → audit з'являться після обробки.</p>
      ) : (
        <ol className="trail-list">
          {events.map((event, index) => (
            <li key={event.step} className={`trail-step trail-step--${event.status}`}>
              <span className="step-number">{String(index + 1).padStart(2, '0')}</span>
              <div><strong>{STEPS[event.step] ?? event.step}</strong><span>{event.detail}</span></div>
            </li>
          ))}
        </ol>
      )}
    </section>
  )
}
