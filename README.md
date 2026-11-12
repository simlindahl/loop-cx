# LOOP Cx
Privat Android-projekt för löpträning. Officiell LOOP Cx-app för C1- och C2-löpning.
- [Produktspecifikation och acceptansprov](knowledge/product.md)
- [Design för granskning](design/LOOP-Cx-direction-5.pdf) · [Beslut](design/decision.md)
- [Arbetsinstruktioner](AGENTS.md)

knowledge/ är ett Open Knowledge Format 0.2-bundle enligt [Googles specifikation](https://github.com/GoogleCloudPlatform/knowledge-catalog/blob/main/okf/SPEC.md). Versionshantera krav, design och evidens här. GitHub Projects följer issues från Todo via In Progress till Done.

[Öppna mobilmockuperna som PDF](design/LOOP-Cx-direction-5.pdf) · [Beslut om LOOP Cx](knowledge/loop-cx.md)

## Så arbetar vi
[Projekttavla](https://github.com/users/simlindahl/projects/2) · [Actions](https://github.com/simlindahl/loop-cx/actions/workflows/check.yml)

Simon prioriterar och fattar produkt- och designbeslut. Agenten tar en issue till In Progress, arbetar på en kortlivad gren och öppnar en PR med krav, resultat och `Closes #nummer`. Simon kan granska diff och evidens där. En fysisk kontroll som ingår i acceptansen ska vara genomförd innan issuen stängs.

Repository checks körs på PR och push till main och kan startas manuellt. Kör samma kontroll lokalt med `ruby scripts/check_docs.rb`. [Android-kontrollen](.github/workflows/android.yml) bygger en debug-APK och startar appen i emulator. [Kör appen på Windows](docs/android.md).
