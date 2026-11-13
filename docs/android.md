# Kör första Android-bygget på Windows

Installera .NET SDK 10.0.100 (x64), JDK 21 och Android Studio. Sätt JAVA_HOME till din JDK och ANDROID_HOME till din Android SDK. Installera Android SDK Platform 36, Build Tools 36.0.0 och Platform Tools i SDK Manager. Lägg platform-tools på PATH. Skapa en Pixel-emulator med Google APIs API 35 och x86_64 i Device Manager, och starta den.

Kör i PowerShell från repots rot med emulatorn som enda anslutna enhet.

```powershell
adb devices
dotnet workload install maui-android
dotnet build src/LoopCx/LoopCx.csproj -t:Run -c Debug -p:RuntimeIdentifier=android-x64
```

Startsidan ska visa **Start a run**. Det verifierar kedjan från Android-start till renderad Razor-komponent. GitHub Actions bygger en debug-signerad APK för x64 och testar samma rubrik via Androids tillgänglighetsträd. APK:n finns i körningens artefakter.

Den här första kontrollen omfattar appstart. GPS under låst skärm, WHOOP via BLE, ljud och Health Connect följs upp i issue #1. Windows-kommandona behöver också provköras på Windows.

## Sessionsjournal och återhämtning
Tjänsten sparar en SQLite-kontrollpunkt per publicerat tillstånd: stabilt ID, start i UTC,
senaste observation, förfluten monoton tid, ursprunglig plan, aktuell plan (inklusive
förlängningar), slutförda faser och aktiva räknare. GPS-observationer sparas med
UTC, monoton tidsstämpel och noggrannhet. Detta är rådata, inte beräknad distans.

Vid första öppning efter processdöd märks kvarvarande registrering som `interrupted`.
Ingen återstående tid eller väg fylls i och passet fortsätter inte automatiskt.
Den registrerade delen finns kvar för kommande historikvyer; inget återhämtnings-UI ingår ännu.
Avslut och en unik exportköpost skrivs i samma transaktion. Någon exportör finns ännu inte.

Debug använder `runs-debug.db`, release `runs.db`. Debug-köposter är alltid
`disabled_synthetic`; ingen Health Connect-export är implementerad eller aktiverad.
SQLite-provet i `scripts/android-smoke.sh` dödar processen med force-stop, kopierar
databasen via run-as och jämför kontrollpunkten efter omstart byte för byte.
Det kontrollerar även bevarade GPS-rader och atomiskt avslut med avstängd export.
Debug-kommandot `complete` accelererar passet till slut och finns inte i release.

Kvarstår: historik/återhämtningsvy, frysta pulszoner, pulsdata, favoriter, sensorkvalitet,
lagringsfel i användargränssnittet och Health Connect-omförsök. Fysiska Pixel/WHOOP-prov
och Windows-prov är ej körda.

## Tidsstämplade manuella ändringar (schema 2)
Databasversion 2 lägger till `run_events` utan att skriva om tidigare mätningar.
Paus, fortsättning, +1 min och manuell fasövergång sparas i samma transaktion
som kontrollpunkten efter kommandot. Händelsen innehåller UTC, monoton förfluten
tid, aktiv tid, fas och plan. Upprepade paus-/fortsätt-kommandon och fasåtgärder
som inte gäller i aktuell fas skapar inga falska händelser. Två tryck på +1 min
är däremot två giltiga ändringar. Start och avslut loggas också.

Efter processdöd bevaras även händelserna exakt. För gamla schema 1-pass finns
ingen full pauslogg; historiken får inte fylla i den i efterhand.
Pausstatus och tidsstämplar i händelserna gör det möjligt att skilja förfluten
paustid från aktiv tid, även om väggklockan justeras under rundan.

`scripts/android-smoke.sh` ersätter endast emulatorns **debugdatabas** med en
syntetisk schema 1-fixtur före provet. Kör det bara i en dedikerad testemulator.
Provet verifierar migrering utan dataförlust, effektiv kommandoföljd, dubbletter,
fasändringar och oförändrad logg efter processdöd. Releasedatabasen berörs inte.
