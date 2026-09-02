create table if not exists systemhealth
(
    id uuid not null,
    name text not null,
    createdon timestamptz not null default now(),
    constraint pksystemhealth primary key (id)
);

create table if not exists users
(
    id uuid not null,
    email text not null,
    createdon timestamptz not null default now(),
    offlineaccessenabled boolean not null default false,
    offlineaccessenabledat timestamptz null,
    constraint pkusers primary key (id)
);

create table if not exists useridentities
(
    id uuid not null,
    userid uuid not null,
    provider text not null,
    subject text not null,
    createdon timestamptz not null default now(),
    constraint pkuseridentities primary key (id),
    constraint fkuseridentitiesuser foreign key (userid) references users (id),
    constraint uxuseridentitiesprovidersubject unique (provider, subject)
);

create index if not exists ixuseridentitiesuserid on useridentities (userid);

create table if not exists profiles
(
    userid uuid not null,
    displayname text null,
    photographid uuid null,
    photographobjectkey text null,
    photographcontenttype text null,
    homeregion text null,
    preferredweightunit integer not null default 0,
    preferredlengthunit integer not null default 0,
    showdisplayname boolean not null default true,
    showphotograph boolean not null default false,
    showhomeregion boolean not null default false,
    showpreferredfishingmethods boolean not null default false,
    showpreferredspecies boolean not null default false,
    onboardingcompletedon timestamptz null,
    createdon timestamptz not null default now(),
    updatedon timestamptz not null default now(),
    constraint pkprofiles primary key (userid),
    constraint fkprofilesuser foreign key (userid) references users (id),
    constraint ckprofilespreferredweightunit check (preferredweightunit in (0, 1)),
    constraint ckprofilespreferredlengthunit check (preferredlengthunit in (0, 1))
);

create table if not exists platformcapabilities
(
    code text not null,
    createdon timestamptz not null default now(),
    constraint pkplatformcapabilities primary key (code)
);

create table if not exists userplatformcapabilities
(
    userid uuid not null,
    capabilitycode text not null,
    createdon timestamptz not null default now(),
    constraint pkuserplatformcapabilities primary key (userid, capabilitycode),
    constraint fkuserplatformcapabilitiesuser foreign key (userid) references users (id),
    constraint fkuserplatformcapabilitiescode foreign key (capabilitycode) references platformcapabilities (code)
);

create table if not exists fishingmethods
(
    id uuid not null,
    code text not null,
    name text not null,
    createdon timestamptz not null default now(),
    constraint pkfishingmethods primary key (id),
    constraint uxfishingmethodscode unique (code)
);

create table if not exists species
(
    id uuid not null,
    code text not null,
    name text not null,
    createdon timestamptz not null default now(),
    constraint pkspecies primary key (id),
    constraint uxspeciescode unique (code)
);

create table if not exists userfishingmethodpreferences
(
    userid uuid not null,
    fishingmethodid uuid not null,
    isdefault boolean not null default false,
    createdon timestamptz not null default now(),
    constraint pkuserfishingmethodpreferences primary key (userid, fishingmethodid),
    constraint fkuserfishingmethodpreferencesuser foreign key (userid) references users (id),
    constraint fkuserfishingmethodpreferencesfishingmethod foreign key (fishingmethodid) references fishingmethods (id)
);

create unique index if not exists uxuserfishingmethodpreferencesdefault
    on userfishingmethodpreferences (userid)
    where isdefault = true;

create table if not exists userfishingspeciespreferences
(
    userid uuid not null,
    fishingmethodid uuid not null,
    speciesid uuid not null,
    isdefault boolean not null default false,
    createdon timestamptz not null default now(),
    constraint pkuserfishingspeciespreferences primary key (userid, fishingmethodid, speciesid),
    constraint fkuserfishingspeciespreferencesusermethod
        foreign key (userid, fishingmethodid)
        references userfishingmethodpreferences (userid, fishingmethodid),
    constraint fkuserfishingspeciespreferencesspecies foreign key (speciesid) references species (id)
);

create unique index if not exists uxuserfishingspeciespreferencesdefault
    on userfishingspeciespreferences (userid, fishingmethodid)
    where isdefault = true;

create table if not exists trips
(
    id uuid not null,
    owneruserid uuid not null,
    title text null,
    placename text null,
    status text not null,
    startedon timestamptz not null,
    endedon timestamptz null,
    latitude double precision null,
    longitude double precision null,
    locationaccuracymetres double precision null,
    locationcapturedon timestamptz null,
    locationsource text null,
    locationvisibility text null,
    locationconsentversion text null,
    createdon timestamptz not null default now(),
    updatedon timestamptz not null default now(),
    constraint pktrips primary key (id),
    constraint fktripsowneruser foreign key (owneruserid) references users (id),
    constraint cktripsstatusallowed check (status in ('Active', 'Completed')),
    constraint cktripsendedafterstarted check (endedon is null or endedon >= startedon),
    constraint cktripsactivehasnoend check (status <> 'Active' or endedon is null),
    constraint cktripslocationcoherent check
    (
        (
            latitude is null
            and longitude is null
            and locationaccuracymetres is null
            and locationcapturedon is null
            and locationsource is null
            and locationvisibility is null
            and locationconsentversion is null
        )
        or
        (
            latitude is not null
            and longitude is not null
            and latitude between -90 and 90
            and longitude between -180 and 180
            and locationcapturedon is not null
            and locationsource is not null
            and locationvisibility is not null
            and locationconsentversion is not null
        )
    ),
    constraint cktripslocationvisibilityallowed check
    (
        locationvisibility is null
        or locationvisibility in ('Private', 'Approximate', 'FishingVenueOnly', 'Public')
    )
);

create index if not exists ixtripsowneruserid on trips (owneruserid);

create unique index if not exists uxtripsowneractive
    on trips (owneruserid)
    where status = 'Active';

create table if not exists tripparticipants
(
    id uuid not null,
    tripid uuid not null,
    userid uuid not null,
    status text not null,
    invitedbyuserid uuid not null,
    invitedon timestamptz not null,
    respondedon timestamptz null,
    removedon timestamptz null,
    createdon timestamptz not null default now(),
    updatedon timestamptz not null default now(),
    constraint pktripparticipants primary key (id),
    constraint fktripparticipantstrip foreign key (tripid) references trips (id),
    constraint fktripparticipantsuser foreign key (userid) references users (id),
    constraint fktripparticipantsinvitedbyuser foreign key (invitedbyuserid) references users (id),
    constraint cktripparticipantsstatusallowed check (status in ('Pending', 'Accepted', 'Declined')),
    constraint cktripparticipantsrespondedafterinvited check (respondedon is null or respondedon >= invitedon),
    constraint cktripparticipantspendinghasnoresponse check (status <> 'Pending' or respondedon is null),
    constraint cktripparticipantsnotselfinvited check (userid <> invitedbyuserid),
    constraint cktripparticipantsremovedwasaccepted check (removedon is null or status = 'Accepted')
);

create unique index if not exists uxtripparticipantstripuser
    on tripparticipants (tripid, userid);

create index if not exists ixtripparticipantsuserstatus
    on tripparticipants (userid, status);

create table if not exists tripphotographs
(
    id uuid not null,
    tripid uuid not null,
    objectkey text not null,
    contenttype text not null,
    capturedon timestamptz null,
    addedon timestamptz not null,
    contributedbyuserid uuid not null,
    createdon timestamptz not null default now(),
    updatedon timestamptz not null default now(),
    constraint pktripphotographs primary key (id),
    constraint fktripphotographstrip foreign key (tripid) references trips (id),
    constraint fktripphotographscontributedbyuser foreign key (contributedbyuserid) references users (id)
);

create index if not exists ixtripphotographstripid on tripphotographs (tripid);
create unique index if not exists uxtripphotographsobjectkey on tripphotographs (objectkey);
create index if not exists ixtripphotographscontributedbyuserid on tripphotographs (contributedbyuserid);

create table if not exists tripnotes
(
    id uuid not null,
    tripid uuid not null,
    createdbyuserid uuid not null,
    text text not null,
    recordedon timestamptz not null,
    createdon timestamptz not null default now(),
    updatedon timestamptz not null default now(),
    constraint pktripnotes primary key (id),
    constraint fktripnotestrip foreign key (tripid) references trips (id),
    constraint fktripnotescreatedbyuser foreign key (createdbyuserid) references users (id)
);

create index if not exists ixtripnotestripid on tripnotes (tripid);
create index if not exists ixtripnotestriprecordedon on tripnotes (tripid, recordedon);

create table if not exists userfishinglocationpreferences
(
    id uuid not null,
    userid uuid not null,
    name text not null,
    isdefault boolean not null default false,
    createdon timestamptz not null default now(),
    constraint pkuserfishinglocationpreferences primary key (id),
    constraint fkuserfishinglocationpreferencesuser foreign key (userid) references users (id),
    constraint ckuserfishinglocationpreferencesname check (btrim(name) <> '' and length(name) <= 160)
);

create index if not exists ixuserfishinglocationpreferencesuserid
    on userfishinglocationpreferences (userid);

create unique index if not exists uxuserfishinglocationpreferencesname
    on userfishinglocationpreferences (userid, lower(btrim(name)));

create unique index if not exists uxuserfishinglocationpreferencesdefault
    on userfishinglocationpreferences (userid)
    where isdefault = true;

create table if not exists catches
(
    id uuid not null,
    caughtbyuserid uuid not null,
    recordedbyuserid uuid not null,
    caughton timestamptz not null,
    createdon timestamptz not null default now(),
    latitude double precision null,
    longitude double precision null,
    locationaccuracymetres double precision null,
    locationcapturedon timestamptz null,
    locationsource text null,
    locationvisibility text null,
    locationconsentversion text null,
    speciesname text null,
    weight numeric(8, 3) null,
    length numeric(8, 2) null,
    method text null,
    baitorlure text null,
    notes text null,
    tripid uuid null,
    constraint pkcatches primary key (id),
    constraint fkcatchescaughtbyuser foreign key (caughtbyuserid) references users (id),
    constraint fkcatchesrecordedbyuser foreign key (recordedbyuserid) references users (id),
    constraint fkcatchestrip foreign key (tripid) references trips (id) on delete set null,
    constraint ckcatcheslocationcoherent check
    (
        (
            latitude is null
            and longitude is null
            and locationaccuracymetres is null
            and locationcapturedon is null
            and locationsource is null
            and locationvisibility is null
            and locationconsentversion is null
        )
        or
        (
            latitude is not null
            and longitude is not null
            and latitude between -90 and 90
            and longitude between -180 and 180
            and locationcapturedon is not null
            and locationsource is not null
            and locationvisibility is not null
            and locationconsentversion is not null
        )
    ),
    constraint ckcatcheslocationvisibilityallowed check
    (
        locationvisibility is null
        or locationvisibility in ('Private', 'Approximate', 'FishingVenueOnly', 'Public')
    ),
    constraint ckcatchesweightrange check (weight is null or (weight > 0 and weight <= 1000)),
    constraint ckcatcheslengthrange check (length is null or (length > 0 and length <= 1000))
);

create index if not exists ixcatchescaughtbyuserid on catches (caughtbyuserid);
create index if not exists ixcatchestripid on catches (tripid);

create table if not exists catchphotographs
(
    id uuid not null,
    catchid uuid not null,
    contenttype text not null,
    constraint pkcatchphotographs primary key (id),
    constraint fkcatchphotographscatch foreign key (catchid) references catches (id)
);

create index if not exists ixcatchphotographscatchid on catchphotographs (catchid);
