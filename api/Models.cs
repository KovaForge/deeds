public record ParentDto(Guid Id, string Email);
public record CreateParent(string Email);

public record ChildDto(Guid Id, Guid ParentId, string Name, decimal DollarPerPoint);
public record CreateChild(Guid ParentId, string Name, decimal? DollarPerPoint);
public record UpdateChild(Guid ParentId, string Name, decimal? DollarPerPoint);

public record DeedTypeDto(Guid Id, Guid ParentId, string Name, int Points, bool Active);
public record CreateDeedType(Guid ParentId, string Name, int Points);
public record UpdateDeedType(Guid ParentId, string Name, int Points, bool Active);

public record CreateDeed(Guid ParentId, Guid ChildId, Guid DeedTypeId, int? Points, string? Note, string? CreatedBy);
public record DeedDto(Guid Id, Guid ChildId, Guid DeedTypeId, int Points, string? Note, DateTime OccurredAt, string CreatedBy);
public record BalanceDto(Guid ChildId, int Points, decimal Dollars);
public record CreateRedemption(Guid ParentId, Guid ChildId, int Points, string? Description, string? CreatedBy);
public record RedemptionDto(Guid Id, Guid ChildId, int Points, string? Description, DateTime CreatedAt, string CreatedBy);
public record ChildHistoryRow(string EntryType, int Points, decimal DollarValue, string? Note, DateTime OccurredAt, string RecordedBy);
public record ChildWithBalanceDto(Guid Id, Guid ParentId, string Name, decimal DollarPerPoint, int Points, decimal Dollars);

public record AiSuggestRequest(string DeedTypeName, string Condition, bool IsPositive);
public record AiSuggestResponse(int Points, string Reason);
public record AiKeyRequest(string ApiKey);
public record AiKeyStatus(bool HasKey);
public record LinkParentRequest(string Email);
public record CreateInvite(Guid ParentId, string Email, int? DaysValid);
public record InviteResponse(Guid InviteId, string Token, DateTime ExpiresAtUtc);
public record ParentInviteDto(Guid Id, Guid ParentId, string Email, DateTime ExpiresAtUtc, DateTime CreatedAtUtc, string? CreatedBy);
public record AcceptInviteRequest(string Token);
public record AcceptInviteResponse(Guid ParentId, string Email);
public record UpdateProfile(string DisplayName);
public record ProfileDto(string? Email, string? DisplayName);
