export const sourceLabels = {
  seed: 'Затверджений шаблон', gemma: '', fallback: 'Резервний режим',
}

const modelFailureLabels: Record<string, string> = {
  timeout: 'Перевищено час очікування відповіді моделі.',
  invalid_json: 'Відповідь моделі містить некоректний JSON.',
  invalid_schema: 'Структура відповіді моделі не відповідає очікуваному формату.',
  empty_response: 'Модель повернула порожню відповідь.',
  network: 'Помилка мережі під час звернення до моделі.',
}

export function labelModelFailure(reason?: string) {
  if (!reason) return 'Сервер не вказав причину резервного режиму.'
  if (modelFailureLabels[reason]) return modelFailureLabels[reason]
  const httpStatus = /^http_(\d{3})$/.exec(reason)?.[1]
  return httpStatus
    ? `Сервіс моделі повернув HTTP ${httpStatus}.`
    : 'Сервер повідомив про іншу помилку моделі.'
}

export const statusLabels = {
  executed: 'Виконано', needs_approval: 'Очікує погодження',
  blocked: 'Дію заблоковано', needs_instruction: 'Потрібна інструкція', needs_clarification: 'Очікує уточнення',
}

const actionLabels: Record<string, string> = {
  refund_duplicate: 'Повернення повторного списання',
  activate_service: 'Активація послуги',
  restore_access: 'Відновлення доступу',
  request_password_reset: 'Запит на скидання пароля',
  cancel_subscription: 'Скасування підписки',
  manual_review: 'Ручний розгляд',
}
export const labelAction = (value: string) => actionLabels[value] ?? 'Невідома дія — потрібна перевірка фахівця'

const fieldLabels: Record<string, string> = {
  charges: 'Списання', paymentConfirmed: 'Підтвердження оплати', subscription: 'Підписка',
  entitlementValid: 'Чинність права доступу', securityBlocked: 'Безпекове блокування',
  access: 'Доступ', taxId: 'Податковий номер', phone: 'Номер телефону',
  email: 'Електронна пошта', resetToken: 'Ключ скидання пароля', caseLogs: 'Журнали сервісів',
}
export const labelField = (value: string) => fieldLabels[value] ?? 'Інше поле'

const stepLabels: Record<string, string> = {
  privacy: 'Захист даних', classification: 'Аналіз звернення', action: 'Перевірка дії', evidence: 'Перевірка журналів',
  human_approval: 'Погодження фахівцем', instruction: 'Вибір інструкції',
  policy: 'Перевірка правил', audit: 'Запис результату', review: 'Перевірка фахівцем',
}
export const labelStep = (value: string) => stepLabels[value] ?? 'Етап перевірки'

const stateLabels: Record<string, string> = {
  ...statusLabels, active: 'Активна', inactive: 'Неактивна', cancelled: 'Скасована', canceled: 'Скасована',
  enabled: 'Відкрито', disabled: 'Закрито', granted: 'Надано', restored: 'Відновлено',
  blocked: 'Заблоковано', suspended: 'Призупинено', paid: 'Сплачено', refunded: 'Повернено',
  pending: 'Очікує', expired: 'Термін минув', valid: 'Дійсна', none: 'Відсутня',
  filtered: 'Дані відфільтровано', gemma: 'Проаналізовано', fallback: 'Резервний режим',
  done: 'Завершено', approved: 'Погоджено', rejected: 'Відхилено', skipped: 'Пропущено',
  reset_requested: 'Запит на скидання створено', confirmed: 'Підтверджено', not_confirmed: 'Не підтверджено', unknown: 'Недостатньо даних',
}
export const labelState = (value: string) => stateLabels[value] ?? 'Потребує уточнення'
