export type MealType = 'breakfast' | 'lunch' | 'dinner' | 'snack'

export interface NutritionTotal {
  calories: number
  proteinGrams: number
  fatGrams: number
  carbohydrateGrams: number
}

export interface MealItem {
  id: string
  mealId: string
  foodProductId: string | null
  recipeId: string | null
  recipeVersion: number | null
  weightGrams: number
  nutritionSnapshot: NutritionTotal
  sourceMessageId: string | null
}

export interface Meal {
  id: string
  occurredAt: string
  mealType: MealType
  notes: string | null
  items: MealItem[]
}

export interface DailySummary {
  date: string
  timeZone: string
  consumed: NutritionTotal
  target: NutritionTotal | null
  remaining: NutritionTotal | null
  meals: Meal[]
}

export interface MealOperationResult {
  operation: string
  isReplay: boolean
  mealItemId: string
  mealItem: MealItem | null
  dailySummary: DailySummary
}

export interface ExecutedAction {
  toolName: string
  isSuccess: boolean
  errorCode: string | null
}

export interface ChatResult {
  messageId: string
  assistantMessage: string
  executedActions: ExecutedAction[]
  pendingClarification: string | null
  pendingConfirmation: string | null
  dailySummary: DailySummary | null
}

export interface FoodProduct {
  id: string
  userId: string | null
  name: string
  normalizedName: string
  brand: string | null
  caloriesPer100g: number
  proteinPer100g: number
  fatPer100g: number
  carbohydratesPer100g: number
  fiberPer100g: number | null
  source: string
  isVerified: boolean
  createdAtUtc: string
  updatedAtUtc: string
}

export interface FoodInput {
  name: string
  brand: string | null
  caloriesPer100g: number
  proteinPer100g: number
  fatPer100g: number
  carbohydratesPer100g: number
  fiberPer100g: number | null
  source: string
  isVerified: boolean
}

export interface RecipeSummary {
  id: string
  userId: string
  name: string
  description: string | null
  totalPreparedWeightGrams: number | null
  version: number
  isArchived: boolean
  updatedAtUtc: string
}

export interface RecipeIngredient {
  foodProductId: string
  weightGrams: number
  caloriesPer100gSnapshot: number
  proteinPer100gSnapshot: number
  fatPer100gSnapshot: number
  carbohydratesPer100gSnapshot: number
}

export interface RecipeVersion {
  version: number
  name: string
  description: string | null
  totalPreparedWeightGrams: number | null
  changeReason: string | null
  changeSource: string
  changedAtUtc: string
  ingredients: RecipeIngredient[]
}

export interface Recipe {
  id: string
  userId: string
  currentVersion: number
  isArchived: boolean
  archivedAtUtc: string | null
  archiveReason: string | null
  archiveSource: string | null
  createdAtUtc: string
  updatedAtUtc: string
  selectedVersion: RecipeVersion
  availableVersions: number[]
}

export interface RecipeNutrition {
  recipeId: string
  version: number
  totalPreparedWeightGrams: number | null
  calories: number
  proteinGrams: number
  fatGrams: number
  carbohydrateGrams: number
  portionWeightGrams: number | null
}
