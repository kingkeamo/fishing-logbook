alter table trips
    add column if not exists plannedstarton timestamptz null;

alter table trips
    drop constraint if exists cktripsstatusallowed;

alter table trips
    add constraint cktripsstatusallowed
    check (status in ('Active', 'Completed', 'Planned', 'Cancelled'));

alter table trips
    drop constraint if exists cktripsplannedhasplannedstart;

alter table trips
    add constraint cktripsplannedhasplannedstart
    check (status not in ('Planned', 'Cancelled') or plannedstarton is not null);

alter table trips
    drop constraint if exists cktripsplannedhasnoend;

alter table trips
    add constraint cktripsplannedhasnoend
    check (status not in ('Planned', 'Cancelled') or endedon is null);

create index if not exists ixtripsplannedstarton
    on trips (plannedstarton)
    where status = 'Planned';
