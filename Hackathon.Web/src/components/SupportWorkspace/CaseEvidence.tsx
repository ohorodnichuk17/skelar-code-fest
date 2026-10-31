import type { CaseVerification } from '../../api/support'

const verificationLabels = {
  confirmed: 'Підтверджено журналами',
  not_confirmed: 'Не підтверджено журналами',
  unknown: 'Недостатньо даних',
}

const logTime = new Intl.DateTimeFormat('uk-UA', {
  dateStyle: 'short', timeStyle: 'medium',
})

function LogTimestamp({ at }: { at: string }) {
  const date = new Date(at)
  return Number.isNaN(date.getTime())
    ? <span className="muted">Час не вказано</span>
    : <time dateTime={at}>{logTime.format(date)}</time>
}

export function CaseEvidence({ verification, executed }: { verification?: CaseVerification; executed: boolean }) {
  if (!verification) return null

  return <section className="result-section case-evidence" aria-label="Перевірка журналами">
    <div className={`evidence-verdict ${verification.status}`}>
      <h3>{verificationLabels[verification.status]}</h3>
      <p>{verification.summary}</p>
    </div>
    <div className="evidence-logs">
      <div className="section-heading"><h3>Демонстраційні журнали на момент звернення</h3><span className="count">{verification.logs.length}</span></div>
      <p className="small muted">Це історичні записи на момент звернення. Поточний стан і результат операції показано окремо.</p>
      {verification.logs.length ? <ol className="evidence-log-list">{verification.logs.map((log) => <li key={log.id}>
        <div className="evidence-log-meta"><LogTimestamp at={log.at} /><strong>{log.service}</strong></div>
        <div className="evidence-log-event"><code>{log.event}</code><code>{log.resourceId}</code></div>
        <p>{log.detail}</p>
      </li>)}</ol> : <p className="small muted">Сервер не надав записів журналу для цього звернення.</p>}
    </div>
    {verification.resolutionPath.length > 0 && <div className="evidence-path">
      <h3>{executed ? 'Виконаний шлях вирішення' : 'Запланований шлях вирішення'}</h3>
      <ol>{verification.resolutionPath.map((step, index) => <li key={`${index}-${step}`}>{step}</li>)}</ol>
    </div>}
  </section>
}
