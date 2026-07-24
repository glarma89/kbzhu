import { useCallback, useEffect, useState } from 'react'
import { useApi } from '../api/ApiContext'
import type { DailySummary } from '../api/types'
import { EmptyState, ErrorState, LoadingState } from '../components/AsyncState'
import { NutritionGrid } from '../components/NutritionGrid'
import { errorMessage, todayIso } from '../utils'

export function GoalSettingsScreen() {
  const api = useApi()
  const [date, setDate] = useState(todayIso())
  const [summary, setSummary] = useState<DailySummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async (value: string) => {
    setLoading(true); setError(null)
    try { setSummary(await api.getDailySummary(value)) }
    catch (caught) { setError(errorMessage(caught)) }
    finally { setLoading(false) }
  }, [api])
  useEffect(() => { void load(date) }, [date, load])

  return (
    <section className="screen narrow-screen">
      <header className="screen-header"><div><span className="eyebrow">Профиль</span><h1>Настройки целей</h1></div></header>
      <div className="settings-intro"><span className="settings-icon">◎</span><div><strong>Актуальная дневная цель</strong><p>Показываем значения, действующие на выбранную дату. Frontend не рассчитывает и не сохраняет КБЖУ самостоятельно.</p></div></div>
      <label className="date-field">Дата действия<input onChange={(event) => setDate(event.target.value)} type="date" value={date} /></label>
      {loading && <LoadingState />}
      {error && <ErrorState message={error} onRetry={() => void load(date)} />}
      {!loading && summary?.target && <div className="panel goal-panel"><h2>Цель на {summary.date}</h2><NutritionGrid values={summary.target} /><div className="read-only-note"><strong>Только просмотр</strong><span>Backend ещё не предоставляет endpoint для изменения целей.</span></div></div>}
      {!loading && summary && !summary.target && <EmptyState title="Цель не задана" detail="Для этой даты backend не вернул действующую дневную цель." />}
    </section>
  )
}
