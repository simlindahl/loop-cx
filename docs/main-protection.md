# Skydda main

## Önskat skydd

[Ruleset-konfigurationen](../.github/main-ruleset.json) definierar `Protect main` för `main`: PR-krav med 0 approvals, lösta review-diskussioner, linjär historik och obligatoriska GitHub Actions-checks `specification`, `emulator` och `mockups`. Branchen måste vara aktuell mot main. Radering och force-push blockeras; bypass-listan är tom. Använd squash merge. Restrict updates, signerade commits och merge queue krävs inte.

Noll approvals gör det möjligt att merga egna PR:ar. Det ersätter inte [repoets godkännanderegler](../AGENTS.md#arbetslista-och-status). Tavlans status uppdateras direkt i [GitHub Projects](https://github.com/users/simlindahl/projects/2) och berör inte main eller dess CI.

## Aktivera efter verifiering

Konfigurationsfilen är ett önskat läge och aktiverar inget automatiskt. Den 13 november 2026 svarade GitHub API med 403: rulesets i det privata repot kräver Pro. Skyddet är därför inte verifierat aktivt. Behåll repot privat tills ett separat beslut om publicering har fattats efter granskning av innehåll och historik.

Förutsättningar: GitHub CLI med inloggning och administratörsbehörighet, ett abonnemang som stöder rulesets för repot samt de tre checksen tillgängliga från en färsk grön PR. Kör från reporoten. Kontrollera först att inga befintliga rulesets överlappar:

```powershell
gh api repos/simlindahl/loop-cx/rulesets
```

Efter att workflow- och tavelflödesändringen har mergats och lästs tillbaka, skapa rulesetet en gång:

```powershell
gh api --method POST repos/simlindahl/loop-cx/rulesets --input .github/main-ruleset.json
```

Om rulesetet redan finns, läs det och uppdatera dess verifierade ID med PUT i stället för att skapa en dubblett. Läs tillbaka rulesetet med ID:t från API-resultatet och kontrollera samtliga regler, checks, appkälla och tom bypass-lista. Rapportera inte skyddet som aktivt innan återläsningen bekräftar det.

Tillåt squash merge i repots merge-inställningar. Linjär historik hindrar merge commits; rebase är också förenligt med den regeln, men squash är arbetssättet här.

## Verifiera PR-flödet

Alla tre workflows startar på varje PR utan path-filter. Android bygger och kör emulatorprovet; mockup-kontrollen verifierar layout och laddar upp skärmbilder. En grön check betyder att kontrollen körts, även på rena dokumentationsändringar. Manuella körningar finns kvar.

Verifiera Projects-flödet med ett syntetiskt kort enligt [tavlans testinstruktion](../AGENTS.md#arbetslista-och-status). Statusbyten i Projects utlöser ingen CI och behöver ingen testbranch eller PR. Repoändringarnas CI och rulesetets aktivering verifieras separat.

## Referenser

[GitHub: tillgängliga ruleset-regler](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets) · [GitHub: obligatoriska checks och överhoppade workflows](https://docs.github.com/en/pull-requests/how-tos/merge-and-close-pull-requests/troubleshooting-required-status-checks)
