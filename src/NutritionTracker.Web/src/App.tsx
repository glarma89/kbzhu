import { useState } from 'react'
import { ApiProvider } from './api/ApiContext'
import type { NutritionApi } from './api/client'
import { ChatScreen } from './screens/ChatScreen'
import { FoodsScreen } from './screens/FoodsScreen'
import { GoalSettingsScreen } from './screens/GoalSettingsScreen'
import { RecipesScreen } from './screens/RecipesScreen'
import { TodayScreen } from './screens/TodayScreen'

type Screen = 'chat' | 'today' | 'foods' | 'recipes' | 'goals'

const navigation: Array<{ id: Screen; label: string; short: string }> = [
  { id: 'chat', label: 'Чат', short: 'Чат' },
  { id: 'today', label: 'Сегодня', short: 'День' },
  { id: 'foods', label: 'Продукты', short: 'Еда' },
  { id: 'recipes', label: 'Рецепты', short: 'Рецепты' },
  { id: 'goals', label: 'Настройки целей', short: 'Цели' },
]

export function App({ apiClient }: { apiClient?: NutritionApi }) {
  const [screen, setScreen] = useState<Screen>('chat')

  return (
    <ApiProvider client={apiClient}>
      <div className="app-shell">
        <aside className="sidebar">
          <div className="brand">
            <span className="brand-mark">N</span>
            <div><strong>Nutrition</strong><span>daily tracker</span></div>
          </div>
          <nav aria-label="Основная навигация">
            {navigation.map((item) => (
              <button
                className={screen === item.id ? 'nav-item active' : 'nav-item'}
                key={item.id}
                onClick={() => setScreen(item.id)}
                type="button"
              >
                <span className="nav-label">{item.label}</span>
                <span className="nav-short">{item.short}</span>
              </button>
            ))}
          </nav>
          <p className="sidebar-note">Данные и расчёты приходят только из backend.</p>
        </aside>
        <main className="main-content">
          {screen === 'chat' && <ChatScreen />}
          {screen === 'today' && <TodayScreen />}
          {screen === 'foods' && <FoodsScreen />}
          {screen === 'recipes' && <RecipesScreen />}
          {screen === 'goals' && <GoalSettingsScreen />}
        </main>
      </div>
    </ApiProvider>
  )
}
