# Ghost Run: ruttunderlag från kontinuerlig huvudfas
Krav: [Ghost Run](../knowledge/product.md#ghost-run), avsnitt Spara och välja rutt
samt Giltighet och osäkerhet. Del av issue #18, ovanpå PR #20.

`MainRouteCapture.Build` är en fristående beräkningsregel. Den läser en avslutad
kontinuerlig registrering med fasmarkerade råa GPS-fixar och skapar fryst
ruttgeometri samt uppspelningsunderlag från enbart huvudfasen. Den läser
ingen Android-sensor, databas, karta eller vy.

Indata innehåller monotont förfluten tid, UTC, koordinater, noggrannhet, fas
och uttrycklig luckmarkering. Endast ett sammanhängande huvudblock får användas.
Intervaller och oavslutade pass avvisas. Uppvärmning och nedvarvning lämnas utanför
geometrin och får inte tillföra en linje mellan faserna. Ingen position vid
en saknad fasgräns interpoleras fram.

Kvalitetspolicyn är explicit och versionsmärkt: maximal lucka, maximal
noggrannhetsradie och maximal observerad förflyttningshastighet. Inga
produktionsvärden är förvalda. Orsaker som GPS-lucka, saknad noggrannhet,
orimligt hopp och fel tidsordning returneras som separata resultat med
engelsk förklaring. En uttrycklig lucka även vid första huvudpunkten avvisas.

Huvudpunktens råa koordinat och UTC bevaras; tidsordning och varaktighet kommer
från monoton tid. Ruttposition är summerat sfäriskt avstånd med jordradie
6 371 008,8 m (beräkningsrevisionen följer policyn). Detta är en härledning,
inte uppmätt exakt markdistans. Den valda beräkningsregeln och GPS-kvaliteten
måste följa med vid framtida lagring och jämförelse.

Identiska stillastående fixar bevaras med tidsstämplar i uppspelningen men
ger inga nollånga geometrisegment. Slingor och ut-och-tillbaka bevarar ordningen,
även när samma koordinat uppträder igen vid en senare ruttposition.
Källpassets rådata ändras aldrig och källsamlingen kopieras.

## Avgränsning
Resultatet är **ruttunderlag**, inte ett verifierat tävlingsresultat. Det finns
inget automatiskt förstarekord här. Observerad tid börjar vid första uppmätta
huvudfixen och slutar vid sista; den bevisar inte en saknad fasgräns eller när
en framtida matchare ska registrera första giltiga målpassage. Efterföljande
stillastående vid målet bevaras som källdata och får inte automatiskt räknas
som tävlingsresultat. SQLite-sparande, konfliktkontroll av versions-ID och
faktisk matchning mot hela rutten återstår.

## Verifiering
`dotnet run --project tests/GhostChecks` kör även `RouteCaptureChecks`.
Syntetiska prov täcker huvudfas utan in-/utjogg, start/mål/riktning, kopierat
råunderlag, stopp, sfäriskt avstånd, vändrutt, GPS-kvalitet, exakta policygränser,
luckor, hopp, fel ordning och ändrad UTC-klocka. Alla toleranser är testparametrar.
Fysiska Pixel/WHOOP/Spotify-prov är ej körda. Ingen hälsoexport eller ny UI/integration
aktiveras.
