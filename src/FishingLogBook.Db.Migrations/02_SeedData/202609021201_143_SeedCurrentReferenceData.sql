insert into systemhealth (id, name, createdon)
select gen_random_uuid(), 'CatchButDontForget database online', now()
where not exists (select 1 from systemhealth);

insert into platformcapabilities (code)
select v.code
from (values
    ('Guide'),
    ('FishingVenueManager'),
    ('CompetitionOrganiser'),
    ('Administrator')
) as v(code)
where not exists (
    select 1
    from platformcapabilities existing
    where existing.code = v.code);

insert into fishingmethods (id, code, name)
select v.id::uuid, v.code, v.name
from (values
    ('6cebaa41-de9c-4264-8034-03636b05f3c0', 'Fly', 'Fly'),
    ('83bf17bb-a4ea-4e81-80c5-cc1d66a19192', 'Spinning', 'Spinning'),
    ('e4ff390b-dc21-4191-93cb-c3c1140f7d52', 'Bait', 'Bait'),
    ('eaa57301-5bd4-406c-abd8-2da9d904ac2a', 'Lure', 'Lure'),
    ('84ee2982-2417-4988-8a1d-4de071431d5c', 'Trolling', 'Trolling')
) as v(id, code, name)
where not exists (
    select 1
    from fishingmethods existing
    where existing.code = v.code);

insert into species (id, code, name)
select v.id::uuid, v.code, v.name
from (values
    ('51e3798c-3d4d-438a-86fc-1b1ada032a83', 'BrownTrout', 'Brown Trout'),
    ('a53d8f1b-8389-40ff-8c28-a19b2e3fc844', 'RainbowTrout', 'Rainbow Trout'),
    ('07c4e443-89aa-44c4-96f1-493b0ce3d30a', 'BrookTrout', 'Brook Trout'),
    ('628fcd12-1a16-43de-b5a5-6f8e344b33aa', 'SeaTrout', 'Sea Trout'),
    ('7ff7ce2c-59da-451e-afcc-66bee96ee267', 'Salmon', 'Salmon'),
    ('1e44f108-5d03-46f6-a390-d24a972d93dc', 'Pike', 'Pike'),
    ('2a39b998-a224-4889-b78b-2c38566f4109', 'Perch', 'Perch'),
    ('efc24354-8200-45b0-b559-62e78d39ee3f', 'Carp', 'Carp'),
    ('a8639e2c-9ef4-491f-84cb-c978bc042b92', 'Bream', 'Bream'),
    ('faddfb08-5bd9-4c03-a002-0d19ee487a96', 'Roach', 'Roach'),
    ('617c0392-0a65-42c2-ac7d-2ab63c0565f8', 'Tench', 'Tench'),
    ('acf02f74-ffe3-4bcd-b730-c573aa67bab1', 'Grayling', 'Grayling')
) as v(id, code, name)
where not exists (
    select 1
    from species existing
    where existing.code = v.code);
