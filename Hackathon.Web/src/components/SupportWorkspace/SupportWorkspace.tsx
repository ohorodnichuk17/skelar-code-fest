import { useEffect, useRef, useState } from 'react'
import {
  approveInstruction, approveResolution, createInstructionDraft, getSupportCatalog, resolveSupport,
  type Catalog, type Company, type Instruction, type Resolution, type SupportCase,
} from '../../api/support'
import { labelAction, labelField, labelModelFailure, labelState, labelStep, sourceLabels, statusLabels } from './labels'
import { CaseEvidence } from './CaseEvidence'
import { ProductSelect } from './ProductSelect'
import { ResolverLogo } from './ResolverLogo'

const tabs = ['Звернення', 'Інструкції', 'Доступ до даних'] as const
type Tab = typeof tabs[number]

export function SupportWorkspace() {
  const [catalog, setCatalog] = useState<Catalog | null>(null)
  const [error, setError] = useState('')
  const [reload, setReload] = useState(0)
  const [loading, setLoading] = useState(true)
  const [companyId, setCompanyId] = useState('')
  const [caseId, setCaseId] = useState('')
  const [tab, setTab] = useState<Tab>('Звернення')

  useEffect(() => {
    const controller = new AbortController()
    getSupportCatalog(controller.signal).then((data) => {
      if (!controller.signal.aborted) { setCatalog(data); setLoading(false) }
    }).catch((e: Error) => {
      if (!controller.signal.aborted) { setError(e.message); setLoading(false) }
    })
    return () => controller.abort()
  }, [reload])

  const company = catalog?.companies.find((item) => item.id === companyId) ?? catalog?.companies[0]
  const selectedCase = company?.cases.find((item) => item.id === caseId) ?? company?.cases[0]
  const instructions = catalog?.instructions.filter((item) => item.companyId === company?.id) ?? []
  const updateInstruction = (instruction: Instruction) => setCatalog((current) => current && ({
    ...current, instructions: [...current.instructions.filter((item) => item.id !== instruction.id), instruction],
  }))

  return <div className="support-app" lang="uk">
    <a className="skip-link" href="#support-main">Перейти до робочого місця</a>
    <header className="support-topbar">
      <ResolverLogo />
    </header>
    <main id="support-main" className="support-main">
      <section className="workspace-heading" aria-labelledby="support-title">
        <div><p className="eyebrow">Робоче місце підтримки</p><h1 id="support-title">Підтримка за правилами.</h1><p className="muted">Звернення → інструкція → перевірка доступу → рішення.</p></div>
        <ProductSelect companies={catalog?.companies ?? []} selectedId={company?.id} disabled={!catalog || loading}
          onChange={(id) => { setCompanyId(id); setCaseId('') }} />
      </section>
      <div className="profile-bar"><span>{company ? `Профіль доступу · ${company.name} · ${company.country}` : 'Профіль компанії ще не завантажено'}</span></div>
      <nav className="workspace-tabs" aria-label="Розділи робочого місця">{tabs.map((item) => <button key={item} type="button" aria-pressed={tab === item} onClick={() => setTab(item)}>{item}</button>)}</nav>
      {loading && <div className="catalog-loading" role="status"><div className="skeleton" /><div className="skeleton" />Завантаження каталогу…</div>}
      {error && <div className="error-box" role="alert"><p>{error}</p><button className="button secondary" onClick={() => { setError(''); setLoading(true); setReload((value) => value + 1) }}>Оновити каталог</button></div>}
      {!loading && catalog && !company && <p className="empty-state">У каталозі немає компаній. Додайте демонстраційні дані на сервері.</p>}
      {company && selectedCase && <div className="support-layout">
        <aside className="case-sidebar" aria-label="Звернення компанії"><div className="section-heading"><h2>Звернення</h2><span className="count">{company.cases.length}</span></div>
          <p className="small muted">Типові скарги та новий випадок</p>
          <div className="case-list">{company.cases.map((item, index) => <button type="button" key={item.id} aria-current={item.id === selectedCase.id ? 'true' : undefined} onClick={() => setCaseId(item.id)}><span className="case-number">{String(index + 1).padStart(2, '0')}</span><span>{item.title}</span><span className="case-arrow" aria-hidden="true">↗</span></button>)}</div>
          <p className="sidebar-note">Вибір компанії ізолює її звернення та інструкції.</p>
        </aside>
        <CaseWorkspace key={`${company.id}/${selectedCase.id}`} company={company} supportCase={selectedCase} instructions={instructions} tab={tab} onInstruction={updateInstruction} />
      </div>}
      {company && !selectedCase && <p className="empty-state">У цієї компанії ще немає звернень.</p>}
      <footer className="support-footer"><span>Стан зберігається в пам’яті й скидається після перезапуску сервера.</span><span>Без реальних платежів і надсилання листів.</span></footer>
    </main>
  </div>
}

function Procedure({ instruction }: { instruction: Instruction }) {
  return <article className="procedure">
    <div className="section-heading"><h3>{instruction.title}</h3><span className={`badge ${instruction.status === 'draft' ? 'warning' : ''}`}>{instruction.status === 'active' ? 'Активна' : 'Чернетка'} · v{instruction.version}</span></div>
    <p>{instruction.summary}</p>
    <ol>{instruction.steps.map((step, index) => <li key={`${index}-${step}`}>{step}</li>)}</ol>
    <p className="small muted">{sourceLabels[instruction.source] && <>{sourceLabels[instruction.source]} · </>}Дія: {labelAction(instruction.action)}</p>
    <p className="small muted">Потрібні поля: {instruction.requiredFields.length ? instruction.requiredFields.map(labelField).join(', ') : 'не потрібні'}</p>
  </article>
}

function CaseWorkspace({ company, supportCase, instructions, tab, onInstruction }: {
  company: Company; supportCase: SupportCase; instructions: Instruction[]; tab: Tab
  onInstruction: (instruction: Instruction) => void
}) {
  const [message, setMessage] = useState(supportCase.message)
  const [solution, setSolution] = useState('')
  const [result, setResult] = useState<Resolution | null>(null)
  const [draft, setDraft] = useState<Instruction | null>(null)
  const [busy, setBusy] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [clarificationOpen, setClarificationOpen] = useState(false)
  const [clarificationAnswer, setClarificationAnswer] = useState('')
  const [clarificationContext, setClarificationContext] = useState('')
  const [evidence, setEvidence] = useState<{ name: string, url: string }[]>([])
  const requestRef = useRef<AbortController | null>(null)
  const evidenceInputRef = useRef<HTMLInputElement | null>(null)
  const evidenceRef = useRef(evidence)
  useEffect(() => { evidenceRef.current = evidence }, [evidence])
  useEffect(() => () => requestRef.current?.abort(), [])
  useEffect(() => () => evidenceRef.current.forEach((item) => URL.revokeObjectURL(item.url)), [])
  // Імітація: скріншот лишається в браузері й не надсилається на сервер чи моделі.
  const addEvidence = (files: FileList | null) => {
    const images = Array.from(files ?? []).filter((file) => file.type.startsWith('image/'))
    if (!images.length) return
    setEvidence((current) => [...current, ...images.map((file) => ({ name: file.name, url: URL.createObjectURL(file) }))])
    setNotice('Скріншот додано до звернення як доказ.')
  }
  const removeEvidence = (url: string) => {
    URL.revokeObjectURL(url)
    setEvidence((current) => current.filter((item) => item.url !== url))
  }
  const body = { companyId: company.id, caseId: supportCase.id, message: message.trim() }
  const clarificationRemaining = Math.max(0, 4000 - clarificationContext.length - '\n\nУточнення користувача: '.length)
  const manualReview = result?.instruction?.action === 'manual_review'
  const canApproveAction = result?.status === 'needs_approval'
    && result.verification?.status === 'confirmed' && !manualReview

  async function run(label: string, action: (signal: AbortSignal) => Promise<void>) {
    if (requestRef.current) return
    const controller = new AbortController()
    requestRef.current = controller
    setBusy(label); setError(''); setNotice('')
    try { await action(controller.signal) }
    catch (e) { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : 'Не вдалося виконати запит. Спробуйте ще раз.') }
    finally {
      if (!controller.signal.aborted) { requestRef.current = null; setBusy('') }
    }
  }
  const submitResolution = (requestMessage: string) => run('Перевірка звернення…', async (signal) => {
    const response = await resolveSupport({ ...body, message: requestMessage }, signal)
    if (signal.aborted) return
    setResult(response)
    if (response.status === 'needs_clarification' && response.clarificationQuestion) {
      setClarificationContext(requestMessage)
      setClarificationAnswer('')
      setClarificationOpen(true)
      return
    }
    setClarificationOpen(false)
    setClarificationContext('')
    setDraft(null)
    setNotice('Рішення отримано із сервера.')
  })
  const resolve = () => {
    setResult(null)
    setClarificationContext('')
    return submitResolution(body.message)
  }
  const submitClarification = () => {
    const answer = clarificationAnswer.trim()
    if (!answer || !result?.clarificationQuestion) return
    return submitResolution(`${clarificationContext}\n\nУточнення користувача: ${answer}`)
  }
  const closeClarification = () => {
    setClarificationOpen(false)
    setClarificationContext('')
    setClarificationAnswer('')
    setResult(null)
  }
  const createDraft = () => run('Створення чернетки…', async (signal) => {
    const instruction = await createInstructionDraft({ ...body, resolution: solution.trim() }, signal)
    if (!signal.aborted) { setDraft(instruction); onInstruction(instruction); setNotice('Чернетку створено. Перевірте її перед погодженням.') }
  })
  const approveDraft = (instruction: Instruction) => run('Погодження інструкції…', async (signal) => {
    const updated = await approveInstruction(instruction.id, company.id, signal)
    if (!signal.aborted) { onInstruction(updated); if (draft?.id === updated.id) setDraft(updated); setNotice('Інструкція активна. Повторно перевірте те саме звернення для її застосування.') }
  })
  const approveAction = () => {
    if (!result || !canApproveAction) return
    return run('Погодження операції…', async (signal) => {
      const response = await approveResolution(result.id, company.id, signal)
      if (!signal.aborted) { setResult(response); setNotice('Сервер повторно перевірив правила й повернув актуальний стан.') }
    })
  }

  return <section className="case-content" aria-label={tab}>
    {clarificationOpen && result?.clarificationQuestion && <div className="clarification-backdrop" onKeyDown={(event) => {
      if (event.key === 'Escape') { event.preventDefault(); closeClarification(); return }
      if (event.key === 'Tab') {
        const dialog = event.currentTarget.querySelector<HTMLElement>('[role="dialog"]')
        const controls = Array.from(dialog?.querySelectorAll<HTMLElement>('textarea:not([disabled]), button:not([disabled])') ?? [])
        if (!controls.length) return
        const first = controls[0]
        const last = controls[controls.length - 1]
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
      }
    }}>
      <section className="clarification-dialog" role="dialog" aria-modal="true" aria-labelledby="clarification-title" aria-describedby="clarification-question">
        <p className="eyebrow">Уточнення звернення</p>
        <h2 id="clarification-title">Потрібно трохи більше інформації</h2>
        <p id="clarification-question">{result.clarificationQuestion}</p>
        <form onSubmit={(event) => { event.preventDefault(); void submitClarification() }}>
          <label htmlFor="clarification-answer">Ваша відповідь</label>
          <textarea id="clarification-answer" autoFocus required rows={4} maxLength={clarificationRemaining} value={clarificationAnswer} disabled={!!busy} onChange={(event) => setClarificationAnswer(event.target.value)} />
          {clarificationRemaining === 0 && <p className="clarification-error" role="alert">Звернення досягло ліміту 4000 символів. Закрийте вікно та скоротіть його перед повторним надсиланням.</p>}
          {error && <p className="clarification-error" role="alert">{error}</p>}
          <div className="clarification-actions">
            <button className="button secondary" type="button" disabled={!!busy} onClick={closeClarification}>Закрити</button>
            <button className="button primary" type="submit" disabled={!!busy || !clarificationAnswer.trim() || clarificationRemaining === 0}>{busy || 'Продовжити'}</button>
          </div>
        </form>
      </section>
    </div>}
    <div className="operation-feedback" aria-live="polite" aria-atomic="true">{busy || notice}</div>
    {error && <div className="error-box" role="alert">{error}</div>}
    <div hidden={tab !== 'Звернення'} className="case-columns">
      <div className="case-editor">
        <div className="section-heading"><h2>{supportCase.title}</h2><span className="eyebrow">Звернення</span></div>
        <form onSubmit={(e) => { e.preventDefault(); void resolve() }}>
          <label htmlFor="case-message">Повідомлення клієнта</label>
          <textarea id="case-message" name="message" autoComplete="off" rows={5} required maxLength={4000} value={message} disabled={!!busy} onChange={(e) => { setMessage(e.target.value); setResult(null); setDraft(null); setNotice(''); setError('') }} aria-describedby="message-help" />
          <p id="message-help" className="small muted">Можна відредагувати текст. Сервер фільтрує дані перед запитом до моделі.</p>
          <div className="evidence-attach">
            <input ref={evidenceInputRef} id="case-evidence" type="file" accept="image/*" multiple hidden onChange={(e) => { addEvidence(e.target.files); e.target.value = '' }} />
            <button className="button secondary" type="button" disabled={!!busy} onClick={() => evidenceInputRef.current?.click()}>Додати скріншот<span aria-hidden="true">+</span></button>
            {evidence.length > 0 && <ul className="evidence-list" aria-label="Додані докази">{evidence.map((item) => <li key={item.url}>
              <img src={item.url} alt={`Скріншот ${item.name}`} />
              <span><strong>{item.name}</strong><span className="small muted">Доказ від клієнта</span></span>
              <button type="button" className="evidence-remove" aria-label={`Прибрати ${item.name}`} onClick={() => removeEvidence(item.url)}>×</button>
            </li>)}</ul>}
          </div>
          <button className="button primary" disabled={!!busy || !message.trim()} type="submit">{busy === 'Перевірка звернення…' ? busy : 'Перевірити та застосувати правила'}<span aria-hidden="true">↗</span></button>
        </form>
        <section className="selected-procedure" aria-label="Вибрана інструкція"><p className="eyebrow">Застосована процедура</p>
          {result?.instruction ? <Procedure instruction={result.instruction} /> : <p className="muted small">Після перевірки тут з’явиться інструкція, яку вибрав сервер. До перевірки її не призначено.</p>}
        </section>
        <details className="draft-form" open={result?.status === 'needs_instruction' || !!draft}>
          <summary>Нова інструкція з рішення фахівця</summary>
          <p className="small muted">Опишіть підтверджене вами рішення без персональних даних. Чернетка не виконує дій і не розширює доступ.</p>
          <form onSubmit={(e) => { e.preventDefault(); void createDraft() }}>
            <label htmlFor="confirmed-solution">Підтверджене рішення</label>
            <textarea id="confirmed-solution" name="resolution" autoComplete="off" required maxLength={4000} rows={3} value={solution} disabled={!!busy} onChange={(e) => setSolution(e.target.value)} placeholder="Наприклад: передати звернення відповідальному агенту…" />
            <button className="button secondary" type="submit" disabled={!!busy || !message.trim() || !solution.trim()}>Створити чернетку</button>
          </form>
          {draft && <div className="draft-preview"><Procedure instruction={draft} />{draft.status === 'draft' ? <button className="button secondary" disabled={!!busy} onClick={() => void approveDraft(draft)}>Погодити інструкцію</button> : <button className="button secondary" disabled={!!busy} onClick={() => void resolve()}>Повторити з активною інструкцією</button>}</div>}
        </details>
      </div>
      <aside className="result-pane" aria-label="Рішення та стан" aria-busy={!!busy}>
        <div className="section-heading"><h2>Рішення та стан</h2><span className="small muted">Серверні дані</span></div>
        {!result ? <div className="empty-result"><span className="empty-mark" aria-hidden="true">↗</span><h3>{busy ? 'Перевіряємо звернення…' : 'Готово до перевірки'}</h3><p>Запустіть правила, щоб побачити рішення, відповідь і фактичний стан демо.</p>{busy && <div className="skeleton" />}</div> : <>
          <div className={`decision-status ${result.status === 'executed' ? 'executed' : ''}`}><span className="eyebrow">Результат перевірки</span><h3>{manualReview && result.instruction?.status === 'active' ? 'Ручний розгляд за інструкцією' : statusLabels[result.status]}</h3><p>{result.summary}</p></div>
          {result.source === 'fallback' && <p className="source-note fallback">{sourceLabels.fallback} · {labelModelFailure(result.modelFailure)}{result.status !== 'executed' && ' Потрібна перевірка фахівцем.'}</p>}
          {manualReview && result.instruction?.status === 'active' && <div className="approval-box"><p>Виконайте кроки інструкції вручну. Автоматична операція для цього звернення недоступна.</p></div>}
          <CaseEvidence verification={result.verification} executed={result.status === 'executed'} />
          {canApproveAction && <div className="approval-box"><p>Погодження запускає повторну перевірку журналів, доступу й операцію в демо.</p><button className="button primary" disabled={!!busy} onClick={() => void approveAction()}>Погодити операцію</button></div>}
          <section className="result-section"><h3>Відповідь клієнту</h3><p className="reply-text">{result.reply || 'Сервер не сформував відповіді.'}</p><p className="small muted">Текст відповіді. Лист не надсилається.</p></section>
          <section className="result-section"><h3>Поточний стан демо</h3><dl className="state-list"><div><dt>Підписка</dt><dd>{labelState(result.state.subscription)}</dd></div><div><dt>Доступ</dt><dd>{labelState(result.state.access)}</dd></div></dl>
            {result.state.charges.length ? <table className="charges-table"><caption className="visually-hidden">Списання у демонстраційному середовищі</caption><thead><tr><th scope="col">Списання</th><th scope="col">Сума списання</th><th scope="col">Стан</th></tr></thead><tbody>{result.state.charges.map((charge) => <tr key={charge.id}><td><code>{charge.id}</code></td><td><strong>{new Intl.NumberFormat('uk-UA', { maximumFractionDigits: 2 }).format(charge.amount)}</strong></td><td>{labelState(charge.status)}</td></tr>)}</tbody></table> : <p className="small muted">Списань немає.</p>}
          </section>
          <details className="timeline"><summary>Журнал перевірки · {result.timeline.length}</summary><ol>{result.timeline.map((event, index) => <li key={index}><strong>{labelStep(event.step)}</strong><span className="small muted">{labelState(event.status)}</span><p>{event.detail}</p></li>)}</ol></details>
        </>}
      </aside>
    </div>
    <section hidden={tab !== 'Інструкції'} className="catalog-panel" aria-label="Інструкції компанії"><div className="section-heading"><h2>Інструкції компанії</h2><span className="count">{instructions.length}</span></div><p className="muted">Активні інструкції доступні для повторної перевірки звернення. Чернетки потребують погодження фахівця.</p>
      {!instructions.length && <p className="empty-state">Інструкцій поки немає. Створіть чернетку з підтвердженого рішення у вкладці «Звернення».</p>}
      {instructions.map((instruction) => <div key={instruction.id} className="catalog-instruction"><Procedure instruction={instruction} />{instruction.status === 'draft' && <button className="button secondary" disabled={!!busy} onClick={() => void approveDraft(instruction)}>Погодити інструкцію</button>}</div>)}
    </section>
    <section hidden={tab !== 'Доступ до даних'} className="catalog-panel" aria-label="Політика доступу"><h2>Доступ до даних</h2><p className="muted">Лише назви полів і причини рішення. Значення обмежених полів не відображаються.</p>
      {!result?.dataAccess.length ? <p className="empty-state">Спочатку перевірте звернення. Тут з’являться дозволи для цієї компанії та задачі.</p> : <ul className="access-list">{result.dataAccess.map((field) => <li key={field.field}><div><strong>{labelField(field.field) === 'Інше поле' && field.label !== field.field ? field.label : labelField(field.field)}</strong><code>{field.field}</code></div><span className={`badge ${field.allowed ? '' : 'warning'}`}>{field.allowed ? 'Дозволено' : 'Заборонено'}</span><p>{field.reason}</p></li>)}</ul>}
    </section>
  </section>
}
