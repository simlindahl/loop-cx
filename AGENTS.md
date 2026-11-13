# Arbete i detta repo
Läs knowledge/product.md före varje ändring. [GitHub Projects · LOOP Cx](https://github.com/users/simlindahl/projects/2) är arbetslistan; Git-versionerad specifikation styr beteendet. Länka issue, kravavsnitt, acceptansprov och PR. Uppdatera specifikationen först när beteendet ändras.
Simon har godkänt revision 5 som grund för implementationen. För ändrad design, visa en ny mockup och invänta beslut. Registrera godkänd version/commit och eventuella villkor i design/decision.md före appkod, scaffold eller integrationsprototyper.
Använd spec-change för krav/design och verify-run för leverans. Behåll engelska användartexter och engelsk röstcoachning, syntetiska testdata och lokalt lagrade mätningar. Lägg aldrig nycklar, signerande certifikat eller verkliga löprundor i Git.

Bygg vertikala funktionssnitt i den modulära monoliten. Följ [Kodorganisation](knowledge/product.md#kodorganisation) även vid första scaffolden; motivera varje nytt lager med det problem det löser.

Inför UI-arbete: läs design/mockups/README.md och berörda vyer i design/mockups/index.html. HTML/CSS är det löpande visuella implementationsunderlaget; knowledge/product.md styr beteendet. Nya vyer är preliminära tills Simon godkänt dem. PDF-mockuperna är borttagna på Simons begäran den 13 november 2026. Tidigare revisioner finns i Git-historiken. Förändringar i HTML är inte i sig godkännande eller bevis på fungerande Android-funktion.

## Arbetslista och status

[GitHub Projects · LOOP Cx](https://github.com/users/simlindahl/projects/2) är den enda aktuella tavlan. Projektet ägs av `simlindahl`, har nummer `2` och är länkat till detta repo. Issues äger krav och diskussioner; PR:ar äger diff och verifieringsresultat. Krav, design och kod versionshanteras i Git. Återskapa inte en parallell tavla i repot.

Använd ett kort per avgränsat arbetsmoment. En feature-issue med återstående arbete kan ligga i Todo samtidigt som dess del-PR ligger i Waiting. Återanvänd ett befintligt kort med samma issue-/PR-URL. Använd draft-kort bara för moment som saknar issue eller PR. Fälten `Arbete`, `Nästa steg` och `Underlag` behåller kortets avgränsning, ansvarig för nästa steg och relevanta länkar; kopiera inte hela krav eller testloggar dit.

| Status | Betydelse |
|---|---|
| Todo | Planerat arbete som ännu inte har påbörjats. |
| Doing | Aktivt arbete, inklusive implementation och verifiering. |
| Waiting | Nästa steg kräver Simons input, beslut, granskning, godkännande eller fysiska test. |
| Done | Acceptanskriterier är verifierade, nödvändiga godkännanden finns och eventuell PR är mergad. |

1. Läs projektets aktuella kort och fält samt berörda issues och PR:ar vid varje körnings början. Välj ett avgränsat moment och sätt dess kort till Doing innan arbetet börjar. Uppdatera `Nästa steg` och länka underlaget.
2. Uppdatera kort och status direkt i Projects via `gh project` inom det aktuella uppdraget. Statusbyten kräver varken commit, PR eller CI. Kod, krav, design och instruktioner följer ordinarie PR-regler. Simon godkänner merge om inte merge för just den PR:n uttryckligen har delegerats. Noll obligatoriska GitHub-approvals ersätter inte detta godkännande.
3. När nästa steg kräver Simon, färdigställ allt oberoende arbete inom momentet först. Sätt Waiting och ange exakt vad Simon behöver göra, vilket steg det låser upp och länk till granskningsunderlaget. Övriga självständiga moment kan fortsätta på egna kort.
4. Efter svaret: återgå till Doing om arbete eller verifiering återstår. Sätt Done endast om samtliga slutvillkor redan är uppfyllda. En öppnad PR eller gröna automatiska tester räcker inte för Done. CI-väntan och tekniska hinder som agenten kan lösa är inte Waiting; ange hindret och agentens nästa åtgärd i Doing. Automatisk stängning eller merge ersätter inte bedömningen av återstående acceptans.
5. Hämta aktuella kort- och fält-ID:n före skrivning. Uppdatera bara avsett kort och fält; bevara andras ändringar och skapa inte dubbletter. Läs tillbaka både status och `Nästa steg` efter varje uppdatering. Vid fel: rapportera konkret fel och senast verifierad status; påstå inte att tavlan är uppdaterad.
6. Stäm av projektet vid körningens slut. Länka leverans-PR och verifieringsresultat från rätt kort. Lämna feature-issuen öppen om obligatorisk acceptans återstår. Behåll aktuella och nyligen avslutade kort; arkivera äldre Done-kort vid behov utan att radera issues eller PR:ar.

GitHub CLI behöver scope `project`. Börja med `gh project item-list 2 --owner simlindahl --format json --limit 100` och `gh project field-list 2 --owner simlindahl --format json`. Paginera om resultatet inte täcker alla kort. Hantera nekad åtkomst som ett konkret hinder; skapa inte en lokal reservtavla som kan bli inaktuell.

Vid ändrat tavelflöde: skapa ett märkt syntetiskt draft-kort i Projects. Prova Todo → Doing → Waiting → Doing → Waiting → Done med riktig skrivning och återläsning efter varje steg. Ta bort testkortet och verifiera borttagningen. Redovisa testkortets resultat separat från verkliga kort och från CI för repoändringar.
