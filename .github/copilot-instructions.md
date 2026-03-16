- [x] Verify that the copilot-instructions.md file in the .github directory is created.
- [x] Clarify project requirements.
- [x] Scaffold the project.
- [x] Customize the project.
- [x] Install required extensions.
- [ ] Compile the project.
- [x] Create and run task definitions.
- [ ] Launch the project.
- [x] Ensure documentation is complete.

Project notes:
- Monorepo contains `control-api`, `infra`, `unity-client`, and `admin-web`.
- Reliability and Windows support are first-class requirements.
- Keep Unity client platform logic behind adapters.
- Keep control plane local-first with short-lived tokens and explicit health states.
- Admin web compiles successfully; Go compile check is blocked until Go is installed and available on PATH.
