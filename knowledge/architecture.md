---
type: Product Specification
title: Architecture and code organization
---
# Architecture and code organization

[Product entry point and ownership](product.md). M1 requirements unless explicitly marked otherwise.

<a id="teknikbeslut"></a>
## Technology decisions
Use C#, .NET MAUI Blazor Hybrid and a local SQLite database. A PWA offers simpler distribution and web development, local storage and a cached interface, but web geolocation requires a visible document. Reliable location recording in a pocket, Bluetooth heart rate and Health Connect determine the choice. MAUI requires the Android SDK, an emulator, platform permissions and maintenance of Android bindings. Measure battery use on the phone. No separate web host is needed.

An Android foreground service, started when the user starts the workout, owns the clock, GPS, heart rate and phase transitions. The Blazor view and notification read the same state and send commands to the same session. Reopening, rerendering or duplicate taps must not create two recordings. Keep workout rules and statistics in plain C# and Android calls at the system boundary. Use a monotonic clock for durations and also timestamp in UTC for export.

<a id="kodorganisation"></a>
### Code organization

Build a modular monolith behind Blazor: one installable app with clear feature boundaries in C#. Organize code into vertical slices by what the runner does. Keep Razor components, input, validation and use-case-specific persistence close together. Folders and namespaces are sufficient to start with.

Example: a change to target-RPE validation for a favourite belongs in the slice for editing favourite workouts. Rules that also apply when starting a workout belong to the workout model and are reused from there. The active session owns phase transitions and pause rules; Blazor and lock-screen controls invoke the same behavior. Android dependencies for GPS and Bluetooth stay at the platform boundary so the workout engine can be verified with simulated measurements.

Choose the shortest clear path from UI to behavior and data. Add an interface, layer or separate project only when it solves a concrete need, such as replacing real sensor data with a deterministic test source. Clean Architecture layers are not required. A review should identify where each rule belongs and why every intermediate step is needed.
