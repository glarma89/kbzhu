import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi } from 'vitest'
import { App } from '../App'
import type { ChatResult } from '../api/types'
import { createMockApi, food, recipe, recipeNutrition, summary } from './fixtures'

describe('основные пользовательские сценарии', () => {
  it('защищает чат от повторной отправки, показывает actions и подтверждает изменение', async () => {
    const user = userEvent.setup()
    let resolveSend: ((value: ChatResult) => void) | undefined
    const sendPromise = new Promise<ChatResult>((resolve) => { resolveSend = resolve })
    const pending: ChatResult = {
      messageId: '60000000-0000-0000-0000-000000000006', assistantMessage: 'Подготовлено изменение.',
      executedActions: [{ toolName: 'search_foods', isSuccess: true, errorCode: null }],
      pendingClarification: null, pendingConfirmation: 'Изменить продукт «Творог»?', dailySummary: null,
    }
    const api = createMockApi({
      sendChatMessage: vi.fn().mockReturnValue(sendPromise),
      confirmChatMessage: vi.fn().mockResolvedValue({ ...pending, assistantMessage: 'Продукт изменён.', pendingConfirmation: null }),
    })
    render(<App apiClient={api} />)

    await user.type(screen.getByLabelText('Сообщение'), 'Измени творог')
    const send = screen.getByRole('button', { name: 'Отправить сообщение' })
    await user.click(send)
    await user.click(send)

    expect(api.sendChatMessage).toHaveBeenCalledTimes(1)
    expect(api.sendChatMessage).toHaveBeenCalledWith('Измени творог', expect.stringMatching(/^chat:/))
    resolveSend?.(pending)
    expect(await screen.findByText(/search_foods/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Подтвердить' }))
    expect(api.confirmChatMessage).toHaveBeenCalledWith(pending.messageId, true)
    expect(await screen.findByText('Продукт изменён.')).toBeInTheDocument()
  })

  it('показывает дневник, редактирует вес и удаляет только после подтверждения', async () => {
    const user = userEvent.setup()
    const api = createMockApi()
    render(<App apiClient={api} />)
    await user.click(screen.getByRole('button', { name: /Сегодня/ }))

    expect((await screen.findAllByText('420')).length).toBeGreaterThan(0)
    await user.click(screen.getByRole('button', { name: 'Изменить' }))
    const weight = screen.getByLabelText('Новый вес')
    await user.clear(weight); await user.type(weight, '200')
    await user.click(screen.getByRole('button', { name: 'Сохранить' }))
    expect(api.updateMealItemWeight).toHaveBeenCalledWith(expect.any(String), 200, expect.stringMatching(/^weight:/))

    await user.click(screen.getByRole('button', { name: 'Удалить' }))
    expect(api.deleteMealItem).not.toHaveBeenCalled()
    await user.click(screen.getByRole('button', { name: 'Да, удалить' }))
    expect(api.deleteMealItem).toHaveBeenCalledTimes(1)
  })

  it('ищет и создаёт продукт с КБЖУ на 100 граммов', async () => {
    const user = userEvent.setup()
    const api = createMockApi()
    render(<App apiClient={api} />)
    await user.click(screen.getByRole('button', { name: /Продукты/ }))
    expect(await screen.findByText('Творог')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Поиск продуктов'), 'творог')
    await user.click(screen.getByRole('button', { name: 'Найти' }))
    expect(api.searchFoods).toHaveBeenLastCalledWith('творог')
    await user.click(screen.getByRole('button', { name: '+ Новый продукт' }))
    await user.type(screen.getByLabelText('Название'), 'Кефир')
    await user.clear(screen.getByLabelText('Калории / 100 г')); await user.type(screen.getByLabelText('Калории / 100 г'), '54')
    await user.click(screen.getByRole('button', { name: 'Сохранить' }))
    expect(api.createFood).toHaveBeenCalledWith(expect.objectContaining({ name: 'Кефир', caloriesPer100g: 54 }))
  })

  it('показывает состав, историю и два авторитетных набора КБЖУ рецепта', async () => {
    const user = userEvent.setup()
    const api = createMockApi()
    render(<App apiClient={api} />)
    await user.click(screen.getByRole('button', { name: /Рецепты/ }))
    await user.click(await screen.findByRole('button', { name: /Сырники/ }))

    expect(await screen.findByText('КБЖУ всего рецепта')).toBeInTheDocument()
    expect(screen.getByText('КБЖУ на 100 граммов')).toBeInTheDocument()
    expect(screen.getByText('400 г')).toBeInTheDocument()
    expect(api.getRecipeNutrition).toHaveBeenCalledWith(recipe.id, undefined)
    expect(api.getRecipeNutritionPer100g).toHaveBeenCalledWith(recipe.id, undefined)
    expect(screen.getByText(String(recipeNutrition.calories))).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'v1' }))
    await waitFor(() => expect(api.getRecipe).toHaveBeenLastCalledWith(recipe.id, 1))
  })

  it('показывает backend-цель в режиме только для чтения', async () => {
    const user = userEvent.setup()
    const api = createMockApi()
    render(<App apiClient={api} />)
    await user.click(screen.getByRole('button', { name: /Настройки целей/ }))

    expect(await screen.findByText('Только просмотр')).toBeInTheDocument()
    expect(screen.getByText(/2.100/)).toBeInTheDocument()
    expect(api.getDailySummary).toHaveBeenCalledWith(expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/))
  })

  it('показывает понятную ошибку API', async () => {
    const user = userEvent.setup()
    const api = createMockApi({ sendChatMessage: vi.fn().mockRejectedValue(new Error('Сервис временно недоступен')) })
    render(<App apiClient={api} />)
    await user.type(screen.getByLabelText('Сообщение'), 'Добавь яблоко')
    await user.click(screen.getByRole('button', { name: 'Отправить сообщение' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Сервис временно недоступен')
  })

  it('показывает уточнение выбора продукта и принимает ответ новым сообщением', async () => {
    const user = userEvent.setup()
    const api = createMockApi({
      sendChatMessage: vi.fn()
        .mockResolvedValueOnce({
          messageId: '70000000-0000-0000-0000-000000000007', assistantMessage: 'Нашёл несколько продуктов.',
          executedActions: [], pendingClarification: 'Выберите: 1 — домашний, 2 — магазинный.',
          pendingConfirmation: null, dailySummary: null,
        })
        .mockResolvedValueOnce({
          messageId: '80000000-0000-0000-0000-000000000008', assistantMessage: 'Выбран магазинный.',
          executedActions: [], pendingClarification: null, pendingConfirmation: null, dailySummary: null,
        }),
    })
    render(<App apiClient={api} />)
    const input = screen.getByLabelText('Сообщение')
    await user.type(input, 'Добавь йогурт')
    await user.click(screen.getByRole('button', { name: 'Отправить сообщение' }))
    expect(await screen.findByText(/Выберите: 1/)).toBeInTheDocument()
    await user.type(input, 'Вариант 2')
    await user.click(screen.getByRole('button', { name: 'Отправить сообщение' }))
    expect(api.sendChatMessage).toHaveBeenCalledTimes(2)
    expect(await screen.findByText('Выбран магазинный.')).toBeInTheDocument()
  })
})
