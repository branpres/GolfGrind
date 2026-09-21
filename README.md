# Golf Grind - Blazor Hybrid prototype

Windows-first .NET 10 MAUI Blazor Hybrid application for a Square Golf launch monitor.

## Current prototype (v0.24.6)

This build reconstructs the accepted v0.14–v0.16 work on top of the preserved v0.13 archive.

- Blazor/Razor UI hosted in a native Windows app
- Connection / battery / ball-ready status
- Club selector
- Animated perspective and side-view SVG shot tracers
- Last-shot metrics
- Session shot table
- Direct Square Golf Home Bluetooth LE discovery and connection on Windows
- Battery and ball-ready status
- Real-time ball speed, launch, spin, spin-axis and available club-delivery data
- Estimated carry, total, offline, apex and flight time from measured ball data
- Numerically generated top-down flight tracer
- Club selection, heartbeat, layered shot de-duplication and automatic re-arming
- Automatic Square reconnection using 2, 4, 8 and 15 second backoff while preserving the active in-memory activity
- Explicit Disconnected, Discovering, Connecting, Initializing, Connected, Ball Ready, Reconnecting and Faulted states
- Connection health counters plus visible accepted/rejected/duplicate shot diagnostics
- Remembered Square device identity, selected club, restored notifications, heartbeat and ball detection after reconnect
- Mock launch monitor retained as an in-app demo mode
- Persistent golf-bag editor with custom club names, types and lofts
- Custom wedge lofts displayed in the club selector and saved between launches
- Atomic validated session writes with last-known-good copies, damaged-file quarantine and automatic recovery
- Saved-session summaries for date/time, clubs, shot count and average carry
- Bag mapping across retained shots with stock/average/median carry, typical range, consistency, dispersion and launch metrics
- Persistent optional stock-carry overrides for every club
- Wedge matrix for half, three-quarter and full swings, beginning at half swing by default
- Two-step shot deletion from the live session or any saved session
- Dedicated navigation for Range, Practice, Bag Mapping, Wedge Matrix, Golf Bag and Sessions
- Unified Practice hub containing Target Practice, Distance Ladder, Golf Combine, Approach Practice, Fairway Finder, Skills Challenge and Pressure Streak
- Distance ladder practice with configurable start, finish, step and advancement tolerance
- Practice target, proximity and score stored with each shot
- Guided wedge matrix that automatically selects each wedge and half, three-quarter and full swing
- Per-club dispersion chart plotted against the club's stock or median carry
- Guided club-by-club bag mapping with selectable clubs and accepted-shot goals
- Automatic carry-gap warnings, confidence labels and distance-based club suggestions
- One-click promotion of recorded medians to persistent stock carries
- Session names and session-type classification
- Complete versioned JSON backup and confirmed restore for moving installations
- CSV export of every stored shot for spreadsheet analysis
- Suggested review labels for unusually long or short shots without automatically deleting them
- Wedge swing-length selection moved out of the global toolbar and into the Wedge Matrix section
- Range graphic marked every 25 yards from 25 through 300, with emphasized 50-yard lines and labels on both sides
- Saved **Keep screen awake** option backed by the Windows display-request API while the monitor is connected
- ShotForge-style Golf Combine with Quick/Full formats, a fixed 55-yard-through-Driver sequence, 0–100 carry scoring and bag-mapped club recommendations
- Local PowerShell build/test script plus an optional Windows GitHub Actions workflow
- Dependency-free Core checks for connection states, shot validation, duplicates, calibration metadata, practice scoring and flight vectors
- Reusable `RangeGraphic` Razor component visible while using Practice, Bag Mapping and Wedge Matrix
- Select All and Deselect All controls for guided bag mapping
- Explicit in-app explanation of complete JSON backup coverage and derived analytics restoration
- Compact last-shot measurements beneath the tracer in Practice, Bag Mapping and Wedge Matrix
- Practice action layout that keeps **End practice** in a separate aligned row
- Local golfer profiles with separate bags, sessions, wedge matrices, combines, and analytics
- Automatic migration of existing single-golfer data into the first profile
- Profile-aware JSON backup/restore and CSV export, with compatibility for version 1 backups
- Guided wedge matrix starts from the wedge and swing length selected by the golfer
- Saved sessions visibly identify their golfer, with corrected sticky-header scrolling in expanded shot tables
- Fresh activity-specific sessions for Range, Bag Mapping, Wedge Matrix, Target Practice, Distance Ladder, and Golf Combine
- Two-step whole-session deletion with clear warnings that derived analytics will be recalculated
- Dedicated golfer-specific Analytics tab with club, date-range, and wedge-swing filters
- Carry trends, recent-versus-prior feedback, dispersion, confidence, and session comparisons
- Fairway Finder club selection, stable Bag Mapping recommendations, fairways-hit rate, lateral spread, and a live shot-pattern view
- Analytics summaries for total distance, rollout, directional tendency, launch, spin, and apex
- Reversible shot exclusion from analytics without deleting the original session record
- Exclusion state included in complete JSON backups and identified in CSV exports
- Analytics implemented as an independent Razor component to begin splitting page responsibilities
- Home-page behavior moved into a partial C# coordinator instead of an oversized inline Razor code block
- Dedicated components for the app toolbar, navigation, Range, Sessions, Golf Bag, Golfer, Practice, Bag Mapping and Wedge Matrix screens
- One shared shot-history table for live and saved sessions, keeping delete/exclude behavior consistent
- One shared activity workspace for Practice, Bag Mapping and Wedge Matrix range/tracer layouts
- One reusable live-updating range control across every Practice setup
- Mode-specific Practice setup components plus separate live-score and Fairway Finder plot components
- Core-owned analytics dashboard projections and Bag Mapping/Wedge Matrix guide state machines
- App services for launch-monitor workspace state and activity/session lifecycle coordination
- Independent setup values for every Practice mode, so shared concepts such as shot count and scoring radius no longer leak between modes
- Selective Bag Mapping and Wedge Matrix restarts that replace calibration shots only for the chosen clubs or wedges
- Wedge Matrix Select All, Deselect All, and per-wedge selection before a guided run

## Application structure

- `Home.razor` composes the current screen and owns activity switching.
- `Home.razor.cs` connects launch-monitor events and view switching while dedicated App services own monitor side effects and activity/session lifecycle.
- Page components own the markup for Range, Sessions, Golf Bag, Golfers and Analytics.
- Shared components own the toolbar, navigation, activity workspace, range graphic and shot-history table.
- Storage, backup, profile and launch-monitor implementations remain behind services and interfaces.

This is a prototype build. Session and backup compatibility with earlier builds is intentionally not guaranteed while the data model is still changing.

### v0.24.6 wedge matrix setup cleanup

- Removes the redundant manual wedge and swing selection controls from Wedge Matrix setup.
- Uses the selected-wedge checklist as the single setup path; the guide chooses the current wedge and advances through half, three-quarter, and full swings automatically.

### v0.24.5 independent practice setup and selective calibration restarts

- Gives every Practice mode its own setup state so moving a slider in one mode does not alter another mode.
- Adds a Core regression check covering independent shot-count and tolerance values.
- Makes Bag Mapping's selected-club workflow an explicit redo: starting replaces older Bag Mapping shots only for the selected clubs.
- Adds Select All, Deselect All, and individual wedge selection to Wedge Matrix.
- Makes a selected Wedge Matrix run begin at half swing and replace older matrix shots only for the selected wedges.
- Keeps all unselected Bag Mapping clubs and Wedge Matrix wedges unchanged.

### v0.24.4 complete-check correction and directional flight symmetry

- Updates protocol-check calibration fixtures to use dedicated Bag Mapping and Wedge Matrix sessions, matching the production analytics separation rules.
- Rotates the ball's spin axis with launch direction instead of leaving it fixed to the screen coordinate system.
- Restores equal radial carry for otherwise identical shots aimed in different directions and removes exaggerated lateral movement from off-line launches.
- Advances retained calculation metadata to `BallFlightCalculator/5`.

### v0.24.3 low-flight regression-check correction

- Corrects the stale protocol-check ceiling that rejected the calibrated 47.5-yard rollout of the established low-flight sample.
- Tests the intended behavior directly: the sample must clear the former 55%-of-carry rollout cap by a meaningful margin while remaining below a conservative 55-yard sanity ceiling.
- Leaves the validated `BallFlightCalculator/4` calculation unchanged.

### v0.24.2 menu placement and demo control

- Opens the navigation drawer from the right edge beside the Menu button.
- Moves Demo mode's Simulate shot action into the persistent header so it remains visible while Settings is closed.
- Keeps Simulate shot disabled until the mock monitor is connected.

### v0.24.1 navigation refinement

- Replaces the full-width workspace picker with a left-side menu drawer that leaves the current screen visible behind it.
- Groups destinations into Practice, Review, and Manage sections with large projector-friendly rows and a clear current-mode highlight.
- Removes the software-oriented “workspace” language and unnecessary menu instructions.
- Moves secondary golfer, monitor, session, display, and ready-chime settings into the bottom of the menu.
- Shows the selected Practice mode in the compact header, such as `Practice · Distance Ladder`.
- Supports closing the drawer with its close button or by clicking the dimmed screen behind it.

### v0.24.0 mode-focused projector workspace

- Replaces the permanently expanded navigation and toolbar with a compact header showing the active workspace, connection/readiness state, battery, club used, and essential connection controls.
- Moves workspace navigation behind a large on-demand Menu and secondary application controls behind Settings.
- Gives Range, Practice, Bag Mapping, and Wedge Matrix substantially more space for their tracer and mode-specific information.
- Separates setup and active states so configuration choices disappear once a guided activity begins, leaving its target, progress, score, result, and stop action prominent.
- Collapses the Range session shot table until requested and moves the Wedge Matrix results table to a full-width surface.
- Raises the base typography, secondary-text contrast, measurement sizes, table text, and tracer yardage labels for projector readability.
- Uses wider desktop layouts while retaining responsive stacked layouts on smaller windows.

### v0.23.4 low-flight and ground-roll calibration

- Leaves the validated airborne carry calculation unchanged.
- Blends rollout smoothly by landing angle so thin shots and low skimmers are no longer limited to 55% of carry.
- Uses measured or estimated backspin, rather than total spin, to determine forward rollout so sidespin does not incorrectly suppress distance.
- Adds bounded speed-dependent resistance for fully ground-running shots instead of applying one coefficient to every worm burner.
- Advances retained calculation metadata to `BallFlightCalculator/4` and adds regression coverage for normal worm burners, faster and slower ground runners, and a low-launch wedge skimmer.

### v0.23.3 analytics club identity correction

- Persists the default golf bag immediately so its club identifiers remain stable across app restarts and build updates.
- Lets Analytics, Bag Mapping, Wedge Matrix, and recommendation queries fall back to the retained club label when a saved shot contains an older club identifier.
- Restores existing session analytics without deleting, editing, or re-recording the affected shots.
- Opens Analytics on the first club that actually has matching performance data instead of defaulting to an empty club.
- Adds regression coverage for summaries, session comparisons, and trend points spanning regenerated club identifiers.

### v0.23.2 flight-model calibration

- Carry now uses the full horizontal distance to the landing point, including lateral travel, matching the Square export definition.
- Removed the temporary 2% carry boost after multi-club comparison showed essentially zero overall bias without it.
- Added a separate ground-roll calculation for non-airborne shots such as worm burners.
- Added club- and launch-aware spin estimation when measured spin is unavailable, with measured versus estimated provenance retained, displayed, and exported.
- Preserved measured topspin direction so topped shots receive downward rather than upward aerodynamic lift.
- Advanced the recorded flight model identifier to `BallFlightCalculator/3`.
- Retained the existing lateral-flight coefficients: wider scaling reduced average directional bias but made individual offline errors materially worse in the supplied comparison set.

## Recovery release notes

### v0.14 architecture

- Split Practice, Bag Mapping and Wedge Matrix markup into dedicated Razor components.
- Moved practice scoring, activity progression and shot-quality rules into deterministic Core services.
- Added `LaunchMonitorCoordinator` for the validated shot-processing pipeline.
- Added `ARCHITECTURE.md` and kept stored v0.13 session fields backward compatible.

### v0.15 Practice

- Folded Target Practice, Distance Ladder and Golf Combine into one practice hub and added the modes that later evolved into Approach Practice, Fairway Finder, Skills Challenge and Pressure Streak.
- Every game creates an activity-specific session and stores its settings snapshot with the session and every scored shot.
- Added automatic club/swing recommendations, end-game summaries, golfer-specific personal bests, history labels, CSV fields and backup coverage.
- Wedge Roulette uses the golfer's mapped ½, ¾ and full wedge carries. Quarter swing is no longer offered for new shots.

### v0.16 reliability and calibration foundation

- Added automatic Square reconnect, connection state/health reporting and restoration of notifications, heartbeat, selected club and ball detection.
- Added physical-range validation, rejection reasons, app-level duplicate detection and visible acceptance diagnostics.
- Added flight-model, calculation-profile, environment/reference-calibration and separate personal-adjustment metadata to retained shots.
- Added atomic session persistence with last-known-good recovery and a dependency-free Core reliability check project.
- Full app/Windows restart resume is intentionally not part of v0.16; recorded shots remain durable, while only a Bluetooth reconnect preserves the currently running activity.

### v0.16.1 interface and session fixes

- Restored readable spacing between Range measurement labels, values, and units.
- Fixed initial wedge swing selection and bag-mapping guidance text after the component split.
- Changed the guided bag-mapping default to five accepted shots per club while keeping it adjustable.
- Renamed the navigation and stop action to **Practice** and **End Practice**.
- Empty practice sessions are kept only in memory and discarded unless at least one accepted shot is recorded.

### v0.16.2 practice recommendations

- Practice club recommendations now use the same effective carry values displayed in Bag Mapping.
- Bag Mapping's full-swing-only wedge calculation is therefore also used by general practice recommendations.
- Wedge Roulette remains based on the swing-specific Wedge Matrix because it recommends both a wedge and a swing length.

### v0.17 approach practice and architecture

- Replaced Random Approach with Approach Practice: choose a target distance, shot count, and green radius; every shot finishing on the green scores one point and increments greens hit.
- Replaced Random Approach and removed Wedge Roulette from selectable practice modes while reserving both original enum values for saved-session and backup compatibility.
- Restored quarter swing as the initial Wedge Matrix selection and as the first step in guided matrix runs.
- Split the Home coordinator into focused activity, session, and profile/backup partials.
- Moved the selectable practice catalog, approach scoring, bag recommendations, carry-gap calculation, carry promotion, dispersion selection, and completed-practice queries into Core services.

### v0.18 Combine and wedge matrix

- Rebuilt Golf Combine around the ShotForge setup: Quick is 3 shots at each of 12 targets (36 total), while Full is two passes of 3 shots at each target (72 total).
- Uses the fixed sequence 55, 70, 85, 95, 105, 115, 130, 145, 160, 170, 180 and Driver. Numeric targets recommend the nearest bag-mapped club; Driver uses the mapped Driver carry.
- Each shot receives a 0–100 carry score from target-distance error and a 1.25× lateral penalty. Driver receives a wider scoring area.
- Removed quarter swing from Wedge Matrix entry, guided runs, table columns and analytics filters. Half swing is now the first/default matrix swing.
- Removed compatibility checks for retired practice modes; prototype session compatibility is not guaranteed.

### v0.18.1 optional bag mapping

- Golf Combine, Fairway Finder, and Skills Challenge can start with no mapped clubs or stock carries.
- Bag Mapping values are used only to show an optional recommended club in these modes.
- Combine uses its fixed distance sequence and a configurable Driver scoring target whether or not a bag has been mapped.

### v0.18.2 adjustable Combine Driver target

- Added an editable Combine Driver target from 100 to 350 yards, defaulting to 250 yards.
- Driver carry is deliberately forgiving: the full-distance scoring band begins 25 yards short of the selected target, and extra carry beyond the target is never penalized.
- Driver offline error still affects the 0–100 score using the wider Driver scoring area.

### v0.18.3 Approach distance modes

- Added Carry and Total modes to Approach Practice.
- Carry mode scores the ball's landing position; Total mode includes rollout and scores its final position.
- Both modes award one point when the applicable shot position is on the configured circular green, track greens hit, and save the selected mode with the session.
- Replaced the technical setup explanation with plain-language landing/finish wording.

### v0.18.4 dedicated calibration datasets and reports

- Bag Mapping now calculates only from dedicated Bag Mapping sessions; Range, Practice, and Wedge Matrix shots cannot change its club numbers.
- Wedge Matrix now calculates only from dedicated Wedge Matrix sessions; full wedge shots from other activities cannot change its cells.
- Main Analytics uses Range and Practice performance sessions while excluding both calibration datasets.
- Practice recommendations combine full-club Bag Mapping carries with half, three-quarter, and full Wedge Matrix carries. Wedge-distance targets can now recommend both a wedge and swing length.
- Added simple standalone printable HTML tables for Bag Mapping and Wedge Matrix under `Documents\Golf Grind`.
- Confirmed session deletion now shows a contextual toast for about three seconds instead of leaving a permanent status message.

### v0.18.4.1 build correction

- Replaced the printable-report interpolated raw string with a tokenized HTML template so CSS braces compile normally.
- Made the stateless Bag Mapping and Wedge Matrix export operations static, resolving both CA1822 analyzer messages.

### v0.18.4.2 build correction

- Added the missing printable-report service namespace to the activity coordinator.
- Returned the asynchronous shot-deletion task from its event callback, resolving CS4014.

### v0.18.4.3 analyzer cleanup

- Enabled unsafe blocks for the generated CsWinRT collection interop code required by the Windows target.
- Simplified the guided Bag Mapping sequence initialization to a collection expression, resolving IDE0305.

### v0.19 Skills Challenge

- Replaced the fixed 75, 150, and 225-yard Skills Assessment with a configurable Skills Challenge.
- Supports 5–20 randomized stations, 20–280 yard minimums, 50–350 yard maximums, and carry or total-distance scoring.
- Added a 0.5×–2.0× margin-of-miss control that scales distance-based target rings.
- Each station scores 100, 75, 50, 25, or 0 based on the ring reached; the completed challenge records the average out of 100.
- Club and wedge-swing recommendations remain optional and come only from dedicated Bag Mapping and Wedge Matrix data.

### v0.19.1 live Practice sliders

- Replaced numeric entry fields throughout every Practice mode with consistent labeled sliders.
- Slider values update continuously while the control moves instead of waiting until it is released.
- Preserved the existing setting ranges, units, clamping, and saved session snapshots.

### v0.19.1.1 slider endpoint correction

- Removed inherited text-input padding, borders, and background from Practice range controls so their tracks reach both available edges.
- Preserved continuous value updates and the native slider thumb behavior.

### v0.19.1.2 Skills Challenge scoring clarity

- Separated the live target distance from its scoring details so the requested shot remains prominent.
- The live station now identifies carry or total scoring, the current scoring radius, and that the radius is automatically sized for the distance.
- Added a plain-language setup explanation covering distance-scaled circles, the margin control, and the 100/75/50/25/0 scoring bands.

### v0.19.2 streamlined Wedge Matrix

- Removed retained-shot counts from the on-screen Wedge Matrix cells.
- Removed retained-shot counts from the printable Wedge Matrix HTML table.
- Wedge Matrix medians still use all retained dedicated calibration shots; guided-run progress and configurable shots per combination are unchanged.

### v0.19.3 streamlined Bag Mapping

- Removed the exact retained-shot count from the on-screen Bag Mapping table.
- Removed the Shots column from the printable Bag Mapping HTML table.
- Retained shots and their internal count still support medians, confidence, gap analysis, dispersion, and practice recommendations; active guided-run progress is unchanged.

### v0.19.4 golf-term help

- Added a reusable tooltip treatment with plain-language descriptions on hover or keyboard focus, plus native tooltip fallback.
- Explained every statistical and shot-measurement heading in Bag Mapping, Wedge Matrix, shot history, Analytics summaries, and session comparisons.
- Added help for Last Shot measurements, golf-bag loft and stock carry, and less-obvious Practice controls such as scoring radius, green radius, fairway width, minimum carry, and margin of miss.
- Defines Consistency as carry standard deviation: lower values mean the retained carries are more tightly grouped and repeatable.

### v0.19.4.1 tooltip cleanup

- Removed the browser-native tooltip fallback so each term now shows only the styled in-app tooltip.
- Removed term tooltips from the Golf Bag editor.
- Consolidated the Skills Challenge distance-scaling and difficulty explanation into the Margin of Miss tooltip.
- Kept the visible Skills Challenge information card focused on the scoring radius and 100/75/50/25/0 bands.

### v0.19.5 report export removal

- Removed the Bag Mapping and Wedge Matrix printable HTML export buttons and supporting report-generation service.
- Bag Mapping and Wedge Matrix recording, calculations, recommendations, and stored calibration data are unchanged.
- Reporting can be reconsidered later without carrying forward the prototype HTML format.

### v0.20 Fairway Finder and Analytics

- Fairway Finder now lets the golfer select any non-putter club or use a stable automatic recommendation based on Bag Mapping.
- Added live fairways hit, hit percentage, average directional finish, lateral spread, and a shot-pattern plot.
- Fairway Finder personal bests now use fairway percentage rather than accumulated points.
- Expanded the Analytics summary with average total distance, rollout, offline tendency, launch, spin, and apex.

### v0.21 UI and coordinator cleanup

- Replaced repeated Practice range markup with one reusable live-updating slider component.
- Split Practice mode setup, live scoring and Fairway Finder plotting into focused Razor components; the main Practice panel is now only the composition layer.
- Moved analytics filtering, confidence, trend comparison, dispersion selection and session-row projections into Core.
- Moved Bag Mapping and Wedge Matrix run progression into deterministic Core state machines.
- Added focused App coordinators for activity/session lifecycle and launch-monitor connection, processing and display-wake side effects.
- Reduced `Home` to view state, event wiring and user-facing formatting while retaining the existing behavior and data-separation rules.

### v0.21.1 club selection and Range autosave

- Practice recommendations no longer change the toolbar club. The golfer's selected club remains authoritative for the shot actually recorded.
- A recommended wedge swing is applied to the recorded shot only when the golfer has selected the recommended club.
- Removed the Range **Save session details** button; name and type changes now save automatically, while empty-session details are retained in memory until the first shot triggers persistence.

### v0.21.2 session renaming

- Removed session-name editing from Range.
- Added inline session renaming to the Sessions tab; a name is saved when the golfer leaves the field or presses Enter.
- Kept recommended wedge swings separate from the recorded swing unless the golfer selects the recommended club.

### v0.21.3 recommendation-only wedge swings

- Practice recommendations never set the recorded shot's swing type.
- The recorded club always comes from the golfer's toolbar selection.
- Recommended clubs and wedge swings remain visible and are retained only in the practice-result snapshot for comparison.

### v0.21.4 session UI cleanup

- Session names on the Sessions tab are displayed as clean title text and become editable when clicked.
- Renames save on Enter or when the field loses focus; Escape cancels the edit.
- Renamed the Range shot-history heading to **Range session** and removed the obsolete session-type dropdown and its coordinator plumbing.

### v0.22 perspective shot tracer

- Replaced the top-down range plot with a lightweight behind-the-ball perspective view.
- The shared tracer now uses the calculated downrange, offline, and height samples to animate the ball through the air while revealing its tracer.
- The same visual is used by Range, every Practice mode, Bag Mapping, and Wedge Matrix through the existing shared workspace.
- Added perspective yard markers, fairway edges, a landing marker, and automatic animation restart for each accepted shot without introducing a WebGL dependency.

### v0.22.1 tracer sizing and animation

- Removed the 520-pixel maximum so the range graphic expands to the available desktop window height.
- Replaced SVG-native motion with a WebView-safe frame animation that moves the ball along the calculated path while revealing the tracer behind it.
- New shots cancel and replace an animation already in progress; reduced-motion settings show the completed path immediately.

### v0.22.2 tracer framing and playback

- Restored the original tracer-card sizing.
- Moved the perspective horizon near the top of the graphic so the range uses the previously empty sky band while retaining a small amount of visual separation.
- Accelerated tracer playback to roughly 40% of calculated flight time, capped between 0.9 and 2.5 seconds.

### v0.22.3 Golf Grind rename

- Applied the **Golf Grind** product name throughout the UI, window title, Windows manifest, backup prompts, diagnostics, and documentation.
- Aligned the solution, projects, assemblies, namespaces, project references, build script, and workflow under the `GolfGrind` name.
- Changed the Windows application ID to `com.local.golfgrind` and the user-facing Documents folder to `Golf Grind`; backward compatibility with the previous app identity is intentionally not retained.

### v0.22.4 explicit local data storage

- Moved all internal state to `%LOCALAPPDATA%\GolfGrind` instead of MAUI's application-ID-based data and preferences locations.
- Golfer profiles, app settings, remembered Square device details, golf bags, and sessions now use atomic JSON files with last-known-good recovery.
- Kept user-facing backups, CSV exports, and packet diagnostics under `Documents\Golf Grind`.
- This build intentionally starts clean in the new location and does not migrate data from earlier application directories.

### v0.22.5 tracer views and distance labels

- Removed the launch-monitor subtitle beneath the Golf Grind brand.
- Added an animated side-on flight view to the shared tracer, with equally spaced 50-yard intervals.
- Tightened the perspective view's near-field spacing so longer shots remain easier to read.
- Labeled every major interval in both views with its yardage.

### v0.22.6 tracer label and side-scale correction

- Reworked both tracers' yardage labels as direct, high-contrast SVG text so they render reliably in the Windows WebView.
- Changed the side tracer to use the same pixels-per-yard scale vertically and horizontally instead of stretching each shot to fill the available height.
- A shot's rendered apex is now proportional to its downrange distance, so lower trajectories no longer appear artificially tall.
- Added a regression check confirming that measured launch angle and direction materially change the calculated path rendered by the tracers.

### v0.22.7 Razor SVG build fix

- Moved SVG label attributes onto nested `tspan` elements because Razor reserves the outer `text` element and rejects attributes on it.
- Preserved the explicit positioning, high-contrast label styling, proportional side-view scale, and measurement-driven flight path from v0.22.6.

### v0.22.8 WebView yardage-label fix

- Replaced SVG `text`/`tspan` labels with compact HTML labels embedded through SVG `foreignObject` elements.
- This bypasses Razor's reserved `text` element and the Windows WebView's failure to paint orphaned SVG `tspan` elements.
- Both perspective and side views now use high-contrast label backgrounds at every 50-yard interval.

### v0.22.9 tracer label presentation

- Removed the dark backgrounds from yardage labels in both tracer views.
- Side-view labels are now bold, opaque white text.
- Perspective labels now appear once in the center of each 50-yard line as translucent white text, with the opaque tracer rendered over them.

### v0.23.0 ball-detection recovery

- Added independent freshness tracking for the monitor's ball-state telemetry; ordinary heartbeat and auxiliary packets no longer conceal a stalled detector.
- When ball-state packets stop for 12 seconds, GolfGrind clears stale readiness and re-arms detection. If telemetry remains absent for another 12 seconds, it reconnects automatically while preserving the activity.
- Serialized notification processing so delayed packets cannot race and overwrite a newer ready/not-ready state.
- A connection now accepts ball-ready only after GolfGrind enables detection and observes a fresh no-ball/detected-to-ready cycle.
- Tightened ready parsing to the captured `01/01` green-ready state and added protocol and recovery regression checks.
- Added an `Export CSV` action to each saved session so a single GolfGrind session can be shared independently.

### v0.23.1 conservative flight calibration and ready chime

- Applies an isolated 2% carry calibration based on the clean nine-iron comparison set and records new shots as `BallFlightCalculator/2`.
- Scales the tracer's down-range coordinates with carry so the visual landing point remains aligned with the displayed result.
- Makes rollout responsive to low-launch shots without the previous 18%-of-carry ceiling; the deliberately conservative calibration can be retuned after multi-club testing.
- Plays one short chime only when the active monitor transitions into a confirmed ball-ready state.
- Adds a persistent `Ready chime` toggle to the toolbar, enabled by default.

### Build fix in v0.2.1

- Added the missing Windows `MauiWinUIApplication` entry point.
- Added explicit .NET 10.0.20 package references for MAUI Controls and Blazor WebView.
- Removed the optional debug-logging extension that required an additional package.
- Added the application icon resource expected by the Windows MAUI build.

### Runtime fix in v0.2.2

- Declared the Razor-component XAML namespace on the root `ContentPage`.
- Corrected the root component type from `local:Components.Routes` to `local:Routes`.

### Packet diagnostics in v0.2.3

- Captures every Bluetooth packet sent to and received from the Square.
- Creates a timestamped log under `Documents\Golf Grind` for each connection.
- Shows the full log path directly below the connection toolbar.

Hit several shots, close the app, and share that `.log` file when investigating firmware packets that are not decoded yet.

## First hardware test

1. In Windows Settings, pair the Square Golf monitor over Bluetooth first.
2. Fully close the official Square app, GSPro connector, and other software that may be using the monitor.
3. Turn on the Square and press its Bluetooth button if needed.
4. Run this app, leave `Square Bluetooth` selected, and click **Connect**.
5. Place a ball in the hitting zone. The status should change to **Ball ready**.
6. Select a club and hit a shot.

The direct BLE message supplies measured launch conditions. The app's independent flight model uses those measurements to estimate carry, total distance, offline distance, apex, flight time and the tracer under standard sea-level, no-wind conditions with normal-fairway rollout. Derived values are labeled as estimates in the UI and raw Square measurements remain unchanged.

Square's known club codes do not expose a distinct hybrid or loft-by-loft wedge choice. The current mapping sends `5 Hybrid` as a 5-iron category, `GW` as approach wedge, and both lofted wedges as sand wedge, while retaining your selected label in the saved shot.

## Requirements on Windows

Use Visual Studio 2026 with the .NET MAUI workload, or VS Code with a .NET 10 SDK plus the MAUI tooling/workloads installed.

From a Developer PowerShell / terminal in this folder:

```powershell
dotnet workload restore
dotnet restore
dotnet build GolfGrind.App/GolfGrind.App.csproj -f net10.0-windows10.0.19041.0
```

Or run the included complete verification command:

```powershell
./build.ps1
```

Then run from Visual Studio, VS Code, or with the appropriate Windows target selected.

The dependency-free protocol and Core reliability checks can be run on any platform with .NET 10:

```powershell
dotnet run --project GolfGrind.ProtocolChecks/GolfGrind.ProtocolChecks.csproj
dotnet run --project GolfGrind.CoreChecks/GolfGrind.CoreChecks.csproj
```

## Troubleshooting

- If the monitor is not found, remove and pair it again in Windows Bluetooth settings.
- If characteristics cannot be discovered, close any program already connected to the Square and power-cycle the monitor.
- Club-data requests are disabled for now; measured ball data is processed and displayed immediately.
- Powering off the Square while connected starts automatic reconnect attempts after 2, 4, 8 and 15 seconds. The current in-memory game or guided activity remains active.

## Ball-flight model

- Flight is integrated in three dimensions with gravity, aerodynamic drag and spin-derived lift/curve.
- Rollout uses landing speed, descent angle and spin under a normal-fairway assumption.
- Initial coefficients are intended for hardware comparison and calibration, not as a claim of matching Square's proprietary flight model exactly.

## Display and range

- The perspective tracer includes 25-yard intervals through 300 yards, with labeled 50-yard major lines. Its selectable side view uses equally spaced, labeled 50-yard intervals and automatically extends for longer shots.
- **Keep screen awake** is enabled by default and saved between launches. Windows is asked to suppress display sleep and the screensaver only while the selected launch monitor is connected.
- Disconnecting, disabling the option or closing the page immediately releases the display request. The app does not permanently modify Windows power-plan settings.

## Golf bag

Open **Golf Bag** to add or remove clubs, edit their names and types, and enter lofts in half-degree increments. Choose **Save bag** to retain the setup under `%LOCALAPPDATA%\GolfGrind`. The included starter bag uses PW 43°, GW 48°, plus 54° and 58° wedges; all of these can be changed.

## Session storage

- A fresh session starts whenever the app launches or **New session** is selected.
- Every shot is saved immediately under `%LOCALAPPDATA%\GolfGrind\profiles\<golfer-id>\sessions`; no explicit save step is required.
- **Sessions** shows all recorded sessions with their date, clubs used, shot count and average carry; **View shots** expands the complete shot table for any session.
- Stored shots retain measured and calculated metrics but omit the dense tracer-point collection to keep files compact.
- Storage uses dependency-free JSON files for this version. The session model is isolated so it can be moved to SQLite later without changing Bluetooth or flight calculation code.
- Session type is assigned automatically from the activity. A saved session can be renamed by clicking its title on the **Sessions** tab.

## Bag mapping and wedge matrix

- Open **Bag Mapping** to calculate per-club numbers from dedicated Bag Mapping sessions. Wedges use only shots tagged as full swings in this view.
- Bag Mapping has its own tab, live tracer and Select All/Deselect All controls so the Golf Bag editor no longer consumes its working space.
- Guided bag mapping lets you select the clubs and accepted shots per club. It changes the selected Square club automatically and advances when each goal is reached.
- The gap column highlights overlaps below five yards and large gaps above twenty yards. Confidence is based on retained sample count.
- Enter a desired distance to see the nearest club by stock carry, or use **Use medians as stock carries** after completing a mapping session.
- The typical carry range is the middle 60% of retained carries, which is less sensitive to an occasional unusually good or poor result than a simple minimum/maximum.
- Consistency is carry standard deviation; a smaller value means the recorded carries are more tightly grouped.
- Use the bag editor to enter an optional stock carry. If it is blank, bag mapping uses the recorded median.
- When a wedge is selected, choose **½ swing**, **¾ swing**, or **Full swing** before hitting. Half swing is selected initially, and the selection is stored with the shot.
- Swing length is intentionally available only within **Wedge Matrix**. Shots recorded from Range, Practice or Bag Mapping are tagged as full swings.
- Open **Wedge Matrix** to see median carry for every wedge/swing combination. The guided matrix automatically walks through every saved wedge and swing length for the selected number of shots.
- **Delete** changes to **Confirm** before removing a shot. A deletion updates only the dedicated dataset associated with that session. Confirmed whole-session deletion displays a brief status toast.
- A **Review** label identifies a carry more than 15 yards or 2.5 standard deviations from that club's median once at least five shots exist. It is advisory; the app never deletes a shot automatically.

## Practice

- **Target practice** scores radial proximity using carry error and offline distance. Shots inside the configured radius score 10 points, followed by 7, 4 and 1 point bands; shots outside four times the radius score zero.
- **Target Practice** repeats one fixed target for the chosen shot count.
- **Distance ladder** begins at the chosen start distance and advances by the configured step whenever carry finishes within the advancement tolerance. It ends after the finish distance is completed.
- Target distance, radial proximity and score are stored with each shot and appear in the current and saved-session tables.
- **Golf Combine** offers Quick (36-shot) and Full (72-shot, two-pass) formats over 55, 70, 85, 95, 105, 115, 130, 145, 160, 170, 180 yards and Driver.
- Every Combine shot is scored 0–100 from carry-distance error and offline error with a 1.25× lateral penalty. The Driver benchmark is editable; its full-distance band starts 25 yards short, longer drives are not penalized, and lateral accuracy still uses a wider scoring area. Combine summaries and personal bests use the session's average shot score.
- **Approach Practice** uses a chosen target distance and green radius. Carry mode scores where the ball lands; Total mode includes rollout and scores where it finishes. Either mode awards one point when that position is on the green, and the session tracks greens hit.
- **Fairway Finder** scores any selected non-putter club against configurable fairway width and minimum carry, or uses Bag Mapping to recommend a long-game club automatically.
- **Skills Challenge** creates randomized target stations across a configurable distance range and scores each shot 0–100 using distance-scaled target rings.
- **Pressure Streak** continues until the configured number of misses and records the best hit streak.
- Each practice mode creates its own session after the first accepted shot, retains its exact settings with every shot, produces an end summary and contributes to golfer-specific personal bests. Shots can still be excluded or deleted later.

## Backup and transfer

- Open **Sessions** and choose **Export complete JSON backup** to write one portable file under `Documents\Golf Grind`.
- The backup contains the complete saved bag—including IDs, lofts and stock carries—plus every session and retained shot.
- Bag Mapping and Wedge Matrix values are derived from retained shots and are reconstructed after restore. The app now explains this directly beside the backup controls.
- Temporary progress in an unfinished guided mapping or wedge run is not included; every shot already recorded before interruption is included.
- Choose **Import JSON backup** on another installation, review the reported club/session counts, and explicitly confirm the restore. Nothing is replaced before confirmation.
- Backups carry a format version and are validated for a valid bag and unique club/session identifiers before the confirmation is offered.
- **Export shots to CSV** creates a flat spreadsheet-friendly history in the same Documents folder; CSV is for analysis, while JSON is the restorable backup.

## Protocol status

The BLE integration is an early community-protocol implementation, not an official Square SDK integration. It uses the known command and notification characteristics and keeps parsing isolated in `SquareProtocol.cs` so captured packets can be tested and adjusted without rewriting the UI or Windows Bluetooth service.
