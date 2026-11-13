---
name: verify-run
description: Verifiera löparappens passregler, emulatorflöden och integrationer inför leverans.
---
1. Följ statusreglerna i AGENTS.md. Flytta ärendet till Doing före aktiv verifiering och läs tillbaka status. Läs berört acceptansprov i knowledge/product.md. Välj deterministisk klocka för passregler, Android-emulator för UI och fysisk Pixel med WHOOP för bakgrundsmätning.
2. Kör scenariot med angivna indata. Jämför faktisk fasföljd, statistik och export mot förväntat utfall; pröva omstart eller mätlucka när ändringen berör lagring eller sensorer.
3. Ange commit, plattform, reproduktionssteg och resultat i PR. Märk varje fysiskt test som verifierat, underkänt eller ej kört. Syntetisk Health Connect-export ska vara avstängd.
4. När nästa steg kräver Simons granskning, godkännande eller fysiska test: flytta till Waiting och länka resultat, teststeg och exakt vad Simon behöver göra. Lämna issuen öppen om obligatorisk acceptans återstår.
5. Efter svar, återgå till Doing om arbete återstår. Stäng issuen och flytta till Done först när acceptanskriterierna är verifierade, nödvändiga godkännanden finns och eventuell PR är mergad. Läs tillbaka varje statusbyte. Rapportera konkreta statusfel enligt AGENTS.md; en kommentar ersätter inte Projects-status.
