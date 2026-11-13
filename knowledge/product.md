---
type: Product Specification
title: LOOP Cx för Android
---
# LOOP Cx för Android · specifikation v1
12 november 2026 · Underlag för implementation och verifiering

## Produkt och leverans
En engelskspråkig löparapp för en löpare som roterar mellan några favoritpass. Från startsidan ska ett sparat upplägg kunna startas med ett tryck efter första konfigurationen. Samma registrering ska fungera med synlig skärm, med Spotify i förgrunden och med telefonen låst. Leverera Android-app, installerbar APK för privat bruk, reproducerbara Windows-instruktioner och verifieringsrapport. Första måltelefon är Pixel med WHOOP-band. Ingen server behövs. Privat APK-distribution är ett föreslaget standardval.

Appens egna vyer, kontroller, tillgänglighetsnamn, fel, aviseringar och röstcoachning använder engelska i v1. Svenska benämningar i denna specifikation beskriver funktioner. Användarens egna passnamn bevaras. Android styr språket i systemägda dialoger.

## LOOP Core
LOOP Cx är den officiella löparappen för C1 och C2. Startsidan visar C1 · Intervaller och C2 · Zon 2 med en startåtgärd vardera, följt av egna favoriter. C1: 10 min progressiv uppvärmning, 5 × 3 min kontrollerat arbete, 2 min lätt rörelse mellan insatserna och 5 min lätt avslut (38 min totalt). C2: lätt löpning i WHOOP-zon 2, normalt 45–60 min; startvärde 45 min. Visa syfte och märk källa, kategori och mallrevision i registreringen. Personliga varianter kan sparas separat. Globala 3/2 min används för egna pass. Stöd för hela 10K-pluginen är ett separat beslut.

## Teknikbeslut
Använd C#, .NET MAUI Blazor Hybrid och en lokal SQLite-databas. PWA ger enklare distribution och webbutveckling, lokal lagring och cachat gränssnitt, men webbens geolokalisering förutsätter ett synligt dokument. Tillförlitlig positionsregistrering i fickan, Bluetooth-puls och Health Connect avgör valet. MAUI kräver Android SDK, emulator, plattformsbehörigheter och underhåll av Android-bindningar. Batterianvändningen ska mätas på telefon. Ingen separat webbvärd behövs.

En Android foreground service som startas när användaren startar passet äger klockan, GPS, puls och fasbyten. Blazor-vyn och aviseringen läser samma tillstånd och skickar kommandon till samma pass. Återöppning, omritning eller dubbla knapptryck får inte skapa två registreringar. Håll passregler och statistik i vanlig C# och Android-anrop vid systemgränsen. Använd en monoton klocka för varaktighet och tidsstämpla även i UTC för export.

### Kodorganisation

Bygg en modulär monolit bakom Blazor: en installerbar app med tydliga funktionsgränser i C#. Organisera koden i vertikala snitt (Vertical Slices) efter vad löparen gör. Håll Razor-komponenter, indata, validering och den lagringskod som hör till ett användningsfall nära varandra. Mappar och namnrymder räcker som utgångspunkt.

Exempel: ändrad validering av mål-RPE för en favorit hör hemma i snittet för att redigera favoritpass. Regler som även gäller vid passstart ägs av passmodellen och återanvänds därifrån. Den pågående sessionen äger fasbyten och pausregler; både Blazor och låsskärmskontrollerna anropar samma beteende. Android-beroenden för GPS och Bluetooth stannar vid plattformsgränsen så att passmotorn kan verifieras med simulerade mätningar.

Välj den kortaste tydliga vägen från UI till beteende och data. Lägg till ett gränssnitt, lager eller separat projekt först när det löser ett konkret behov, exempelvis att byta verklig sensordata mot en deterministisk testkälla. Clean Architectures lagerindelning är inget krav. Vid granskning ska det gå att peka ut var en regel ägs och varför varje mellanled behövs.

## Vyer och utformning
- **Starta pass.** Visa ordnade favoriter direkt med namn, protokoll och egen Starta-knapp. Skapa, redigera, ordna och ta bort via en sekundär ingång. Stöd även ett enstaka pass. Tillåt start utan nät. Visa GPS- och pulsstatus utan att kräva en extra bekräftelse när allt är klart.
- **Träningsvy.** Överst fas, mål-RPE, eventuellt tempo- eller zonmål, repetitionsnummer och återstående tid/distans där slutet är bestämt. Två tydligt märkta grupper visar huvudpass respektive aktuell split, vardera med tid, distans, snittempo och snittpuls. Längst ner visas aktuellt tempo och aktuell puls störst. Under uppvärmning visas fasens egna data. Vid huvudstart börjar huvudräknarna på noll. Under nedvarvning är huvudpassets resultat färdigt.
- **Historik.** Nyaste rundan först. Detalj visar huvudpass, total inklusive in- och utjogg, splits, tid i pulszoner, karta över hela GPS-spåret, exportstatus och Dela passdata. Ändring av en favorit eller zongräns får aldrig förändra ett tidigare resultat.
- **Inställningar.** WHOOP-anslutning, manuella zongränser, Health Connect-status och standardtider för uppvärmning/nedvarvning. Ljudinställningarna för en favorit ska följa med när passet upprepas.
- **Beslutad visuell inriktning.** Minimalist Material Design med Space Grotesk och tabellsiffror, neutral bakgrund, hög kontrast och en accent för aktiv fas eller primär handling. Använd Androids textskalning, tydliga enheter och minst 48 dp för tryckytor. Kontrollera läsordning med TalkBack, stor text och avläsning utomhus. Status behöver en textetikett även när färg används.

## Pass och statistik
1. Kontinuerligt pass slutar vid vald aktiv tid, vald distans eller manuellt. Automatisk split per hel kilometer i huvudpasset. Spara även den sista ofullständiga spliten med faktisk distans.
2. Intervallpass använder N arbetsinsatser och N−1 återhämtningar. Arbete och vila har samma typ av längd, antingen tid eller distans, med separata värden. Fasbyten och splits sker automatiskt. Spara varje arbetsfas och återhämtning separat så att arbetsinsatsernas tempo går att jämföra. N=1 ger ingen vila.
3. Obligatoriskt mål-RPE som heltal 1–10 för arbete och för återhämtning när sådan finns. Varje fas kan därutöver ha ett måltempo i min/km eller en pulszon. RPE är avsedd ansträngning. Visa och exportera värdet som ett mål.
4. Uppvärmning och nedvarvning kan slås på oberoende per pass. Standard 3 respektive 2 min, ändras i Inställningar. Föreslagen regel: läs aktuell standard vid start och frys den för pågående runda. Under respektive fas ger varje separat tryck på +1 min ytterligare 60 sekunder. Volym upp har samma funktion när träningsvyn är öppen. Börja huvudpass och Avsluta och spara kan användas när som helst under respektive fas. Behåll redan registrerade data.
5. Hela rundan inkluderar uppvärmning, arbete, vila och nedvarvning. Huvudpass inkluderar arbete och planerad vila. Spara både förfluten och aktiv tid. Snittempo beräknas som aktiv tid genom distans och snittpuls som tidsvägt medel över giltiga pulsmätningar. Redovisa luckor som okänd tid. Ingen division med noll. Beräkna zonstatistik med de gränser som gällde vid start.
6. Autopaus efter fem sekunders tillförlitligt observerat stillastående och automatisk fortsättning vid rörelse. Undanta hela återhämtningsfasen. Vid tidsvila räknas ståvila fullt. Vid distansvila fortsätter tidtagningen medan återstående distans inväntar rörelse. Manuell paus går före automatiken och kräver manuell fortsättning. Frys aktiv tid, registrerad sträcka, fasnedräkning och coaching under paus. Rekommenderad första regel är att pausen börjar vid detektion. Bortfall av GPS är okänd rörelse, inte stillastående.
7. Filtrera orimliga GPS-hopp. Markera avbrott i kartspåret och extrapolera inte tappad distans. Vid distansstyrd fas med GPS-bortfall fortsätter aktiv tid och fasen väntar på mätbar distans. Dokumentera och fältprova gränserna för rörelse, återstart, GPS-kvalitet och utjämning av aktuellt tempo.

## Återkoppling under passet
Löparen väljer uppläsning efter eget positivt tidsintervall (minuter och sekunder) eller distansintervall i meter. Läs huvudpassets fyra värden, aktuell splits fyra värden samt aktuellt tempo och puls på engelska. Uppläsningsfrekvens och splitlängd är oberoende. Tala om fasbyten och mål-RPE. Låt kort fasbesked få företräde framför längre sammanfattningar, och undvik överlappande eller föråldrade besked när musik spelas. Föreslagen standard är att periodiska sammanfattningar räknas från huvudstart med aktiv tid eller huvuddistans.

Ge kort coaching vid mer än 10 procents tempoavvikelse eller puls utanför vald zon i 30 sammanhängande aktiva sekunder inom samma split. Föreslagen tolkning är utjämnat aktuellt tempo i sekunder/km och giltig aktuell puls. Vid 5:00 min/km är gränserna 4:30 och 5:30. Återställ avvikelsetid vid ny split, paus, datalucka eller återgång inom mål. Ge ett besked per sammanhängande avvikelse. RPE utan mätmål ger fasbesked och ordinarie uppläsning.

Löpande Android-avisering på låsskärmen och i aviseringspanelen visar fas, kvarvarande tid/distans, puls och tempo inom systemets tillgängliga utrymme. Uppvärmning: +1 min och Börja huvudpass. Huvudpass: Pausa samt för öppet slut Börja nedvarvning eller Avsluta och spara. Återhämtning: Pausa. Pausat: Fortsätt. Nedvarvning: +1 min och Avsluta och spara. Tryck på aviseringen öppnar aktuell vy. Testa verklig synlighet i hopfällt och expanderat läge med låsskärmsaviseringar tillåtna.

## Integration, lagring och fel
- **WHOOP.** Läs bandets Bluetooth LE-puls med pulssändning aktiverad. Låt användaren ansluta bandet vid första användning och ange sina personliga zoner. Verifiera ordnade gränser utan oavsiktliga glapp/överlapp. Återanslut vid avbrott. Visa streck för saknad puls, fortsätt passet och undertryck pulsbaserad coaching tills nya värden kommer. Realtidsregistrering kräver inget WHOOP-webb-API eller egen molntjänst.
- **Android.** Begär exakt plats och relevanta Bluetooth-behörigheter för sökning/anslutning när funktionerna används. Deklarera de foreground service-typer och behörigheter som den valda Android-versionen kräver för plats och ansluten enhet. Starta tjänsten från synlig app. Begär aviseringstillstånd för mini-UI. Vid nekad åtkomst, visa konsekvensen och en väg till inställningarna. Fortsätt med möjliga funktioner. Ett tidsstyrt pass kan fungera utan GPS; förklara och låt användaren ändra upplägget om distans är nödvändig.
- **Lokal databas.** Spara favoriter, inställningar, passets frysta plan och zoner, fasgränser, råa sensorvärden, pauser, kvalitetsmarkeringar och exportstatus. Skriv löpande under passet. Efter processdöd ska användaren åtminstone kunna spara den registrerade delen med markerat avbrott. Lägg aldrig till tid eller väg som saknar underlag. Avslut ska atomiskt färdigställa passet och köa export.
- **Health Connect.** Begär skrivbehörigheter när integrationen aktiveras. Exportera automatiskt en löpsession för hela rundan samt distans och tillgängliga pulsdata. Använd stabila externa post-ID:n för session och delposter. Gör säkra omförsök efter avbrott eller ändrad behörighet. Visa väntar/exporterat/kräver åtgärd. Exporterat betyder skrivet till Health Connect. WHOOP har egen import och använder sina egna pulsmätningar. Kontrollera med verkligt konto hur antal pass, tid, distans och tid i zon räknas samt om dubblering uppstår. Läsimport av andras pass ingår inte i v1.
- **Karta.** Google Maps SDK för Android visar lokalt GPS-spår först i historiken. Standard-SDK har obegränsad kostnadsfri användning enligt aktuell prislista men kräver Google Cloud-projekt med fakturering och API-nyckel. Begränsa nyckeln till Android-appens paket/signatur och rätt API. Utan nät eller kartnyckel ska registrering, statistik och export fungera. Visa tydlig kartstatus.
- **AI-export.** Dela en UTF-8-JSON per runda via Androids delningsmeny. Versionera schemat. Inkludera stabilt pass-ID, UTC-tider och lokal tidszon, plan med alla mål och zoner, faktiska faser/splits, aktiv och förfluten tid, total och huvudpass, zonstatistik, GPS med noggrannhet, puls, pauser, dataluckor och manuella fasändringar. Ange enheter uttryckligen och skilj uppmätta från härledda värden. Använd null för okänt. Leverera ett schema och en liten exempelfil som agenten kan validera. Koordinater ingår i full export.

## Windows, emulator och leveransordning
1. Lås en stödd .NET SDK och kompatibel MAUI-workload, Android SDK och JDK. Dokumentera faktiska versioner i repot. Installera SDK och Android-emulator på Windows, skapa en virtuell Pixel och starta den. Från projektet, kör dotnet workload restore och vid behov MSBuild-målet InstallAndroidDependencies. Bygg projektet och kör med Android-emulatorn som mål från IDE eller dotnet build -t:Run -f <projektets Android-TFM>. Ersätt platshållare med provkörda kommandon i README. Dokumentera även publicering, signering och installation av APK på fysisk telefon.
2. Prova tidigt en vertikal funktion på telefon: starta tjänsten, lås skärmen, samla GPS/BLE, ge ett ljudbesked och använd en aviseringsknapp. Kontrollera även att Health Connect-bindningen kan skriva en testaktivitet. Dokumentera enhetsberoende hinder innan övrig UI förfinas.
3. Implementera passmotor och lokal lagring med deterministisk klocka. Lägg till ett utvecklingsläge i Android-emulatorn med syntetisk rutt, puls, rörelse/stopp och sensorbortfall. Agenten ska kunna hoppa direkt till exempelvis intervall 3, ståvila, autopaus och nedvarvning. Scenarier måste kunna startas reproducerbart med dokumenterat kommando. Isolera testdata och inaktivera riktig hälsoexport för simulerade pass.
4. Koppla vyer, favoriter, uppläsning, avisering, export och karta. Leverera en kort testredovisning med faktiskt resultat och markera de fysiska kontroller som kräver löparens utrustning. Spela inte upp syntetiska data som bevis på fysisk funktion.

## Acceptansprov
| Prov | Förväntat resultat |
|---|---|
| Starta samma favorit två gånger vid olika tillfällen och redigera sedan favoriten | Ett tryck per start. Två separata rundor med ursprunglig plan i historiken. |
| 3 min uppvärmning, 5 × 3 min arbete med 2 min vila, 2 min nedvarvning | 5 arbetsfaser, 4 vilor, huvudtid 23 min, totalt 28 min. Exakt en export av hela rundan. |
| +1 min två gånger, hoppa sedan över resten av uppvärmningen | Planerad sluttid flyttas 120 s. Verklig uppvärmning sparas och huvudräknare börjar på noll. Samma förlängning fungerar under nedvarvning. |
| 2,4 km kontinuerligt huvudpass och coaching var 500 m | Fyra distansutlösta rapporter, split på 1 km, 1 km och 0,4 km. Ingen uppvärmningssträcka räknas in. |
| 29 s tempoavvikelse följt av ny split; sedan 30 s avvikelse | Ingen tid förs över till ny split. Ett besked när den nya avvikelsen når 30 s. Pröva även zon, exakt tempogräns och pulsbortfall. |
| Stopp under arbete, ståvila, manuell paus | Autopaus efter fem sekunders påvisat stopp i arbete och automatisk fortsättning vid rörelse. Tidsvila löper ut trots stillastående. Distansvila slutar först vid rätt sträcka. Manuell paus kräver aktiv fortsättning. |
| Pulsen kopplas bort, GPS försvinner, telefonen saknar internet | Ingen påhittad puls eller distans. Tidspasset fortsätter. Luckor syns i exporten. Sparad historik kan öppnas utan nät. |
| Avsluta med nekad hälsoexport, öppna appen igen, ge behörighet och försök två gånger | Lokalt pass finns direkt. En exporterad löpaktivitet efter återförsök; inga dubbla distans- eller pulsposter. |
| Dela JSON och läs med en oberoende parser | Återskapa fasordning, huvudtid och totaldistans från tidsserie/faser inom dokumenterad avrundning. |
| Pixel + WHOOP, Spotify, låst skärm under en riktig runda | GPS och puls fortsätter, ljud går att uppfatta, mini-UI styr rätt fas och öppning av appen återupptar samma vy. Kontrollera musikvolym och notera batteriförbrukning per timme. |
| Health Connect → WHOOP | Verifiera en aktivitet med rätt start/slut och distans. Dokumentera konkret effekt på vart och ett av löparens mål och eventuell konflikt med WHOOP-autodetektion. |

## Primärkällor och kvarstående verifiering
- [Microsoft – Blazor Hybrid](https://learn.microsoft.com/aspnet/core/blazor/hybrid/) · [MAUI-installation](https://learn.microsoft.com/dotnet/maui/get-started/installation)
- [W3C – Geolocation](https://www.w3.org/TR/geolocation/) · [Android – plats under ett träningspass](https://developer.android.com/develop/sensors-and-location/location/battery/scenarios) · [typer av foreground services](https://developer.android.com/develop/background-work/services/fgs/service-types)
- [WHOOP – puls via BLE](https://developer.whoop.com/docs/developing/support/) · [WHOOP – Health Connect](https://support.whoop.com/s/article/Google-Health-Integration-For-Android)
- [Android – ljud och volym](https://developer.android.com/media/platform/output) · [Google Maps – debitering](https://developers.google.com/maps/documentation/android-sdk/usage-and-billing) · [prislista](https://developers.google.com/maps/billing-and-pricing/pricing)

Test på fysisk telefon återstår. Särskilt viktiga mätpunkter är falsk autopaus vid långsam jogg, GPS-brus vid ståvila, aviseringskontroller på låsskärmen och WHOOP:s faktiska måltillgodoräkning. Specifikationen ger agenten mandat att justera mätfiltren efter dokumenterade prov. Ändringar av passregler eller målstatistik kräver ett produktbeslut.

## UI/UX – designkontrakt för v1
Minimalist Material Design är projektets eget urval av Material Design. Använd flat listvy, primär fylld knapp, sekundär textknapp, diskreta avdelare och navigationsfält med tre namngivna destinationer. En accentfärg; använd elevation bara när överlappande ytor behöver särskiljas. Visa fokus, tryckt, valt och inaktivt tillstånd.
- **Typografi:** paketera Space Grotesk lokalt för appens egna vyer, regular och bold; testa å/ä/ö, systemtextskalning och stabil sifferbredd.
- **Ikoner:** använd Googles Material Icons, Filled, 24 dp som standard. Välj play_arrow för start, pause för paus och history/settings för navigation. Märk åtgärder med synlig text eller tillgängligt namn. Dekorativa ikoner döljs för skärmläsare. Standardaviseringar följer Androids systemtypografi och layout.
- **Designprov:** kontrollera Space Grotesk och ikoner i flygplansläge, vald flik med text och form samt en direkt start från vardera Core-mallen.

Minimalism mäts i antalet beslut för löparen. Efter första konfigurationen: en startåtgärd från favoritlistan, noll obligatoriska tryck för ordinarie fasbyten, automatisk sparning efter avslut. Skilj primära åtgärder från redigering. Startsidan visar 2–3 favoriter utan att öppna meny. En tom lista erbjuder Skapa första passet. Avbruten favoritredigering ska bevara föregående version.
- Navigation: Pass, Historik och Inställningar. Under pågående pass får fas och kontroller utrymmet. Återöppning från avisering visar samma runda. Kartan ligger i passdetaljen efter avslut.
- Visuell hierarki: fas, återstående tid/distans och mål överst; huvudpass och split med samma ordning och enheter i mitten; aktuellt tempo/puls störst längst ned. Visa begripligt tomvärde vid bortfall. Tillåt radbrytning till två kolumner vid textförstoring.
- Typografisk skala: Space Grotesk med tabellsiffror. Rubrik 28–32 sp, aktuell mätning 40–48 sp, grupprubrik 18 sp, brödtext 16 sp, etikett 12–14 sp. Mellanrum i 4/8/16/24 dp, 20–24 dp yttermarginal. Mörkt standardtema: Basalt enligt färgtabellen nedan. Inget dekorativt diagram i träningsvyn.
- Handling: minst 48 × 48 dp tryckyta. Fasberoende knappnamn förklarar effekten. Skilj manuell paus från autopaus med text. Appen ska gå att använda med TalkBack och stor systemtext utan förlorade värden eller åtgärder.
- Acceptans: testa första start, återkommande favorit, ståvila, uppvärmning +1 min, avslut, frånkopplat pulsband och väntande export. Kontrollera kontrast (4,5:1 för normal text), läsordning och text vid 200 %. Låsskärmens exempel anger prioritering av innehåll och åtgärder; Android styr exakt rendering.
- Granskningsunderlag: [statiska HTML-mockuper](../design/mockups/index.html) med [kravkoppling och läsanvisning](../design/mockups/README.md) är löpande visuellt underlag för hela appen. Simon godkände layoutens inriktning den 12 november och valde Basalt den 13 november 2026. Nytillkomna skärmar och detaljlayouter är preliminära. PDF-mockuperna är borttagna på Simons begäran den 13 november 2026. Tidigare revisioner finns i Git-historiken. HTML ersätter inte passregler, acceptansprov eller verifiering i Android. Beslut journalförs i design/decision.md före implementation.

### Basalt – beslutade färger

| Roll | Färg | Användning |
|---|---|---|
| Bakgrund | #10120F | Appens grund |
| Yta | #1B1E18 | Grupper och navigationsfält |
| Primär text | #EEEFE6 | Rubriker, timer och mätvärden |
| Sekundär text | #A6AE9A | Etiketter och enheter |
| Accent | #C4DA73 | Primära handlingar, aktiv fas och vald destination |
| På accent | #10120F | Text och ikoner på fyllda accentknappar |

Paletten ersätter revision 5:s blå färger; layout, engelska, Space Grotesk och Material Icons består. Använd samma färgroller i alla appägda vyer. Systemägda Android-aviseringar följer systemets färg- och layoutregler. Färg får inte ensam bära betydelse; visa även text och form. Inaktiva åtgärder ska skiljas från aktiva med tillstånd och tillgänglighetsinformation.

Acceptans: start- och träningsvyn använder tabellens färger utan kvarvarande blå accent. Primär och sekundär text samt accenttext når minst 4,5:1 mot både bakgrund och yta. Beräknad lägsta kontrast är 7,34:1 (sekundär text mot yta); mörk text på accent når 12,21:1. Verifiera textskalning, fokus och avläsning utomhus i appen; statiska mockuper bevisar inte dessa beteenden.

## Projektstyrning
GitHub Projects är den gemensamma arbetslistan. Varje byggbar ändring har en issue med länkat krav och acceptansprov. Följ Todo → Doing → Waiting → Done enligt [statusreglerna i AGENTS.md](../AGENTS.md#github-projects-obligatoriskt-statusflöde). Waiting betyder att nästa steg kräver Simons input eller godkännande. Verifiera varje statusbyte genom återläsning. Länka PR och verifieringsresultat; Done kräver verifierad acceptans, nödvändiga godkännanden och mergad PR när sådan finns. Git är källan för krav och design; Projects visar status. Skapad lokal startstruktur är inte bevis på att fjärrprojektet skapats.
