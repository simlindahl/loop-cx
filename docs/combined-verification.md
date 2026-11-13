# Samlad verifiering och granskningsordning
Krav: [produktspecifikationen](../knowledge/product.md), issue #1 och #18.
Detta dokument sammanför verifieringsunderlag; produktregler och designbeslut ändras inte.

## Kandidater
| Spår | Granskningsordning | Vad som finns |
|---|---|---|
| Android/session | #8 → #9 → #14 → #17 | Tidsstyrd passmotor, tjänst/GPS, löpande SQLite-kontrollpunkt, återhämtning och kommandologg |
| Ghost Run | #19 → #20 → #21 → #22 → #23 → #24 | Krav/mockuper, frysta referenser, huvudfasunderlag, tävlingsklocka, ordnad matchning och SQLite-datamodell |
| Samlad kontroll | Denna PR efter #24 | Tillfällig källkodssammansättning och gemensamma bygg-/acceptansprov |

Granskningsordningen är inte ett mergegodkännande. Ingen av dessa PR:ar får
mergas utan Simon. Main innehåller ännu inte de öppna kandidatändringarna.

## Vad kontrollen gör
[Combined candidate verification](../.github/workflows/combined.yml) checkar ut
Ghost-kandidaten och hämtar full Git-historik. Den förenar Android-kandidaten
`d70fe80d431c1762f8e23d260d4a7e73e86a38c2` i en **tillfällig arbetskopia** med
`git merge --no-commit --no-ff`. Den skapar ingen mergecommit och pushar inget.
Den fasta revisionen gör resultatet reproducerbart; nya ändringar i Android-
spåret måste få en uttrycklig uppdaterad kontroll.

Båda spåren har ändrat `scripts/android-smoke.sh`. Den enda automatiskt tillåtna
konfliktlösningen behåller Android-kandidatens starkare tjänst-/GPS-/SQLite-prov.
Andra konflikter stoppar jobbet. Vid senare godkänd merge ska samma scenarier
bevaras; den enklare startkontrollen från Ghost-spåret får inte ersätta dem.

Jobbet kör:
1. `dotnet run --project tests/SessionChecks`.
2. `dotnet run --project tests/GhostChecks`.
3. `python3 tests/GhostStorageChecks/check.py`.
4. Android-bygge av den sammansatta källkoden.
5. API 35-emulator: start/dubblettstart, släckt skärm med tid/GPS, manuell paus,
   fortsättning, återöppnad Razor-vy, processdöd och exakt bevarat SQLite-underlag.

Artefakten `combined-candidate-evidence` innehåller källrevisioner,
sammansättningsstatistik och, när bygget lyckats, en debug-APK för x64-emulator.
APK:n är inte en verifierad Pixel-leverans. Faktiskt resultat och körningslänk
läggs i PR-beskrivningen efter avslutat jobb.

## Vad kontrollen inte gör
Den kopplar inte in Ghost i Android-tjänsten, GPS, karta, avisering eller UI.
Ghost-reglerna kompileras i samma app men provas som fristående regler.
SQLite-schemat provas på host; appen använder inte Ghost-arkivet ännu.
Ghosts GPS under träningspaus är således inte verifierad av vanliga GPS-provet.

## Kvarstående leverans
- Favoriter, C2/fler passmål, distans-/autopausregler och färdig träningsvy.
- BLE/WHOOP, ljud/coaching, zonstatistik, karta, JSON-export och Health Connect-omförsök.
- Ghost-arkivets Android-läs-/skrivkod, frysta startreferenser i tjänsten,
  fortsatt tävlings-GPS under träningspaus och atomisk slutregistrering.
- Visuellt godkännande av nya detaljlayouter före ny UI/integrationsprototyp.
- Fysisk Pixel/WHOOP/Spotify, aviseringsknappar, Windows och energimätning:
  **ej körda**.

Syntetisk hälsoexport är avstängd. Issues #1, #2, #3, #4 och #18 förblir öppna
med återstående acceptansprov. Projects-verktyg är inte exponerade här.
