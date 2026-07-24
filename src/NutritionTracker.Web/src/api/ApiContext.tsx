import { createContext, useContext, type ReactNode } from 'react'
import { apiClient, type NutritionApi } from './client'

const ApiContext = createContext<NutritionApi>(apiClient)

export function ApiProvider({ client = apiClient, children }: { client?: NutritionApi; children: ReactNode }) {
  return <ApiContext.Provider value={client}>{children}</ApiContext.Provider>
}

export function useApi(): NutritionApi {
  return useContext(ApiContext)
}
