# Source layout

- `backend/`: ASP.NET Core modular-monolith projects.
- `frontend/`: React + TypeScript application organized by feature with shared UI primitives.

Both applications are runnable foundations. Product features are added vertically without weakening the backend project boundaries or moving feature-specific frontend code into shared folders prematurely.
