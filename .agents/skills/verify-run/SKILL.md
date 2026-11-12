---
name: verify-run
description: Verifiera löparappens passregler, emulatorflöden och integrationer inför leverans.
---
1. Läs berört acceptansprov i knowledge/product.md. Välj deterministisk klocka för passregler, Android-emulator för UI och fysisk Pixel med WHOOP för bakgrundsmätning.
2. Kör scenariot med angivna indata. Jämför faktisk fasföljd, statistik och export mot förväntat utfall; pröva omstart eller mätlucka när ändringen berör lagring eller sensorer.
3. Ange commit, plattform, reproduktionssteg och resultat i PR. Märk varje fysiskt test som verifierat, underkänt eller ej kört. Syntetisk Health Connect-export ska vara avstängd.
4. Stäng issuen efter verifierade acceptanskriterier. Kvarvarande fysiska kontroller får ett uttryckligt ägarskap i projektet.
5. Uppdatera samma issue i GitHub Projects med faktisk verifieringsstatus och länk till resultat. Lämna den öppen om obligatoriskt acceptansprov återstår.
