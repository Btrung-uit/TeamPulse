# TeamPulse UI P1 Fixes Design

## Intent

Resolve the three P1 findings from the 2026-10-26 Impeccable critique: make planning reachable on mobile, reduce Task Board decision load, and replace the join-workspace dead end with a durable waiting state.

## Scope

- Add Projects, Members, and Task Board destinations to the existing mobile More page; keep the four-item bottom navigation unchanged.
- Keep Task Board search visible, place secondary filters behind one disclosure control, and reuse existing `view`/`attention` URL state for My tasks, Needs attention, and Full workflow views. Needs attention renders a compact task list instead of seven empty columns.
- Persist a local “waiting for Leader” acknowledgement per signed-in email and present a clear pending state with Check access and Back actions.

## Non-goals

- No new backend invitation/request model, email delivery, notifications, dependencies, or design-system rewrite.
- No change to task lifecycle, permissions, APIs, or desktop sidebar information architecture.

## Approach

The rejected larger approach adds invitation entities and notification delivery; it is valuable post-MVP but exceeds this UI request. The rejected navigation approach adds a fifth bottom-navigation item, which reduces space and still hides other workspace destinations. The selected approach reuses existing routes, query parameters, components, and browser storage, producing the smallest complete UI fix.

## Acceptance

- A mobile user can reach Projects, Members, and Task Board from More.
- Task Board initially exposes search and three clear workflow views; advanced filters remain accessible and reopen automatically when active.
- Join onboarding survives refresh in an honest “waiting” state without claiming TeamPulse sent a request.
- Existing permissions, URL persistence, exports, accessibility labels, and desktop behavior remain intact.
