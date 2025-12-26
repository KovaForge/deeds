create table if not exists parent_auth_links (
    provider text not null,
    user_id text not null,
    parent_id uuid not null references parents(id) on delete cascade,
    email text,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key(provider, user_id)
);
