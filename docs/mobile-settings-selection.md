# Native Settings selection (#312)

## Investigation

The explicit `SettingsRadioButton` style in PR #313 failed on the user's physical
iPhone. This rules out treating the implicit-style lookup as a sufficient fix.
The exact iOS hit-testing failure remains unconfirmed: there is no connected Mac
simulator or physical iPhone in this development environment.

MAUI 10.0.120's [RadioButton implementation](https://github.com/dotnet/maui/blob/10.0.120/src/Controls/src/Core/RadioButton/RadioButton.cs)
uses a tap recognizer for templated controls. Its [iOS gesture manager](https://github.com/dotnet/maui/blob/10.0.120/src/Controls/src/Core/Platform/GestureManager/GesturePlatformManager.iOS.cs)
also accepts descendant touches when the owning view has gesture recognizers.
Consequently, the presence of a nested Border alone does not establish the cause.
An iOS comparison of the old template, an untemplated radio and the replacement
is still needed to identify that cause conclusively.

## Replacement

Theme rows use full-size native Button click handling. Input-transparent sibling
views draw the existing 22dp circular indicators and localized labels, preserving
the colours, 15sp text and 12dp indicator spacing. The button is the accessible
element, with a localized name and selected state; its minimum touch height is
48dp. This bypasses the old templated-radio gesture path entirely.

Language uses a data-driven selector and the existing CommunityToolkit popup
dependency. Native buttons handle the trigger, option rows and dismiss action.
The selected item has a highlight and checkmark. Project-owned SVG flags are
decorative; names and selection descriptions identify the accessible controls.
The menu scrolls within a bounded height and closes after selection, outside tap
or back dismissal. Focus returns to the trigger.

SettingsViewModel delegates persistence and theme application to the existing
PreferenceService. LanguageSelectorViewModel owns menu state and delegates a
selection to its supplied callback. Failed persistence retains the current choice
and displays a localized error. No-op selections do not rewrite preferences.
PreferenceService, device theme mapping, account actions and authentication are
unchanged by this Settings change.

## Automated evidence

Tests exercise the real SettingsViewModel, LanguageSelectorViewModel,
PreferenceService and UiTextService. A stateful preference-store substitute proves
both choices reload through new service/ViewModel instances. Native platform
storage remains a device boundary. Tests include all three themes, both languages,
failure/no-op cases, notifications, external preference changes, localized
accessibility descriptions, and a third language supplied through data.

XAML contract tests protect native event wiring, decorative touch transparency,
selection bindings, flags, checkmarks and the 48dp minimum. These tests do not
prove native touch routing, popup positioning, accessibility focus or OS theme
events. Android device checks and iOS TestFlight checks are required separately.

## Android evidence

Android 16/API 36, `sdk_gphone64_x86_64`, FishingLogbook emulator,
1080×2400 display. The final Debug APK with embedded assemblies was installed.

| Check | Result |
|---|---|
| Theme indicator, label and empty row taps | Passed; selected state and colours updated |
| System follows OS dark/light changes | Passed; background changed from #102A43 to #F5F7FA |
| Explicit Light with OS dark | Passed; background remained #F5F7FA |
| Language trigger and both full option rows | Passed; English/French text and flags displayed |
| Selected highlight/checkmark in light/dark | Passed; screenshots inspected locally |
| Language selection/localized text/automatic closure | Passed in both directions |
| Back, outside tap and explicit dismiss | Passed; menu could reopen with unchanged selection |
| Cold restart restores French and Dark | Passed |
| System font scale 1.5 | Passed; both options and dismiss remained reachable |
| Accessible native node names and selected-state descriptions | Passed in English/French |
| TalkBack interaction/focus announcements | Not run |
| Smaller device, rotation and maximum accessibility font | Not run |

One cold launch after reinstall showed an input-dispatch timeout on the dashboard
before Settings interaction. Restarting allowed the checks above; subsequent
restart checks reached the workspace. Its cause has not been established, and
this work does not claim to fix startup responsiveness. Screenshots are retained
locally under ignored `artifacts/settings-312/`; they are not published because
the Settings background includes account information.

## Required iOS TestFlight verification

Record device model, iOS version and build number. Do not describe the original
iOS defect as fixed until this passes on the device:

1. Select every theme by indicator, label and empty row space; confirm immediate
   colours and selected state.
2. With System selected, change OS appearance; confirm the app follows it. Light
   and Dark must remain explicit when OS appearance changes.
3. Open the language menu, inspect both flags/names/checkmarks, select each
   language and confirm localized text plus automatic closure.
4. Dismiss with the close button and outside tap; reopen and confirm no change.
5. Leave/reopen Settings and restart the app; confirm both persisted choices.
6. Operate every control using VoiceOver, including selected-state announcements
   and focus after closing the menu.
7. Repeat in light/dark appearance, with larger accessibility fonts and on a
   small screen; verify wrapping, scrolling and reachable dismissal.

Android TalkBack must also be checked separately. Native accessibility node labels
are supporting evidence, not a completed screen-reader journey.
