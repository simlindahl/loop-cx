---
type: Product Specification
title: Integrations, storage and data
---
# Integrations, storage and data

[Product entry point and ownership](product.md). M1 requirements unless explicitly marked otherwise.

<a id="integration-lagring-och-fel"></a>
## Integration, storage and errors
- **WHOOP.** Read the band's Bluetooth LE heart rate with heart-rate broadcast enabled. Let the user connect the band on first use and enter personal zones. Verify ordered boundaries without unintended gaps or overlaps. Reconnect after interruption. Show a dash for missing heart rate, continue the workout and suppress heart-rate coaching until new readings arrive. Live recording requires no WHOOP web API or custom cloud service.
- **Android.** Request precise location and the relevant Bluetooth scan/connect permissions when the features are used. Declare foreground service types and permissions required by the selected Android version for location and a connected device. Start the service from a visible app. Request notification permission for the mini-UI. If access is denied, show the consequence and a path to settings. Continue with available features. A timed workout can work without GPS; explain and let the user change the plan when distance is essential.
- **Local database.** Save favourites, settings, the frozen workout plan and zones, phase boundaries, raw sensor readings, pauses, quality markers and export status. Write continuously during the workout. After process death, the user must at least be able to save the recorded part with an interruption marker. Never add unsupported time or distance. Finishing must atomically finalize the session and enqueue export.
- **Health Connect.** Request write permissions when integration is enabled. Automatically export a running session for the whole run, distance and available heart-rate data. Use stable external record IDs for the session and child records. Retry safely after interruption or changed permissions. Show pending/exported/action required. Exported means written to Health Connect. WHOOP imports separately and uses its own heart-rate readings. Check with a real account how workout count, time, distance and time in zones are credited and whether duplication occurs. Importing other apps' workouts is outside v1.
- **Map.** Google Maps SDK for Android shows the local GPS track only in history. The standard SDK has unlimited free use according to the current pricing list but requires a Google Cloud project with billing and an API key. Restrict the key to the Android package/signature and correct API. Recording, statistics and export must work without network access or a map key. Show clear map status.
- **AI export.** Share one UTF-8 JSON file per run through Android's share sheet. Version the schema. Include stable session ID, UTC timestamps and local time zone, plan with all targets and zones, actual phases/splits, active and elapsed time, total and main set, zone statistics, GPS with accuracy, heart rate, pauses, data gaps and manual phase changes. Specify units explicitly and distinguish measured from derived values. Use null for unknown. Deliver a schema and a small sample file the agent can validate. Coordinates are included in the full export.

## Early native binding proof

[Issue #1 feasibility boundaries](delivery-acceptance.md#issue-1-bounded-native-feasibility) govern the debug-only BLE, speech, notification and isolated Health Connect probes. These do not enable normal Health Connect export or change the production integration requirements above. Use the existing Android service ownership and platform bindings; no new server or integration framework is required.
