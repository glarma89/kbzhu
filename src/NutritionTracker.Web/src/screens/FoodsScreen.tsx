import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/ApiContext'
import type { FoodInput, FoodProduct } from '../api/types'
import { EmptyState, ErrorState, LoadingState } from '../components/AsyncState'
import { formatNumber } from '../components/NutritionGrid'
import { errorMessage } from '../utils'

const emptyForm: FoodInput = {
  name: '', brand: null, caloriesPer100g: 0, proteinPer100g: 0,
  fatPer100g: 0, carbohydratesPer100g: 0, fiberPer100g: null,
  source: 'frontend', isVerified: false,
}

export function FoodsScreen() {
  const api = useApi()
  const [query, setQuery] = useState('')
  const [foods, setFoods] = useState<FoodProduct[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [form, setForm] = useState<FoodInput | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)

  const search = useCallback(async (value: string) => {
    setLoading(true); setError(null)
    try { setFoods(await api.searchFoods(value)) }
    catch (caught) { setError(errorMessage(caught)) }
    finally { setLoading(false) }
  }, [api])

  useEffect(() => { void search('') }, [search])

  const updateField = <K extends keyof FoodInput>(key: K, value: FoodInput[K]) => {
    setForm((current) => current ? { ...current, [key]: value } : current)
  }

  const edit = (food: FoodProduct) => {
    setEditingId(food.id)
    setForm({
      name: food.name, brand: food.brand, caloriesPer100g: food.caloriesPer100g,
      proteinPer100g: food.proteinPer100g, fatPer100g: food.fatPer100g,
      carbohydratesPer100g: food.carbohydratesPer100g, fiberPer100g: food.fiberPer100g,
      source: food.source, isVerified: food.isVerified,
    })
  }

  const save = async (event: FormEvent) => {
    event.preventDefault()
    if (!form || !form.name.trim()) return
    setLoading(true); setError(null)
    try {
      if (editingId) await api.updateFood(editingId, form)
      else await api.createFood(form)
      setForm(null); setEditingId(null)
      await search(query)
    } catch (caught) { setError(errorMessage(caught)); setLoading(false) }
  }

  return (
    <section className="screen">
      <header className="screen-header"><div><span className="eyebrow">Каталог</span><h1>Продукты</h1></div><button className="primary-button" onClick={() => { setEditingId(null); setForm({ ...emptyForm }) }} type="button">+ Новый продукт</button></header>
      <form className="search-bar" onSubmit={(event) => { event.preventDefault(); void search(query) }}>
        <label className="sr-only" htmlFor="food-search">Поиск продуктов</label><input id="food-search" onChange={(event) => setQuery(event.target.value)} placeholder="Название или бренд" value={query} /><button type="submit">Найти</button>
      </form>
      {form && (
        <form className="edit-panel" onSubmit={(event) => void save(event)}>
          <div className="section-title"><div><span className="eyebrow">{editingId ? 'Редактирование' : 'Создание'}</span><h2>{editingId ? 'Изменить продукт' : 'Новый продукт'}</h2></div><button onClick={() => setForm(null)} type="button">Закрыть</button></div>
          <div className="form-grid">
            <label>Название<input required onChange={(event) => updateField('name', event.target.value)} value={form.name} /></label>
            <label>Бренд<input onChange={(event) => updateField('brand', event.target.value || null)} value={form.brand ?? ''} /></label>
            <NumberField label="Калории / 100 г" onChange={(value) => updateField('caloriesPer100g', value ?? 0)} value={form.caloriesPer100g} />
            <NumberField label="Белки / 100 г" onChange={(value) => updateField('proteinPer100g', value ?? 0)} value={form.proteinPer100g} />
            <NumberField label="Жиры / 100 г" onChange={(value) => updateField('fatPer100g', value ?? 0)} value={form.fatPer100g} />
            <NumberField label="Углеводы / 100 г" onChange={(value) => updateField('carbohydratesPer100g', value ?? 0)} value={form.carbohydratesPer100g} />
            <NumberField label="Клетчатка / 100 г" nullable onChange={(value) => updateField('fiberPer100g', value)} value={form.fiberPer100g} />
          </div>
          <button className="primary-button" disabled={loading} type="submit">{loading ? 'Сохраняем…' : 'Сохранить'}</button>
        </form>
      )}
      {loading && foods.length === 0 && <LoadingState />}
      {error && foods.length === 0 && <ErrorState message={error} onRetry={() => void search(query)} />}
      {error && foods.length > 0 && <div className="inline-error" role="alert">{error}</div>}
      {!loading && !error && foods.length === 0 && <EmptyState title="Ничего не найдено" detail="Измените запрос или создайте новый продукт." />}
      {foods.length > 0 && <div className="card-list">{foods.map((food) => (
        <article className="food-card" key={food.id}>
          <div className="food-main"><span className="food-avatar">{food.name.slice(0, 1).toUpperCase()}</span><div><h2>{food.name}</h2><p>{food.brand ?? 'Без бренда'} · на 100 г</p></div></div>
          <div className="macro-line"><strong>{formatNumber(food.caloriesPer100g)} ккал</strong><span>Б {formatNumber(food.proteinPer100g)}</span><span>Ж {formatNumber(food.fatPer100g)}</span><span>У {formatNumber(food.carbohydratesPer100g)}</span></div>
          <button className="secondary-button" onClick={() => edit(food)} type="button">Редактировать</button>
        </article>
      ))}</div>}
    </section>
  )
}

function NumberField({ label, value, nullable = false, onChange }: { label: string; value: number | null; nullable?: boolean; onChange: (value: number | null) => void }) {
  return <label>{label}<input min="0" onChange={(event) => onChange(event.target.value === '' && nullable ? null : Number(event.target.value))} step="0.1" type="number" value={value ?? ''} /></label>
}
