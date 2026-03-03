# Testing Patterns

Code examples for common testing scenarios. For testing philosophy and guidelines, see [AGENTS.md](AGENTS.md).

## React Hooks

```typescript
import { renderHook, act } from '@testing-library/react'

it('updates state correctly', () => {
  const { result } = renderHook(() => useMyHook())
  act(() => { result.current.doSomething() })
  expect(result.current.value).toBe('expected')
})
```

## Components

```typescript
import { render, screen } from '@testing-library/react'

it('renders the expected content', () => {
  render(<MyComponent />)
  expect(screen.getByRole('heading')).toHaveTextContent('Title')
})
```

## Utilities

```typescript
it('handles edge cases', () => {
  expect(myUtil(null)).toBe('default')
  expect(myUtil('input')).toBe('output')
})
```

## Global Stubs (Vitest)

Use `vi.stubGlobal()` instead of direct assignment for proper cleanup between tests:

```typescript
// Bad — leaks across tests
global.fetch = vi.fn();

// Good — cleaned up by vi.restoreAllMocks() or vi.unstubAllGlobals()
vi.stubGlobal('fetch', vi.fn());
```

Works for `fetch`, `localStorage`, `window.matchMedia`, and any other global.
