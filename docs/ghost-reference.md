# Ghost Run: fristående referensregler
Krav: [Ghost Run](../knowledge/product.md#ghost-run), issue #18. Beroende krav/design: PR #19.

Den här leveransen innehåller en fryst datamodell för ruttversion och försök,
JSON-rundtur, deterministiskt rekordurval och uppspelning av redan härledd
ruttposition. Ingen Android-tjänst, GPS-, kart- eller UI-integration ingår.

`GhostRoute` har stabilt ID, versions-ID, källpass, namn, beräkningsrevision och
ordnade koordinater med ruttposition i meter. Ordningen bestämmer start, mål
och riktning; geometrin kopieras och kan inte ändras via källsamlingen.
`GhostAttempt` bevarar eget ID, källpass, ruttversion, sparad lokal start med
UTC-offset och tidszonsnamn, förfluten tid, verifieringsstatus och mätserie.
Modellerna kan serialiseras och läsas tillbaka med System.Text.Json utan
att ändra lokal start eller ruttordning. Detta är ett lagringskontrakt;
SQLite-tabeller och extraktion av endast kontinuerlig huvudfas återstår.

`GhostReferences.Select` tar ett explicit startår som anroparen ska frysa från
lokal tid vid tävlingsstart. Endast verifierade försök med samma rutt- och
beräkningsversion, rätt första/sista position och full förfluten serie är
valbara. Årsbästa använder försökets redan sparade lokala år. PB använder
alla år. Lika tider avgörs med tidigaste UTC-start och därefter stabilt ID.
Exakt samma inspelning är idempotent; motstridiga kopior av ett källpass avvisas.
Urvalet är en fryst referens. Saknat årsbästa ersätts inte automatiskt med PB.

`GhostPlayback` använder förfluten tävlingstid, aldrig aktiv träningstid.
Den spelar upp registrerade tempovariationer, stopp och rörelse bakåt.
Första verifierade ankomst används för sekunder före; meter före beräknas
separat vid samma tävlingstid. Spöket stannar vid sin registrerade måltid.
Interpolation kräver giltiga ändpunkter inom en explicit angiven luckgräns.
Okända intervall och tid före första mätningen extrapoleras inte. En tidigare
lucka som kan dölja första ankomsten ger okänd tidsskillnad. Jämförelsen döljs
om löparens eller spökets position är okänd.

Värdena i proven är syntetiska och luckgränserna enbart testparametrar.
Inga universella GPS-toleranser eller fysiskt verifierade filter införs här.

## Verifiering
Kör `dotnet run --project tests/GhostChecks` med SDK från global.json.
Actions `Ghost Run rules` kör samma prov. Proven täcker 27:30 årsbästa och
26:40 PB, årsskifte över UTC-gräns, stabila lika tider, saknade rekord,
dubbletter, oförändrad referens, JSON, inspelat stopp, bakåtrörelse,
första ankomst, 12 sekunders försprång vid 2 km, spöke efter mål och GPS-luckor.

## Nästa avgränsade steg
Ruttlagring i SQLite och extraktion från kontinuerlig huvudfas; därefter
sekventiell ruttmatchning med korsning, parallella stråk, vändpunkt, slinga
och genväg som negativa prov. Modellen litar än så länge på ett explicit
verifieringsutlåtande från en framtida matchare: den är inte ett bevis på
fullbordad fysisk rutt. Tjänstens tävlings-GPS under träningspaus, återhämtning
efter processdöd och fysiska Pixel/WHOOP/Spotify-prov återstår. Detaljlayout
måste godkännas före app-UI/integrationsprototyp. Ingen hälsoexport aktiveras.
