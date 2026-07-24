export function todayIso(): string {
  const now = new Date()
  const year = now.getFullYear()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export function createClientId(prefix: string): string {
  return `${prefix}:${crypto.randomUUID()}`
}

export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'Неизвестная ошибка.'
}
