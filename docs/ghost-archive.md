# Ghost Run: SQLite-arkivets datamodell
Krav: [Ghost Run](../knowledge/product.md#ghost-run), Spara och välja rutt,
Motståndare och årsurval samt Giltighet och osäkerhet. Del av #18, ovanpå #23.

`storage/ghost/schema-1.sql` definierar ett separat namnområde i SQLite:
- `ghost_routes`: fryst version, källpass, namn, beräkningsrevision, geometri och kvalitetspolicy.
- `ghost_attempts`: stabilt försöks-/käll-ID, exakt rutt-/regelversion, lokal start med tidszon och sparat år, UTC-start i .NET-ticks, förfluten tid, kvalitet/orsak samt rå- och uppspelningspayload.
- `ghost_race_starts`: fryst ruttversion, kalenderår, läge, referens och jämförelseprofil. En första tid utan spöke har null i samtliga referensfält.
- `ghost_schema`: arkivets revisionsmarkör. Appens `PRAGMA user_version` ändras inte av denna fil.

## Skrivkontrakt
En källinspelning får förekomma högst en gång per ruttversion. Referensens ID,
ruttversion, beräkningsrevision, rekordtid och Verified-status måste stämma med
samma arkiverade försök. Samma avstånd eller ett rekord från annan version
räcker inte. Icke verifierade försök behöver en konkret orsak.
Arkiverade rader och tävlingsreferenser kan inte uppdateras. Ny snabbare tid
skapar en ny kandidat för nästa start; den ersätter aldrig pågående referens.

`GhostReferences` i C# äger fortsatt rankningen av PB/årsbästa. Ingen andra
produktionsimplementation av rekordurvalet läggs i SQL. Den sparade lokala
startens år kontrolleras mot lokalt datum, aldrig mot senare systemtidszon
eller UTC-år. Ticks betyder 100 ns enligt .NET:s UTC-/TimeSpan-kontrakt.

Alla databasanslutningar måste slå på `PRAGMA foreign_keys = ON`.
Använd INSERT, aldrig INSERT OR REPLACE, för arkivrader. Vid omförsök ska det
framtida kommandolagret läsa befintlig identitet: identiskt underlag returnerar
samma resultat, motstridigt underlag avvisas. Schemaunikheten hindrar en
dubblett men ersätter inte den ännu oskrivna applikationshanteringen.

Rutt, källförsök och slutregistrering ska skrivas i en explicit transaktion.
Rekordurval får läsa det nya resultatet först efter lyckad commit.
Fryst referens och arkiverad evidens skyddas med främmande nycklar och
uppdateringstriggers. Rader som en historisk tävlingsreferens använder kan
inte tas bort utan en separat, avsiktlig hantering av den historiken.

JSON-payload lagras som UTF-8-text utan beroende på SQLite:s JSON-tillägg.
Det framtida skrivlagret måste validera typade data och JSON före INSERT,
använda kanoniska stabila ID:n och bevara rådata. Databasens Verified-etikett
är inte i sig ett bevis på full rutt eller ett fysiskt godkänt filter.

## Faktiskt verifierat
Kör `python3 tests/GhostStorageChecks/check.py`.
Provet använder den exakta DDL-filen i en temporär SQLite-fil. Det kontrollerar
version/profil, källunikhet, lokal årssnapshot, ofullständiga referenser,
icke verifierad motståndare, frysta rader och referens efter nytt snabbare
resultat. Fel under sluttransaktionen rullar tillbaka både rutt och försök.
Stängning/återöppning bevarar rå-/uppspelningspayload, Unicode och referens.
foreign_key_check och integrity_check ska vara gröna.

## Kvarstående integration
Detta är en verifierad datamodell med riktiga lagringsprov, inte appens färdiga
ruttarkiv. Ingen produktionsanslutning, migration eller Android-bindning kör
filen ännu. Framtida native-migration ska köras atomiskt och behandla kompletta
DDL-satser inklusive triggerkroppar; naiv uppdelning på semikolon är fel.
Appens sessionsdatabas, omförsökskommando, tjänstens GPS under paus och
rekordfinalisering behöver kopplas senare enligt godkännandereglerna.
Androids SQLite-version och fysisk Pixel/WHOOP/Spotify är ej provade här.
Ingen UI/integrationsprototyp eller hälsoexport är aktiverad.
