alter table if exists parent_auth_links add column if not exists display_name text;

create table if not exists parent_invites(
  id uuid primary key,
  parent_id uuid not null references parents(id) on delete cascade,
  email text not null,
  token text not null unique,
  created_by text,
  created_at timestamptz not null default now(),
  expires_at timestamptz not null,
  accepted_at timestamptz,
  accepted_by text,
  cancelled_at timestamptz
);

create index if not exists idx_parent_invites_parent on parent_invites(parent_id);
create index if not exists idx_parent_invites_token on parent_invites(token);
