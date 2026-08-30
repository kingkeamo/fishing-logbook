# FishingLogBook

## Product Requirements Document

**Document Status:** Initial MVP Requirements  
**Product:** FishingLogBook  
**Initial Target:** Progressive Web Application  
**Primary Users:** Anglers, Fishing Guides, Fisheries / Fishing Venues, Fishing Clubs and Competition Organisers

---

## 1. Product Vision

FishingLogBook is a mobile-first fishing platform allowing anglers to record, preserve and explore their fishing history.

The application should be useful to an angler regardless of whether a fishing venue, club, guide or competition organiser is registered with FishingLogBook.

Over time, FishingLogBook should connect anglers with fishing venues, fishing clubs, fishing guides and competitions through a common platform.

The product should ultimately provide value to:

- Recreational anglers
- Fisheries / fishing venues
- Fishing guides
- Fishing clubs
- Competition organisers

The initial product must be designed around real-world fishing conditions. Mobile network connectivity may be poor or completely unavailable at fishing locations.

Catch recording must therefore be offline-first.

A user standing beside the water must be able to photograph and record a catch regardless of network availability.

---

# 2. Core Product Principles

## 2.1 Mobile First

The primary FishingLogBook experience is expected to be a mobile device.

All major workflows must be designed for touch interaction and small screens first.

Desktop and tablet layouts should remain fully supported.

---

## 2.2 Offline First

Core catch-recording functionality must not depend on an internet connection.

Users must be able to:

- photograph a catch
- enter catch details
- optionally capture location when the device can provide it
- save the catch
- view locally recorded catches

while completely offline.

Failure to obtain location must never prevent a catch from being saved.

The application must retain unsynchronised catches locally and synchronise them when connectivity becomes available.

Users must be able to identify whether a catch is:

- saved locally
- waiting to synchronise
- synchronising
- successfully synchronised
- failed to synchronise

A manual retry/synchronise option must be provided.

Closing or restarting the application must not cause an offline catch to be lost.

---

## 2.3 Localisation

FishingLogBook UI copy must be localisable from the first screens, not retrofitted later.

The PWA is the source of UI language. Translations are bundled with the application so catch recording still works offline.

- Default culture: British English (`en-GB`)
- First additional language: French (`fr`)
- Missing translations fall back to English
- User-generated content (notes, fishery names, reviews) is not translated
- The API stays language-neutral (codes, ISO timestamps, metric storage). The client formats for display
- Units (kg vs lb) are a separate user preference and must not be mixed into language resources

Users can choose a language in the application. The choice is remembered on the device. Browser language is used on first visit when it matches a supported language.

---

# 3. User Accounts

Every person using FishingLogBook has a standard User account.

A fishing club is an **organisation**, not a user account.

A normal FishingLogBook user may belong to one or more clubs. A user may also hold club-specific roles on those clubs.

A user may additionally have one or more capabilities or roles.

Platform-level examples include:

- Angler
- Guide
- FishingVenue manager
- Competition Organiser
- Administrator

Club-scoped examples include:

- ClubMember
- ClubAdmin
- ClubOfficer
- ClubCompetitionOrganiser

Club permissions are scoped to an **individual club**. Being an administrator of Club A must not grant administrative permissions over Club B.

These capabilities must not be mutually exclusive. Do not create a separate account type for club officers.

For example, a fishing guide may also maintain their own personal fishing log, belong to a club, and hold ClubOfficer on that club only.

A fishing venue manager may also participate in competitions as an angler.

---

# 4. Angler Profiles

Every registered user should have an angler profile.

The profile should support:

- Display name
- Profile photograph
- Home region or general location
- Preferred fishing types
- Preferred species
- Catch history
- Personal bests
- Basic fishing statistics

Users must have control over the visibility of their personal information and catches.

Precise fishing locations should not automatically become public. Enabling device location, joining a club, or associating a catch with a FishingVenue must not by itself make coordinates visible to other users.

---

# 5. Catch Logging

Catch logging is the primary feature of FishingLogBook.

Creating a catch must be fast and practical while standing beside the water.

A catch should support:

- One or more photographs
- Species
- Weight
- Length
- Date
- Time
- Optional captured location (see **# 33. Location and Privacy**)
- FishingVenue association, independent of captured location
- Fishing method
- Bait or lure
- Notes
- Angler
- Person who recorded the catch
- Guide trip, where applicable
- Competition, where applicable
- Club association, where applicable

Not every field should be mandatory. **Location is never mandatory.** A catch must be saveable when location is missing, denied, inaccurate, or unavailable.

At minimum, users should be able to record a catch quickly with a photograph and basic information and add additional information later.

The initial MVP Catch model must support optional location capture, including:

- optional automatic capture when permission exists
- offline persistence of any captured location with the local catch
- location accuracy where the device supplies it
- location source (so device GPS is distinguishable from manual selection or a FishingVenue)
- location privacy/visibility, with precise coordinates private by default
- graceful handling when permission is denied or location cannot be obtained
- a FishingVenue association that can exist with or without GPS coordinates, and GPS coordinates that can exist with or without a FishingVenue

---

# 6. Catch Photographs

Users must be able to:

- take a photograph using the device camera
- select an existing photograph
- attach multiple photographs to a catch
- review photographs before saving

Photographs captured offline must remain available until successfully uploaded.

The system should preserve sufficient image quality for future fish identification capabilities while balancing storage and upload requirements.

---

# 7. Catch Ownership and Provenance

The system must distinguish between:

- the person who caught the fish
- the person who recorded the catch

For a normal recreational catch these will normally be the same person.

For a guided trip, the guide may record the catch on behalf of their client.

The system must retain this provenance permanently.

Example:

**Angler:** John Smith  
**Recorded by:** Patrick Murphy  
**Role:** Fishing Guide  
**Trip:** Lough Corrib Guided Day

This information may later be used as part of catch verification.

---

# 8. Catch History

Users must have a personal fishing log containing their historical catches.

The catch history should support:

- chronological browsing
- photographs
- species filtering
- date filtering
- fishing venue filtering
- location filtering
- club filtering
- guide/trip filtering
- competition filtering

Users should be able to open an individual catch and see its complete details.

---

# 9. Personal Bests

FishingLogBook should automatically calculate personal bests from recorded catches.

Personal bests may include:

- Heaviest fish by species
- Longest fish by species
- Overall heaviest fish
- Overall longest fish

A catch that becomes a new personal best should be clearly identified.

---

# 10. Basic Statistics

The MVP should provide useful basic statistics including:

- Total catches
- Catches by species
- Catches by year
- Catches by fishing venue
- Largest fish
- Most frequently caught species

Advanced analytics are outside the initial MVP.

---

# 11. Fishing Venue Directory

The underlying domain concept is **FishingVenue**. A FishingVenue may represent:

- Fishery
- Lake
- River
- River section / stretch
- Canal
- Reservoir
- Commercial fishery
- Other managed fishing water or location
- Coastal or sea-fishing locations where appropriate

The domain is not restricted to commercial fisheries.

The UI may later display context-appropriate language such as Fishery, Water, Lake, River or Venue. Do not force a single user-facing label where a more natural term applies.

FishingLogBook should maintain a searchable directory of fishing venues.

A venue does not need to have joined FishingLogBook before appearing in the directory.

Users should be able to discover venues and record catches against them.

Venue owners or authorised managers should later be able to claim an existing venue profile.

A venue must be able to exist independently of a club. Do not duplicate venue records for club use.

A venue may also potentially be associated with more than one organisation where business rules permit.

Existing season, opening, closure, rules, facilities, catch and competition functionality continues to operate against the venue.

---

# 12. Fishing Venue Profiles

A fishing venue profile should contain:

- Name
- Description
- Location
- Contact information
- Website where applicable
- Photographs
- Fishing types
- Species available
- Facilities
- Rules
- Opening information
- Season information
- Ratings
- Reviews

The profile should clearly indicate whether the venue is currently available for fishing.

Commercial fishery-specific information (for example day tickets, bookings, facilities and staff-oriented ratings) remains supported where it applies to that venue.

---

# 13. Fishing Venue Seasons

Season information is an MVP requirement.

Venues must be able to define:

- Season start date
- Season end date
- Closed season
- Temporary closures
- Exceptional closures
- Species-specific seasons where appropriate
- Notes regarding seasonal restrictions

FishingLogBook should derive a clear current status where possible.

Examples:

**Open**

**Closed today**

**Closed for season**

**Temporarily closed**

Users should be able to see upcoming opening and closing dates.

---

# 14. Fishing Venue Opening Hours

Venues should be able to define normal operating hours independently from their fishing season.

Opening hours should support different times for different days.

Season status takes precedence over normal opening hours.

For example, a venue normally open from 06:00 to 20:00 should still display as closed when outside its fishing season.

---

# 15. Fishing Venue Facilities

Venue profiles should support facilities such as:

- Parking
- Toilets
- Disabled access
- Food
- Tackle shop
- Boat hire
- Accommodation
- Fish cleaning facilities

The exact facility list should be extensible.

---

# 16. Fishing Venue Rules

Venues must be able to publish rules.

Examples include:

- Catch and release requirements
- Keep limits
- Permitted methods
- Bait restrictions
- Hook restrictions
- Minimum or maximum sizes
- Required equipment
- Boat restrictions

Rules should be easily accessible from the venue profile.

Club-level rules and venue-level rules must be distinguishable. Where they conflict, users must be able to see clearly which rule applies to the specific venue or event.

---

# 17. Fishing Venue Ratings and Reviews

Users must be able to rate and review fishing venues.

The rating system should support an overall rating.

The design should allow additional rating categories later, potentially including:

- Fishing quality
- Facilities
- Value
- Staff
- Accessibility

Users should be able to provide written feedback.

Reviews should record when they were submitted.

The system should support moderation and reporting of inappropriate reviews.

---

# 18. Verified Fishing Venue Reviews

The system should be designed to distinguish verified visits from unverified reviews.

Evidence of a verified visit may eventually include:

- A booking made through FishingLogBook
- A catch recorded at the venue
- Participation in a competition at the venue
- A guided trip associated with the venue

Verified reviews should be visually distinguishable.

Full automated verification rules may evolve after the MVP.

---

# 19. Fishing Venue Availability

Venues should be able to publish fishing availability.

Depending on the type of venue this may represent:

- Day sessions
- Half-day sessions
- Pegs
- Swims
- Boats
- Fishing areas

Users should be able to see availability before deciding to visit.

---

# 20. Fishing Venue Bookings

The system should support anglers reserving fishing through FishingLogBook.

The MVP should support the underlying booking workflow.

Online payment processing may be introduced separately if it materially increases the initial implementation scope. Do not introduce payment-provider requirements into the MVP solely because Clubs are being added.

A booking should associate:

- User
- FishingVenue
- Date
- Session
- Peg/swim/boat where applicable
- Booking status

---

# 21. Fishing Licences and Day Tickets

The product should be designed so venues can offer:

- Day tickets
- Fishing permits
- Venue-specific licences

Users should eventually be able to purchase or obtain these through FishingLogBook.

Payment processing can be introduced incrementally. Future payment support should be compatible with membership fees, competition entry fees, venue bookings and day tickets/licences.

---

# 22. Fishing Clubs

Fishing clubs are a first-class organisation type within FishingLogBook.

A club is an organisation, not a user account. Clubs must be integrated into the existing product model for anglers, fishing venues, guides and competitions rather than forming a disconnected club-management subsystem.

The MVP must focus on fishing-related club functionality. FishingLogBook must not attempt to become a full general-purpose club administration system.

## 22.1 Club profile

A club should support:

- Name
- Description
- Logo
- Photographs
- General location/region
- Contact details
- Website
- Social links
- Fishing disciplines
- Target species
- Club rules
- Associated fishing venues/waters
- Membership information
- Competition calendar

Ratings and reviews of clubs are not mandatory for the MVP. They may be considered later if appropriate.

## 22.2 Club membership

A user may belong to multiple clubs.

A club membership should support:

- User
- Club
- Membership number/reference
- Membership type
- Membership start date
- Membership end date
- Membership status
- Joined date

Membership statuses should support at least:

- Pending
- Active
- Expired
- Suspended
- Cancelled

Membership types should be extensible and may include:

- Adult
- Junior
- Senior
- Family
- Student
- Guest

Do not hard-code these membership types as permanent product-wide enums if the design may later allow clubs to configure their own membership types.

## 22.3 Club-scoped roles and permissions

Do not create a separate account type for club officers.

A normal User may have club-specific capabilities. Examples:

- ClubMember
- ClubAdmin
- ClubOfficer
- ClubCompetitionOrganiser

These permissions must be scoped to an individual club.

For example, being an administrator of Club A must not grant administrative permissions over Club B.

Club-scoped capabilities sit alongside platform-level capabilities (Angler, Guide, FishingVenue manager, Competition Organiser, Administrator) and must not be mutually exclusive with them.

## 22.4 Associated fishing venues / waters

A club may own, lease, manage or have fishing rights over one or more fishing venues.

Examples include:

- Lakes
- Rivers
- Stretches of river
- Canals
- Reservoirs
- Commercial fisheries
- Coastal or sea-fishing locations where appropriate

A venue must be able to exist independently of a club.

A venue may also potentially be associated with more than one organisation where business rules permit.

Do not duplicate venue records for club use.

Existing season, opening, closure, rules, facilities, catch and competition functionality must continue to operate against the venue.

## 22.5 Club waters / venues on the club profile

A club must be able to display and manage its associated fishing venues.

A club profile should show:

- Club waters/venues
- Venue-specific seasons
- Venue-specific rules
- Current open/closed status
- Species available
- Competition use where applicable

Club-level rules and venue-level rules must be distinguishable.

Where rules conflict, the product must be designed so users can clearly see which rule applies to the specific venue or event.

## 22.6 Club competitions

Clubs use the existing competition model. A club should be able to:

- Create competitions
- Maintain a competition calendar
- Restrict a competition to club members where appropriate
- Run open competitions where appropriate
- Associate a competition with one or more fishing venues
- Maintain results
- Maintain season/league standings later

The MVP should support individual competitions.

The requirements explicitly leave room for future:

- Club championship points
- Seasonal leagues
- Multi-event standings

Do not make advanced seasonal league scoring mandatory for the first MVP.

## 22.7 Club catch records and statistics

Catches recorded on club-managed waters must be able to contribute to future club-level aggregated statistics.

Potential future statistics include:

- Total catches
- Catches by species
- Average size
- Largest catches
- Catch trends over time
- Catch rates by venue

This must respect angler privacy.

A club must not automatically gain access to:

- private notes
- private exact coordinates
- unrelated catches
- personal data not required for the club use case

Club membership does not give a club unrestricted ownership of a user's fishing history. A user's personal logbook remains their own data.

Aggregated club statistics should be designed so they can be produced without exposing private catch-location information.

Advanced club statistics are not required for the initial MVP UI. The Catch and club-venue model must not prevent them later.

## 22.8 Club membership fees

The product should allow clubs eventually to collect membership fees through FishingLogBook.

For the MVP:

- Membership status and membership type should be supported
- Membership fee information may be displayed
- Payment processing is not mandatory

Future payment support should be compatible with:

- Membership fees
- Competition entry fees
- Fishery/venue bookings
- Day tickets/licences

Do not introduce payment-provider requirements into the MVP solely because Clubs are being added.

## 22.9 Explicitly outside club MVP

The following are outside the Club MVP:

- Accounting
- AGM management
- Committee minutes
- Elections
- General document management
- Full CRM functionality
- Advanced club financial reporting

---

# 23. Fishing Guides

Fishing guides are a first-class user group within FishingLogBook.

A guide should use the same normal user account as any other angler while having additional guide capabilities.

---

# 24. Guide Profiles

A guide profile should support:

- Guide name
- Profile photograph
- Description
- Areas covered
- Fishing venues covered
- Fishing types
- Target species
- Contact information
- Guide services
- Ratings
- Reviews

Future versions may include pricing and availability.

---

# 25. Guide Clients

Guides must be able to associate FishingLogBook users with guided trips.

A client should not need to hand their phone to the guide for every catch.

The guide should be able to record catches directly for clients participating in the trip.

---

# 26. Guided Trips

A guide should be able to create a guided fishing session.

A trip should support:

- Guide
- Date
- Start/end times
- General location
- FishingVenue where applicable
- Clients
- Target species
- Notes
- Catches

Example:

**Lough Corrib Pike Day**

Guide: Patrick Murphy

Clients:
- John Smith
- Jane Smith

The guide should be able to switch quickly between clients when recording catches.

---

# 27. Guide-Recorded Catches

During a guided trip, the guide must be able to create a catch on behalf of a client.

The catch should automatically appear in the client's FishingLogBook once synchronised.

The catch must retain:

- Angler identity
- Guide identity
- Guided trip
- Date/time
- Location / FishingVenue
- Catch information

The catch should be identifiable as having been recorded by a guide.

---

# 28. Guide Ratings and Reviews

Users should be able to rate and review fishing guides.

The system should be designed to support verified reviews where the reviewer participated in a guided trip.

---

# 29. Competitions

FishingLogBook should support local fishing competitions.

Competition organisers should be able to create a competition. A club may create competitions through the same competition model, using club-scoped capabilities such as ClubCompetitionOrganiser. Do not introduce a separate club-only competition type.

A competition should support:

- Name
- Description
- Location / one or more FishingVenues
- Organising club, where applicable
- Member-only or open entry, where applicable
- Start date/time
- End date/time
- Rules
- Participants
- Eligible species
- Scoring method
- Catch submissions
- Results

The MVP should support individual competitions. Club championship points, seasonal leagues and multi-event standings are future capabilities and are not mandatory for the first MVP.

The initial implementation should concentrate on straightforward local competitions rather than supporting every possible competition format.

---

# 30. Competition Catch Submission

Competition participants should be able to associate catches with an active competition.

Competition catches should retain the same normal FishingLogBook catch record while additionally referencing the competition.

Competition submission must continue to work offline.

The original capture time must be retained regardless of when the catch is synchronised.

---

# 31. Competition Verification

The system should provide competition organisers with sufficient information to review submitted catches.

Potential evidence includes:

- Photograph
- Capture timestamp
- Angler
- Location where permitted
- Weight
- Length
- Guide/official verification where applicable

Advanced automated fraud detection is outside the MVP.

---

# 32. Competition Leaderboards

Competitions should have a leaderboard based on their configured scoring method.

The MVP should support at least simple ranking based on:

- Weight
- Length
- Number of qualifying catches

More sophisticated scoring systems can be introduced later.

---

# 33. Location and Privacy

Location is part of the initial MVP Catch model. Capture is **optional**. A user must always be able to record a catch when:

- location permission has not been requested
- location permission is denied
- location services are disabled
- GPS/location acquisition fails
- the device cannot obtain a sufficiently accurate location
- the application is offline

Failure to obtain location must **never** prevent a catch from being saved.

Exact fishing spots are particularly sensitive for anglers. FishingLogBook must not automatically reveal an angler's fishing spot because they enabled device location.

## 33.1 Capture and sharing are separate

There is an explicit distinction between:

1. Permission to **capture and store** location.
2. Permission to **share or expose** that location.

A user may allow FishingLogBook to record precise coordinates for their private fishing history without allowing those coordinates to be visible to other users.

Precise coordinates are **private by default**.

Potential visibility levels include:

- Private
- Approximate area
- Fishing venue only
- Public

The model must be able to evolve without forcing exact coordinates to be exposed.

Do not imply that granting location permission makes the location public.

## 33.2 Automatic location capture

When the user has granted location permission, FishingLogBook should attempt to capture the device's location when a catch is recorded.

Where available, retain conceptually equivalent information to:

- Latitude
- Longitude
- Location accuracy in metres
- Date/time the location was captured
- Location source
- Location visibility
- Location consent version (or equivalent)

Do not treat this list as prescribed database column names.

`LocationSource` must distinguish origin, including:

- Device GPS/location services
- Manual user selection
- Fishing venue
- Other future location sources

A FishingVenue association must **not** be treated as equivalent to an accurate device GPS coordinate.

## 33.3 Location accuracy

Retain location accuracy information supplied by the device.

The system must not treat all coordinates as equally accurate. A location with approximately 5 metre accuracy provides different information from one with 1,000 metre accuracy.

Future mapping and analytics must be able to account for location accuracy.

## 33.4 Offline location capture

Location capture must work independently of API availability where the device/browser can provide location information.

When a catch is created offline:

1. Attempt to obtain location if permission exists.
2. Store the location information with the local catch.
3. Preserve the location while the catch remains in IndexedDB/offline storage.
4. Synchronise the location metadata with the catch when connectivity returns.

Failure to obtain location while offline must not affect catch creation.

## 33.5 Location permission

FishingLogBook must explicitly request permission before accessing device location where required by the platform.

The application must handle all permission outcomes gracefully.

If the user denies permission:

- Continue catch logging normally.
- Do not repeatedly interrupt or nag the user.
- Clearly allow location permission to be enabled later.
- Allow location to be manually associated with a catch where appropriate.

The application should explain the benefit of location capture **before** requesting permission. For example, location can provide:

- a private record of where the angler caught the fish
- personal catch maps
- historical fishing information
- better catch statistics
- FishingVenue identification
- future aggregated fishing insights

## 33.6 FishingVenue and GPS location

A Catch must be capable of independently having:

- a FishingVenue association
- a precise or approximate captured location

Either may exist without the other.

Examples:

A user selects "Lough Corrib" but refuses GPS permission:

- FishingVenue = Lough Corrib
- GPS = None

A user fishes an unregistered remote location and allows GPS:

- FishingVenue = None
- GPS = Available

A user fishes a registered venue and permits location:

- FishingVenue = Lough Corrib
- GPS = Available

Do not make one dependent upon the other. Club association, FishingVenue association and precise GPS location remain separate concepts.

## 33.7 Fishing spot privacy and privileged roles

Public catch views, club views, guide views and FishingVenue views must respect the catch owner's location visibility settings.

Being any of the following must **not** automatically grant access to an angler's private precise coordinates:

- ClubAdmin
- ClubOfficer
- ClubCompetitionOrganiser
- Guide
- FishingVenue manager
- CompetitionOrganiser

Where a competition genuinely requires location verification, coordinates may be made available specifically for verification according to the competition rules and user consent, without automatically making them public.

## 33.8 Clubs and catch location

A catch associated with a club-managed FishingVenue may contribute to permitted aggregate club statistics.

This must **not** mean that club administrators automatically receive precise catch coordinates, private notes, unrelated catches, or personal data not required for the club use case.

Club membership does not give a club unrestricted ownership of a user's fishing history. A user's personal logbook remains their own data. See **# 22. Fishing Clubs** and **# 42. Data Ownership**.

For example, FishingLogBook could eventually tell a club:

```text
312 Pike recorded on Club Water A this season
Average weight: ...
Catch trend: ...
```

without exposing the exact coordinates of each catch.

## 33.9 Location consent

The system must be able to retain sufficient information to understand the consent conditions associated with collected location data.

The future implementation should be capable of recording which version of the relevant location/privacy consent applied when location information was captured. This may conceptually include a location consent version or equivalent.

Do not define the legal wording or consent policy in this document.

## 33.10 Manual location

Users should eventually be able to:

- add a location manually
- correct an inaccurate location
- associate a catch with a FishingVenue
- remove location from a catch where permitted

Manual location selection must be distinguishable from an automatically captured device location.

Full manual location UX is not required for the initial MVP UI, but the Catch/location model must not prevent it.

## 33.11 Future personal catch mapping

The MVP Catch data should support future personal mapping features. These are not all required for the initial MVP UI.

Potential features include:

- Personal catch map
- Previous fishing locations
- Catches by area
- Catches by FishingVenue
- Species distribution within the user's own history

## 33.12 Future aggregated location analytics

FishingLogBook should be designed so appropriately permissioned and privacy-protected catch location data could eventually provide aggregated fishing insights.

Potential future capabilities include:

- Catch heat maps
- Species distribution maps
- Catch rates by area
- Seasonal catch patterns
- Fish size distributions
- Time-of-day patterns
- FishingVenue catch trends
- Regional fishing trends

This has potentially significant value to anglers, clubs, fisheries/fishing venues and guides.

Aggregated analytics must **not** expose an individual's private fishing location.

The future analytics model must consider:

- User consent
- Location visibility
- Minimum aggregation thresholds
- Location precision reduction
- Anonymisation/pseudonymisation where appropriate
- Protection against reverse-engineering individual fishing spots

Do **not** implement these analytics as part of the MVP. The requirement at this stage is that the underlying Catch/location model does not prevent them later.

---

# 34. Offline Synchronisation

The system must treat local data as authoritative until successfully synchronised.

Each offline-created record should have a unique identifier generated on the device.

Synchronisation must avoid creating duplicate catches if the same record is retried.

The user must be able to see outstanding synchronisation activity.

Network failure during synchronisation must not result in data loss.

Photograph uploads should be capable of retrying independently where appropriate.

Location metadata captured with an offline catch must synchronise with that catch and must not be dropped if photograph upload is retried separately.

---

# 35. Multiple Devices

A user may access FishingLogBook from multiple devices.

Once data has successfully synchronised, it should be available from the user's other devices.

The MVP does not need sophisticated simultaneous offline editing conflict resolution.

Conflicts must, however, never silently cause a catch to disappear.

---

# 36. Individual Fish Identification

Individual fish recognition is a planned future capability and is not required for the initial MVP.

The system should nevertheless collect data in a way that allows this capability to be developed.

Potential identification signals include:

- Spot patterns
- Scale patterns
- Colour patterns
- Fin shape
- Scars
- Body markings
- Other stable visual characteristics

Future functionality could compare a newly photographed fish against previous catches.

The system should return a probability or candidate match rather than claiming certainty where confidence is insufficient.

Example:

**Possible previous match: 92%**

The user or venue manager may then confirm or reject the match.

---

# 37. Fish History

Future individual fish identification could allow FishingLogBook to build a history for a particular fish.

Potential information includes:

- Previous captures
- Previous weights
- Previous lengths
- Growth
- Fishing venues
- Capture dates
- Photographs

This could provide significant value to fisheries and clubs interested in monitoring fish populations and growth.

---

# 38. Species Recognition

Automatic species identification from photographs is a future capability.

Users must always be able to manually select or correct the species.

Automated species recognition must not silently replace user-entered information.

---

# 39. Notifications

The product should be designed to support future notifications including:

- Booking confirmations
- Competition updates
- Guide invitations
- Guided trip updates
- Venue closures
- Club competition updates
- Season opening reminders
- Synchronisation problems

Full notification functionality is not required for the initial MVP unless needed by a core workflow.

---

# 40. Search and Discovery

Users should be able to search for:

- Fishing venues
- Guides
- Fishing clubs

Keep the initial search implementation simple. The initial search implementation may use a smaller subset of filters.

Future venue search filters may include:

- Location
- Distance
- Species
- Fishing type
- Rating
- Currently open
- In season
- Facilities
- Availability

Potential club search filters may eventually include:

- Region
- Fishing discipline
- Species
- Venues
- Membership availability

---

# 41. Administration

FishingLogBook requires basic administrative functionality.

Administrators should be able to:

- Manage users
- Manage fishing venues
- Manage claimed fishing venue profiles
- Manage clubs
- Manage claimed club profiles
- Handle club ownership/admin disputes
- Manage reported club content where necessary
- Manage guides
- Review reported reviews
- Moderate inappropriate content
- Manage competitions where necessary

Administration can initially be functional rather than highly polished.

---

# 42. Data Ownership

Users should retain access to their fishing history.

A user's personal logbook remains their own data.

Club membership does not give a club unrestricted ownership of a user's fishing history.

Private fishing data must remain protected according to the existing privacy model, including precise catch coordinates, private notes and unrelated catches.

The product should eventually provide a mechanism for users to export their catch data.

Deleting an account must follow applicable data protection requirements.

Shared records such as competition results, guide records and fishery/venue statistics may require retention or anonymisation rather than simple deletion.

Club-related relationships may allow the platform to retain relevant shared records such as:

- Membership history
- Competition results
- Club event participation
- Verified catches submitted to competitions

Detailed retention rules will be defined separately.

---

# 43. MVP Scope

The MVP should concentrate on proving that FishingLogBook is genuinely useful beside the water.

The MVP includes:

### Anglers

- User registration/login
- User profile
- Catch recording
- Optional location capture (never required to save)
- Multiple catch photographs
- Offline catch recording
- Offline photograph storage
- Offline location persistence with the catch
- Synchronisation of catch metadata, location and photographs
- Catch history
- Personal bests
- Basic statistics
- Location privacy (precise coordinates private by default)

### Fishing Venues

- Fishing venue directory
- Fishing venue profile
- Season information
- Opening hours
- Current open/closed status
- Facilities
- Rules
- Ratings
- Reviews
- Basic availability
- Initial booking capability
- Day-ticket/licence information
- Commercial fishery-specific booking, ticket and facility behaviour where it applies

### Clubs

- Club profile
- Memberships (status, type, dates, membership number/reference)
- Club-scoped roles (ClubMember, ClubAdmin, ClubOfficer, ClubCompetitionOrganiser)
- Club waters/venues
- Club rules (distinguishable from venue-level rules)
- Club competition support
- Basic competition calendar
- Member management
- Simple search/discovery of clubs
- Membership fee information may be displayed; payment processing is not mandatory

### Guides

- Guide profile
- Guide ratings/reviews
- Clients
- Guided trips
- Recording catches for clients
- Guide-recorded catch provenance
- Guided trip history

### Competitions

- Competition creation
- Participants
- Rules
- Catch submissions
- Offline submissions
- Basic verification
- Basic leaderboard/results

### Platform

- Mobile-first experience
- Installable PWA
- Offline-first behaviour
- Localisation infrastructure with English and French UI
- Synchronisation
- Role/capability model, including club-scoped permissions
- Basic administration, including clubs and claimed club profiles

---

# 44. Explicitly Outside Initial MVP

The following should be considered during architecture but are not required before the first usable release:

- Individual fish AI recognition
- Automatic species recognition
- Advanced fishery/venue analytics
- Advanced club aggregated statistics (model must allow them later)
- Club championship points, seasonal leagues and multi-event standings
- Catch heat maps and other aggregated location analytics
- Public or shared catch maps
- Mapping providers
- Weather integration
- Tide integration
- Complex competition scoring engines
- Club ratings/reviews (not mandatory for MVP)
- Accounting, AGM management, committee minutes, elections and general document management
- Full CRM functionality
- Advanced club financial reporting
- Payment processing for membership fees, competition entry, venue bookings or day tickets (design for later compatibility only)
- Social feeds
- Direct messaging
- Advanced push notifications
- Native Android application
- Native iOS application
- Complete translation of every future screen into additional languages beyond English and French

These features should not delay delivery of the core FishingLogBook.

---

# 45. Initial User Journey - Recreational Angler (Critical Offline Acceptance)

This workflow is a critical MVP acceptance scenario. Location must be tested as well as the catch and photograph.

### 45.1 Location permission previously granted

1. User has previously granted location permission.
2. Device has no internet connectivity.
3. User records a catch.
4. FishingLogBook obtains device location where available.
5. Catch, location and photograph are persisted locally.
6. Application is closed.
7. Application is reopened while offline.
8. Catch, photograph and location still exist.
9. Connectivity returns.
10. Catch metadata, location and photographs synchronise successfully.

### 45.2 Location permission denied

Repeat the same workflow with location permission denied (or never granted).

1. Device has no internet connectivity.
2. User records a catch.
3. FishingLogBook does not obtain device location.
4. Catch and photograph are persisted locally. The catch succeeds without location.
5. Application is closed and reopened while offline.
6. Catch and photograph still exist.
7. Connectivity returns.
8. Catch metadata and photographs synchronise successfully.

The catch must succeed in both cases. Failure to obtain location must never block save.

### 45.3 Core recreational flow

1. User installs or opens FishingLogBook.
2. User creates an account.
3. User goes fishing.
4. Network connectivity becomes unavailable.
5. User catches a fish.
6. User opens FishingLogBook.
7. User photographs the fish.
8. User records basic catch information.
9. Catch is saved immediately on the device, with location if permission and acquisition succeed.
10. Catch appears in the user's logbook.
11. User continues fishing.
12. Connectivity later returns.
13. FishingLogBook synchronises the catch, location metadata and photographs.
14. User can subsequently access the catch from another device.

---

# 46. Initial User Journey - Guided Fishing

1. Guide creates a guided trip.
2. Guide adds participating clients.
3. Guide and clients begin fishing.
4. A client catches a fish.
5. Guide opens the active trip.
6. Guide selects the client.
7. Guide photographs and records the fish.
8. Catch is saved locally if offline.
9. Additional catches are recorded throughout the day.
10. Data synchronises when connectivity becomes available.
11. Each client's catches appear in their own FishingLogBook.
12. The guide retains the trip history.

This workflow is a critical MVP acceptance scenario.

---

# 47. Initial User Journey - Fishing Venue Discovery

1. User searches for a fishing venue.
2. User opens the venue profile.
3. User sees species, facilities and rules.
4. User sees whether the venue is currently in season.
5. User sees today's opening status.
6. User reads ratings and reviews.
7. User checks availability.
8. User books or obtains the information required to fish.
9. User later records catches against that venue.
10. User can leave feedback following the visit.

The UI may still use context-appropriate language such as fishery, water, lake or river.

---

# 48. Initial User Journey - Fishing Club

This workflow is a critical MVP acceptance scenario. Catch recording remains offline-first.

1. Club creates or claims its club profile.
2. Club administrator adds or approves members.
3. Club associates its fishing venues/waters.
4. Members view club rules, seasons and venue status.
5. Club creates a competition.
6. Members participate and record catches. Catch recording must work offline; failure to obtain location must never block save. Catches, photographs and any captured location persist locally and synchronise when connectivity returns.
7. Competition results are available through the club.
8. Catch records associated with club waters contribute to future aggregate club statistics subject to privacy rules. The club does not receive private notes, private exact coordinates, unrelated catches, or personal data not required for the club use case.

---

# 49. Initial User Journey - Competition

1. Organiser creates a competition.
2. Anglers join or are registered.
3. Competition begins.
4. Angler catches a fish while offline.
5. Angler records the catch and associates it with the competition.
6. Catch is stored locally with its original timestamp.
7. Catch synchronises when connectivity becomes available.
8. Organiser reviews the submission.
9. Valid catch contributes to the leaderboard.
10. Final results are available after the competition.

---

# 50. MVP Success Criteria

The initial FishingLogBook MVP should be considered technically successful when:

- It can be installed on a mobile device as a PWA.
- A user can record catches with no network connection.
- A catch can be saved when location permission is denied or location cannot be obtained.
- When location permission is granted, an offline catch retains captured location through close/reopen and synchronises it with the catch when connectivity returns.
- Offline catches survive application/device interruptions within reasonable browser limitations.
- Photographs synchronise reliably when connectivity returns.
- A user's synchronised fishing history is available across devices.
- Fishing venues can maintain meaningful profiles and seasonal information.
- Users can review fishing venues.
- Clubs can maintain a profile, memberships, club-scoped roles, associated waters and a basic competition calendar.
- Club permissions remain scoped to that club.
- Guides can create trips and record catches for clients.
- Local competitions, including club competitions, can collect catch submissions.
- The same application works effectively on modern Android and iOS devices without requiring native applications.

The product should be considered commercially validated separately based on actual usage by anglers, fishing venues, clubs and guides.

---

# 51. Fishing Trips

A Fishing Trip records the experience of going fishing. A Catch records a fish caught during it.

A trip with zero catches is a valid and meaningful fishing record. It must never be presented as a failed or incomplete entry.

A trip must not be required in order to record a catch. An angler who catches a fish unexpectedly must still be able to record it immediately.

## Trip ownership

A trip belongs to one owner.

Catches recorded during a trip continue to belong to their own angler and retain their existing ownership and provenance.

## Trip lifecycle

A trip has an explicit status. The supported statuses are:

- Active
- Completed

An owner may have at most one Active trip at a time.

A trip records:

- start time
- end time, once finished

Start and end times are stored as absolute instants and displayed in the angler's local time.

A trip may be created retrospectively as Completed, so historical trips can be recorded later.

## Trip identity and title

A trip title is optional.

Where no title is supplied, the application displays a localised date. The generated title is not stored.

A title is a description chosen by the angler, for example "Day with Dad". It is not a substitute for where the trip happened.

## Trip location

Trip location is optional but first-class. A trip without location context is incomplete, particularly a blank trip with no catches.

Three location concepts are distinct and must not be conflated:

- exact coordinates, captured from the device where the angler allows it
- a display place, describing where the fishing happened, for example "Lough Corrib"
- a fishing venue or fishery, which is a future concept

Trip coordinates are private by default and follow the same visibility, source and consent rules as catch location.

A catch does not inherit trip coordinates. A catch keeps its own location and provenance.

## Trip content

A trip may own:

- trip photographs that belong to the day rather than to a catch
- trip notes recorded during or at the end of the trip
- catches recorded during the trip

Trip photographs and trip notes are separate from catch photographs and catch notes.

Trip notes are timestamped so they can be shown in the order they were written.

## Offline use

A trip must be usable with little or no connectivity.

An angler must be able to start a trip, record catches, add photographs and notes, and finish the trip without live connectivity, and have that data synchronise safely when connectivity returns.

An active trip must remain recoverable after the application is closed and reopened while offline.

## Trip history

A completed trip should be presented as a chronological record of the day, interleaving trip start and finish, photographs, notes and catches by time.

## Trip collaboration

Anglers who fish together participate in one shared trip: one trip, one trip id, one
timeline, several participating anglers.

The trip owner invites an existing FishingLogBook angler, who accepts or declines. Only
accepted participants may contribute. Participants add their own catches, notes and trip
photographs, and those contributions keep their real author. The owner alone edits
trip-level details, finishes the trip and manages participants. Removing a participant
ends their future write access but leaves their existing contributions in the diary.

Once an accepted participant has the shared trip on their device, their own catches,
notes and photographs stay offline-first and synchronise back into the same trip.
Inviting, accepting, declining and membership changes require connectivity.

## Out of scope for the first Trip implementation

- guided trip and client workflows
- video
- fishing venues and fisheries
- planned future trips
- public sharing
- advanced trip search
- historical import workflows

These concepts influence the trip model but are not implemented as part of it.

---

# 52. Next Document

The next project document will be:

**Architecture and Technical Requirements**

It will define:

- Application architecture
- PWA implementation
- Offline storage
- Synchronisation architecture
- Image storage and processing
- Catch location model (optional coordinates, accuracy, source, visibility, consent version)
- Location permission and geolocation on the device/browser
- Offline storage of location metadata with the catch
- Synchronisation of location with catch metadata
- API and authorisation rules that enforce location visibility (including clubs, guides, venues and competitions)
- Privacy-preserving aggregation for future analytics (not MVP)
- Database
- Authentication and authorisation
- Hosting
- Infrastructure as Code
- CI/CD
- Development and production environments
- Logging
- Monitoring
- Security
- Backup and recovery
- Expected operating costs
- Scaling strategy

Technology and hosting decisions should be made in that document rather than being embedded unnecessarily into these product requirements.

### Location-related implications for a later Architecture update

These product requirements do **not** implement location. A later Architecture update may need to cover:

- Optional Catch location in the domain/API/database (coordinates, accuracy, captured time, source, visibility, consent version) without prescribing column names here
- Browser/device geolocation and permission UX, including denied and unavailable outcomes
- Offline IndexedDB persistence of location with the catch, surviving close/reopen
- Synchronisation of location metadata with the catch (independent of whether a photograph is still uploading)
- Authorisation so ClubAdmin, ClubOfficer, ClubCompetitionOrganiser, Guide, FishingVenue manager and CompetitionOrganiser cannot read private precise coordinates by role alone
- Competition verification access to coordinates only where rules and consent allow, without making them public
- Independent FishingVenue association vs captured GPS
- Future mapping providers and personal catch maps (not MVP UI)
- Future aggregated analytics with consent, visibility, minimum thresholds, precision reduction and anti-reidentification (not MVP)
- Manual add/correct/remove location (model now; full UX later)

Do not create Catch entities, geolocation code, IndexedDB location stores, maps, analytics or Terraform changes as part of this requirements update.

### Club-related implications for a later Architecture update

These product requirements do **not** implement clubs in code, schema or infrastructure. A later Architecture update may need to cover:

- Club as an organisation entity, distinct from User
- Club membership (user, club, number/reference, type, start/end, status, joined date)
- Extensible membership types without permanent product-wide enums
- Club-scoped roles and authorisation (ClubMember, ClubAdmin, ClubOfficer, ClubCompetitionOrganiser) that never cross club boundaries
- FishingVenue as the broader venue domain, with optional association to one or more organisations including clubs, without duplicating venue records
- Club profile, club rules vs venue rules, and competition calendar
- Competitions that can be club-organised, member-only or open, and associated with one or more FishingVenues
- Privacy boundaries so club membership cannot expose private notes, private coordinates, unrelated catches or the user's full logbook
- Future privacy-preserving aggregate club statistics from catches on club-managed waters (not MVP UI)
- Future payment compatibility for membership fees, competition entry, venue bookings and day tickets without introducing a payment provider in the MVP
- Search/discovery of clubs and claimed-profile / ownership-dispute administration

Do not create Club entities, membership tables, migrations, payment integrations or Terraform changes as part of this requirements update.

---