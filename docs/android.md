# Kör första Android-bygget på Windows

Installera .NET SDK 10.0.100 (x64), JDK 21 och Android Studio. Sätt JAVA_HOME till din JDK och ANDROID_HOME till din Android SDK. Installera Android SDK Platform 36, Build Tools 36.0.0 och Platform Tools i SDK Manager. Lägg platform-tools på PATH. Skapa en Pixel-emulator med Google APIs API 35 och x86_64 i Device Manager, och starta den.

Kör i PowerShell från repots rot med emulatorn som enda anslutna enhet.

```powershell
adb devices
dotnet workload install maui-android
dotnet build src/LoopCx/LoopCx.csproj -t:Run -c Debug -p:RuntimeIdentifier=android-x64
```

Startsidan ska visa **Start a run**. Det verifierar kedjan från Android-start till renderad Razor-komponent. GitHub Actions bygger en debug-signerad APK för x64 och testar samma rubrik via Androids tillgänglighetsträd. APK:n finns i körningens artefakter.

Den här första kontrollen omfattar appstart. GPS under låst skärm, WHOOP via BLE, ljud och Health Connect följs upp i issue #1. Windows-kommandona behöver också provköras på Windows.
