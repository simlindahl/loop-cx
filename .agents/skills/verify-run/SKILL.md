---
name: verify-run
description: Verifiera löparappens passregler, emulatorflöden och integrationer inför leverans.
---
1. Följ statusreglerna i AGENTS.md och använd GitHub Projects · LOOP Cx (https://github.com/users/simlindahl/projects/2) före, under och efter arbetet. Läs berört acceptansprov i knowledge/product.md. Välj deterministisk klocka för passregler, Android-emulator för UI och fysisk Pixel med WHOOP för bakgrundsmätning.
2. Kör scenariot med angivna indata. Jämför faktisk fasföljd, statistik och export mot förväntat utfall; pröva omstart eller mätlucka när ändringen berör lagring eller sensorer.
3. Ange commit, plattform, reproduktionssteg och resultat i PR. Märk varje fysiskt test som verifierat, underkänt eller ej kört. Syntetisk Health Connect-export ska vara avstängd.
4. Länka verifieringsresultatet från issuen och projektkortet. Om nästa steg kräver Simons granskning, godkännande eller fysiska test, följ Waiting-reglerna i AGENTS.md och ange konkreta teststeg och granskningsunderlag.
5. Lämna issuen öppen om obligatorisk acceptans återstår. Följ slutvillkoren för Done i AGENTS.md; en del-PR ska referera till feature-issuen utan att stänga den.
