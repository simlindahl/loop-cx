---
type: Product Specification Index
title: LOOP Cx product specifications
---
<a id="loop-cx-för-android--specifikation-v1"></a>
<a id="loop-cx-for-android--specification-v1"></a>
# LOOP Cx product specifications

The current Android running app remains **LOOP Cx** through milestone 1. At the start of milestone 2, the app and GitHub repository are to become **LOOP**, subject to the identity/update/data transition gate below. Cx then denotes the conditioning domain within LOOP, not the product identity. This documentation change does not rename the current app, repository, package or namespace.

## Specification ownership

Read this entry point and the affected owning specification before a change. Rules and acceptance examples have one owner; links below and legacy landing anchors do not duplicate them. The split preserves the approved baseline at `eb160e5a6683d9361c73e1d58d9493a2c5ca7798`.

| Owner | Scope |
|---|---|
| [Running workouts](running-workouts.md) | Current product delivery, C1/C2, workout/phase/statistics/pause rules and coaching |
| [Architecture](architecture.md) | Approved local modular monolith, service ownership and code organization |
| [Integrations and data](integrations-data.md) | Android permissions, WHOOP, SQLite, maps, Health Connect and JSON |
| [Screens and design](ux-design.md) | Screen behavior, approved neutral Basalt, accessibility and design acceptance |
| [Ghost Run](ghost-run.md) | Routes, reference selection, race clock, matching, uncertainty and Ghost acceptance |
| [Delivery and acceptance](delivery-acceptance.md) | Windows/emulator sequence, cross-feature acceptance, primary sources and remaining physical checks |
| [LOOP knowledge feature](loop-knowledge.md) | Approved reader scope and acceptance; runtime implementation remains on hold |
| [Future LOOP Core](loop-core-roadmap.md) | M2 planning, rename gate, Sx/M management boundary and open product decisions |

[Training guide](what-is-loop.md) owns the approved explanatory Core content. [C1/C2 product decision](loop-cx.md) retains source-template decisions. [Design decisions](../design/decision.md) record approvals; [AGENTS.md](../AGENTS.md) owns delivery/review rules. GitHub Projects is the sole work list; milestones group outcomes and do not replace issue acceptance.

## Milestones

- [M1 · LOOP Cx](https://github.com/simlindahl/loop-cx/milestone/1): complete the current running app and existing acceptance, including Ghost and required physical evidence. Current naming/identity remains.
- [M2 · LOOP](https://github.com/simlindahl/loop-cx/milestone/2): first rename app and repository to LOOP through the [transition gate](loop-core-roadmap.md#rename-at-the-start-of-m2), then integrate complete Core including S1/S2 and separately managed M selected from Sx. Planning does not authorize implementation now.

No dates or completion percentages are inferred. A merged slice is not full milestone acceptance. Publication and licensing still require separate approval.

## Legacy requirement links

Existing issue, PR and document links into product.md continue here. Follow the linked section for the single authoritative rule text.

<a id="produkt-och-leverans"></a>

Continue to [Product and delivery](running-workouts.md#produkt-och-leverans).

<a id="product-and-delivery"></a>

Continue to [Product and delivery](running-workouts.md#product-and-delivery).

<a id="loop-core"></a>

Continue to [LOOP Core](running-workouts.md#loop-core).

<a id="pass-och-statistik"></a>

Continue to [Workouts and statistics](running-workouts.md#pass-och-statistik).

<a id="workouts-and-statistics"></a>

Continue to [Workouts and statistics](running-workouts.md#workouts-and-statistics).

<a id="återkoppling-under-passet"></a>

Continue to [Feedback during workouts](running-workouts.md#återkoppling-under-passet).

<a id="feedback-during-workouts"></a>

Continue to [Feedback during workouts](running-workouts.md#feedback-during-workouts).

<a id="teknikbeslut"></a>

Continue to [Technology decisions](architecture.md#teknikbeslut).

<a id="technology-decisions"></a>

Continue to [Technology decisions](architecture.md#technology-decisions).

<a id="kodorganisation"></a>

Continue to [Code organization](architecture.md#kodorganisation).

<a id="code-organization"></a>

Continue to [Code organization](architecture.md#code-organization).

<a id="integration-lagring-och-fel"></a>

Continue to [Integration, storage and errors](integrations-data.md#integration-lagring-och-fel).

<a id="integration-storage-and-errors"></a>

Continue to [Integration, storage and errors](integrations-data.md#integration-storage-and-errors).

<a id="vyer-och-utformning"></a>

Continue to [Screens and layout](ux-design.md#vyer-och-utformning).

<a id="screens-and-layout"></a>

Continue to [Screens and layout](ux-design.md#screens-and-layout).

<a id="uiux--designkontrakt-för-v1"></a>

Continue to [UI/UX – design contract for v1](ux-design.md#uiux--designkontrakt-för-v1).

<a id="uiux--design-contract-for-v1"></a>

Continue to [UI/UX – design contract for v1](ux-design.md#uiux--design-contract-for-v1).

<a id="basalt--beslutade-färger"></a>

Continue to [Basalt – approved colors](ux-design.md#basalt--beslutade-färger).

<a id="basalt--approved-colors"></a>

Continue to [Basalt – approved colors](ux-design.md#basalt--approved-colors).

<a id="windows-emulator-och-leveransordning"></a>

Continue to [Windows, emulator and delivery order](delivery-acceptance.md#windows-emulator-och-leveransordning).

<a id="windows-emulator-and-delivery-order"></a>

Continue to [Windows, emulator and delivery order](delivery-acceptance.md#windows-emulator-and-delivery-order).

<a id="acceptansprov"></a>

Continue to [Acceptance tests](delivery-acceptance.md#acceptansprov).

<a id="acceptance-tests"></a>

Continue to [Acceptance tests](delivery-acceptance.md#acceptance-tests).

<a id="primärkällor-och-kvarstående-verifiering"></a>

Continue to [Primary sources and remaining verification](delivery-acceptance.md#primärkällor-och-kvarstående-verifiering).

<a id="primary-sources-and-remaining-verification"></a>

Continue to [Primary sources and remaining verification](delivery-acceptance.md#primary-sources-and-remaining-verification).

<a id="projektstyrning"></a>

Continue to [Project governance](delivery-acceptance.md#projektstyrning).

<a id="project-governance"></a>

Continue to [Project governance](delivery-acceptance.md#project-governance).

<a id="loop-knowledge--review-proposal"></a>

Continue to [LOOP knowledge — review proposal](loop-knowledge.md#loop-knowledge--review-proposal).

<a id="ghost-run"></a>

Continue to [Ghost Run](ghost-run.md#ghost-run).

<a id="omfattning-och-ingång"></a>

Continue to [Scope and entry point](ghost-run.md#omfattning-och-ingång).

<a id="scope-and-entry-point"></a>

Continue to [Scope and entry point](ghost-run.md#scope-and-entry-point).

<a id="spara-och-välja-rutt"></a>

Continue to [Save and select a route](ghost-run.md#spara-och-välja-rutt).

<a id="save-and-select-a-route"></a>

Continue to [Save and select a route](ghost-run.md#save-and-select-a-route).

<a id="motståndare-och-årsurval"></a>

Continue to [Opponent and year selection](ghost-run.md#motståndare-och-årsurval).

<a id="opponent-and-year-selection"></a>

Continue to [Opponent and year selection](ghost-run.md#opponent-and-year-selection).

<a id="start-tävlingsklocka-och-paus"></a>

Continue to [Start, race clock and pause](ghost-run.md#start-tävlingsklocka-och-paus).

<a id="start-race-clock-and-pause"></a>

Continue to [Start, race clock and pause](ghost-run.md#start-race-clock-and-pause).

<a id="uppspelning-och-jämförelse"></a>

Continue to [Playback and comparison](ghost-run.md#uppspelning-och-jämförelse).

<a id="playback-and-comparison"></a>

Continue to [Playback and comparison](ghost-run.md#playback-and-comparison).

<a id="giltighet-och-osäkerhet"></a>

Continue to [Validity and uncertainty](ghost-run.md#giltighet-och-osäkerhet).

<a id="validity-and-uncertainty"></a>

Continue to [Validity and uncertainty](ghost-run.md#validity-and-uncertainty).

<a id="livekarta-ljud-och-låst-skärm"></a>

Continue to [Live map, audio and locked screen](ghost-run.md#livekarta-ljud-och-låst-skärm).

<a id="live-map-audio-and-locked-screen"></a>

Continue to [Live map, audio and locked screen](ghost-run.md#live-map-audio-and-locked-screen).

<a id="acceptansprov-och-leverans"></a>

Continue to [Acceptance tests and delivery](ghost-run.md#acceptansprov-och-leverans).

<a id="acceptance-tests-and-delivery"></a>

Continue to [Acceptance tests and delivery](ghost-run.md#acceptance-tests-and-delivery).
