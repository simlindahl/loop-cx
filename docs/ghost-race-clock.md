# Ghost Run: fristående tävlingsklocka
Krav: [Ghost Run](../knowledge/product.md#ghost-run), Start, tävlingsklocka och
paus samt Uppspelning och jämförelse. Del av #18, ovanpå PR #21.

`GhostRaceClock` tar explicita absoluta monotona tidsstämplar. Den anropar
ingen systemklocka, sensor, Android-tjänst eller UI-timer. Anroparen ska vid
senare integration mata den från samma tjänsteägda session som styr träningen.

Tävlingsklockan räknar all observerad förfluten tid från huvudstart. Manuell
träningspaus bevaras tills aktiv fortsättning, utan att frysa tävlingstid
eller uppspelning. `TrainingActive` är en härledd kontrollsumma från samma
paushändelser, inte en separat träningsmotor. Den omfattar den pågående
huvudfasen; fas-/sensorregler ägs fortsatt av sessionsmotorn.
Upprepade paus-/fortsätt-kommandon skapar inte dubbla pausintervall.
En uttagen kopia av pausunderlaget är fryst.

En framtida matchare anropar `CompleteAt` med tidsstämpeln för den verifierade
målpassagen. Detta är tillåtet även under träningspaus. Tidsstämpeln får ligga
före senaste timerobservation när GPS-beskedet är fördröjt. Aktiv tid och
pausstatus klipps då historiskt till den faktiska måltiden. Senare timer-,
knapp- och målbesked får inte ändra slutresultatet.

`EndAttempt` fryser ett ofullständigt försök. `InterruptAtLastObservation`
fryser endast den kända delen med avbrottsstatus; ingen tid efter senaste
observation läggs till. Detta modellerar avbrottsregeln men implementerar
inte upptäckt av processdöd, återinläsning eller SQLite-persistens.

Klockans Finished-status certifierar inte att rutten genomförts. Den framtida
matcharen ansvarar för målbeviset, och arkivet för atomisk lagring och
rekorduppdatering. Ingen verklig tävlings-GPS under paus är kopplad ännu.

## Verifiering
Kör `dotnet run --project tests/GhostChecks`.
Syntetiska prov täcker 25 min träning + 2 min paus = 27 min tävlingstid,
mål under paus, idempotenta kommandon, manuell fortsättning, fryst pausunderlag,
bakåtgående tid, fördröjt GPS-mål före/under paus, sena besked efter mål,
ofullständigt/avbrutet försök och uppspelning från tävlingstid under träningspaus.

Ingen ny app-UI eller integrationsprototyp ingår. Fysisk Pixel/WHOOP/Spotify,
tjänstens GPS under paus och energimätning är ej körda. Hälsoexport är avstängd.
