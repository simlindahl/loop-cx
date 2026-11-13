# LOOP Cx · statiska mockuper

Öppna `index.html` direkt i en webbläsare. Inget byggsteg, ingen server och inget nät krävs. Från repo-roten på Windows: `Start-Process .\design\mockups\index.html` i PowerShell. GitHubs filvy visar HTML-källan; den kör inte galleriet.

En enda HTML-fil med gemensam inline-CSS, inbäddad Space Grotesk och Googles Material Icons. Det underlättar öppning offline och gör att mått, färgroller, komponentstruktur och tillstånd går att överföra till Blazor. Den stora första CSS-regeln innehåller fontdata; hoppa till `:root` för designtokens och till önskat skärm-ID för markup.

## Vad som gäller

Basalt, engelska, Space Grotesk, Material Icons och revision 5:s inriktning är beslutade. Nya vyer och detaljlayouter är **preliminära** och inväntar Simons granskning. Läs [designbeslut](../decision.md) och [produktspecifikation](../../knowledge/product.md). Mockupbeställningen är inte ett designgodkännande. PDF:erna bevaras som historik.

Galleriets index och bildtexter är granskningsverktyg, inte app-UI. Klick på en åtgärd går till ett statiskt exempel med ankarlänk. Inga data ändras; formulär är skrivskyddade och brytare är bilder av tillstånd. Ingen JavaScript, sensoråtkomst, Android-behörighet, GPS, lagring, uppläsning eller export körs. Tider och mätningar är syntetiska. Låsskärm, aviseringspanel och delningsmeny ägs av Android och kan se annorlunda ut på telefonen. Kartan är avsiktligt en markerad platshållare.

## Använd vid implementation

1. Läs motsvarande krav i produktspecifikationen och välj skärm-ID nedan.
2. Återanvänd `--cx-*`-tokens och HTML-strukturen som utgångspunkt för små Razor-komponenter i rätt vertikalt funktionssnitt. `.review-*`, `.gallery`, `.phone` och `.statusbar` är endast granskningsramar.
3. Flytta gemensam CSS och det inbäddade typsnittet till appens lokala resurser. Koppla beteendet till den gemensamma passmodellen; ankarlänkar ska inte bli appens passlogik.
4. Verifiera i Android-emulatorn och på Pixel enligt acceptansproven. HTML visar avsikt, inte bevis för bakgrundsregistrering eller Android-integration.

## Vyer och kravkoppling

### Setup

| Vy | Skärm-ID | Krav i product.md |
|---|---|---|
| [First launch](index.html#welcome) | `welcome` | Produkt och leverans; Android |
| [Permissions](index.html#permissions) | `permissions` | Integration, lagring och fel – Android |

### Training

| Vy | Skärm-ID | Krav i product.md |
|---|---|---|
| [Training](index.html#training) | `training` | Vyer och utformning; LOOP Core |
| [No personal favourites](index.html#empty-training) | `empty-training` | Vyer och utformning – Starta pass |
| [Manage favourites](index.html#manage) | `manage` | Vyer och utformning – Starta pass |
| [Reordered favourites](index.html#manage-ordered) | `manage-ordered` | Vyer och utformning – ordna favoriter |
| [Delete confirmation](index.html#delete-workout) | `delete-workout` | Vyer och utformning – ta bort favorit |

### Editor

| Vy | Skärm-ID | Krav i product.md |
|---|---|---|
| [Choose workout type](index.html#workout-type) | `workout-type` | Pass och statistik – kontinuerligt/intervall |
| [Interval favourite](index.html#edit-intervals) | `edit-intervals` | Pass och statistik 2–4; Återkoppling |
| [Distance intervals](index.html#edit-distance) | `edit-distance` | Pass och statistik 2 |
| [Continuous favourite](index.html#edit-easy) | `edit-easy` | Pass och statistik 1, 3, 4 |
| [Finish condition](index.html#edit-open) | `edit-open` | Pass och statistik 1; Återkoppling – öppet slut |
| [Intensity target](index.html#edit-target) | `edit-target` | Pass och statistik 3; Återkoppling |
| [One-off workout](index.html#run-once) | `run-once` | Vyer och utformning – enstaka pass |

### Run

| Vy | Skärm-ID | Krav i product.md |
|---|---|---|
| [Warm-up](index.html#warm-up) | `warm-up` | Pass och statistik 4; LOOP Core |
| [Warm-up extended](index.html#warm-up-extended) | `warm-up-extended` | Pass och statistik 4 |
| [Work interval](index.html#work) | `work` | Vyer och utformning – Träningsvy; Pass och statistik 2 |
| [Timed recovery](index.html#recovery) | `recovery` | Pass och statistik 2, 6 |
| [Distance recovery](index.html#distance-recovery) | `distance-recovery` | Pass och statistik 2, 6 |
| [Zone 2 run](index.html#easy-run) | `easy-run` | LOOP Core; Pass och statistik 1, 3 |
| [Open finish](index.html#open-run) | `open-run` | Pass och statistik 1; Återkoppling – öppet slut |
| [Open finish, no cool-down](index.html#open-no-cooldown) | `open-no-cooldown` | Återkoppling – öppet slut |
| [Manual pause](index.html#paused) | `paused` | Pass och statistik 6 |
| [Automatic pause](index.html#autopaused) | `autopaused` | Pass och statistik 6 |
| [Cool-down](index.html#cool-down) | `cool-down` | Pass och statistik 4, 5 |
| [Cool-down extended](index.html#cool-down-extended) | `cool-down-extended` | Pass och statistik 4 |

### History

| Vy | Skärm-ID | Krav i product.md |
|---|---|---|
| [Saved locally](index.html#saved) | `saved` | Integration, lagring och fel – Lokal databas/Health Connect |
| [Interrupted recording](index.html#interrupted) | `interrupted` | Integration, lagring och fel – processdöd |
| [Run history](index.html#history) | `history` | Vyer och utformning – Historik |
| [Empty history](index.html#empty-history) | `empty-history` | Vyer och utformning – Historik |
| [Run details](index.html#run-detail) | `run-detail` | Vyer och utformning – Historik; Pass och statistik 5; AI-export |
| [Easy run details](index.html#c2-detail) | `c2-detail` | LOOP Core; Historik; Pass och statistik 1 |
| [Interrupted run detail](index.html#interrupted-detail) | `interrupted-detail` | Integration, lagring och fel – avbrott |
| [Phases and splits](index.html#splits) | `splits` | Pass och statistik 1, 2, 5 |
| [Continuous splits](index.html#continuous-splits) | `continuous-splits` | Pass och statistik 1 |
| [Recorded zones](index.html#zone-history) | `zone-history` | Pass och statistik 5; WHOOP |
| [Recorded route](index.html#route) | `route` | Integration, lagring och fel – Karta |
| [Unavailable map](index.html#route-offline) | `route-offline` | Integration, lagring och fel – Karta; Pass och statistik 7 |
| [Share run data](index.html#share) | `share` | Integration, lagring och fel – AI-export |

### System

| Vy | Skärm-ID | Krav i product.md |
|---|---|---|
| [Share sheet boundary](index.html#share-sheet) | `share-sheet` | AI-export |
| [Lock screen · warm-up](index.html#notification-warm) | `notification-warm` | Återkoppling – mini-UI |
| [Lock screen · work](index.html#notification-work) | `notification-work` | Återkoppling – mini-UI |
| [Lock screen · recovery](index.html#notification-recovery) | `notification-recovery` | Återkoppling – mini-UI |
| [Lock screen · paused](index.html#notification-paused) | `notification-paused` | Återkoppling – mini-UI |
| [Lock screen · cool-down](index.html#notification-cool) | `notification-cool` | Återkoppling – mini-UI |
| [Notification · open finish](index.html#notification-open) | `notification-open` | Återkoppling – mini-UI |

### Settings

| Vy | Skärm-ID | Krav i product.md |
|---|---|---|
| [Settings](index.html#settings) | `settings` | Vyer och utformning – Inställningar |
| [WHOOP connection](index.html#devices) | `devices` | Integration, lagring och fel – WHOOP |
| [Find a band](index.html#pairing) | `pairing` | Integration, lagring och fel – WHOOP |
| [No band found](index.html#pairing-empty) | `pairing-empty` | Integration, lagring och fel – WHOOP/Android |
| [Band disconnected](index.html#devices-disconnected) | `devices-disconnected` | Integration, lagring och fel – WHOOP |
| [Personal heart-rate zones](index.html#zones) | `zones` | Integration, lagring och fel – WHOOP; Pass och statistik 5 |
| [Zone validation](index.html#zone-error) | `zone-error` | WHOOP – ordnade gränser |
| [Audio coaching](index.html#audio) | `audio` | Återkoppling under passet |
| [Distance audio summaries](index.html#audio-distance) | `audio-distance` | Återkoppling; Acceptansprov 2,4 km |
| [Health Connect settings](index.html#health-settings) | `health-settings` | Integration, lagring och fel – Health Connect |

### Export

| Vy | Skärm-ID | Krav i product.md |
|---|---|---|
| [Export pending](index.html#export-pending) | `export-pending` | Health Connect – säkra omförsök |
| [Export needs permission](index.html#export-action) | `export-action` | Health Connect – ändrad behörighet |
| [Export complete](index.html#exported) | `exported` | Health Connect; Health Connect → WHOOP |

### Errors

| Vy | Skärm-ID | Krav i product.md |
|---|---|---|
| [Permission denied](index.html#permission-denied) | `permission-denied` | Integration, lagring och fel – Android |
| [Distance requires GPS](index.html#distance-blocked) | `distance-blocked` | Android – distans nödvändig |
| [Sensor loss in a run](index.html#sensor-loss) | `sensor-loss` | Pass och statistik 7; WHOOP |
| [Distance phase with GPS loss](index.html#distance-wait) | `distance-wait` | Pass och statistik 7 |

## Granskningskontroll

[Static mockup review](../../.github/workflows/mockups.yml) använder Chromium på GitHub Actions. Kontrollen öppnar filen offline, granskar trasiga ankarlänkar, dubbla ID:n, inbäddad font, skript och externa anrop. Den mäter horisontellt överflöde vid 360, 390, 700, 1024 och 1440 px, följer Start C1 till uppvärmning och sparar representativa skärmbilder samt `report.json` som Actions-artifact `mockup-review`. En approximation av 200 % textskalning rapporteras separat; den ersätter inte Androids systemtextskalning eller TalkBack.

Lokal motsvarighet med Node och Playwright installerat: `node design/mockups/verify.cjs`. `PLAYWRIGHT_MODULE` kan ange en separat installation. Skärmbilder och rapport hamnar i `design/mockups/qa/` och ska inte checkas in. Arbetsmiljön här kunde inte starta lokal Chromium; browserkontrollerna utförs därför i Actions. Registrera faktiskt resultat i PR efter körningen.

## Licenser

Space Grotesk: [SIL Open Font License](FONT-LICENSE.txt), full variabel font från `google/fonts/ofl/spacegrotesk/SpaceGrotesk[wght].ttf`. Material Icons: [Apache 2.0](ICON-LICENSE.txt), samma ikonformer som `design/icons/`. Ändra inte ikonernas form för att skapa ett eget ikonbibliotek.
