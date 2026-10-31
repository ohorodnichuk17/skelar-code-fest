export type Source = 'gemma' | 'fallback'
export interface SupportCase { id: string; title: string; message: string }
export interface Company {
  id: string; name: string; country: string; profileLabel: string; cases: SupportCase[]
}
export interface Instruction {
  id: string; companyId: string; title: string; summary: string; steps: string[]
  requiredFields: string[]; action: string; status: 'active' | 'draft'
  version: number; source: 'seed' | Source
}
export interface Catalog { companies: Company[]; instructions: Instruction[] }
export interface CaseVerification {
  status: 'confirmed' | 'not_confirmed' | 'unknown'
  summary: string
  logs: { id: string; at: string; service: string; event: string; resourceId: string; detail: string }[]
  resolutionPath: string[]
}
export interface Resolution {
  id: string; companyId: string; caseId: string; instruction: Instruction | null
  status: 'executed' | 'needs_approval' | 'blocked' | 'needs_instruction' | 'needs_clarification'
  source: Source; summary: string; reply: string
  clarificationQuestion?: string | null
  modelFailure?: string
  dataAccess: { field: string; label: string; allowed: boolean; reason: string }[]
  timeline: { step: string; status: string; detail: string }[]
  state: { subscription: string; access: string; charges: { id: string; amount: number; status: string }[] }
  draftId: string | null
  verification?: CaseVerification
}
export interface CaseRequest { companyId: string; caseId: string; message: string }

async function request<T>(path: string, signal: AbortSignal, body?: unknown): Promise<T> {
  let response: Response
  try {
    response = await fetch(`/api/support${path}`, {
      signal,
      ...(body === undefined ? {} : {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
      }),
    })
  } catch (error) {
    if (signal.aborted) throw error
    throw new Error('Немає зв’язку із сервером. Перевірте підключення та повторіть запит.')
  }
  if (!response.ok) {
    // Never reflect server error content that could contain submitted sensitive text.
    const messages: Record<number, string> = {
      400: 'Перевірте текст і вибране звернення. Сервер відхилив некоректний запит.',
      404: 'Звернення або інструкція недоступні для цієї компанії. Оновіть сторінку.',
      409: 'Дію вже погоджено або її стан змінився. Оновіть сторінку чи перевірте звернення.',
    }
    throw new Error(messages[response.status] ?? `Помилка сервера (${response.status}). Спробуйте пізніше.`)
  }
  try { return await response.json() as T }
  catch { throw new Error('Сервер повернув неочікувану відповідь. Перевірте доступність Support API.') }
}

export const getSupportCatalog = (signal: AbortSignal) => request<Catalog>('/catalog', signal)
export const resolveSupport = (body: CaseRequest, signal: AbortSignal) => request<Resolution>('/resolve', signal, body)
export const createInstructionDraft = (body: CaseRequest & { resolution: string }, signal: AbortSignal) =>
  request<Instruction>('/instructions/drafts', signal, body)
export const approveInstruction = (id: string, companyId: string, signal: AbortSignal) =>
  request<Instruction>(`/instructions/${encodeURIComponent(id)}/approve`, signal, { companyId })
export const approveResolution = (id: string, companyId: string, signal: AbortSignal) =>
  request<Resolution>(`/resolutions/${encodeURIComponent(id)}/approve`, signal, { companyId })
