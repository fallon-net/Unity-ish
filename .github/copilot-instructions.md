- [x] Verify that the copilot-instructions.md file in the .github directory is created.
- [x] Clarify project requirements.
- [x] Scaffold the project.
- [x] Customize the project.
- [x] Install required extensions.
- [x] Compile the project.
- [x] Create and run task definitions.
- [x] Launch the project.
- [x] Ensure documentation is complete.

Project notes:
- Monorepo contains `control-api`, `infra`, `desktop-client`, `unity-client` (legacy), and `admin-web`.
- Reliability and Windows support are first-class requirements.
- Keep desktop client transport logic independent from the Unity game engine.
- Keep control plane local-first with short-lived tokens and explicit health states.
- Admin web compiles successfully; Go compile check now passes locally.
- Desktop client typechecks, builds, and launches in dev mode locally.
