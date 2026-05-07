namespace PayFlow.Domain.Enums;

public enum WebhookDeliveryStatus
{
    Pending = 0,
    Delivered = 1,
    Failed = 2,
    PermanentlyFailed = 3
}
