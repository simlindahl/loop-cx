# Arbete i detta repo
Läs knowledge/product.md före varje ändring. GitHub Projects är arbetslistan; Git-versionerad specifikation styr beteendet. Länka issue, kravavsnitt, acceptansprov och PR. Uppdatera specifikationen först när beteendet ändras.
Simon har godkänt revision 5 som grund för implementationen. För ändrad design, visa en ny mockup och invänta beslut. Registrera godkänd version/commit och eventuella villkor i design/decision.md före appkod, scaffold eller integrationsprototyper.
Använd spec-change för krav/design och verify-run för leverans. Behåll engelska användartexter och engelsk röstcoachning, syntetiska testdata och lokalt lagrade mätningar. Lägg aldrig nycklar, signerande certifikat eller verkliga löprundor i Git.

Bygg vertikala funktionssnitt i den modulära monoliten. Följ [Kodorganisation](knowledge/product.md#kodorganisation) även vid första scaffolden; motivera varje nytt lager med det problem det löser.

Inför UI-arbete: läs design/mockups/README.md och berörda vyer i design/mockups/index.html. HTML/CSS är det löpande visuella implementationsunderlaget; knowledge/product.md styr beteendet. Nya vyer är preliminära tills Simon godkänt dem. PDF-mockuperna är borttagna på Simons begäran den 13 november 2026. Tidigare revisioner finns i Git-historiken. Förändringar i HTML är inte i sig godkännande eller bevis på fungerande Android-funktion.

## GitHub Projects: obligatoriskt statusflöde

Använd [projekttavlan](https://github.com/users/simlindahl/projects/2) som gemensam arbetslista. Flödet är **Todo → Doing → Waiting → Done**.

| Status | När den används |
|---|---|
| Todo | Planerat arbete som ännu inte har påbörjats. |
| Doing | Agenten arbetar aktivt med ärendet, inklusive implementation och verifiering. |
| Waiting | Nästa steg kräver Simons input, beslut, granskning, godkännande eller fysiska test. |
| Done | Acceptanskriterierna är verifierade, nödvändiga godkännanden finns och eventuell PR är mergad. |

Flytta ärendet till Doing innan arbetet börjar. När nästa steg kräver Simon, färdigställ allt oberoende arbete först och flytta sedan till Waiting. Länka PR, underlag och verifieringsresultat i ärendet samt ange exakt vad Simon behöver göra och vilket steg det låser upp. En öppnad PR eller gröna automatiska tester räcker inte för Done.

Efter Simons svar: återgå till Doing om arbete eller verifiering återstår. Gå direkt från Waiting till Done endast när alla slutvillkor redan är uppfyllda. Waiting används inte för CI-körningar eller tekniska hinder som agenten kan lösa själv.

Läs tillbaka Projects-status efter varje ändring och vid varje körnings början och slut. Kontrollera verkligt statusfält; en issue-kommentar, etikett eller lokal anteckning ersätter inte statusändringen. Hitta fält och alternativ i det aktuella projektet, anta inte deras ID:n. Om tavlan fortfarande använder In Progress är det den befintliga arbetsstatusen som ska döpas om till Doing; skapa inte två parallella arbetsstatusar.

Om en statusändring misslyckas: rapportera det konkreta felet, faktisk senast verifierad status och vad som krävs för att lösa hindret. Fortsätt med oberoende arbete och försök synkronisera igen vid nästa körning. Påstå aldrig att tavlan är uppdaterad utan återläsning.

Vid ändring av statusflödet: skapa ett tydligt märkt tillfälligt testkort, kör Todo → Doing → Waiting → Doing → Waiting → Done och läs tillbaka status efter varje steg. Ta sedan bort testkortet från projektet. Flytta inga riktiga ärenden till Done enbart för att testa flödet. Redovisa separat vad som testats lokalt och vad som faktiskt verifierats i GitHub.
