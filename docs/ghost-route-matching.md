# Ghost Run: ordnad ruttmatchning
Krav: [Ghost Run](../knowledge/product.md#ghost-run), Uppspelning och jämförelse
samt Giltighet och osäkerhet. Del av #18, ovanpå #22.

`OrderedRouteMatcher` är en fristående C#-regel för syntetisk verifiering och
kommande fältprov. Den läser samma frysta ruttversion som referensen och
kräver en explicit versionsmärkt policy. Ingen Android-sensor, karta, vy eller
SQLite-koppling ingår.

## Beviskedja
Start är ett explicit anrop med säker position nära rätt start; vanliga
observationer startar inte en tävling. Startens observerade monotona tidsstämpel
fryses. Anropande tjänst måste senare samordna denna med användarens
tävlingsstart och GPS-färskhet; ingen tjänstekoppling finns här.

Matcharen håller en sammanhängande täckt ruttprefix. Endast det förväntade
segmentet kan ge aktuell ruttposition. En mätförflyttning får passera flera
ruttpunkter bara när de ligger i rätt ordning längs den korta observerade
förflyttningen, inom grindtoleransen och i inspelad riktning. Slutpunkten ska
fortfarande ligga i rätt ruttkorridor. Närmaste punkt på hela rutten används
aldrig för att flytta fram segmentindex.

Lucka, osäker GPS, orimlig hastighet/steglängd och ruttavvikelse döljer aktuell
jämförelse utan att flytta det täckta prefixet. Återinträde längre fram bevisar
inte en överhoppad del. Efter en stor lucka kan återkomst nära senaste täckta
ankare återställa jämförelsen; resten måste faktiskt traverseras i ordning.
En mindre lucka kan accepteras mellan giltiga ändpunkter inom den explicit
valda policyn. En uttryckligt markerad lucka bryggas inte till ny täckning.

Mål kräver alla grindar i rätt ordning. En tidig passage vid samma fysiska
koordinat i slinga/ut-och-tillbaka är därför inget mål. En tät, giltig passage
mellan fixar kan härleda målstämpeln genom projektion längs mätförflyttningen.
Måltid och sluttillstånd fryses. Ett syntetiskt prov använder denna stämpel
med `GhostRaceClock.CompleteAt` under träningspaus.

## Beräkningsgränser
Koordinater projiceras till ett lokalt ekvirektangulärt plan med första
ruttpunkten som origo och jordradie 6 371 008,8 m. Longituddifferens normaliseras
över datumgränsen. Rutter utanför explicit projektionsutsträckning eller med
origo vid minst 85 graders latitud avvisas; nollånga geometrisegment avvisas.
Framsteg interpoleras från ruttversionens lagrade meterpositioner. Uppmätt
löpdistans och denna ruttposition är olika värden.

Policyn anger korridor, grindradie, maximal noggrannhetsradie, lucka, hastighet,
mätsteg och projektionsutsträckning. Toleranser använder även fixens
noggrannhetsradie. Alla värden i proven är syntetiska, inte förvalda
produktionsfilter eller fysiskt godkända gränser.

Denna första regel är konservativ. Längre återgång över redan passerade
segment och återinträde långt från sista täckta ankare kan förbli okända.
Nära parallella stråk inom samma GPS-osäkerhet kräver fortsatt kvalitets- och
fältanalys. Lokalt linjära antaganden och interpolerad målpassage måste
fältprovas innan verkliga rekord tilldelas. Finished betyder hela kedjan
under den angivna policyn, inte en fristående fysisk certifiering. Ingen
`GhostAttempt` med Verified-status skapas automatiskt.

## Verifiering
`dotnet run --project tests/GhostChecks` kör även `RouteMatchChecks`.
Proven täcker fast start, dubblettstart/fix, ordnade segment, diagonal genväg,
fel riktning, tidig målpassage i slinga, vändpunkt, parallella stråk, figuråtta,
GPS-lucka med återinträde vid mål, återgång till täckt ankare, osäker GPS,
orimligt hopp, fel tidsordning, projektionsgräns och fryst mål. Ett tätt
syntetiskt mål mellan fixar avslutar tävlingen under träningspaus med rätt tid.

SQLite-ruttarkiv, faktisk GPS under paus, systemägda aviseringar, godkänd UI och
fysiska Pixel/WHOOP/Spotify-prov återstår. Hälsoexport är fortsatt avstängd.
