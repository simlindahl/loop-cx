# LOOP Cx
Privat Android-projekt för löpträning. Officiell LOOP Cx-app för C1- och C2-löpning.
- [Produktspecifikation och acceptansprov](knowledge/product.md)
- [Statiska HTML-mockuper för hela appen](design/mockups/index.html) · [Öppna och granska](design/mockups/README.md) · [Beslut](design/decision.md)
- [Arbetslista · Todo / Doing / Waiting / Done](https://github.com/users/simlindahl/projects/2)
- [Arbetsinstruktioner](AGENTS.md)

knowledge/ är ett Open Knowledge Format 0.2-bundle enligt [Googles specifikation](https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/main/okf/SPEC.md). Versionshantera krav, design och evidens här. [GitHub Projects · LOOP Cx](https://github.com/users/simlindahl/projects/2) följer arbetet genom Todo → Doing → Waiting → Done.

[Öppna de statiska mobilmockuperna](design/mockups/index.html) · [Beslut om LOOP Cx](knowledge/loop-cx.md)

## Så arbetar vi
[Arbetslista](https://github.com/users/simlindahl/projects/2) · [Actions](https://github.com/simlindahl/loop-cx/actions/workflows/check.yml)

Simon prioriterar och fattar produkt- och designbeslut. Agenten läser GitHub-projektet, sätter aktuellt kort till Doing och arbetar på en kortlivad branch med krav och resultat. Använd `Closes #nummer` bara när PR:n uppfyller hela issuen; annars `Refs #nummer`. När nästa steg kräver Simon sätts Waiting med en konkret begäran och länkat underlag. Status uppdateras direkt i Projects via `gh project` och läses tillbaka; ingen tavle-PR eller CI behövs för statusbyten. [Statusreglerna i AGENTS.md](AGENTS.md#arbetslista-och-status) styr arbetssättet. Simon godkänner merge om inte den aktuella PR:n uttryckligen har delegerats. En fysisk kontroll som ingår i acceptansen ska vara genomförd innan issuen stängs.

Repository checks körs på PR och push till main och kan startas manuellt. Android och Static mockup review körs på varje PR utan path-filter, även vid rena dokumentationsändringar, och kan startas manuellt. Static mockup review körs även vid push till main. Checks heter `specification`, `emulator` och `mockups`. Emulatorn kör ett riktigt build och smoke-test även när appkoden inte har ändrats. Se [main-skydd och aktivering](docs/main-protection.md). Kör samma kontroll lokalt med `ruby scripts/check_docs.rb`. [Android-kontrollen](.github/workflows/android.yml) bygger en debug-APK och startar appen i emulator. [Kör appen på Windows](docs/android.md).
