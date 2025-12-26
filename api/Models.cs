public record ParentDto(Guid Id, string Email);
public record CreateParent(string Email);

public record ChildDto(Guid Id, Guid ParentId, string Name, decimal DollarPerPoint);
public record CreateChild(Guid ParentId, string Name, decimal? DollarPerPoint);
public record UpdateChild(Guid ParentId, string Name, decimal? DollarPerPoint);

public record DeedTypeDto(Guid Id, Guid ParentId, string Name, int Points, bool Active);
public record CreateDeedType(Guid ParentId, string Name, int Points);
public record UpdateDeedType(Guid ParentId, string Name, int Points, bool Active);

public record CreateDeed(Guid ParentId, Guid ChildId, Guid DeedTypeId, int? Points, string? Note, string? CreatedBy);
public record DeedDto(Guid Id, Guid ChildId, Guid DeedTypeId, int Points, string? Note, DateTimeOffset OccurredAt, string CreatedBy);
public record BalanceDto(Guid ChildId, int Points, decimal Dollars);
public record CreateRedemption(Guid ParentId, Guid ChildId, int Points, string? Description, string? CreatedBy);
public record RedemptionDto(Guid Id, Guid ChildId, int Points, string? Description, DateTimeOffset CreatedAt, string CreatedBy);
public record ChildHistoryRow(string EntryType, int Points, decimal DollarValue, string? Note, DateTimeOffset OccurredAt, string RecordedBy);
public record ChildWithBalanceDto(Guid Id, Guid ParentId, string Name, decimal DollarPerPoint, int Points, decimal Dollars);

public record AiSuggestRequest(string DeedTypeName, string Condition, bool IsPositive);
public record AiSuggestResponse(int Points, string Reason);
public record AiKeyRequest(string ApiKey);
public record AiKeyStatus(bool HasKey);
public record LinkParentRequest(string Email);
