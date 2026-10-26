# Native measurement controls and loaders (#250)

Native .NET 10 MAUI XAML/C# recreation of the PWA weight scale, length ruler and range-size
fish buttons, plus fishing-themed loaders. It is shown on a DEBUG-only developer screen
(Showcase, then "Open measurement controls"). It does not implement Catch creation,
authentication, API integration or offline storage.

## Structure

| Concern | Location |
|---|---|
| Conversions, rounding, bounds, range model | `FishingLogBook.Shared/Measurements` (moved from Web, shared by Web and Mobile) |
| Editor state and rules (MAUI-free, unit tested) | `Features/Measurements/ViewModels/MeasurementEditorViewModel.cs` |
| Composite editor control | `Native/Controls/MeasurementEditorView.xaml` |
| Dial and tape drawing, drag and snapping | `Native/Controls/MeasurementVisualView.cs`, `MeasurementDrawable.cs`, `FishDrawing.cs` |
| Loaders (leaping fish, boat on the drift, bobber; ported from the `cdbf-spinners` spike) | `Native/Controls/FishingLoaderView.cs`, `LoaderDrawable.cs`, `Presentation/LoaderMotion.cs` |
| Reduced motion | `IMotionPreference`; Android reads `animator_duration_scale`; iOS reads `UIAccessibility.IsReduceMotionEnabled` |

Rendering uses MAUI `GraphicsView` (no SkiaSharp, no new dependencies).

## Units come from preferences

Units are not chosen in the controls. As in the web profile (`PreferredWeightUnit`,
`PreferredLengthUnit`), the editor reads the user's preference. Mobile gained a
"Measurement units" card in Settings (weight kg or lb, length cm or in, default metric),
stored through `PreferenceService` like language and appearance. It is not yet synced
with the web profile, which belongs with the authentication and API work.

## Layout parity with the PWA

The visual (dial or tape) is on the left and the Monster, Large, Medium, Small fish
buttons are a column on the right. Below are the exact entry (Pounds and Ounces for
imperial weight), the slider with min and max labels, the minus and plus fine-step buttons
and Clear and Apply. Canonical values are kg and cm; behaviour comes from the shared
`MeasurementService`.

Screenshots (Android emulator, 1080x2400, English, light) are in
`docs/images/mobile-measurement-controls/`:
`native-weight-kg.png`, `native-weight-lb-oz.png`, `native-length-cm.png`,
`native-settings-units.png`.

## Differences and limitations

- The loaders are native ports of the three `cdbf-spinners` spike designs (leaping fish, boat on the drift, bobber): same SVG geometry, palettes and CSS keyframe timings (1.8s, 1.6s, 2.6s). They are drawn with `Microsoft.Maui.Graphics` and follow the app light/dark theme. The PWA itself does not use them yet.
- The spike's `boot` theme (full-screen splash colours) is not implemented; only light and dark are. Reduced motion shows a still pose (leaping fish mid-leap, others at rest).
- Pixel-level parity with the browser rendering is approximate (hook arc is a polyline, easing is smoothstep rather than CSS `ease-in-out`).

Screenshots: `docs/images/mobile-measurement-controls/native-loaders-light.png` and `native-loaders-dark.png` (Android emulator).
- The dial and tape are redrawn in `GraphicsView` from the SVG geometry, not the SVG assets.
- The web Clear, Cancel and Apply modal chrome is not reproduced. The showcase uses Clear and
  Apply buttons and Android Back.
- French, dark mode and large font scale were exercised earlier in this work on the
  Android emulator; they were not re-captured after the layout change.
- iOS: `DeviceMotionPreference.iOS.cs` is written but has not been compiled or run
  (no Mac/Xcode). Touch handling and drawing on iOS are untested.
