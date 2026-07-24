import type { NutritionTotal } from '../api/types'

const numberFormatter = new Intl.NumberFormat('ru-RU', { maximumFractionDigits: 1 })

export function formatNumber(value: number): string {
  return numberFormatter.format(value)
}

export function NutritionGrid({ values, compact = false }: { values: NutritionTotal; compact?: boolean }) {
  const items = [
    { key: 'calories', label: 'Калории', value: values.calories, unit: 'ккал' },
    { key: 'protein', label: 'Белки', value: values.proteinGrams, unit: 'г' },
    { key: 'fat', label: 'Жиры', value: values.fatGrams, unit: 'г' },
    { key: 'carbs', label: 'Углеводы', value: values.carbohydrateGrams, unit: 'г' },
  ]
  return (
    <div className={compact ? 'nutrition-grid compact' : 'nutrition-grid'}>
      {items.map((item) => (
        <div className="nutrition-stat" key={item.key}>
          <span>{item.label}</span><strong>{formatNumber(item.value)}</strong><small>{item.unit}</small>
        </div>
      ))}
    </div>
  )
}
