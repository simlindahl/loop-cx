<a id="loop-cx--designbeslut"></a>
# LOOP Cx · design decisions

<a id="gällande-beslut"></a>
## Current decision
Revision 5's approved layout with the Basalt palette chosen by Simon on 13 November 2026. Space Grotesk, Material Icons, a dark default theme and English apply. Colors and roles are specified in [the design specification](../knowledge/ux-design.md#basalt--approved-colors). The [HTML gallery](mockups/index.html) uses the approved neutral palette recorded below. The color choice does not change workout rules or layout.

<a id="statiskt-underlag-för-hela-appen"></a>
## Static reference for the whole app
At Simon's request on 13 November 2026, the [HTML gallery](mockups/index.html) is the ongoing visual reference instead of new PDFs. [Reading guide and requirements mapping](mockups/README.md). The palette, typography and approved revision 5 hierarchy still apply. New screens, states and detailed layouts are preliminary; requesting mockups does not approve their design. Android-owned surfaces are illustrations, not a promise of exact rendering. PDF mockups were removed at Simon's request on 13 November 2026. Earlier revisions remain in Git history.

<a id="historik"></a>
## History
Simon approved the product name, C1/C2 direction, Minimalist Material Design, Material Icons and Noto Sans. The visual design in Lopp-mockups.pdf awaits approval before implementation. Revision 1 remains in Git history.

Simon requested changes to revision 2's palette and layout. English in v1 is approved. Revision 3, Track, explores two views in LOOP-Cx-direction-3.pdf and awaits review.

Revision 4: a dark default theme was approved. Two views in LOOP-Cx-direction-4.pdf propose Barlow Semi Condensed with Noto Sans. Awaiting visual approval.

Revision 5: Simon chose Space Grotesk for all app-owned UI. A dark theme, English and Material Icons apply. Simon approved the layout as the implementation baseline on 12 November 2026. Reference: design/LOOP-Cx-direction-5.pdf in commit 12747f7.

## What is LOOP? · content and layout review

13 November 2026: Simon requested an English guide draft and Settings → What is LOOP? mockup before app code. Approved scope: offline reading, complete Core, four-part Get started, optional deeper learning, no separate main tab and no wearable requirement for following the guide. The [review brief](../docs/loop-knowledge-review.md), [guide](../knowledge/what-is-loop.md) and new `loop-*` gallery screens are **preliminary**. Basalt and the existing typography remain. Neither the detailed layout nor final training copy is approved. At that review stage, repository and Projects visibility remained private; no runtime changes were authorized. The later publication decision below supersedes only repository visibility.


### Approval and implementation hold

Simon approved the English guide and six `loop-*` reader mockups in PR #33 at commit `4a4c97170c858c2206b507b0cd9296795b5ccc84` on 13 November 2026. This supersedes the preliminary status above for this material only. Approval covers the audience and tone, practical workout instructions and home alternatives, Settings → What is LOOP? → Get started flow, and amount of optional learning. The phone frames are representative excerpts; the complete guide is the content baseline.

Simon separately authorized final review and merge of PR #33. App code remains on hold pending a separate instruction to proceed. The implementation split is now defined by the [roles and decision-making rules](../AGENTS.md#roles-and-decision-making); no new split agreement is needed. Static approval does not establish Android navigation, TalkBack, physical exercise safety or outdoor readability. The later approved publication made this repository public; the Project and private source remain private. See [publication review](../docs/publication-review.md). This supersedes the earlier repository-visibility restriction only: the reader runtime hold remains unchanged.

## Ghost Run · 13 November 2026

Simon approved a separate feature with routes saved after continuous workouts and racing only against year best or personal best. The race clock continues through all stops and pauses; a pause alone does not disqualify. On 13 November, mockup additions were requested before specification and the feature was added to overnight work. See the [Ghost Run specification](../knowledge/ghost-run.md) and mockups/index.html#ghost-routes. The 20 new states are preliminary. These product rules are approved; detailed layout is not approved for app UI or integration prototypes. Existing Basalt decisions apply.

## Android launcher icon · 13 November 2026

Simon approved the concrete launcher preview, revision 1, and authorized native integration and a data-preserving phone update on 13 November 2026 (issue #37). The originally approved mark is a continuous running-track loop tilted 35 degrees, with Basalt background `#10120F` and accent `#C4DA73`. The neutral approval below supersedes only that background color. Keep the original 108 × 108 layered geometry and its central safe area; do not add a second foreground padding step. Android controls launcher masks and themed coloring.

Original revision 1 SHA-256 identities (background and preview superseded by the neutral approval below): foreground `0A8D12E031F7E0366AB1598686371F9AAC57A000FA9FA9388956B60F9AFE79F6`; background `86A2D9A7AFD7C53EA0F59FA53B0C144ED40543A0560FAA8588CF0A85F03295ED`; preview `C6CEB661833BF044A437DDF5447808C3AA242C90348CCE010E2629A81A802755`. The production sources are `src/LoopCx/Resources/AppIcon/appicon.svg` and `appiconfg.svg`. Verify generated Android resources and the installed icon against the approved preview. Themed-launcher behavior is not established by standard icon verification.

Approval is limited to the app icon. It does not approve a wider UI redesign, Ghost detailed layouts or the held What is LOOP? app implementation. Preserve package identity, signing compatibility and existing run data; do not uninstall to refresh launcher artwork.

## Neutral Basalt approval · 13 November 2026

Simon approved the exact neutral palette and launcher background shown in the review: ‘Godkänn palett och ikonbakgrund’. Approval covers the [canonical color table](../knowledge/ux-design.md#basalt--approved-colors), including #111111 background, #1C1C1C surface, #F2F2F2 primary text, #ADADAD secondary text, #505050 decorative dividers and the unchanged #C4DA73 single accent. Review material: PR #40 at commit `2b690b47d47693104b1b49cbc98136cc8e18c1de` and its neutral UI/launcher previews. This supersedes the earlier green-tinted base colors and the launcher's #10120F background only.

The approved continuous forward-tilted loop geometry, foreground color, 108 × 108 layers, native integration authorization and data-preserving phone update remain. Regenerate and verify icon assets for the neutral background; the original background/preview hashes above identify historical artwork. Keep #505050 decorative; functional control/state cues require the contrast described in the specification. This color approval does not approve Ghost detailed layouts, release the What is LOOP? runtime hold or change any other product/design condition.

Neutral native artwork SHA-256: background `711099AF8751DDA5F2C370699E7C615B535053356FEE6D9B0F55E3C8A6768FA3`; foreground remains `0A8D12E031F7E0366AB1598686371F9AAC57A000FA9FA9388956B60F9AFE79F6`. Apply the approved #111111 background to both the SVG layer and MauiIcon color metadata; preserve all foreground geometry and sizing.

## Health Connect diagnostic rationale — 13 November 2026

Simon explicitly approved the exact offline **About this test** page and English text before implementation, against main `193fb70909737011016a50e8bce5ee1a1980e2d7`. The reviewed local artifacts are `artifacts/design/health-connect-rationale-proposal.html`, its normal/text200 PNG previews and `health-connect-rationale-decision.md` in the shared LOOP Cx workspace. This approval covers only the debug, dedicated account-free emulator compatibility probe: existing neutral Basalt, locally bundled font, four explanatory sections, scrollable text and one Back action finishing to the caller. It grants no production health-export policy or personal-phone write authority. Normal exports remain disabled. Native permission routing, text scaling, Back behavior and alias protection require verification; the static preview is not that evidence.

## Motion studies 01–07 · 13 November 2026

Simon explicitly approved the complete seven-study motion proposal and exact values, superseding the earlier 01–02-only starting recommendation. The shared local review material is `artifacts/design/motion/LOOP-motion-forslag.md` and `LOOP-motion-study.html`, based on `193fb70`; [#62](https://github.com/simlindahl/loop-cx/issues/62) records the owner decision. The [canonical motion contract](../knowledge/ux-design.md#approved-motion-and-interaction-system) owns values, reduced motion and runtime acceptance; [mapping](mockups/README.md#motion-implementation-dependencies) owns real-surface dependencies. No new general approval checkpoint is required before implementing the approved family. New material layouts still require their existing approval; absent product operations, reader runtime hold and preliminary Ghost layouts are not approved by this motion decision. Motion is not implemented merely because the studies and decision exist.

## Final C1 preparation decision · 13 November 2026

Simon explicitly decided to **retain the existing C1 app default: 10-minute warm-up and 5-minute cool-down**. Five 3-minute work efforts and four 2-minute recoveries remain a 23-minute main set and 38-minute total, matching the training-source dose. This final owner decision supersedes the earlier request for 3/2-minute app defaults in [#65](https://github.com/simlindahl/loop-cx/issues/65). That provisional implementation was not delivered or merged; its preserved work does not authorize future delivery. The current app already matches the final decision: no factory, template revision, stored plan, runtime or installation change is required. Do not revive the superseded 3/2 authorization in a later chat; a new change would require a new owner decision. This records the choice without claiming that research established universally optimal preparation durations.

## Official C1 revision 2 · 13 November 2026

In [#70](https://github.com/simlindahl/loop-cx/issues/70), Simon explicitly chose **8-minute warm-up and 4-minute cool-down as official C1**, replacing the preceding 10/5 default decision. Five 3-minute work efforts and four 2-minute recoveries remain 23 minutes; the new official total is 35 minutes. This updates the training guide as well as the app default and is an owner programme decision, not a claim of scientifically optimal preparation durations.

New C1 plans use template revision 2; existing active/saved revision 1 plans remain 10/5 with 38 total/23 main, and old unknown metadata stays unknown. C2 remains 45 minutes at target RPE 3 with no preparation. Global Settings may override C1 preparation defaults for new starts, resolved and frozen with the original plan; there are no per-run preparation options. This small delivery changes the official default, not a Settings editor/storage implementation.

The latest Core-first direction allows bounded C1/C2 personalizations rather than an arbitrary workout builder. Earlier generic builder/flow/numbering approvals and unmerged Time/Distance/Open prototypes are held pending the Core-first redesign; preserved work is not shipping authority. This supersedes the earlier 3/2 request and the subsequent keep 10/5 decision for current official C1 only, without rewriting historical runs. Reader runtime/design holds, identity/signing and physical acceptance remain in force; no phone update is authorized by this decision alone.


<a id="core-first-approved-variants"></a>
## C1 official variants and Core-first design · 13–14 November 2026

Simon clarified C1.1 as maximal SIT, C1.2 as aerobic HIIT and C1.3 as threshold intervals. The [owning specification](../knowledge/running-workouts.md#c1-variant-purposes-and-evidence) records the approved final doses and research limits: eight 30-second SIT efforts/seven three-minute recoveries at RPE 10/2; five three-minute HIIT efforts/four two-minute recoveries at 7/3; three six-minute threshold efforts/two three-minute recoveries at 6/3. Shared preparation is 4 minutes up/3 minutes down, with runtime extension/skip retained. Default main/total times are 25/32, 23/30 and 24/31 minutes. These latest decisions supersede controlled 1:1/30:30 C1.1 proposals, earlier SIT counts/recovery, seven-minute threshold efforts and earlier differing or 6/3 preparation proposals. The guide and specification agree. High-zone minutes remain a follow-up aim, not a stop condition or physiological guarantee.

Simon explicitly approved the interactive Core study's design direction and choices: two Core cards; compact C1 variant selection; nearby short RPE dialog; target pace or heart-rate zone; saved shortcuts; initial C1.2 and retained last selection; one global C1 preparation Settings pair resolved at each new start, including existing shortcuts. Earlier started/historical plans stay frozen. The reviewed study was `core-variants/index.html`, SHA256 B86EEF1771FE49F2FE23BA3626A8FA16150395587B933D16F2A8375CCF51988B, with script AAACD3E71776B312A16ECD683890E9370214236B20DE5D45A750666CFC654378 and notes 84479C044744E4A06C1823BFA05DD4CD0E5B1FC3F357ADD2D1D9E91753F17E6D. The subsequent explicit SIT dose/recovery and six-minute threshold changes update content within that design; they do not require reapproval of the same layout. The repository's [Core study reference](mockups/core-variants/index.html) preserves the approved direction with those content corrections.

This approves the bounded Core-first flows, not the old generic builder or every gallery screen. RPE is visible/read-only initially; the accessible information dialog explains the relevant variant. Simon corrected C2 target RPE from3 to4 and then replaced intermediate presets with a20–120-minute slider in five-minute steps, default45. He also explicitly authorised C1 repetition-count selection with10–60minutes planned MAIN SET INCLUDING RECOVERY (preparation excluded), clarified in direct response. This gives SIT4–18 reps/default8, HIIT3–12/default5, threshold2–7/default3. Work/recovery times and fixed RPE remain unchanged. The counts/slider reuse the approved compact adjustment surfaces; this does not approve an arbitrary phase/unit builder. C2 retains no preparation; legacy revision1/RPE3 plans stay frozen. The current app's delivered C1 revision2 and immutable history remain unchanged until separately reviewed implementation; design approval is not runtime acceptance. Other reader/Ghost holds, identity/signing and physical gates remain in force.

## Autopause state and controls conformance · 13 November 2026

The team lead adopted UX's bounded mapping to implement the existing requirement to distinguish automatic/manual pause and preserve manual precedence in PR #73. This is conformance within the existing active-run surface/action group and styles, not approval of a new layout, information architecture or preliminary gallery screen. Auto shows **Autopaused**, **Move to resume automatically.**, existing **Resume** and secondary **Pause manually**; Manual shows **Manually paused**, **Use Resume to continue.** and **Resume**. Keep the current phase and frozen values. Standard Android notification titles/text/actions distinguish the same states. The old gallery's five-second explanatory copy is not adopted. Actual tap-to-manual behavior, notification text/actions and reachable ≥48 dp controls at large text require new native evidence; backend-only ADB promotion is insufficient.


Paused notification action priority: Manual shows Resume only; Auto shows Resume and Pause manually. Preparation +1/skip notification actions return while running, avoiding four actions being truncated by the system. Existing app preparation commands remain unchanged. Standard notification subtext retains phase/countdown while title/body explain pause reason. This is the lead-confirmed bounded state mapping, not a new Core dose or layout approval.

## M2 identity and fresh installation · 14 November 2026

Simon explicitly chose full technical as well as visible LOOP naming, then removed the migration requirement because he is the only installed user and accepts reinstallation. The [owning M2 roadmap](../knowledge/loop-core-roadmap.md#rename-at-the-start-of-m2) now requires a consistent technical rename and verified fresh installation after M1 acceptance, without a local-data bridge or history/settings import. This supersedes the prior data-preserving package-transition gate for the M2 rename only. No actual rename or app removal is performed by this specification update.
