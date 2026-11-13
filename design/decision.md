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

## C1 official variants: owner decisions, implementation pending

On 13 November 2026 Simon defined three official C1 protocols, delegated repetition counts and confirmed the resulting fixed RPE/dose proposal. The [owning variant specification](../knowledge/running-workouts.md#c1-variant-purposes-and-evidence) records their distinct purposes, doses, preparation and primary research with transfer limits. Higher-zone time is a follow-up aim, not a completion rule; threshold intent takes precedence for C1.3. RPE is visible/read-only initially; a nearby information button must open a short explanation dialog.

This records product/content decisions, not approval of a new screen, dialog layout or runtime implementation. The delivered C1 revision 2 and immutable history remain unchanged until a separately reviewed delivery. Initial variant selection and remaining allowed adjustments need resolution before dependent implementation; revised UX must follow normal approval.

Simon subsequently clarified C1.1 as SIT, C1.2 as aerobic HIIT and C1.3 as threshold intervals, then approved C1.1 as five 30-second maximal efforts with four four-minute easy recoveries and RPE 10/2. This supersedes the preceding controlled short-interval definition; the owning table and research distinguish maximal SIT from aerobic HIIT. Simon then replaced the differing preparation values and intervening 6/3 proposal with a shared 4-minute warm-up and 3-minute cool-down for all C1 variants, retaining in-session extensions. Main sets are unchanged: total25:30/30/34 for SIT/HIIT/threshold. The training guide and owning specification agree; current delivered revision2 and historical plans remain frozen.
