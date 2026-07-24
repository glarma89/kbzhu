import { useRef, useState, type FormEvent } from 'react'
import { useApi } from '../api/ApiContext'
import type { ChatResult, ExecutedAction } from '../api/types'
import { createClientId, errorMessage } from '../utils'

interface UiMessage {
  id: string
  role: 'user' | 'assistant'
  content: string
  actions?: ExecutedAction[]
}

interface PendingSubmission {
  message: string
  clientMessageId: string
}

export function ChatScreen() {
  const api = useApi()
  const [messages, setMessages] = useState<UiMessage[]>([])
  const [draft, setDraft] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState<ChatResult | null>(null)
  const sendingRef = useRef(false)
  const pendingSubmissionRef = useRef<PendingSubmission | null>(null)

  const applyResult = (result: ChatResult) => {
    setMessages((current) => [...current, {
      id: `assistant:${result.messageId}:${current.length}`,
      role: 'assistant',
      content: result.assistantMessage,
      actions: result.executedActions,
    }])
    setPending(result.pendingClarification || result.pendingConfirmation ? result : null)
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    const message = draft.trim()
    if (!message || sendingRef.current) return

    sendingRef.current = true
    setLoading(true)
    setError(null)
    const previous = pendingSubmissionRef.current
    const submission = previous?.message === message
      ? previous
      : { message, clientMessageId: createClientId('chat') }
    pendingSubmissionRef.current = submission
    if (!previous || previous.message !== message) {
      setMessages((current) => [...current, {
        id: submission.clientMessageId,
        role: 'user',
        content: message,
      }])
    }

    try {
      const result = await api.sendChatMessage(submission.message, submission.clientMessageId)
      applyResult(result)
      pendingSubmissionRef.current = null
      setDraft('')
    } catch (caught) {
      setError(errorMessage(caught))
    } finally {
      sendingRef.current = false
      setLoading(false)
    }
  }

  const confirm = async (accepted: boolean) => {
    if (!pending || loading) return
    setLoading(true)
    setError(null)
    try {
      const result = await api.confirmChatMessage(pending.messageId, accepted)
      applyResult(result)
    } catch (caught) {
      setError(errorMessage(caught))
    } finally {
      setLoading(false)
    }
  }

  return (
    <section className="screen chat-screen">
      <header className="screen-header">
        <div><span className="eyebrow">AI assistant</span><h1>Чат о питании</h1></div>
        <span className={loading ? 'status-pill busy' : 'status-pill'}>{loading ? 'Обрабатываем' : 'Готов'}</span>
      </header>

      <div className="chat-thread" aria-live="polite">
        {messages.length === 0 && (
          <div className="chat-welcome">
            <span className="welcome-orb">✦</span>
            <h2>Расскажите, что вы съели</h2>
            <p>Например: «Добавь 180 г творога на завтрак». Все значения подтвердит backend.</p>
          </div>
        )}
        {messages.map((message) => (
          <article className={`message ${message.role}`} key={message.id}>
            <span className="message-role">{message.role === 'user' ? 'Вы' : 'Ассистент'}</span>
            <p>{message.content}</p>
            {message.actions && message.actions.length > 0 && (
              <div className="action-list" aria-label="Выполненные действия">
                {message.actions.map((action, index) => (
                  <span className={action.isSuccess ? 'action success' : 'action failed'} key={`${action.toolName}-${index}`}>
                    {action.isSuccess ? '✓' : '!'} {action.toolName}
                  </span>
                ))}
              </div>
            )}
          </article>
        ))}
        {loading && <div className="typing" role="status"><span /><span /><span /> Ассистент думает</div>}
      </div>

      {pending?.pendingClarification && (
        <div className="decision-card clarification" role="status">
          <strong>Нужно уточнение</strong><p>{pending.pendingClarification}</p>
          <small>Ответьте в поле ниже — предыдущий контекст сохранён.</small>
        </div>
      )}
      {pending?.pendingConfirmation && (
        <div className="decision-card confirmation" role="alert">
          <strong>Подтвердите изменение</strong><p>{pending.pendingConfirmation}</p>
          <div className="button-row">
            <button className="primary-button" disabled={loading} onClick={() => void confirm(true)} type="button">Подтвердить</button>
            <button className="secondary-button" disabled={loading} onClick={() => void confirm(false)} type="button">Отменить</button>
          </div>
        </div>
      )}
      {error && <div className="inline-error" role="alert">{error} <span>Повторная отправка сохранит тот же clientMessageId.</span></div>}

      <form className="chat-composer" onSubmit={(event) => void submit(event)}>
        <label className="sr-only" htmlFor="chat-message">Сообщение</label>
        <textarea
          id="chat-message"
          onChange={(event) => setDraft(event.target.value)}
          placeholder={pending?.pendingClarification ? 'Введите уточнение…' : 'Опишите еду или задайте вопрос…'}
          rows={2}
          value={draft}
        />
        <button className="send-button" disabled={loading || !draft.trim()} type="submit" aria-label="Отправить сообщение">↑</button>
      </form>
    </section>
  )
}
