# LOOP Cx
Privat Android-projekt för löpträning. Officiell LOOP Cx-app för C1- och C2-löpning.
- [Produktspecifikation och acceptansprov](knowledge/product.md)
- [Statiska HTML-mockuper för hela appen](design/mockups/index.html) · [Öppna och granska](design/mockups/README.md) · [Beslut](design/decision.md)
- [Arbetslista · Todo / Doing / Waiting / Done](BOARD.md)
- [Arbetsinstruktioner](AGENTS.md)

knowledge/ är ett Open Knowledge Format 0.2-bundle enligt [Googles specifikation](https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/main/okf/SPEC.md). Versionshantera krav, design och evidens här. [BOARD.md](BOARD.md) följer arbetet genom Todo → Doing → Waiting → Done.

[Öppna de statiska mobilmockuperna](design/mockups/index.html) · [Beslut om LOOP Cx](knowledge/loop-cx.md)

## Så arbetar vi
[Arbetslista](BOARD.md) · [Actions](https://github.com/simlindahl/loop-cx/actions/workflows/check.yml)

Simon prioriterar och fattar produkt- och designbeslut. Agenten flyttar arbetsmomentet till Doing i BOARD.md på main, arbetar på en kortlivad gren och öppnar en PR med krav och resultat. Använd `Closes #nummer` bara när PR:n uppfyller hela issuen; annars `Refs #nummer`. När nästa steg kräver Simon flyttas momentet till Waiting med en konkret begäran och länkat underlag. Tavlan uppdateras direkt på main och varje uppdatering läses tillbaka. [Statusreglerna i AGENTS.md](AGENTS.md#arbetslista-och-status) styr arbetssättet. Simon kan granska diff och evidens där. En fysisk kontroll som ingår i acceptansen ska vara genomförd innan issuen stängs.

Repository checks körs på PR och push till main och kan startas manuellt. Kör samma kontroll lokalt med `ruby scripts/check_docs.rb`. [Android-kontrollen](.github/workflows/android.yml) bygger en debug-APK och startar appen i emulator. [Kör appen på Windows](docs/android.md).
