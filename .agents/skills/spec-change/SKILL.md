---
name: spec-change
description: Use when a requirement, flow or design for the running app changes.
---
1. Follow AGENTS.md status rules and use GitHub Projects · LOOP Cx (https://github.com/users/simlindahl/projects/2) before, during and after work. Read AGENTS.md, knowledge/product.md and the affected owning specification linked from it. Describe the user's concrete situation and desired result in a GitHub issue.
2. Update the canonical OKF page. Add an observable acceptance example and link it from the issue. Preserve YAML frontmatter with type and relative links.
3. Update affected mockups. For UI changes, record the material Simon approved in design/decision.md. A draft or an agent's assessment is not approval.
4. Split work into the smallest buildable change. Link the issue from the project card, link the PR and report deviations from approved requirements. Follow AGENTS.md roles and decision-making rules: the team lead coordinates separate requirements analyst, QA engineer, fullstack developer and UX designer subagents. The requirements analyst checks scope and acceptance coverage; the UX designer develops and evaluates flows and design proposals within the approved visual direction; the fullstack developer implements approved changes. Escalate product, significant architecture and design decisions to Simon through the team lead. Do not request a new implementation split. When such a decision or approval is needed, follow Waiting rules. PR review and merge follow the delegated workflow in AGENTS.md.
