# Historical Photo Import Device Validation

**Issue:** #214  
**Status:** Pending physical-device execution

Record the device model, operating-system version, browser version and whether the PWA was installed for every run. Use test photographs with known EXIF values and compare the parsed result with an independent metadata reader.

## Android installed PWA

- [ ] Select 1, 10 and 20 photographs; confirm order is retained.
- [ ] Confirm multi-select cancellation and system Back behavior.
- [ ] Test JPEG, PNG and WebP supplied by the native picker.
- [ ] Select HEIC-origin photos and record the actual supplied MIME type/transcoding behavior.
- [ ] Verify EXIF original/digitized date, explicit offset, offset-less wall clock and GPS retention.
- [ ] Verify orientations 1–8 display correctly after sanitisation.
- [ ] Remove, replace and cancel selections; confirm previews disappear and no stale preview is shown.
- [ ] Observe responsiveness and browser/PWA memory pressure at 20 photographs.

## iPhone Home Screen PWA

- [ ] Select 1, 10 and 20 photographs; confirm order is retained.
- [ ] Record Photos limited/full permission and picker behavior.
- [ ] Confirm multi-select cancellation and navigation-away behavior.
- [ ] Test JPEG, PNG and WebP where the picker supplies them.
- [ ] Select HEIC-origin photos and record conversion, MIME type and metadata retention.
- [ ] Verify EXIF original/digitized date, explicit offset, offset-less wall clock and GPS retention.
- [ ] Verify orientations 1–8 display correctly after sanitisation.
- [ ] Remove, replace and cancel selections; confirm previews disappear and no stale preview is shown.
- [ ] Observe responsiveness, Home Screen PWA termination and memory pressure at 20 photographs.

Native-picker behavior is not proven by automated CI. A missing or stripped timestamp/GPS value is a supported per-photo metadata-unavailable outcome, not an Import failure.
