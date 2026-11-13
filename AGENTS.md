# Arbete i detta repo
Läs knowledge/product.md före varje ändring. GitHub Projects är arbetslistan; Git-versionerad specifikation styr beteendet. Länka issue, kravavsnitt, acceptansprov och PR. Uppdatera specifikationen först när beteendet ändras.
Simon har godkänt revision 5 som grund för implementationen. För ändrad design, visa en ny mockup och invänta beslut. Registrera godkänd version/commit och eventuella villkor i design/decision.md före appkod, scaffold eller integrationsprototyper.
Använd spec-change för krav/design och verify-run för leverans. Behåll engelska användartexter och engelsk röstcoachning, syntetiska testdata och lokalt lagrade mätningar. Lägg aldrig nycklar, signerande certifikat eller verkliga löprundor i Git.

Bygg vertikala funktionssnitt i den modulära monoliten. Följ [Kodorganisation](knowledge/product.md#kodorganisation) även vid första scaffolden; motivera varje nytt lager med det problem det löser.

Inför UI-arbete: läs design/mockups/README.md och berörda vyer i design/mockups/index.html. HTML/CSS är det löpande visuella implementationsunderlaget; knowledge/product.md styr beteendet. Nya vyer är preliminära tills Simon godkänt dem. Behåll PDF-revisionerna som historik. Förändringar i HTML är inte i sig godkännande eller bevis på fungerande Android-funktion.
