# FishingLogBook

## Product Requirements Document

**Document Status:** Initial MVP Requirements  
**Product:** FishingLogBook  
**Initial Target:** Progressive Web Application  
**Primary Users:** Anglers, Fishing Guides, Fisheries and Competition Organisers

---

## 1. Product Vision

FishingLogBook is a mobile-first fishing platform allowing anglers to record, preserve and explore their fishing history.

The application should be useful to an angler regardless of whether a fishery, guide or competition organiser is registered with FishingLogBook.

Over time, FishingLogBook should connect anglers with fisheries, fishing guides and competitions through a common platform.

The product should ultimately provide value to four groups:

- Recreational anglers
- Fishing guides
- Fisheries
- Fishing competition organisers

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
- save the catch
- view locally recorded catches

while completely offline.

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

A user may additionally have one or more capabilities or roles.

Examples include:

- Angler
- Guide
- Fishery Manager
- Competition Organiser
- Administrator

These roles must not be mutually exclusive.

For example, a fishing guide may also maintain their own personal fishing log.

A fishery manager may also participate in competitions as an angler.

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

Precise fishing locations should not automatically become public.

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
- Location
- Fishery
- Fishing method
- Bait or lure
- Notes
- Angler
- Person who recorded the catch
- Guide trip, where applicable
- Competition, where applicable

Not every field should be mandatory.

At minimum, users should be able to record a catch quickly with a photograph and basic information and add additional information later.

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
- fishery filtering
- location filtering
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
- Catches by fishery
- Largest fish
- Most frequently caught species

Advanced analytics are outside the initial MVP.

---

# 11. Fisheries Directory

FishingLogBook should maintain a searchable directory of fisheries.

A fishery does not need to have joined FishingLogBook before appearing in the directory.

Users should be able to discover fisheries and record catches against them.

Fishery owners or authorised managers should later be able to claim an existing fishery profile.

---

# 12. Fishery Profiles

A fishery profile should contain:

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

The profile should clearly indicate whether the fishery is currently available for fishing.

---

# 13. Fishery Seasons

Season information is an MVP requirement.

Fisheries must be able to define:

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

# 14. Fishery Opening Hours

Fisheries should be able to define normal operating hours independently from their fishing season.

Opening hours should support different times for different days.

Season status takes precedence over normal opening hours.

For example, a fishery normally open from 06:00 to 20:00 should still display as closed when outside its fishing season.

---

# 15. Fishery Facilities

Fishery profiles should support facilities such as:

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

# 16. Fishery Rules

Fisheries must be able to publish rules.

Examples include:

- Catch and release requirements
- Keep limits
- Permitted methods
- Bait restrictions
- Hook restrictions
- Minimum or maximum sizes
- Required equipment
- Boat restrictions

Rules should be easily accessible from the fishery profile.

---

# 17. Fishery Ratings and Reviews

Users must be able to rate and review fisheries.

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

# 18. Verified Fishery Reviews

The system should be designed to distinguish verified visits from unverified reviews.

Evidence of a verified visit may eventually include:

- A booking made through FishingLogBook
- A catch recorded at the fishery
- Participation in a competition at the fishery
- A guided trip associated with the fishery

Verified reviews should be visually distinguishable.

Full automated verification rules may evolve after the MVP.

---

# 19. Fishery Availability

Fisheries should be able to publish fishing availability.

Depending on the type of fishery this may represent:

- Day sessions
- Half-day sessions
- Pegs
- Swims
- Boats
- Fishing areas

Users should be able to see availability before deciding to visit.

---

# 20. Fishery Bookings

The system should support anglers reserving fishing through FishingLogBook.

The MVP should support the underlying booking workflow.

Online payment processing may be introduced separately if it materially increases the initial implementation scope.

A booking should associate:

- User
- Fishery
- Date
- Session
- Peg/swim/boat where applicable
- Booking status

---

# 21. Fishing Licences and Day Tickets

The product should be designed so fisheries can offer:

- Day tickets
- Fishing permits
- Fishery-specific licences

Users should eventually be able to purchase or obtain these through FishingLogBook.

Payment processing can be introduced incrementally.

---

# 22. Fishing Guides

Fishing guides are a first-class user group within FishingLogBook.

A guide should use the same normal user account as any other angler while having additional guide capabilities.

---

# 23. Guide Profiles

A guide profile should support:

- Guide name
- Profile photograph
- Description
- Areas covered
- Fisheries covered
- Fishing types
- Target species
- Contact information
- Guide services
- Ratings
- Reviews

Future versions may include pricing and availability.

---

# 24. Guide Clients

Guides must be able to associate FishingLogBook users with guided trips.

A client should not need to hand their phone to the guide for every catch.

The guide should be able to record catches directly for clients participating in the trip.

---

# 25. Guided Trips

A guide should be able to create a guided fishing session.

A trip should support:

- Guide
- Date
- Start/end times
- General location
- Fishery where applicable
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

# 26. Guide-Recorded Catches

During a guided trip, the guide must be able to create a catch on behalf of a client.

The catch should automatically appear in the client's FishingLogBook once synchronised.

The catch must retain:

- Angler identity
- Guide identity
- Guided trip
- Date/time
- Location/fishery
- Catch information

The catch should be identifiable as having been recorded by a guide.

---

# 27. Guide Ratings and Reviews

Users should be able to rate and review fishing guides.

The system should be designed to support verified reviews where the reviewer participated in a guided trip.

---

# 28. Competitions

FishingLogBook should support local fishing competitions.

Competition organisers should be able to create a competition.

A competition should support:

- Name
- Description
- Location/fishery
- Start date/time
- End date/time
- Rules
- Participants
- Eligible species
- Scoring method
- Catch submissions
- Results

The initial implementation should concentrate on straightforward local competitions rather than supporting every possible competition format.

---

# 29. Competition Catch Submission

Competition participants should be able to associate catches with an active competition.

Competition catches should retain the same normal FishingLogBook catch record while additionally referencing the competition.

Competition submission must continue to work offline.

The original capture time must be retained regardless of when the catch is synchronised.

---

# 30. Competition Verification

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

# 31. Competition Leaderboards

Competitions should have a leaderboard based on their configured scoring method.

The MVP should support at least simple ranking based on:

- Weight
- Length
- Number of qualifying catches

More sophisticated scoring systems can be introduced later.

---

# 32. Location and Privacy

Location information is useful but potentially sensitive.

FishingLogBook must not assume that anglers want exact catch locations publicly visible.

Catch location visibility should support privacy controls.

Potential visibility levels include:

- Private
- Approximate area
- Fishery only
- Public location

Exact coordinates should be treated as private information unless the user explicitly chooses otherwise.

Competition rules may require location information to be supplied privately to organisers without making it publicly visible.

---

# 33. Offline Synchronisation

The system must treat local data as authoritative until successfully synchronised.

Each offline-created record should have a unique identifier generated on the device.

Synchronisation must avoid creating duplicate catches if the same record is retried.

The user must be able to see outstanding synchronisation activity.

Network failure during synchronisation must not result in data loss.

Photograph uploads should be capable of retrying independently where appropriate.

---

# 34. Multiple Devices

A user may access FishingLogBook from multiple devices.

Once data has successfully synchronised, it should be available from the user's other devices.

The MVP does not need sophisticated simultaneous offline editing conflict resolution.

Conflicts must, however, never silently cause a catch to disappear.

---

# 35. Individual Fish Identification

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

The user or fishery may then confirm or reject the match.

---

# 36. Fish History

Future individual fish identification could allow FishingLogBook to build a history for a particular fish.

Potential information includes:

- Previous captures
- Previous weights
- Previous lengths
- Growth
- Fisheries
- Capture dates
- Photographs

This could provide significant value to fisheries interested in monitoring fish populations and growth.

---

# 37. Species Recognition

Automatic species identification from photographs is a future capability.

Users must always be able to manually select or correct the species.

Automated species recognition must not silently replace user-entered information.

---

# 38. Notifications

The product should be designed to support future notifications including:

- Booking confirmations
- Competition updates
- Guide invitations
- Guided trip updates
- Fishery closures
- Season opening reminders
- Synchronisation problems

Full notification functionality is not required for the initial MVP unless needed by a core workflow.

---

# 39. Search and Discovery

Users should be able to search for fisheries and guides.

Future search filters may include:

- Location
- Distance
- Species
- Fishing type
- Rating
- Currently open
- In season
- Facilities
- Availability

The initial search implementation may use a smaller subset.

---

# 40. Administration

FishingLogBook requires basic administrative functionality.

Administrators should be able to:

- Manage users
- Manage fisheries
- Manage claimed fishery profiles
- Manage guides
- Review reported reviews
- Moderate inappropriate content
- Manage competitions where necessary

Administration can initially be functional rather than highly polished.

---

# 41. Data Ownership

Users should retain access to their fishing history.

The product should eventually provide a mechanism for users to export their catch data.

Deleting an account must follow applicable data protection requirements.

Shared records such as competition results, guide records and fishery statistics may require retention or anonymisation rather than simple deletion.

Detailed retention rules will be defined separately.

---

# 42. MVP Scope

The MVP should concentrate on proving that FishingLogBook is genuinely useful beside the water.

The MVP includes:

### Anglers

- User registration/login
- User profile
- Catch recording
- Multiple catch photographs
- Offline catch recording
- Offline photograph storage
- Synchronisation
- Catch history
- Personal bests
- Basic statistics
- Location privacy

### Fisheries

- Fisheries directory
- Fishery profile
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
- Role/capability model
- Basic administration

---

# 43. Explicitly Outside Initial MVP

The following should be considered during architecture but are not required before the first usable release:

- Individual fish AI recognition
- Automatic species recognition
- Advanced fishery analytics
- Weather integration
- Tide integration
- Complex competition scoring engines
- Social feeds
- Direct messaging
- Advanced push notifications
- Native Android application
- Native iOS application
- Complete translation of every future screen into additional languages beyond English and French

These features should not delay delivery of the core FishingLogBook.

---

# 44. Initial User Journey - Recreational Angler

1. User installs or opens FishingLogBook.
2. User creates an account.
3. User goes fishing.
4. Network connectivity becomes unavailable.
5. User catches a fish.
6. User opens FishingLogBook.
7. User photographs the fish.
8. User records basic catch information.
9. Catch is saved immediately on the device.
10. Catch appears in the user's logbook.
11. User continues fishing.
12. Connectivity later returns.
13. FishingLogBook synchronises the catch and photographs.
14. User can subsequently access the catch from another device.

This workflow is a critical MVP acceptance scenario.

---

# 45. Initial User Journey - Guided Fishing

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

# 46. Initial User Journey - Fishery Discovery

1. User searches for a fishery.
2. User opens the fishery profile.
3. User sees species, facilities and rules.
4. User sees whether the fishery is currently in season.
5. User sees today's opening status.
6. User reads ratings and reviews.
7. User checks availability.
8. User books or obtains the information required to fish.
9. User later records catches against that fishery.
10. User can leave feedback following the visit.

---

# 47. Initial User Journey - Competition

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

# 48. MVP Success Criteria

The initial FishingLogBook MVP should be considered technically successful when:

- It can be installed on a mobile device as a PWA.
- A user can record catches with no network connection.
- Offline catches survive application/device interruptions within reasonable browser limitations.
- Photographs synchronise reliably when connectivity returns.
- A user's synchronised fishing history is available across devices.
- Fisheries can maintain meaningful profiles and seasonal information.
- Users can review fisheries.
- Guides can create trips and record catches for clients.
- Local competitions can collect catch submissions.
- The same application works effectively on modern Android and iOS devices without requiring native applications.

The product should be considered commercially validated separately based on actual usage by anglers, fisheries and guides.

---

# 49. Next Document

The next project document will be:

**Architecture and Technical Requirements**

It will define:

- Application architecture
- PWA implementation
- Offline storage
- Synchronisation architecture
- Image storage and processing
- API architecture
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