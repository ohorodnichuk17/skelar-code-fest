// Контракт із Hakathon.Server/Features/CoreFeature/ResolveTicketContracts.cs

export type DecisionStatus = 'auto' | 'needs_approval' | 'blocked' | 'no_action'

export interface ProposedAction {
  type: 'none' | 'refund' | 'cancel_subscription' | 'change_plan' | 'reset_password'
  chargeId: string | null
  amount: number | null
}

export interface Triage {
  trueGoal: string
  intent: string
  literalTone: string
  actualTone: string
  sarcasm: boolean
  urgency: string
  facts: string[]
  proposedAction: ProposedAction
  risk: string
  manipulationAttempt: boolean
}

export interface Charge {
  id: string
  amount: number
  currency: string
  chargedAt: string
  description: string
  status: 'paid' | 'refunded'
}

export interface BillingState {
  customerName: string
  plan: string
  charges: Charge[]
}

export interface TimelineEvent {
  step: 'message' | 'triage' | 'policy' | 'action' | 'audit' | 'review'
  status: string
  detail: string
}

export interface ResolveTicketResponse {
  ticketId: string
  source: 'gemma' | 'fallback'
  triage: Triage
  decision: { status: DecisionStatus; rule: string; reason: string }
  action: { executed: boolean; summary: string }
  billing: BillingState
  timeline: TimelineEvent[]
  reply: string
  pendingAction: PendingAction | null
}

export interface PendingAction {
  id: string
  canApprove: boolean
  cannotApproveReason: string | null
}

export interface ReviewActionResponse {
  actionId: string
  status: 'approved' | 'rejected'
  action: { executed: boolean; summary: string }
  billing: BillingState
  event: TimelineEvent
  reply: string
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, init)
  if (!response.ok) {
    const error = await response.json().catch(() => null)
    throw new Error(error?.error ?? `Сервер відповів ${response.status}`)
  }
  return response.json() as Promise<T>
}

export const resolveTicket = (ticketId: string, message: string) =>
  request<ResolveTicketResponse>(`/api/tickets/${encodeURIComponent(ticketId)}/messages`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message }),
  })

export const getBilling = (ticketId: string) =>
  request<BillingState>(`/api/tickets/${encodeURIComponent(ticketId)}/billing`)

export const reviewAction = (actionId: string, approve: boolean) =>
  request<ReviewActionResponse>(`/api/actions/${encodeURIComponent(actionId)}/${approve ? 'approve' : 'reject'}`, {
    method: 'POST',
  })
