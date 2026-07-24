export function LoadingState({ label = 'Загружаем данные…' }: { label?: string }) {
  return <div className="state-card loading-state" role="status"><span className="spinner" />{label}</div>
}

export function EmptyState({ title, detail }: { title: string; detail: string }) {
  return <div className="state-card"><strong>{title}</strong><p>{detail}</p></div>
}

export function ErrorState({ message, onRetry }: { message: string; onRetry?: () => void }) {
  return (
    <div className="state-card error-state" role="alert">
      <strong>Не удалось загрузить</strong><p>{message}</p>
      {onRetry && <button className="secondary-button" onClick={onRetry} type="button">Повторить</button>}
    </div>
  )
}
