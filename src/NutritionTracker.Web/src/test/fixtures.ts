import { vi } from 'vitest'
import type { NutritionApi } from '../api/client'
import type { DailySummary, FoodProduct, Recipe, RecipeNutrition, RecipeSummary } from '../api/types'

export const nutrition = { calories: 420, proteinGrams: 31, fatGrams: 14, carbohydrateGrams: 44 }
export const target = { calories: 2100, proteinGrams: 140, fatGrams: 70, carbohydrateGrams: 220 }

export const summary: DailySummary = {
  date: '2026-07-23', timeZone: 'Asia/Jerusalem', consumed: nutrition, target,
  remaining: { calories: 1680, proteinGrams: 109, fatGrams: 56, carbohydrateGrams: 176 },
  meals: [{
    id: '10000000-0000-0000-0000-000000000001', occurredAt: '2026-07-23T08:00:00+03:00',
    mealType: 'breakfast', notes: null, items: [{
      id: '20000000-0000-0000-0000-000000000002', mealId: '10000000-0000-0000-0000-000000000001',
      foodProductId: '30000000-0000-0000-0000-000000000003', recipeId: null,
      recipeVersion: null, weightGrams: 180, nutritionSnapshot: nutrition, sourceMessageId: null,
    }],
  }],
}

export const food: FoodProduct = {
  id: '30000000-0000-0000-0000-000000000003', userId: '40000000-0000-0000-0000-000000000004',
  name: 'Творог', normalizedName: 'ТВОРОГ', brand: 'Ферма', caloriesPer100g: 121,
  proteinPer100g: 18, fatPer100g: 5, carbohydratesPer100g: 3, fiberPer100g: null,
  source: 'label', isVerified: false, createdAtUtc: '2026-07-23T08:00:00Z', updatedAtUtc: '2026-07-23T08:00:00Z',
}

export const recipeSummary: RecipeSummary = {
  id: '50000000-0000-0000-0000-000000000005', userId: '40000000-0000-0000-0000-000000000004',
  name: 'Сырники', description: 'Домашние', totalPreparedWeightGrams: 500, version: 2,
  isArchived: false, updatedAtUtc: '2026-07-23T08:00:00Z',
}

export const recipe: Recipe = {
  id: recipeSummary.id, userId: recipeSummary.userId, currentVersion: 2, isArchived: false,
  archivedAtUtc: null, archiveReason: null, archiveSource: null,
  createdAtUtc: '2026-07-20T08:00:00Z', updatedAtUtc: '2026-07-23T08:00:00Z',
  selectedVersion: {
    version: 2, name: 'Сырники', description: 'Домашние', totalPreparedWeightGrams: 500,
    changeReason: 'Меньше муки', changeSource: 'frontend', changedAtUtc: '2026-07-23T08:00:00Z',
    ingredients: [{ foodProductId: food.id, weightGrams: 400, caloriesPer100gSnapshot: 121, proteinPer100gSnapshot: 18, fatPer100gSnapshot: 5, carbohydratesPer100gSnapshot: 3 }],
  },
  availableVersions: [1, 2],
}

export const recipeNutrition: RecipeNutrition = {
  recipeId: recipe.id, version: 2, totalPreparedWeightGrams: 500,
  calories: 720, proteinGrams: 52, fatGrams: 24, carbohydrateGrams: 73, portionWeightGrams: null,
}

export function createMockApi(overrides: Partial<NutritionApi> = {}): NutritionApi {
  return {
    sendChatMessage: vi.fn(), confirmChatMessage: vi.fn(),
    getDailySummary: vi.fn().mockResolvedValue(summary),
    updateMealItemWeight: vi.fn().mockResolvedValue({ operation: 'update', isReplay: false, mealItemId: summary.meals[0]!.items[0]!.id, mealItem: summary.meals[0]!.items[0], dailySummary: summary }),
    deleteMealItem: vi.fn().mockResolvedValue({ operation: 'delete', isReplay: false, mealItemId: summary.meals[0]!.items[0]!.id, mealItem: null, dailySummary: { ...summary, meals: [] } }),
    searchFoods: vi.fn().mockResolvedValue([food]), createFood: vi.fn().mockResolvedValue(food), updateFood: vi.fn().mockResolvedValue(food),
    searchRecipes: vi.fn().mockResolvedValue([recipeSummary]), getRecipe: vi.fn().mockResolvedValue(recipe),
    getRecipeNutrition: vi.fn().mockResolvedValue(recipeNutrition),
    getRecipeNutritionPer100g: vi.fn().mockResolvedValue({ ...recipeNutrition, calories: 144, proteinGrams: 10.4, fatGrams: 4.8, carbohydrateGrams: 14.6, portionWeightGrams: 100 }),
    ...overrides,
  }
}
