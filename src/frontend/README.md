# Frontend layout

- `src/app`: routing, providers, application shell, and global configuration.
- `src/components/ui`: reusable design-system primitives.
- `src/features`: vertical product features such as workspace, tasks, imports, resources, and engineering.
- `src/hooks`: cross-feature React hooks.
- `src/lib`: API client and framework-independent helpers.
- `src/pages`: route-level compositions.
- `src/styles`: design tokens and global styles.
- `src/types`: genuinely shared TypeScript types.

Feature-specific components and types should remain inside their feature instead of being moved into global folders prematurely.

## Commands

```powershell
npm ci
npm run dev
npm run lint
npm run format:check
npm run build
```

Use the `@/*` alias for imports rooted at `src`, for example `@/app/App`. The current page is a foundation placeholder; routing, providers, and the full application shell belong to TP-005.
