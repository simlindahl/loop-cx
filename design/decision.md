<a id="loop-cx--designbeslut"></a>
# LOOP Cx · design decisions

<a id="gällande-beslut"></a>
## Current decision
Revision 5's approved layout with the Basalt palette chosen by Simon on 13 November 2026. Space Grotesk, Material Icons, a dark default theme and English apply. Colors and roles are specified under Basalt in knowledge/product.md. The [HTML gallery](mockups/index.html) shows the chosen palette. The color choice does not change workout rules or layout.

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

13 November 2026: Simon requested an English guide draft and Settings → What is LOOP? mockup before app code. Approved scope: offline reading, complete Core, four-part Get started, optional deeper learning, no separate main tab and no wearable requirement for following the guide. The [review brief](../docs/loop-knowledge-review.md), [guide](../knowledge/what-is-loop.md) and new `loop-*` gallery screens are **preliminary**. Basalt and the existing typography remain. Neither the detailed layout nor final training copy is approved. Keep repository and Projects private; no runtime changes until review.


### Approval and implementation hold

Simon approved the English guide and six `loop-*` reader mockups in PR #33 at commit `4a4c97170c858c2206b507b0cd9296795b5ccc84` on 13 November 2026. This supersedes the preliminary status above for this material only. Approval covers the audience and tone, practical workout instructions and home alternatives, Settings → What is LOOP? → Get started flow, and amount of optional learning. The phone frames are representative excerpts; the complete guide is the content baseline.

Simon separately authorized final review and merge of PR #33. App code remains on hold pending a separate instruction to proceed. The implementation split is now defined by the [roles and decision-making rules](../AGENTS.md#roles-and-decision-making); no new split agreement is needed. Static approval does not establish Android navigation, TalkBack, physical exercise safety or outdoor readability. Keep the repository and Project private. Licensing and publication remain separate decisions.

## Ghost Run · 13 November 2026

Simon approved a separate feature with routes saved after continuous workouts and racing only against year best or personal best. The race clock continues through all stops and pauses; a pause alone does not disqualify. On 13 November, mockup additions were requested before specification and the feature was added to overnight work. See the Ghost Run section in knowledge/product.md and mockups/index.html#ghost-routes. The 20 new states are preliminary. These product rules are approved; detailed layout is not approved for app UI or integration prototypes. Existing Basalt decisions apply.

## Android launcher icon · 13 November 2026

Simon approved the concrete launcher preview, revision 1, and authorized native integration and a data-preserving phone update on 13 November 2026 (issue #37). The approved mark is a continuous running-track loop tilted 35 degrees, with Basalt background `#10120F` and accent `#C4DA73`. Keep the original 108 × 108 layered geometry and its central safe area; do not add a second foreground padding step. Android controls launcher masks and themed coloring.

Approved SHA-256 identities: foreground `0A8D12E031F7E0366AB1598686371F9AAC57A000FA9FA9388956B60F9AFE79F6`; background `86A2D9A7AFD7C53EA0F59FA53B0C144ED40543A0560FAA8588CF0A85F03295ED`; preview `C6CEB661833BF044A437DDF5447808C3AA242C90348CCE010E2629A81A802755`. The production sources are `src/LoopCx/Resources/AppIcon/appicon.svg` and `appiconfg.svg`. Verify generated Android resources and the installed icon against the approved preview. Themed-launcher behavior is not established by standard icon verification.

Approval is limited to the app icon. It does not approve a wider UI redesign, Ghost detailed layouts or the held What is LOOP? app implementation. Preserve package identity, signing compatibility and existing run data; do not uninstall to refresh launcher artwork.
