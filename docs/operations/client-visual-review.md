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

## Themes and motion

Dark is the product default. Light and system themes use the same semantic palette and can be applied immediately from Settings; saving persists the preference. Pages use theme resources for canvases, surfaces, overlays, text, borders, editors, and status colors. Optional transitions must not be required to understand application state. The reduced-motion preference is available to visual-review and settings flows; the production preview currently uses no essential animated transitions.

## Keyboard and accessibility checklist

- Walk onboarding, login, shell navigation, operational tables, editors, Problem Studio navigation, dialogs, and Settings using Tab, Shift+Tab, Enter, Space, arrow keys, and Escape.
- Confirm a visible focus indicator and a route out of multiline editors.
- Confirm dialog focus returns to the initiating command.
- Give icon-only controls accessible names and exclude decorative icons from the accessibility tree.
- Announce loading, errors, status changes, and empty results politely without relying only on color.
- Explain candidate warning/termination, hidden-content denial, and authoring lock states in text.
- Check 125%, 150%, and 200% text scaling for wrapping and command reachability.

This checklist supports inspection; it is not a claim that a particular screen reader has been runtime-tested. WebAssembly remains dependent on the `wasm-tools` workload and is not covered by the desktop review harness.
