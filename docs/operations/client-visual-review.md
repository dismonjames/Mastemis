# Desktop visual review

The desktop review harness is isolated from production startup. It is disabled by default and fails closed unless the process explicitly sets `MASTEMIS_ENABLE_VISUAL_REVIEW=1`. It uses the reserved `visual-review.invalid` server identity, deterministic role and state descriptors, and never loads production credentials or data.

Example:

```bash
MASTEMIS_ENABLE_VISUAL_REVIEW=1 \
  src/Client/bin/Release/net10.0-desktop/Mastemis.Client \
  --visual-review problem-studio-statements \
  --state populated --role ProblemOwner \
  --width 1024 --height 768 --theme dark
```

Supported review options include `--state`, `--role`, `--theme dark|light`, `--text-scale 1..2`, and `--reduced-motion`. Scenario aliases cover onboarding, role dashboards, operations, candidate views, invigilation, every Problem Studio section, worker health, settings, and About. The catalog and parser are tested in `Client.Tests`; fixture mode does not bypass normal authentication.

## Responsive model

Desktop layouts use three deterministic width classes. Wide layouts begin at an effective width of 1320 pixels, medium layouts at 980 pixels, and compact layouts below that. Effective width accounts for requested text scaling. Navigation compacts outside the wide class, authoring rails narrow without becoming horizontal tab strips, and candidate workspace side regions retain bounded widths so the editor remains usable.

The review sizes are 1600×900, 1366×768, 1024×768, and 900×700. Screenshots are temporary review artifacts and must not be committed unless a documentation change deliberately introduces reviewed assets.

## Deterministic fixtures

Every configured route resolves through `VisualFixtureRegistry`. Fixtures use a fixed UTC clock, bounded text, reserved identifiers, and policy-safe summaries. They never resolve production HTTP clients and contain no credentials, candidate source, hidden input, expected output, package content, or storage path. The fixture strip visible at the top of a review window identifies the exact route, role, state, theme, and representative data; it is created only after the environment gate succeeds.

Automated coverage verifies all route aliases, roles, and supported states. Review mode remains fail-closed when `MASTEMIS_ENABLE_VISUAL_REVIEW` is absent.

## X11 capture runner

The repository-local runner correlates a newly created X11 window with the launched client by rejecting pre-existing Mastemis window IDs, then verifies the exact `Mastemis.Client` class and `Mastemis` title. On KWin it temporarily enables Show Desktop before each launch, waits for compositor mapping, raises the client through `_NET_ACTIVE_WINDOW`, confirms the active-window identity, and verifies dimensions. Every review window contains a small magenta capture marker outside the meaningful accessibility tree. The runner validates that marker in the captured pixels, so an obscured client, desktop, or unrelated foreground window is a failed capture rather than a false success.

```bash
dotnet run --project tools/VisualReview/Mastemis.VisualReview.csproj -c Release --no-build -- \
  --root "$PWD" --route problem-studio-statements --state populated \
  --theme dark --size 1366x768 --output /tmp/mastemis-review
```

Use `--page-matrix` for all four sizes of one route, `--complete` for the full dark matrix, and `--light-matrix` for the representative light matrix. Additional inputs include `--role`, `--text-scale`, `--reduced-motion`, `--keyboard-smoke`, and a bounded `--timeout`. `matrix.json` records requested and actual dimensions, theme, state, process, window ID, activation, exit, and capture success. Generated PNG and JSON files belong in a temporary directory and are not committed.

KWin reserves desktop space for its panel. A request matching the physical 1600×900 display can therefore produce a maximized client near 1600×828; the runner records the actual size and accepts only this bounded work-area constraint. Other requested sizes must remain within the normal capture tolerance.

## Themes and motion

Dark is the product default. Light and system themes use the same semantic palette and can be applied immediately from Settings; saving persists the preference. Pages use theme resources for canvases, surfaces, overlays, text, borders, editors, and status colors. Optional transitions must not be required to understand application state. The reduced-motion preference is available to visual-review and settings flows; the production preview currently uses no essential animated transitions.

The runner's text-scale option increases the shared title, section, caption, and editor typography resources. Layout selection also divides effective width by the requested scale, so 125%, 150%, and 200% reviews exercise progressively more compact layouts. This is a deterministic desktop stress mechanism, not a claim that every Linux desktop accessibility stack exposes an identical OS text-scale signal.

## Keyboard and accessibility checklist

- Walk onboarding, login, shell navigation, operational tables, editors, Problem Studio navigation, dialogs, and Settings using Tab, Shift+Tab, Enter, Space, arrow keys, and Escape.
- Confirm a visible focus indicator and a route out of multiline editors.
- Confirm dialog focus returns to the initiating command.
- Give icon-only controls accessible names and exclude decorative icons from the accessibility tree.
- Announce loading, errors, status changes, and empty results politely without relying only on color.
- Explain candidate warning/termination, hidden-content denial, and authoring lock states in text.
- Check 125%, 150%, and 200% text scaling for wrapping and command reachability.

This checklist supports inspection; it is not a claim that a particular screen reader has been runtime-tested. WebAssembly remains dependent on the `wasm-tools` workload and is not covered by the desktop review harness.

When `libXtst` is present, `--keyboard-smoke` activates the verified application window and injects Tab, Shift+Tab, Enter, Space, Escape, and arrow-key input. It confirms delivery without a window switch or process failure. Semantic focus order and dialog focus restoration still require a human walkthrough; the capture runner does not infer accessibility-tree focus targets.
