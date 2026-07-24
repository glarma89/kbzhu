import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { useApi } from '../api/ApiContext'
import type { Recipe, RecipeNutrition, RecipeSummary } from '../api/types'
import { EmptyState, ErrorState, LoadingState } from '../components/AsyncState'
import { formatNumber, NutritionGrid } from '../components/NutritionGrid'
import { errorMessage } from '../utils'

function nutritionValues(value: RecipeNutrition) {
  return { calories: value.calories, proteinGrams: value.proteinGrams, fatGrams: value.fatGrams, carbohydrateGrams: value.carbohydrateGrams }
}

export function RecipesScreen() {
  const api = useApi()
  const [query, setQuery] = useState('')
  const [recipes, setRecipes] = useState<RecipeSummary[]>([])
  const [selected, setSelected] = useState<Recipe | null>(null)
  const [total, setTotal] = useState<RecipeNutrition | null>(null)
  const [per100, setPer100] = useState<RecipeNutrition | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const search = useCallback(async (value: string) => {
    setLoading(true); setError(null)
    try { setRecipes(await api.searchRecipes(value)) }
    catch (caught) { setError(errorMessage(caught)) }
    finally { setLoading(false) }
  }, [api])
  useEffect(() => { void search('') }, [search])

  const open = async (id: string, version?: number) => {
    setLoading(true); setError(null)
    try {
      const [recipe, recipeTotal, recipePer100] = await Promise.all([
        api.getRecipe(id, version), api.getRecipeNutrition(id, version), api.getRecipeNutritionPer100g(id, version),
      ])
      setSelected(recipe); setTotal(recipeTotal); setPer100(recipePer100)
    } catch (caught) { setError(errorMessage(caught)) }
    finally { setLoading(false) }
  }

  const submit = (event: FormEvent) => { event.preventDefault(); void search(query) }

  return (
    <section className="screen">
      <header className="screen-header"><div><span className="eyebrow">Кулинарная книга</span><h1>Рецепты</h1></div></header>
      <form className="search-bar" onSubmit={submit}><label className="sr-only" htmlFor="recipe-search">Поиск рецептов</label><input id="recipe-search" onChange={(event) => setQuery(event.target.value)} placeholder="Найти рецепт" value={query} /><button type="submit">Найти</button></form>
      {error && <ErrorState message={error} onRetry={() => void search(query)} />}
      {loading && recipes.length === 0 && <LoadingState />}
      {!loading && !error && recipes.length === 0 && <EmptyState title="Рецептов пока нет" detail="Создайте рецепт через чат или API — он появится здесь." />}
      <div className="recipe-layout">
        {recipes.length > 0 && <div className="recipe-index">{recipes.map((recipe) => (
          <button className={selected?.id === recipe.id ? 'recipe-row selected' : 'recipe-row'} key={recipe.id} onClick={() => void open(recipe.id)} type="button">
            <span><strong>{recipe.name}</strong><small>{recipe.description ?? 'Без описания'}</small></span><span>v{recipe.version}</span>
          </button>
        ))}</div>}
        <div className="recipe-detail">
          {!selected && recipes.length > 0 && <EmptyState title="Выберите рецепт" detail="Покажем состав, вес, КБЖУ и версии." />}
          {selected && total && per100 && (
            <article>
              <div className="section-title"><div><span className="eyebrow">Версия {selected.selectedVersion.version}</span><h2>{selected.selectedVersion.name}</h2><p>{selected.selectedVersion.description}</p></div><strong>{selected.selectedVersion.totalPreparedWeightGrams ? `${formatNumber(selected.selectedVersion.totalPreparedWeightGrams)} г` : 'Вес не задан'}</strong></div>
              <div className="version-strip" aria-label="История версий">{selected.availableVersions.map((version) => <button className={version === selected.selectedVersion.version ? 'active' : ''} key={version} onClick={() => void open(selected.id, version)} type="button">v{version}</button>)}</div>
              <div className="two-column"><div className="panel"><h3>КБЖУ всего рецепта</h3><NutritionGrid compact values={nutritionValues(total)} /></div><div className="panel"><h3>КБЖУ на 100 граммов</h3><NutritionGrid compact values={nutritionValues(per100)} /></div></div>
              <div className="ingredient-list"><h3>Состав</h3>{selected.selectedVersion.ingredients.map((ingredient) => <div key={ingredient.foodProductId}><span>Продукт · {ingredient.foodProductId.slice(0, 8)}</span><strong>{formatNumber(ingredient.weightGrams)} г</strong></div>)}</div>
              <div className="version-note"><span>Источник изменения: {selected.selectedVersion.changeSource}</span><span>{new Date(selected.selectedVersion.changedAtUtc).toLocaleDateString('ru-RU')}</span>{selected.selectedVersion.changeReason && <p>{selected.selectedVersion.changeReason}</p>}</div>
            </article>
          )}
        </div>
      </div>
    </section>
  )
}
