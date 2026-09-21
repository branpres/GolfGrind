# Architecture

## Boundaries

- `GolfGrind.Core` contains launch-monitor contracts, shot/session models, ball-flight calculations, analytics projections, practice-game rules, guided-activity state machines, validation, duplicate detection, ball-detection health/recovery rules, calibration metadata and connection-state logic. It has no MAUI or Windows dependency.
- `GolfGrind.App` contains the MAUI Blazor Hybrid shell, Razor components, Windows Bluetooth implementation and local persistence adapters.
- `GolfGrind.ProtocolChecks` verifies captured/known Square protocol behavior without the UI.
- `GolfGrind.CoreChecks` verifies deterministic game and reliability behavior without test-framework packages.

## UI composition

`Home.razor` composes the compact mode header, on-demand workspace navigation and current page component. Its partials connect UI events to focused Core rules and App services rather than owning device, session-lifecycle or guided-progression logic. Range, Practice, Bag Mapping, Wedge Matrix, Analytics, Golf Bag, Sessions and Golfers own their screen markup in dedicated components. Practice mode setup, live scoring and Fairway Finder plotting are separate components; shared components provide range inputs, the setup/live activity workspace, tracer and shot-history table.

## Shot pipeline

1. `SquareLaunchMonitor` discovers or reconnects to the remembered monitor, restores subscriptions/heartbeat/club/ball detection, parses a packet and calculates its flight.
2. `LaunchMonitorWorkspace` owns device selection, connection-facing state and display-wake side effects; `LaunchMonitorCoordinator` validates physical ranges, rejects duplicates and attaches calculation/calibration metadata.
3. `PracticeGameEngine` adds the active game's prompt/settings/result snapshot and advances its deterministic state.
4. `ActivitySessionCoordinator` owns the live activity and empty-session lifecycle; `SessionStorageService` converts accepted shots to compact durable form and performs atomic validated writes with last-known-good copies.
5. Bag Mapping derives only from Bag Mapping sessions, Wedge Matrix derives only from Wedge Matrix sessions, and general Analytics derives from the remaining Range/Practice performance sessions.
6. Practice recommendations combine Bag Mapping full-club carries with Wedge Matrix club/swing carries without mixing either calibration dataset into performance analytics.

## Prototype data and recovery

All internal state is rooted at `%LOCALAPPDATA%\GolfGrind`. `profiles.json` stores golfer identities, `settings.json` stores application and remembered-device settings, and each `profiles\<id>` directory contains `bag.json` plus individual session files. These stores use atomic validated writes and last-known-good recovery. User-requested exports and packet diagnostics remain in `Documents\Golf Grind`.

Backward compatibility is not a v0.24.0 requirement. This build does not migrate data from MAUI's previous application-ID-based files or preferences. Practice modes and their snapshots may change without migrations while the prototype is being refined. A damaged JSON file is still quarantined, and its last-known-good copy is restored when available.

v0.16 preserves the active in-memory activity during a Bluetooth reconnect. Persisted checkpoint/resume after closing the app or restarting Windows remains future work.
