create table if not exists parents (
    id uuid primary key,
    email text unique not null,
    created_date timestamptz not null default now()
);

create table if not exists children (
    id uuid primary key,
    parent_id uuid not null references parents(id) on delete cascade,
    name text not null,
    dollar_per_point numeric(12,2) not null default 1.00,
    created_date timestamptz not null default now()
);

create table if not exists deed_types (
    id uuid primary key,
    parent_id uuid not null references parents(id) on delete cascade,
    name text not null,
    points integer not null,
    active boolean not null default true,
    unique(parent_id, name)
);

create table if not exists deeds (
    id uuid primary key,
    child_id uuid not null references children(id) on delete cascade,
    deed_type_id uuid not null references deed_types(id),
    points integer not null,
    note text,
    occurred_at timestamptz not null,
    created_by text not null,
    created_at timestamptz not null default now()
);

create table if not exists redemptions (
    id uuid primary key,
    child_id uuid not null references children(id) on delete cascade,
    points integer not null check(points > 0),
    description text,
    created_by text not null,
    created_at timestamptz not null default now()
);
