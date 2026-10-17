# TeamPulse frontend

React + TypeScript client for the TeamPulse project workspace. TP-005 provides
the design-system foundation and responsive application shell. TP-006 adds the
reusable, accessible primitives used by subsequent product features.

TP-023 adds the first data-backed work-management screen: a project-scoped
Kanban board, URL-persisted filters and sorting, paginated task loading, and a
Leader-only task creation flow.

## Runtime foundation

- React Router owns the route tree, not-found handling, and route-level error
  boundary. The dashboard is lazy-loaded with a visible skeleton fallback.
- TanStack Query is configured once in `src/app/providers` for future server
  state. Task list/options queries and task creation mutations consume this
  shared client and invalidate only the active project's task cache.
- `ThemeProvider` persists `dark` or `light` in `localStorage` under
  `teampulse-theme` and applies the matching design tokens to the root element.
- `GlobalErrorBoundary` protects failures above the router; route failures use
  the narrower `RouteErrorBoundary`.

## Layout and responsive behavior

- Desktop: 256 px sidebar, 72 px topbar, and a 32 px content gutter.
- Tablet (`<= 1024 px`): compact 88 px icon sidebar and reduced content gutter.
- Mobile (`< 768 px`): monitoring-first topbar and four-item bottom navigation;
  desktop administration navigation is hidden.
- Theme, spacing, radius, typography, semantic colors, motion, and layout values
  live in `src/styles/tokens.css` and `src/styles/tokens.ts`.

## Source layout

- `src/app`: routing, providers, application shell, and global configuration.
- `src/components/ui`: reusable design-system primitives.
- `src/features`: vertical product features such as workspace, tasks, imports, resources, and engineering.
- `src/hooks`: cross-feature React hooks.
- `src/lib`: API client and framework-independent helpers.
- `src/pages`: route-level compositions.
- `src/styles`: design tokens and global styles.
- `src/types`: genuinely shared TypeScript types.

Feature-specific components and types should remain inside their feature instead of being moved into global folders prematurely.

## UI primitives

Reusable components are exported from `src/components/ui/index.ts`:

- `Button`: primary, secondary, ghost, danger, link and icon-only variants with
  three sizes, loading and disabled states.
- `Input` and `Textarea`: visible labels, helper/error association, optional
  icons/actions, read-only, disabled and searching states.
- `StatusBadge`: the canonical task-status mapping with icon and text.
- `Modal`: portal rendering, focus trap, Escape/backdrop handling, focus restore
  and mobile bottom-sheet behavior.
- `ToastProvider` and `useToast`: five semantic variants, at most three visible
  notifications, appropriate timeouts and live-region announcements.
- `EmptyState`, `TaskCard` and generic `Table`: responsive workflow and
  data-display foundations, including skeleton/empty states and mobile cards.

Open `/dev/components` during development to review every component and its
important states in dark/light and desktop/mobile layouts.

## Task board

`/workspaces/{workspaceId}/projects/{projectId}/tasks` reads every server page,
groups active tasks into the seven lifecycle columns, and keeps search,
assignee, sprint, priority, and sort state in the query string. The board uses
horizontal scrolling so columns remain readable instead of collapsing.

Only a Leader sees task creation controls. The modal validates the same task
code, title, description, estimate, membership, sprint, and priority contract
as the API. Member and Advisor views remain read-only in this slice. Status
mutation and task-detail conflict handling intentionally remain in TP-024, so
TP-023 does not expose drag-and-drop.

## Commands

```powershell
npm ci
npm run dev
npm run lint
npm run format:check
npm run test
npm run build
```

Use the `@/*` alias for imports rooted at `src`, for example `@/app/App`.
Keep route and provider wiring in `src/app`; feature-specific pages and logic
belong in `src/features`.
