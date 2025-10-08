namespace UVP.ExternalIntegration.Business.Interfaces
{
    public interface IIntegrationOrchestrationService
    {
        Task<bool> ExecuteFullClearanceCycleAsync(long doaCandidateId, long candidateId, string integrationType);
        Task<bool> CheckAndProgressClearanceAsync(long doaCandidateId, long candidateId, string integrationType);
    }
}
