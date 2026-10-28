# Task Board Bulk Sprint Design

## Goal

Let workspace Leaders move multiple currently visible tasks to one Sprint or Backlog in one safe operation, and add lightweight Task Board keyboard shortcuts.

## Behavior

- Only Leaders see and can use task selection and bulk assignment.
- Each visible task has an accessible checkbox. "Select all visible" affects only tasks in the current filtered board result.
- Changing filters or board mode clears selection so hidden tasks are never changed accidentally.
- The bulk bar requires a destination: an existing Sprint or Backlog.
- The request includes every selected task's row version. The backend validates the project, destination, task membership, archive state, duplicates, and every row version before changing anything.
- One save persists all task changes and activity entries. Any invalid or stale task rejects the whole request.
- Success clears selection and refreshes task queries; failure preserves selection and explains the error.
- `/` focuses task search, `N` opens New task for Leaders, and `Esc` clears bulk selection.
- Shortcuts do nothing while the user is typing or while a dialog is open.

## Out of scope

- Bulk status changes, bulk archive, cross-page selection, undo, and member-facing bulk actions.
