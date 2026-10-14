# TeamPulse Domain Context

TeamPulse coordinates university project work and the evidence used to review that work. This glossary defines the canonical language used by product documentation, APIs, database models, and user interfaces.

## Organization

**User**:
A person with a TeamPulse account who can participate in one or more workspaces.
_Avoid_: Account, employee

**Workspace**:
A durable collaboration boundary that owns members and projects for one student team or learning group.
_Avoid_: Team, organization, tenant

**Workspace Member**:
A user's membership in a workspace, including the role that controls access inside that workspace.
_Avoid_: Employee, collaborator record

**Leader**:
A workspace member responsible for planning, assignment, governance, and final contribution confirmation.
_Avoid_: Manager, admin

**Member**:
A workspace member who performs project work and provides evidence of that work.
_Avoid_: Employee, worker

**Advisor**:
A read-focused workspace member who observes progress and reports without managing day-to-day work.
_Avoid_: Teacher, manager, reviewer

## Work

**Project**:
A defined body of work owned by one workspace and completed toward a shared outcome.
_Avoid_: Workspace, board

**Sprint**:
A time-bounded planning period containing a selected set of project tasks.
_Avoid_: Phase, milestone

**Task**:
The smallest independently assignable unit of project work, identified by a project-scoped task code.
_Avoid_: Job, ticket, issue

**Task Code**:
A human-readable project-scoped identifier such as `TP-123`, used to connect work with external evidence.
_Avoid_: GitHub issue number, database ID

**Task Dependency**:
A relationship stating that one task is affected by the progress or completion of another task.
_Avoid_: Subtask, blocker

**Task Status**:
The current lifecycle position of a task: Todo, In Progress, Evidence Linked, In Review, Needs Changes, Blocked, Done, or Archived.
_Avoid_: Progress percentage

**Archived Task**:
A task removed from active planning while retaining its history, evidence, and contribution record.
_Avoid_: Deleted task

## Evidence and review

**Evidence**:
A reference that supports a member's claimed work on a task, such as a commit, pull request, document, design, or demo.
_Avoid_: Attachment, activity

**Review**:
A deliberate assessment of task evidence before the work is accepted as complete.
_Avoid_: Activity, approval notification

**Contribution Profile**:
A factual view of a member's accepted tasks, evidence, reviews, and activity; it is not an automatic quality score.
_Avoid_: Performance score, commit score

**Activity Log**:
An immutable chronological record of meaningful changes made by users or automations.
_Avoid_: Notification, audit score

## Resources and communication

**Resource**:
Metadata and a link to project material stored in TeamPulse or an external service.
_Avoid_: Evidence, document copy

**Notification**:
A user-facing alert about an event that may require awareness or action.
_Avoid_: Activity log

**Presentation Mode**:
A read-only project view that presents progress, contributions, evidence, and key resources for an audience.
_Avoid_: Slide editor, dashboard editor

## Onboarding

**Import Batch**:
A reviewed group of task rows prepared from one Excel upload and committed to one project as a single operation.
_Avoid_: Spreadsheet, sync job
