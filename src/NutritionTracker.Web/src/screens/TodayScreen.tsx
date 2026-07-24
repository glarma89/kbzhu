import { useCallback, useEffect, useState } from 'react'
import { useApi } from '../api/ApiContext'
import type { DailySummary, MealItem, MealType } from '../api/types'
import { EmptyState, ErrorState, LoadingState } from '../components/AsyncState'
import { formatNumber, NutritionGrid } from '../components/NutritionGrid'
import { createClientId, errorMessage, todayIso } from '../utils'

const mealNames: Record<MealType, string> = {
  breakfast: 'Завтрак', lunch: 'Обед', dinner: 'Ужин', snack: 'Перекус',
}

export function TodayScreen() {
  const api = useApi()
  const [summary, setSummary] = useState<DailySummary | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [editing, setEditing] = useState<MealItem | null>(null)
  const [weight, setWeight] = useState('')
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const date = todayIso()

  const load = useCallback(async () => {
    setLoading(true); setError(null)
    try { setSummary(await api.getDailySummary(date)) }
    catch (caught) { setError(errorMessage(caught)) }
    finally { setLoading(false) }
  }, [api, date])

  useEffect(() => { void load() }, [load])

  const saveWeight = async () => {
    const value = Number(weight)
    if (!editing || !Number.isFinite(value) || value <= 0) return
    setLoading(true); setError(null)
    try {
      const result = await api.updateMealItemWeight(editing.id, value, createClientId('weight'))
      setSummary(result.dailySummary); setEditing(null)
    } catch (caught) { setError(errorMessage(caught)) }
    finally { setLoading(false) }
  }

  const remove = async (id: string) => {
    setLoading(true); setError(null)
    try {
      const result = await api.deleteMealItem(id, createClientId('delete'))
      setSummary(result.dailySummary); setDeletingId(null)
    } catch (caught) { setError(errorMessage(caught)) }
    finally { setLoading(false) }
  }

  return (
    <section className="screen">
      <header className="screen-header">
        <div><span className="eyebrow">{date}</span><h1>Сегодня</h1></div>
        <button className="secondary-button" onClick={() => void load()} type="button">Обновить</button>
      </header>
      {loading && !summary && <LoadingState />}
      {error && !summary && <ErrorState message={error} onRetry={() => void load()} />}
      {summary && (
        <>
          {error && <div className="inline-error" role="alert">{error}</div>}
          <div className="summary-hero">
            <div><span>Потреблено</span><strong>{formatNumber(summary.consumed.calories)}</strong><small>ккал</small></div>
            <div className="summary-meta"><span>Часовой пояс</span><strong>{summary.timeZone}</strong></div>
          </div>
          <NutritionGrid values={summary.consumed} />
          <div className="two-column">
            <div className="panel"><h2>Дневная цель</h2>{summary.target ? <NutritionGrid compact values={summary.target} /> : <EmptyState title="Цель не задана" detail="Добавьте цель в backend, чтобы видеть ориентир." />}</div>
            <div className="panel"><h2>Осталось</h2>{summary.remaining ? <NutritionGrid compact values={summary.remaining} /> : <EmptyState title="Нет расчёта" detail="Остаток появится вместе с дневной целью." />}</div>
          </div>
          <div className="section-title"><div><span className="eyebrow">Дневник</span><h2>Приёмы пищи</h2></div><span>{summary.meals.length}</span></div>
          {summary.meals.length === 0 ? <EmptyState title="Дневник пока пуст" detail="Добавьте еду через чат — она появится здесь." /> : (
            <div className="meal-list">
              {summary.meals.map((meal) => (
                <article className="meal-card" key={meal.id}>
                  <header><div><strong>{mealNames[meal.mealType]}</strong><span>{new Date(meal.occurredAt).toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}</span></div><b>{meal.items.length} поз.</b></header>
                  {meal.items.map((item) => (
                    <div className="meal-item" key={item.id}>
                      <div><strong>{item.foodProductId ? 'Продукт' : 'Рецепт'}</strong><span>{formatNumber(item.weightGrams)} г · {formatNumber(item.nutritionSnapshot.calories)} ккал</span></div>
                      <div className="item-actions">
                        <button onClick={() => { setEditing(item); setWeight(String(item.weightGrams)) }} type="button">Изменить</button>
                        <button className="danger-link" onClick={() => setDeletingId(item.id)} type="button">Удалить</button>
                      </div>
                      {editing?.id === item.id && <div className="inline-editor"><input aria-label="Новый вес" min="0.1" onChange={(event) => setWeight(event.target.value)} step="0.1" type="number" value={weight} /><span>г</span><button className="primary-button" onClick={() => void saveWeight()} type="button">Сохранить</button><button onClick={() => setEditing(null)} type="button">Отмена</button></div>}
                      {deletingId === item.id && <div className="confirm-row" role="alert"><span>Удалить запись без возможности отмены?</span><button className="danger-button" onClick={() => void remove(item.id)} type="button">Да, удалить</button><button onClick={() => setDeletingId(null)} type="button">Нет</button></div>}
                    </div>
                  ))}
                </article>
              ))}
            </div>
          )}
        </>
      )}
    </section>
  )
}
