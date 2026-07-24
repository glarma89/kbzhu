import type {
  ChatResult,
  DailySummary,
  FoodInput,
  FoodProduct,
  MealOperationResult,
  Recipe,
  RecipeNutrition,
  RecipeSummary,
} from './types'

export interface NutritionApi {
  sendChatMessage(message: string, clientMessageId: string): Promise<ChatResult>
  confirmChatMessage(messageId: string, confirm: boolean): Promise<ChatResult>
  getDailySummary(date: string): Promise<DailySummary>
  updateMealItemWeight(id: string, weightGrams: number, idempotencyKey: string): Promise<MealOperationResult>
  deleteMealItem(id: string, idempotencyKey: string): Promise<MealOperationResult>
  searchFoods(query: string): Promise<FoodProduct[]>
  createFood(input: FoodInput): Promise<FoodProduct>
  updateFood(id: string, input: FoodInput): Promise<FoodProduct>
  searchRecipes(query: string): Promise<RecipeSummary[]>
  getRecipe(id: string, version?: number): Promise<Recipe>
  getRecipeNutrition(id: string, version?: number): Promise<RecipeNutrition>
  getRecipeNutritionPer100g(id: string, version?: number): Promise<RecipeNutrition>
}

interface ProblemDetails {
  title?: string
  detail?: string
  status?: number
}

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    credentials: 'same-origin',
    headers: {
      'Content-Type': 'application/json',
      ...init?.headers,
    },
  })

  if (!response.ok) {
    let problem: ProblemDetails | null = null
    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      // The status text remains the safe fallback for a non-JSON response.
    }
    throw new Error(problem?.detail ?? problem?.title ?? `Ошибка API: ${response.status}`)
  }

  return (await response.json()) as T
}

function query(path: string, values: Record<string, string | number | undefined>): string {
  const parameters = new URLSearchParams()
  Object.entries(values).forEach(([key, value]) => {
    if (value !== undefined) parameters.set(key, String(value))
  })
  return `${path}?${parameters.toString()}`
}

export const apiClient: NutritionApi = {
  sendChatMessage: (message, clientMessageId) => request<ChatResult>('/api/chat/messages', {
    method: 'POST',
    body: JSON.stringify({ message, clientMessageId, occurredAt: null }),
  }),
  confirmChatMessage: (messageId, confirm) => request<ChatResult>(
    `/api/chat/messages/${messageId}/confirmation`,
    { method: 'POST', body: JSON.stringify({ confirm }) },
  ),
  getDailySummary: (date) => request<DailySummary>(query('/api/daily-summary', { date })),
  updateMealItemWeight: (id, weightGrams, idempotencyKey) => request<MealOperationResult>(
    `/api/meals/items/${id}`,
    {
      method: 'PUT',
      body: JSON.stringify({ idempotencyKey, weightGrams, occurredAt: null, mealType: null }),
    },
  ),
  deleteMealItem: (id, idempotencyKey) => request<MealOperationResult>(
    query(`/api/meals/items/${id}`, { idempotencyKey }),
    { method: 'DELETE' },
  ),
  searchFoods: (search) => request<FoodProduct[]>(query('/api/foods', { query: search, limit: 25 })),
  createFood: (input) => request<FoodProduct>('/api/foods', {
    method: 'POST',
    body: JSON.stringify(input),
  }),
  updateFood: (id, input) => request<FoodProduct>(`/api/foods/${id}`, {
    method: 'PUT',
    body: JSON.stringify(input),
  }),
  searchRecipes: (search) => request<RecipeSummary[]>(
    query('/api/recipes', { query: search, includeArchived: 'false', limit: 25 }),
  ),
  getRecipe: (id, version) => request<Recipe>(query(`/api/recipes/${id}`, { version })),
  getRecipeNutrition: (id, version) => request<RecipeNutrition>(
    query(`/api/recipes/${id}/nutrition`, { version }),
  ),
  getRecipeNutritionPer100g: (id, version) => request<RecipeNutrition>(
    query(`/api/recipes/${id}/nutrition/portion`, { weightGrams: 100, version }),
  ),
}
