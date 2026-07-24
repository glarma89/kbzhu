import '@testing-library/jest-dom/vitest'
import { afterEach } from 'vitest'
import { cleanup } from '@testing-library/react'

afterEach(() => cleanup())

if (!crypto.randomUUID) {
  Object.defineProperty(crypto, 'randomUUID', {
    value: () => '00000000-0000-4000-8000-000000000001',
  })
}
