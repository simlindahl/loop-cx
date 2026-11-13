# Arbete i detta repo
Läs knowledge/product.md före varje ändring. BOARD.md på main är arbetslistan; Git-versionerad specifikation styr beteendet. Länka issue, kravavsnitt, acceptansprov och PR. Uppdatera specifikationen först när beteendet ändras.
Simon har godkänt revision 5 som grund för implementationen. För ändrad design, visa en ny mockup och invänta beslut. Registrera godkänd version/commit och eventuella villkor i design/decision.md före appkod, scaffold eller integrationsprototyper.
Använd spec-change för krav/design och verify-run för leverans. Behåll engelska användartexter och engelsk röstcoachning, syntetiska testdata och lokalt lagrade mätningar. Lägg aldrig nycklar, signerande certifikat eller verkliga löprundor i Git.

Bygg vertikala funktionssnitt i den modulära monoliten. Följ [Kodorganisation](knowledge/product.md#kodorganisation) även vid första scaffolden; motivera varje nytt lager med det problem det löser.

Inför UI-arbete: läs design/mockups/README.md och berörda vyer i design/mockups/index.html. HTML/CSS är det löpande visuella implementationsunderlaget; knowledge/product.md styr beteendet. Nya vyer är preliminära tills Simon godkänt dem. PDF-mockuperna är borttagna på Simons begäran den 13 november 2026. Tidigare revisioner finns i Git-historiken. Förändringar i HTML är inte i sig godkännande eller bevis på fungerande Android-funktion.

## Arbetslista och status

[BOARD.md](BOARD.md) på `main` är den enda aktuella tavlan och ersätter GitHub Projects. Issues äger krav och diskussioner; PR:ar äger diff och verifieringsresultat. Tavlan innehåller korta rader med länk till issue eller PR, arbete och nästa steg. Använd en rad per avgränsat arbetsmoment; flera moment får länka samma feature-issue när de har olika status. Kopiera inte hela krav eller testloggar till tavlan.

| Status | Betydelse |
|---|---|
| Todo | Planerat arbete som ännu inte har påbörjats. |
| Doing | Aktivt arbete, inklusive implementation och verifiering. |
| Waiting | Nästa steg kräver Simons input, beslut, granskning, godkännande eller fysiska test. |
| Done | Acceptanskriterier är verifierade, nödvändiga godkännanden finns och eventuell PR är mergad. |

1. Läs senaste BOARD.md på main samt berörda issues och PR:ar vid varje körnings början. Välj ett avgränsat moment och flytta dess rad till Doing innan arbetet börjar.
2. Uppdatera tavlan direkt på main i separata statuscommits. Detta är godkänt för tavlan; produktkod, krav- och designändringar följer ordinarie PR- och godkännanderegler. Ta inte med en gammal kopia av BOARD.md från en feature-gren i en senare merge.
3. När nästa steg kräver Simon, färdigställ allt oberoende arbete inom momentet först. Flytta till Waiting och ange exakt vad Simon behöver göra, vilket steg det låser upp och länk till granskningsunderlaget. Övriga självständiga moment kan fortsätta på egna rader.
4. Efter svaret: återgå till Doing om arbete eller verifiering återstår. Flytta direkt till Done endast om samtliga slutvillkor redan är uppfyllda. En öppnad PR eller gröna automatiska tester räcker inte för Done. CI-väntan och tekniska hinder som agenten kan lösa är inte Waiting; ange hindret och agentens nästa åtgärd i Doing.
5. Hämta senaste fil och blob-SHA före varje skrivning. Vid konflikt: läs om, tillämpa bara den avsedda radändringen och bevara andras uppdateringar. Skriv aldrig över tavlan med en gammal helfil och använd inte force-push.
6. Läs tillbaka BOARD.md från main efter varje statuscommit och verifiera raden i rätt avsnitt. Stäm av tavlan vid körningens slut. Rapportera konkret fel och senast verifierad status om skrivning eller återläsning misslyckas; påstå inte att tavlan är uppdaterad.
7. Behåll bara aktuella och nyligen avslutade poster. Äldre Done-rader får tas bort; Git och issues behåller historiken.

Vid ändrat tavelflöde: använd en separat testgren med ett märkt syntetiskt kort. Prova Todo → Doing → Waiting → Doing → Waiting → Done genom riktiga skrivningar och återläsning efter varje steg. Ta bort kortet efter testet och merga inte testgrenen. Redovisa testgrenens resultat separat från verifieringen av den riktiga tavlan på main.
