# LOOP Cx · arbetslista

Status på `main` är aktuell. [Arbetsregler](AGENTS.md#arbetslista-och-status) · [Issues](https://github.com/simlindahl/loop-cx/issues) · [Pull requests](https://github.com/simlindahl/loop-cx/pulls)

Varje rad avser ett avgränsat moment. En feature kan ha återstående implementation i Todo och en del-PR i Waiting. Ingen av de öppna del-PR:arna nedan är mergad; gröna tester innebär inte färdig feature.

## Todo

| Ärende | Arbete | PR | Nästa steg |
|---|---|---|---|
| [#1](https://github.com/simlindahl/loop-cx/issues/1) | Återstående Android-stöd och telefonleverans | [#9](https://github.com/simlindahl/loop-cx/pull/9) | Agenten: förbered återstående BLE/ljud/aviseringsstöd och installerbar telefonversion; fysiska prov enligt issuen återstår. |
| [#2](https://github.com/simlindahl/loop-cx/issues/2) | Komplettera passmotorns acceptans | [#8](https://github.com/simlindahl/loop-cx/pull/8) | Agenten: implementera och verifiera återstående distansstyrning och autopaus. |
| [#3](https://github.com/simlindahl/loop-cx/issues/3) | Favoritstart och träningsvy | — | Agenten: läs aktuella mockuper och designbeslut; avgränsa första UI-snittet. |
| [#4](https://github.com/simlindahl/loop-cx/issues/4) | Historik, JSON och Health Connect-export | [#14](https://github.com/simlindahl/loop-cx/pull/14), [#17](https://github.com/simlindahl/loop-cx/pull/17) | Agenten: komplettera återstående lagring/export; följ issuen för kvarvarande acceptans. |
| [#18](https://github.com/simlindahl/loop-cx/issues/18) | Återstående Ghost Run-implementation | [#25](https://github.com/simlindahl/loop-cx/pull/25) | Agenten: förbered native arkivskrivare/migration och kvarvarande regler. App-UI/integrationsprototyp kräver godkänd detaljlayout i #19. |

## Doing

| Ärende | Arbete | PR | Nästa steg |
|---|---|---|---|
| BOARD-1 | Införa och verifiera repohanterad tavla | — | Agenten: uppdatera README, instruktioner och projektskills; verifiera på main. |

## Waiting

| Ärende | Arbete | PR | Nästa steg |
|---|---|---|---|
| [PR #8](https://github.com/simlindahl/loop-cx/pull/8) | Lägg till deterministisk passmotor för tidsstyrda intervaller | [#8](https://github.com/simlindahl/loop-cx/pull/8) | Simon: granska diff och verifieringsresultat; godkänn merge. |
| [PR #9](https://github.com/simlindahl/loop-cx/pull/9) | Koppla tidsstyrd session till Android foreground service och GPS | [#9](https://github.com/simlindahl/loop-cx/pull/9) | Simon: granska diff och verifieringsresultat; godkänn merge. |
| [PR #14](https://github.com/simlindahl/loop-cx/pull/14) | Bevara registrerade pass i SQLite efter processdöd | [#14](https://github.com/simlindahl/loop-cx/pull/14) | Simon: granska diff och verifieringsresultat; godkänn merge. |
| [PR #17](https://github.com/simlindahl/loop-cx/pull/17) | Spara paus och manuella fasändringar med tidsstämplar | [#17](https://github.com/simlindahl/loop-cx/pull/17) | Simon: granska diff och verifieringsresultat; godkänn merge. |
| [PR #19](https://github.com/simlindahl/loop-cx/pull/19) | Ghost Run: egna rutter, årsbästa och personbästa | [#19](https://github.com/simlindahl/loop-cx/pull/19) | Simon: granska Ghost Run-detaljlayout och godkänn krav/design före app-UI. |
| [PR #20](https://github.com/simlindahl/loop-cx/pull/20) | Lägg till fristående Ghost Run-regler för rekord och uppspelning | [#20](https://github.com/simlindahl/loop-cx/pull/20) | Simon: granska diff och verifieringsresultat; godkänn merge. |
| [PR #21](https://github.com/simlindahl/loop-cx/pull/21) | Skapa Ghost-ruttunderlag från enbart kontinuerlig huvudfas | [#21](https://github.com/simlindahl/loop-cx/pull/21) | Simon: granska diff och verifieringsresultat; godkänn merge. |
| [PR #22](https://github.com/simlindahl/loop-cx/pull/22) | Behåll Ghost Runs tävlingstid genom träningspaus | [#22](https://github.com/simlindahl/loop-cx/pull/22) | Simon: granska diff och verifieringsresultat; godkänn merge. |
| [PR #23](https://github.com/simlindahl/loop-cx/pull/23) | Verifiera Ghost-ruttens segment i ordning före mål | [#23](https://github.com/simlindahl/loop-cx/pull/23) | Simon: granska diff och verifieringsresultat; godkänn merge. |
| [PR #24](https://github.com/simlindahl/loop-cx/pull/24) | Verifiera Ghost-arkivets SQLite-modell och frysta referenser | [#24](https://github.com/simlindahl/loop-cx/pull/24) | Simon: granska diff och verifieringsresultat; godkänn merge. |
| [PR #25](https://github.com/simlindahl/loop-cx/pull/25) | Verifiera Android- och Ghost-kandidater i samma bygge | [#25](https://github.com/simlindahl/loop-cx/pull/25) | Simon: granska diff och verifieringsresultat; godkänn merge. |

Granskningsordning: Android #8 → #9 → #14 → #17. Ghost #19 → #20 → #21 → #22 → #23 → #24 → #25. Samlad evidens och kvarvarande acceptans finns i [#25](https://github.com/simlindahl/loop-cx/pull/25) och feature-issues. Godkänd del-PR avslutar inte en hel feature med återstående acceptans.

## Done

| Ärende | Arbete | PR | Nästa steg |
|---|---|---|---|
