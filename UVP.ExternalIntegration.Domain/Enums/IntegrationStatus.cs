namespace UVP.ExternalIntegration.Domain.Enums
{
    public enum IntegrationStatus
    {
        PENDING,
        IN_PROGRESS,
        SUCCESS,
        FAILED,
        RETRY,
        SENT,
        FINAL,
        DELIVERED,
        PERMANENTLY_FAILED
    }
}
