namespace GoodDeeds.Client.Models;

public record ParentDto(Guid Id, string Email);
public record CreateParentRequest(string Email);

public record ChildDto(Guid Id, Guid ParentId, string Name, decimal DollarPerPoint);
public record CreateChildRequest(Guid ParentId, string Name, decimal DollarPerPoint);
public record UpdateChildRequest(Guid ParentId, string Name, decimal DollarPerPoint);

public record DeedTypeDto(Guid Id, Guid ParentId, string Name, int Points, bool Active);
public record CreateDeedTypeRequest(Guid ParentId, string Name, int Points);

public record DeedDto(Guid Id, Guid ChildId, Guid DeedTypeId, int Points, string? Note, DateTimeOffset OccurredAt, string CreatedBy);
public record CreateDeedRequest(Guid ParentId, Guid ChildId, Guid DeedTypeId, int? Points, string? Note, string? CreatedBy);

public record BalanceDto(Guid ChildId, int Points, decimal Dollars);

public record RedemptionDto(Guid Id, Guid ChildId, int Points, string? Description, DateTimeOffset CreatedAt, string CreatedBy);
public record CreateRedemptionRequest(Guid ParentId, Guid ChildId, int Points, string? Description, string? CreatedBy);

public record ErrorResponse(string Error);

public record AiKeyStatusDto(bool HasKey);
public record AiKeyRequest(string ApiKey);
public record LinkParentRequest(string Email);
