using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.WebUtilities;
using Npgsql;

public record DeedDetails(Guid Id, Guid ChildId, Guid ParentId);
public record DeedTypeDetails(Guid Id, Guid ParentId);
public record AiKeyRecord(string CipherText, string Nonce, string Tag);
public record ParentAuthLink(Guid ParentId, string Provider, string UserId, string? Email, string? DisplayName);
public record ParentInvite(Guid Id, Guid ParentId, string Email, string Token, DateTime ExpiresAtUtc, DateTime CreatedAtUtc, string? CreatedBy, DateTime? AcceptedAtUtc, DateTime? CancelledAtUtc);
public record ParentInviteSummary(Guid Id, Guid ParentId, string Email, DateTime ExpiresAtUtc, DateTime CreatedAtUtc, string? CreatedBy);

public static class Data
{
  public static NpgsqlConnection Conn(string cs) => new NpgsqlConnection(cs);

  public static async Task EnsureSchema(string cs)
  {
    const string sql = @"
create table if not exists parents(
  id uuid primary key,
  email text unique not null,
  created_date timestamptz not null default now()
);

create table if not exists children(
  id uuid primary key,
  parent_id uuid not null references parents(id) on delete cascade,
  name text not null,
  dollar_per_point numeric(12,2) not null default 1.00,
  created_date timestamptz not null default now()
);

create table if not exists deed_types(
  id uuid primary key,
  parent_id uuid not null references parents(id) on delete cascade,
  name text not null,
  points integer not null,
  active boolean not null default true,
  unique(parent_id, name)
);

create table if not exists redeem_types(
  id uuid primary key,
  parent_id uuid not null references parents(id) on delete cascade,
  name text not null,
  points integer not null,
  active boolean not null default true,
  unique(parent_id, name)
);

create table if not exists deeds(
  id uuid primary key,
  child_id uuid not null references children(id) on delete cascade,
  deed_type_id uuid not null references deed_types(id),
  points integer not null,
  note text,
  occurred_at timestamptz not null,
  created_by text not null,
  created_at timestamptz not null default now()
);

create table if not exists redemptions(
  id uuid primary key,
  child_id uuid not null references children(id) on delete cascade,
  points integer not null check(points > 0),
  description text,
  redeem_type_id uuid references redeem_types(id),
  created_by text not null,
  created_at timestamptz not null default now()
);

create table if not exists parent_ai_keys(
  parent_id uuid primary key references parents(id) on delete cascade,
  api_key_cipher text not null,
  api_key_iv text not null,
  api_key_tag text not null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists parent_auth_links(
  provider text not null,
  user_id text not null,
  parent_id uuid not null references parents(id) on delete cascade,
  email text,
  display_name text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  primary key(provider, user_id)
);

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
create index if not exists idx_parent_invites_token on parent_invites(token);";
    await using var db = Conn(cs);
    await db.ExecuteAsync(sql);
    await db.ExecuteAsync("alter table if exists redemptions add column if not exists redeem_type_id uuid references redeem_types(id);");
  }

  public static async Task<ParentDto?> GetParentById(string cs, Guid id)
  {
    const string sql = "select id as Id, email as Email from parents where id = @Id";
    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<ParentDto>(sql, new { Id = id });
  }

  public static async Task<ParentDto?> GetParentByEmail(string cs, string email)
  {
    const string sql = "select id as Id, email as Email from parents where email = @Email";
    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<ParentDto>(sql, new { Email = email });
  }

  public static async Task<ParentDto> CreateParent(string cs, string email)
  {
    const string sql = @"
insert into parents(id, email)
values(@Id, @Email)
returning id as Id, email as Email;";

    await using var db = Conn(cs);
    var id = Guid.NewGuid();
    return await db.QuerySingleAsync<ParentDto>(sql, new { Id = id, Email = email });
  }

  public static async Task<ChildDto?> GetChildById(string cs, Guid id)
  {
    const string sql = "select id as Id, parent_id as ParentId, name as Name, dollar_per_point as DollarPerPoint from children where id = @Id";
    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<ChildDto>(sql, new { Id = id });
  }

  public static async Task<bool> DeleteChild(string cs, Guid childId)
  {
    const string sql = "delete from children where id = @Id";
    await using var db = Conn(cs);
    var affected = await db.ExecuteAsync(sql, new { Id = childId });
    return affected > 0;
  }

  public static async Task<IEnumerable<ChildDto>> GetChildrenForParent(string cs, Guid parentId)
  {
    const string sql = "select id as Id, parent_id as ParentId, name as Name, dollar_per_point as DollarPerPoint from children where parent_id = @ParentId order by created_date";
    await using var db = Conn(cs);
    return await db.QueryAsync<ChildDto>(sql, new { ParentId = parentId });
  }

  public static async Task<ChildDto> CreateChild(string cs, Guid parentId, string name, decimal dollarPerPoint)
  {
    const string sql = @"
insert into children(id, parent_id, name, dollar_per_point)
values(@Id, @ParentId, @Name, @DollarPerPoint)
returning id as Id, parent_id as ParentId, name as Name, dollar_per_point as DollarPerPoint;";

    await using var db = Conn(cs);
    var id = Guid.NewGuid();
    return await db.QuerySingleAsync<ChildDto>(sql, new
    {
      Id = id,
      ParentId = parentId,
      Name = name,
      DollarPerPoint = dollarPerPoint
    });
  }

  public static async Task<ChildDto?> UpdateChild(string cs, Guid id, string name, decimal dollarPerPoint)
  {
    const string sql = @"
update children
set name = @Name,
  dollar_per_point = @DollarPerPoint
where id = @Id
returning id, parent_id, name, dollar_per_point;";

    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<ChildDto>(sql, new
    {
      Id = id,
      Name = name,
      DollarPerPoint = dollarPerPoint
    });
  }

  public static async Task<DeedTypeDto?> GetDeedTypeById(string cs, Guid id)
  {
    const string sql = "select id as Id, parent_id as ParentId, name as Name, points as Points, active as Active from deed_types where id = @Id";
    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<DeedTypeDto>(sql, new { Id = id });
  }

  public static async Task<IEnumerable<DeedTypeDto>> GetDeedTypesForParent(string cs, Guid parentId)
  {
    const string sql = "select id as Id, parent_id as ParentId, name as Name, points as Points, active as Active from deed_types where parent_id = @ParentId order by name";
    await using var db = Conn(cs);
    return await db.QueryAsync<DeedTypeDto>(sql, new { ParentId = parentId });
  }

  public static async Task<DeedTypeDto?> GetDeedTypeByName(string cs, Guid parentId, string name)
  {
    const string sql = "select id as Id, parent_id as ParentId, name as Name, points as Points, active as Active from deed_types where parent_id = @ParentId and lower(name) = lower(@Name)";
    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<DeedTypeDto>(sql, new { ParentId = parentId, Name = name });
  }

  public static async Task<int> CountDeedsUsingDeedType(string cs, Guid deedTypeId)
  {
    const string sql = "select count(*) from deeds where deed_type_id = @DeedTypeId";
    await using var db = Conn(cs);
    return await db.ExecuteScalarAsync<int>(sql, new { DeedTypeId = deedTypeId });
  }

  public static async Task<DeedTypeDto> CreateDeedType(string cs, Guid parentId, string name, int points)
  {
    const string sql = @"
insert into deed_types(id, parent_id, name, points, active)
values(@Id, @ParentId, @Name, @Points, true)
returning id as Id, parent_id as ParentId, name as Name, points as Points, active as Active;";

    await using var db = Conn(cs);
    var id = Guid.NewGuid();
    return await db.QuerySingleAsync<DeedTypeDto>(sql, new
    {
      Id = id,
      ParentId = parentId,
      Name = name,
      Points = points
    });
  }

  public static async Task<DeedTypeDto?> UpdateDeedType(string cs, Guid id, string name, int points, bool active)
  {
    const string sql = @"
update deed_types
set name = @Name,
  points = @Points,
  active = @Active
where id = @Id
returning id as Id, parent_id as ParentId, name as Name, points as Points, active as Active;";

    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<DeedTypeDto>(sql, new
    {
      Id = id,
      Name = name,
      Points = points,
      Active = active
    });
  }

  public static async Task<bool> DeleteDeedType(string cs, Guid deedTypeId)
  {
    const string sql = "delete from deed_types where id = @Id";
    await using var db = Conn(cs);
    var affected = await db.ExecuteAsync(sql, new { Id = deedTypeId });
    return affected > 0;
  }

  public static async Task<RedeemTypeDto?> GetRedeemTypeById(string cs, Guid id)
  {
    const string sql = "select id as Id, parent_id as ParentId, name as Name, points as Points, active as Active from redeem_types where id = @Id";
    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<RedeemTypeDto>(sql, new { Id = id });
  }

  public static async Task<IEnumerable<RedeemTypeDto>> GetRedeemTypesForParent(string cs, Guid parentId)
  {
    const string sql = "select id as Id, parent_id as ParentId, name as Name, points as Points, active as Active from redeem_types where parent_id = @ParentId order by name";
    await using var db = Conn(cs);
    return await db.QueryAsync<RedeemTypeDto>(sql, new { ParentId = parentId });
  }

  public static async Task<RedeemTypeDto?> GetRedeemTypeByName(string cs, Guid parentId, string name)
  {
    const string sql = "select id as Id, parent_id as ParentId, name as Name, points as Points, active as Active from redeem_types where parent_id = @ParentId and lower(name) = lower(@Name)";
    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<RedeemTypeDto>(sql, new { ParentId = parentId, Name = name });
  }

  public static async Task<RedeemTypeDto> CreateRedeemType(string cs, Guid parentId, string name, int points)
  {
    const string sql = @"
insert into redeem_types(id, parent_id, name, points, active)
values(@Id, @ParentId, @Name, @Points, true)
returning id as Id, parent_id as ParentId, name as Name, points as Points, active as Active;";

    await using var db = Conn(cs);
    var id = Guid.NewGuid();
    return await db.QuerySingleAsync<RedeemTypeDto>(sql, new
    {
      Id = id,
      ParentId = parentId,
      Name = name,
      Points = points
    });
  }

  public static async Task<RedeemTypeDto?> UpdateRedeemType(string cs, Guid id, string name, int points, bool active)
  {
    const string sql = @"
update redeem_types
set name = @Name,
  points = @Points,
  active = @Active
where id = @Id
returning id as Id, parent_id as ParentId, name as Name, points as Points, active as Active;";

    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<RedeemTypeDto>(sql, new
    {
      Id = id,
      Name = name,
      Points = points,
      Active = active
    });
  }

  public static async Task<bool> DeleteRedeemType(string cs, Guid redeemTypeId)
  {
    const string sql = "delete from redeem_types where id = @Id";
    await using var db = Conn(cs);
    var affected = await db.ExecuteAsync(sql, new { Id = redeemTypeId });
    return affected > 0;
  }

  public static async Task<DeedDto> CreateDeed(string cs, Guid childId, Guid deedTypeId, int points, string? note, Guid createdBy, DateTimeOffset occurredAt)
  {
    const string sql = @"
insert into deeds(id, child_id, deed_type_id, points, note, occurred_at, created_by)
values(@Id, @ChildId, @DeedTypeId, @Points, @Note, @OccurredAt, @CreatedBy)
returning id as Id, child_id as ChildId, deed_type_id as DeedTypeId, points as Points, note as Note, occurred_at as OccurredAt, created_by::text as CreatedBy;";

    await using var db = Conn(cs);
    var id = Guid.NewGuid();
    return await db.QuerySingleAsync<DeedDto>(sql, new
    {
      Id = id,
      ChildId = childId,
      DeedTypeId = deedTypeId,
      Points = points,
      Note = note,
      OccurredAt = occurredAt,
      CreatedBy = createdBy
    });
  }

  public static async Task<IEnumerable<DeedDto>> GetDeedsForChild(string cs, Guid childId)
  {
    const string sql = @"
  select id as Id, child_id as ChildId, deed_type_id as DeedTypeId, points as Points, note as Note, occurred_at as OccurredAt, created_by::text as CreatedBy
  from deeds
  where child_id = @ChildId
  order by occurred_at desc;";

    await using var db = Conn(cs);
    return await db.QueryAsync<DeedDto>(sql, new { ChildId = childId });
  }

  public static async Task<DeedDto?> GetDeedById(string cs, Guid deedId)
  {
    const string sql = @"
  select id as Id, child_id as ChildId, deed_type_id as DeedTypeId, points as Points, note as Note, occurred_at as OccurredAt, created_by::text as CreatedBy
  from deeds where id = @Id";

    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<DeedDto>(sql, new { Id = deedId });
  }

  public static async Task<DeedDetails?> GetDeedDetails(string cs, Guid deedId)
  {
    const string sql = @"
select d.id as Id, d.child_id as ChildId, c.parent_id as ParentId
from deeds d
join children c on c.id = d.child_id
where d.id = @Id";

    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<DeedDetails>(sql, new { Id = deedId });
  }

  public static async Task<bool> DeleteDeed(string cs, Guid deedId)
  {
    const string sql = "delete from deeds where id = @Id";
    await using var db = Conn(cs);
    var affected = await db.ExecuteAsync(sql, new { Id = deedId });
    return affected > 0;
  }

  public static async Task<DeedTypeDetails?> GetDeedTypeDetails(string cs, Guid deedTypeId)
  {
    const string sql = "select id as Id, parent_id as ParentId from deed_types where id = @Id";
    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<DeedTypeDetails>(sql, new { Id = deedTypeId });
  }

  public static async Task<RedemptionDto> CreateRedemption(string cs, Guid childId, Guid redeemTypeId, int points, string? description, Guid createdBy, DateTimeOffset createdAt)
  {
    const string sql = @"
insert into redemptions(id, child_id, points, description, redeem_type_id, created_at, created_by)
values(@Id, @ChildId, @Points, @Description, @RedeemTypeId, @CreatedAt, @CreatedBy)
returning id as Id, child_id as ChildId, points as Points, description as Description, redeem_type_id as RedeemTypeId, created_at as CreatedAt, created_by::text as CreatedBy;";

    await using var db = Conn(cs);
    var id = Guid.NewGuid();
    return await db.QuerySingleAsync<RedemptionDto>(sql, new
    {
      Id = id,
      ChildId = childId,
      Points = points,
      Description = description,
      RedeemTypeId = redeemTypeId,
      CreatedAt = createdAt,
      CreatedBy = createdBy
    });
  }

  public static async Task<IEnumerable<RedemptionDto>> GetRedemptionsForChild(string cs, Guid childId)
  {
    const string sql = @"
  select id as Id, child_id as ChildId, points as Points, description as Description, redeem_type_id as RedeemTypeId, created_at as CreatedAt, created_by::text as CreatedBy
  from redemptions
  where child_id = @ChildId
  order by created_at desc;";

    await using var db = Conn(cs);
    return await db.QueryAsync<RedemptionDto>(sql, new { ChildId = childId });
  }

  public static async Task<BalanceDto?> GetBalance(string cs, Guid childId)
  {
    const string sql = @"
select
  c.id as ""ChildId"",
  (coalesce(d.points, 0) - coalesce(r.points, 0))::integer as ""Points"",
  (coalesce(d.points, 0) - coalesce(r.points, 0)) * c.dollar_per_point as ""Dollars""
from children c
left join (
  select child_id, sum(points)::integer as points
  from deeds
  where child_id = @ChildId
  group by child_id
) d on d.child_id = c.id
left join (
  select child_id, sum(points)::integer as points
  from redemptions
  where child_id = @ChildId
  group by child_id
) r on r.child_id = c.id
where c.id = @ChildId;";

    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<BalanceDto>(sql, new { ChildId = childId });
  }

  public static async Task<IEnumerable<ChildHistoryRow>> GetChildHistory(string cs, Guid childId)
  {
    const string sql = @"
select entry_type as EntryType,
     points as Points,
     dollar_value as DollarValue,
     note as Note,
     occurred_at as OccurredAt,
     recorded_by as RecordedBy
from (
  select 'deed' as entry_type,
       d.points,
       (d.points * c.dollar_per_point) as dollar_value,
       d.note,
       d.occurred_at,
       d.created_by::text as recorded_by
  from deeds d
  join children c on c.id = d.child_id
  where d.child_id = @ChildId

  union all

  select 'redemption' as entry_type,
       -r.points,
       (-r.points * c.dollar_per_point) as dollar_value,
       r.description,
       r.created_at,
       r.created_by::text as recorded_by
  from redemptions r
  join children c on c.id = r.child_id
  where r.child_id = @ChildId
) history
order by occurred_at;";

    await using var db = Conn(cs);
    return await db.QueryAsync<ChildHistoryRow>(sql, new { ChildId = childId });
  }

  public static async Task<IEnumerable<ChildWithBalanceDto>> GetChildrenWithBalances(string cs, Guid parentId)
  {
    const string sql = @"
select
  c.id as Id,
  c.parent_id as ParentId,
  c.name as Name,
  c.dollar_per_point as DollarPerPoint,
  (coalesce(d.points, 0) - coalesce(r.points, 0))::integer as Points,
  (coalesce(d.points, 0) - coalesce(r.points, 0)) * c.dollar_per_point as Dollars
from children c
left join (
  select child_id, sum(points)::integer as points
  from deeds
  group by child_id
) d on d.child_id = c.id
left join (
  select child_id, sum(points)::integer as points
  from redemptions
  group by child_id
) r on r.child_id = c.id
where c.parent_id = @ParentId
order by c.created_date;";

    await using var db = Conn(cs);
    return await db.QueryAsync<ChildWithBalanceDto>(sql, new { ParentId = parentId });
  }

  public static async Task<AiKeyRecord?> GetAiKeyForParent(string cs, Guid parentId)
  {
    const string sql = @"
select api_key_cipher as ""CipherText"", api_key_iv as ""Nonce"", api_key_tag as ""Tag""
from parent_ai_keys
where parent_id = @ParentId;";

    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<AiKeyRecord>(sql, new { ParentId = parentId });
  }

  public static async Task UpsertAiKeyForParent(string cs, Guid parentId, AiKeyRecord record)
  {
    const string sql = @"
insert into parent_ai_keys(parent_id, api_key_cipher, api_key_iv, api_key_tag)
values(@ParentId, @CipherText, @Nonce, @Tag)
on conflict (parent_id)
do update set
  api_key_cipher = excluded.api_key_cipher,
  api_key_iv = excluded.api_key_iv,
  api_key_tag = excluded.api_key_tag,
  updated_at = now();";

    await using var db = Conn(cs);
    await db.ExecuteAsync(sql, new
    {
      ParentId = parentId,
      record.CipherText,
      record.Nonce,
      record.Tag
    });
  }

  public static async Task<bool> DeleteAiKeyForParent(string cs, Guid parentId)
  {
    const string sql = "delete from parent_ai_keys where parent_id = @ParentId;";
    await using var db = Conn(cs);
    var affected = await db.ExecuteAsync(sql, new { ParentId = parentId });
    return affected > 0;
  }

  public static async Task<Guid?> GetLinkedParentId(string cs, string provider, string userId)
  {
    const string sql = @"
select parent_id
from parent_auth_links
where provider = @Provider and user_id = @UserId;";

    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<Guid?>(sql, new { Provider = provider, UserId = userId });
  }

  public static async Task UpsertParentAuthLink(string cs, string provider, string userId, Guid parentId, string? email, string? displayName = null)
  {
    const string sql = @"
insert into parent_auth_links(provider, user_id, parent_id, email, display_name)
values(@Provider, @UserId, @ParentId, @Email, @DisplayName)
on conflict (provider, user_id)
do update set
  parent_id = excluded.parent_id,
  email = coalesce(excluded.email, parent_auth_links.email),
  display_name = coalesce(excluded.display_name, parent_auth_links.display_name),
  updated_at = now();";

    await using var db = Conn(cs);
    await db.ExecuteAsync(sql, new
    {
      Provider = provider,
      UserId = userId,
      ParentId = parentId,
      Email = email,
      DisplayName = displayName
    });
  }

  public static async Task<ProfileDto?> GetProfile(string cs, string provider, string userId)
  {
    const string sql = @"
select email as Email, display_name as DisplayName
from parent_auth_links
where provider = @Provider and user_id = @UserId;";

    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<ProfileDto>(sql, new { Provider = provider, UserId = userId });
  }

  public static async Task<ParentInvite> CreateParentInvite(string cs, Guid parentId, string email, DateTime expiresAtUtc, string? createdBy)
  {
    const string sql = @"
insert into parent_invites(id, parent_id, email, token, created_by, expires_at)
values(@Id, @ParentId, @Email, @Token, @CreatedBy, @ExpiresAtUtc)
returning id as Id, parent_id as ParentId, email as Email, token as Token, expires_at as ExpiresAtUtc, created_at as CreatedAtUtc, created_by as CreatedBy, accepted_at as AcceptedAtUtc, cancelled_at as CancelledAtUtc;";

    await using var db = Conn(cs);
    var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    var id = Guid.NewGuid();
    return await db.QuerySingleAsync<ParentInvite>(sql, new
    {
      Id = id,
      ParentId = parentId,
      Email = email,
      Token = token,
      CreatedBy = createdBy,
      ExpiresAtUtc = expiresAtUtc
    });
  }

  public static async Task<IEnumerable<ParentInviteSummary>> ListPendingParentInvites(string cs, Guid parentId)
  {
    const string sql = @"
select id as Id, parent_id as ParentId, email as Email, expires_at as ExpiresAtUtc, created_at as CreatedAtUtc, created_by as CreatedBy
from parent_invites
where parent_id = @ParentId
  and cancelled_at is null
  and accepted_at is null
  and expires_at > now()
order by created_at desc;";

    await using var db = Conn(cs);
    return await db.QueryAsync<ParentInviteSummary>(sql, new { ParentId = parentId });
  }

  public static async Task<bool> CancelParentInvite(string cs, Guid parentId, Guid inviteId)
  {
    const string sql = @"
update parent_invites
set cancelled_at = now()
where id = @InviteId and parent_id = @ParentId and cancelled_at is null and accepted_at is null;";

    await using var db = Conn(cs);
    var affected = await db.ExecuteAsync(sql, new { InviteId = inviteId, ParentId = parentId });
    return affected > 0;
  }

  public static async Task<(Guid ParentId, string Email)?> AcceptParentInvite(string cs, string token, string acceptedBy)
  {
    const string sql = @"
update parent_invites
set accepted_at = now(),
  accepted_by = @AcceptedBy
where token = @Token
  and cancelled_at is null
  and accepted_at is null
  and expires_at > now()
returning parent_id as ParentId, email as Email;";

    await using var db = Conn(cs);
    return await db.QuerySingleOrDefaultAsync<(Guid ParentId, string Email)?>(sql, new { Token = token, AcceptedBy = acceptedBy });
  }

  public static string ToCsv(IEnumerable<ChildHistoryRow> rows)
  {
    var sb = new StringBuilder();
    sb.AppendLine("entry_type,points,dollar_value,note,occurred_at,recorded_by");

    foreach (var row in rows)
    {
      var line = string.Join(',', new[]
      {
        EscapeCsv(row.EntryType),
        row.Points.ToString(CultureInfo.InvariantCulture),
        row.DollarValue.ToString(CultureInfo.InvariantCulture),
        EscapeCsv(row.Note),
        row.OccurredAt.ToString("O", CultureInfo.InvariantCulture),
        EscapeCsv(row.RecordedBy)
      });
      sb.AppendLine(line);
    }

    return sb.ToString();
  }

  private static string EscapeCsv(string? value)
  {
    if (string.IsNullOrEmpty(value))
    {
      return string.Empty;
    }

    var sanitized = value.Replace("\"", "\"\"");
    return (sanitized.Contains(',') || sanitized.Contains('\n') || sanitized.Contains('\r'))
      ? $"\"{sanitized}\""
      : sanitized;
  }
}
