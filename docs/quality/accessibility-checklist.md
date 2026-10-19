# TeamPulse system states and accessibility checklist

This checklist is the release gate for TP-050. It applies to desktop and mobile layouts in both light and dark themes.

## System states

- [x] Route and data loading states expose `role="status"` and readable loading text.
- [x] Project, task, dashboard, resource and presentation views show a skeleton or progress state while requests are pending.
- [x] Recoverable API errors explain that existing data/filter context is safe and expose a retry or return action.
- [x] First-use empty states and filtered empty states use different guidance and actions.
- [x] Invalid workspace/project links and restricted import routes show an explicit no-permission state.
- [x] Project archive, task archive, resource removal and transactional Excel import require a consequence-focused confirmation dialog.
- [x] Deadline changes require confirmation and state that dependent deadlines are not moved automatically.
- [x] Inline field errors remain associated with their fields through `aria-invalid` and `aria-describedby`.

## Keyboard and focus

- [x] A skip link moves focus to `#main-content`.
- [x] Interactive cards and table rows support Enter/Space where applicable.
- [x] Modal dialogs trap Tab/Shift+Tab, close with Escape when safe, and restore focus to the trigger.
- [x] Task detail traps focus, restores the triggering card and does not close behind an active confirmation dialog.
- [x] The core Playwright demo activates the main workflow controls with Enter, including auth, project creation, import, task updates, review and presentation navigation.
- [x] Focus indicators are visible in both themes.

## Visual and motion

- [x] Status and priority are communicated with text/icons, not color alone.
- [x] Light and dark semantic text tokens meet WCAG AA contrast in the automated core-flow audit.
- [x] `prefers-reduced-motion: reduce` disables non-essential animation, smooth scrolling and transitions.
- [x] Layout remains usable from 320 px width and interactive controls keep visible labels or accessible names.

## Automated verification

Run from `src/frontend`:

```powershell
npm run format:check
npm run lint
npm run test
npm run build
npm run e2e
```

The Playwright suite runs axe against the empty dashboard, Excel validation preview, member task detail, populated dashboard and Presentation Mode. It fails on WCAG 2.0/2.1 A or AA violations and retains screenshot, video and trace evidence on failure.

Manual checks still required before a public release:

- Test browser zoom at 200% on dashboard, task detail and import preview.
- Test Windows High Contrast/forced-colors mode.
- Complete one pass with NVDA or another desktop screen reader.
