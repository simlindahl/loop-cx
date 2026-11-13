---
name: verify-run
description: Verify workout rules, emulator flows and integrations before delivering the running app.
---
1. Follow AGENTS.md status rules and use GitHub Projects · LOOP Cx (https://github.com/users/simlindahl/projects/2) before, during and after work. Read the relevant acceptance test in knowledge/product.md. Use a deterministic clock for workout rules, an Android emulator for UI and a physical Pixel with WHOOP for background measurements.
2. Run the scenario with the specified input. Compare actual phase order, statistics and export with the expected result; test restart or data gaps when storage or sensors are affected.
3. Record commit, platform, reproduction steps and results in the PR. Mark each physical test as verified, failed or not run. Synthetic Health Connect export must be disabled.
4. Link verification results from the issue and project card. Follow AGENTS.md roles and decision-making rules: the team lead coordinates separate requirements analyst and tester reviews and delegates corrections to the developer subagent. Record both review outcomes and reviewed commits in the PR. Merge only under AGENTS.md merge conditions; routine technical review does not require Simon. When a product or architecture decision, explicit design approval or physical testing requires Simon, follow Waiting rules with concrete decision or test material.
5. Leave the issue open if mandatory acceptance remains. Follow AGENTS.md Done conditions; a partial PR references the feature issue without closing it.
