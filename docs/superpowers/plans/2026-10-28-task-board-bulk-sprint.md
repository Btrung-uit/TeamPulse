# Task Board Bulk Sprint Implementation Plan

> **For implementers:** Use `superpowers:executing-plans` and `superpowers:test-driven-development`; execute inline because the user already approved this design.

**Goal:** Add safe Leader-only bulk Sprint/Backlog assignment and `/`, `N`, `Esc` Task Board shortcuts.

**Architecture:** Extend the existing Planning endpoint/service for a single atomic bulk command, then expose it through the existing task API/query layer and a compact selection bar on `TaskBoardPage`.

**Tech Stack:** ASP.NET Core Minimal APIs, EF Core, React, TypeScript, TanStack Query, Vitest/Testing Library.

**Spec:** `docs/superpowers/specs/2026-10-28-task-board-bulk-sprint-design.md`

**Global Constraints:** Preserve existing single-task Sprint assignment; Leader-only authorization; no new dependency; no unrelated redesign; retain unrelated worktree changes.

**Review Focus:** Atomicity on stale/invalid tasks, tenant/project boundaries, selection scope after filters change, shortcut suppression in editable/dialog contexts, and keyboard/accessibility labels.

## Task 1: Backend bulk assignment contract

**Files:** `PlanningContracts.cs`, `IPlanningService.cs`, `PlanningService.cs`, `PlanningEndpoints.cs`, `PlanningEndpointsTests.cs`.

1. Add integration tests for successful multi-task assignment, stale row-version all-or-nothing rejection, and Leader authorization.
2. Add the request/command contract and Planning failure mapping.
3. Validate all inputs before mutation, write one activity per changed task, and call `SaveChangesAsync` once.
4. Run the focused backend integration tests.

## Task 2: Frontend bulk UI and shortcuts

**Files:** `types.ts`, `taskApi.ts`, `taskQueries.ts`, `TaskBoardPage.tsx`, `task-board.css`, `TaskBoardFlow.test.tsx`.

1. Add failing flow tests for Leader bulk assignment, filter-cleared selection, member visibility, and shortcuts.
2. Add the API type/mutation and compact selection UI.
3. Add guarded shortcuts and selection reset behavior.
4. Run focused frontend tests and type/lint checks.

## Task 3: Verification and delivery

1. Run backend and frontend focused suites plus build/type checks.
2. Run Impeccable detector and visually inspect desktop/mobile Task Board.
3. Review the whole diff, stage only task-owned files, and create one scoped commit.
