create table if not exists parent_ai_keys (
    parent_id uuid primary key references parents(id) on delete cascade,
    api_key_cipher text not null,
    api_key_iv text not null,
    api_key_tag text not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);
