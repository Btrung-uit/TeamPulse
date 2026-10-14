---
status: accepted
---

# Use a modular monolith

TeamPulse will begin as a modular monolith because a single deployable ASP.NET Core application keeps delivery realistic for one developer while still enforcing domain, application, infrastructure, and API boundaries. Microservices were rejected for the MVP because their independent deployment, networking, observability, and consistency costs would not improve the core student-team workflow.
