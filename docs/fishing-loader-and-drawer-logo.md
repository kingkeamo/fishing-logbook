# Fishing loader and native drawer logo (#250)

Final polish pass for the MVP. It covers the branded fishing loader in Web/PWA and native MAUI,
the native navigation drawer logo, and a review of the native ruler and scales. The measurement
controls themselves are documented in `docs/mobile-measurement-controls.md`.

## Design source

The loader artwork is the `cdbf-spinners` spike (a separate local repo, not part of this one):
three designs, **leaping fish**, **boat on the drift** and **bobber**, with these brand colours.

| Token | Hex |
|---|---|
| Navy (ink) | `#0B1F3A` |
| Teal | `#0F766E` |
| Fish green | `#16A34A` |
| Water | `#38BDF8` |

The native loaders were already ported from the spike (`FishingLoaderView`, `LoaderDrawable`) but only
appeared on a DEBUG showcase screen. The PWA did not use them at all. This pass makes both clients
use them in real loading states.

`LoaderKindEnum` (`LeapingFish`, `Boat`, `Bobber`, `Random`) now lives in `FishingLogBook.Shared/Enums` so Web and
native name the designs identically.

### Random design

The default is `Random`: each time a loader appears it shows one of the three designs, so users do not always see
the same one. `LoaderKindResolver` (Shared) turns `Random` into a concrete design and passes a pinned design
through unchanged. The choice is made once per appearance, so the design never changes while a wait is on
screen. Pin a design with `Kind` if a screen should always show the same one.

- Web: chosen when the component is created, and again only if the caller changes `Kind`.
- Native: chosen when the view is created and again each time `IsLoading` becomes true.

## Web/PWA

`Components/FishingLoader/FishingLoader.razor` (three-file component). The SVG and the CSS keyframes
are the spike's, with the same timings (1.8 s, 1.6 s, 2.6 s).

| Parameter | Meaning |
|---|---|
| `Kind` | `LoaderKindEnum`, default `Random` (one of the three designs per appearance) |
| `Compact` | small inline size (1.75 rem) instead of the 6 rem page size |
| `Label` | accessible name; defaults to the localised `Common_Loading` ("Loading" / "Chargement") |
| `Class`, `id` and other attributes | passed through to the root element |

- `role="status"` with an `aria-label`; the artwork is `aria-hidden`.
- Fixed width and height, so it causes no layout shift. It does not block interaction.
- Light and dark: the "ink" parts use `var(--mud-palette-text-primary)`, which MudBlazor flips in dark mode.
  Brand green, teal and water are fixed mid-tones that read on both backgrounds. No colour is hard-coded
  to navy.
- Reduced motion: under `prefers-reduced-motion: reduce` every animation is off and each design rests in a
  recognisable still pose (fish mid-leap, boat on the plane, bobber at rest with no fish).
- Used where a page, modal or panel is genuinely loading (22 places: catch list, edit, view and record,
  offline catch pages, dashboard, profile, public profile, onboarding, trips, add-catches modal, location
  privacy modal, import start, install guidance). Each keeps its existing `id`, and the loading, error,
  retry, empty and progress states are unchanged and still separate.
- Not changed: small spinners inside buttons and inline rows (`Size.Small`), and the Import page's
  determinate-style processing bar, which is a progress state, not a loading state.
- Not changed: the pre-Blazor boot ring in `index.html`. It renders before any component can run, so it
  cannot use the Razor component. It is a candidate for a follow-up if you want the fish there too.

## Native MAUI

`FishingLoaderView` gained a one-property loading API:

```xml
<controls:FishingLoaderView IsLoading="{Binding IsLoading}"
                            SemanticProperties.Description="{Binding Text[LoaderLoading]}" />
```

- `IsLoading` shows the loader and starts it. When it becomes false the loader is hidden and its
  animation is stopped, so a hidden loader uses no CPU or battery.
- Screen readers: when `IsLoading` becomes true, `SemanticProperties.Description` is announced.
- Reduced motion: the loader reads `IMotionPreference` itself (Android `animator_duration_scale`, iOS
  `UIAccessibility.IsReduceMotionEnabled`), refreshes it when the app resumes, and shows the still pose.
  `ReduceMotion` stays as an explicit override.
- Pauses while the window is stopped, and follows live light/dark changes.

Wired into the page-level loading surfaces, replacing `ActivityIndicator`: the shared
`ContentStateView` (Catch summary and planned states), Dashboard, Trips (list, detail, edit, plan,
add-catches modal), Import, Startup (restoring the session), Authentication, the Onboarding gate,
Onboarding, Profile and the Record Catch editor. The error, retry, empty and offline states
(`ContentStateModel`) are unchanged.

Kept as `ActivityIndicator`: the small busy indicators inside controls (photo picker, wizard stepper
button, location capture, update check, import angler modal, trip participants row).

Existing time limits are unchanged and still bound each wait: API calls time out after 30 seconds and
session restoration after 30 seconds, after which the existing error and retry states show.

## Native drawer logo

- Larger: the logo is 80 dp high (was 48 dp), with more header padding and spacing so it does not crowd the
  avatar, name or email.
- Dark theme: `Resources/Images/brand_horizontal_dark.png` is generated from the existing
  `brand-horizontal-transparent.png`. It keeps the alpha channel (no opaque patch) and recolours each family:
  navy to light sky (`#E0F2FE`), green to `#4ADE80`, blue to `#5CCBFB`. The light logo is unchanged.
- The right file is chosen by `{AppThemeBinding Light=brand_horizontal.png, Dark=brand_horizontal_dark.png}`,
  so it changes when the theme changes.
- The same themed logo is used on every other native page that shows the horizontal logo on the themed page
  background: Startup, Sign-in, the Onboarding gate and the Onboarding welcome step. Before this, those pages
  showed the navy-text logo on the dark navy background, which was hard to read. The two header icons that tint
  the logo (`IconView` with `Tone="Inverse"` on Catch and Trips) are unchanged.
- The Web logo and Web navigation were not touched.
- Preview of both variants on their real backgrounds: `docs/images/mobile-polish-250/logo-light-vs-dark-preview.png`.

## Ruler and scales review (no rebuild)

Reviewed `MeasurementEditorViewModel`, `MeasurementGestureMath`, the dial and tape views and their tests against the
original #250 criteria. No defect found in the logic: snapping uses a per-unit step, ounces carry into pounds,
values outside the valid range are flagged and not silently changed, and decrease at zero clears the value.
Remaining gaps, not addressed here:

- No side-by-side Web versus native comparison images are checked in; only native screenshots.
- French, dark mode and large font scale were exercised earlier on the Android emulator but not re-captured
  after the later layout change.
- iOS: the iOS target builds in CI, but the dial and tape touch handling and the motion preference have not been
  exercised on an iOS device.
- Hook arc in the loaders is a polyline and easing is smoothstep, so pixel parity with the browser is approximate.

## Verification status

See the PR description for the run results. In short, automated tests cover the loader wiring, playback rules,
drawer logo configuration and the Web component. Android emulator and iOS device checks are listed there with
their actual status.
