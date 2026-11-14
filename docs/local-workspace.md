# Local workspace

Use the repository root as the local project workspace. Build and verification
commands must use an explicit worktree and device; moving local files does not
authorize a phone action or Health Connect write.

## Maintained sources and local files

Version application/test source, specifications, instructions, scripts and
approved portable design references in their existing repository owners. Review
and promote a useful local artifact explicitly rather than committing the
artifact directory wholesale.

The root `.gitignore` excludes local evidence and screenshots in `artifacts/`,
downloaded SDKs and virtual environments in `local-toolchain/`, generated
`local-build/`, publication audit working files in `publication-preparation/`,
and isolated `.worktrees/`. It also excludes generated outputs, local recordings,
packages and signing material. The historical `loop-guide-approval.txt` and
one-off `split-product-specs.py` are preserved locally, not maintained authority.

## Workspace consolidation

On 14 November 2026, Simon requested moving all contents of
`C:/Users/Simon/Documents/ChatGPT/LOOP Cx - app` into this repository root.
The old directory is not an alternative workspace. New toolchain and evidence
paths are repository-relative; for example, `local-toolchain/dotnet-10.0.100/`
and `artifacts/design/`.

Historical logs and immutable verification manifests retain their original
execution paths and hashes. Resolve their former workspace prefix against the
repository root when locating moved evidence; that relocation is not a new
build or repeated acceptance result. Current scripts and handoffs must use the
new location. Local move inventories belong under
`artifacts/workspace-consolidation/` and remain excluded from Git.

Python virtual-environment launchers and emulator configuration may contain
absolute paths. Verify the needed tools at the new location before resuming
them, and repair local configuration without rewriting historical evidence.
