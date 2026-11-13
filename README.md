# LOOP Cx
Private Android project for running workouts. The official LOOP Cx app for C1 and C2 running.
- [Product specification and acceptance tests](knowledge/product.md)
- [Static HTML mockups for the whole app](design/mockups/index.html) · [Open and review](design/mockups/README.md) · [Decisions](design/decision.md)
- [Work list · Todo / Doing / Waiting / Done](https://github.com/users/simlindahl/projects/2)
- [Working instructions](AGENTS.md)

knowledge/ is an Open Knowledge Format 0.2 bundle following [Google's specification](https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/main/okf/SPEC.md). Version requirements, design and evidence here. [GitHub Projects · LOOP Cx](https://github.com/users/simlindahl/projects/2) tracks work through Todo → Doing → Waiting → Done.

[Open the static mobile mockups](design/mockups/index.html) · [LOOP Cx product decision](knowledge/loop-cx.md)

<a id="så-arbetar-vi"></a>
## How we work
[Work list](https://github.com/users/simlindahl/projects/2) · [Actions](https://github.com/simlindahl/loop-cx/actions/workflows/check.yml)

The [roles and decision-making rules](AGENTS.md#roles-and-decision-making) define Simon as product owner and architect and the primary agent as team lead, coordinating separate requirements analyst, tester and developer subagents. The team lead reports meaningful progress, material blockers and decisions awaiting Simon; internal team coordination stays within the team. The agent reads the GitHub project, sets the current card to Doing and works on a short-lived branch with requirements and results. Use `Closes #number` only when the PR fulfils the entire issue; otherwise use `Refs #number`. When the next step requires Simon, set Waiting with a concrete request and linked evidence. Update status directly in Projects through `gh project` and read it back; no board PR or CI is needed for status changes. The [status rules in AGENTS.md](AGENTS.md#work-tracking-and-status) govern this workflow. The team lead may merge after separate requirements and test approvals and passing required checks under AGENTS.md, without waiting for Simon's merge approval. After valuable feature merges, offer an optional USB installation on Simon's phone under the [phone installation workflow](docs/android.md#usb-phone-installation). Complete physical checks that are part of acceptance before closing the issue.

Repository checks run on PRs and pushes to main and can be started manually. Android and Static mockup review run on every PR without path filters, including documentation-only changes, and can be started manually. Static mockup review also runs on pushes to main. Check names are `specification`, `emulator` and `mockups`. The emulator performs a real build and smoke test even when app code has not changed. See [main protection and activation](docs/main-protection.md). Run the repository check locally with `ruby scripts/check_docs.rb`. The [Android check](.github/workflows/android.yml) builds a debug APK and starts the app in an emulator. [Run the app on Windows](docs/android.md).

## Publication preparation

Maintained repository and GitHub content uses English; historical commits, comments and binary design assets are preserved. The repository remains private until publication is explicitly approved. No project-wide license has been selected. Read the [publication review](docs/publication-review.md) before proposing a visibility change.

## What is LOOP? · approved reference

[English practical guide](knowledge/what-is-loop.md) · [Review brief](docs/loop-knowledge-review.md) · [Settings knowledge mockups](design/mockups/index.html#loop-about). Content and layout approved by Simon on 13 November 2026; app implementation remains on hold. See [approval and conditions](design/decision.md#what-is-loop--content-and-layout-review).
