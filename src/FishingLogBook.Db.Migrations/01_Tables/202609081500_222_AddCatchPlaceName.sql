alter table catches
    add column if not exists placename text null;

do $$
begin
    if not exists (
        select 1
        from pg_constraint
        where conname = 'ckcatchesplacenamelength'
            and conrelid = 'catches'::regclass) then
        alter table catches
            add constraint ckcatchesplacenamelength
                check (placename is null or (btrim(placename) <> '' and length(placename) <= 160));
    end if;

    if not exists (
        select 1
        from pg_constraint
        where conname = 'ckcatchesplacenamehaslocation'
            and conrelid = 'catches'::regclass) then
        alter table catches
            add constraint ckcatchesplacenamehaslocation
                check (placename is null or latitude is not null);
    end if;
end
$$;
