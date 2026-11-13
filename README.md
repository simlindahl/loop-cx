# LOOP Cx
Private Android project for running workouts. The official LOOP Cx app for C1 and C2 running.
- [Product specification and acceptance tests](knowledge/product.md)
- [Design for review](design/LOOP-Cx-direction-5.pdf) · [Decisions](design/decision.md)
- [Working instructions](AGENTS.md)

knowledge/ is an Open Knowledge Format 0.2 bundle following [Google's specification](https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/main/okf/SPEC.md). Version requirements, design and evidence here. [GitHub Projects · LOOP Cx](https://github.com/users/simlindahl/projects/2) tracks work through Todo → Doing → Waiting → Done.

[Open mobile mockups as PDF](design/LOOP-Cx-direction-5.pdf) · [LOOP Cx product decision](knowledge/loop-cx.md)

<a id="så-arbetar-vi"></a>
## How we work
[Projekttavla](https://github.com/users/simlindahl/projects/2) · [Actions](https://github.com/simlindahl/loop-cx/actions/workflows/check.yml)

Simon prioritizes work and makes product and design decisions. The agent reads the GitHub project, sets the current card to Doing and works on a short-lived branch with requirements and results. Use `Closes #number` only when the PR fulfils the entire issue; otherwise use `Refs #number`. When the next step requires Simon, set Waiting with a concrete request and linked evidence. Update status directly in Projects through `gh project` and read it back; no board PR or CI is needed for status changes. The [status rules in AGENTS.md](AGENTS.md#work-tracking-and-status) govern this workflow. Simon approves merge unless that specific PR has been explicitly delegated. Complete physical checks that are part of acceptance before closing the issue.

Repository checks run on PRs and pushes to main and can be started manually. Run the same check locally with `ruby scripts/check_docs.rb`. The [Android check](.github/workflows/android.yml) builds a debug APK and starts the app in an emulator. [Run the app on Windows](docs/android.md).
