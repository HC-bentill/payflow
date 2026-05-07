namespace PayFlow.Domain.Messages;

public sealed record NotificationJob(
    Guid NotificationId,
    Guid TenantId,
    string Type,
    string Recipient,
    string Subject,
    string Body,
    DateTime CreatedAt);
