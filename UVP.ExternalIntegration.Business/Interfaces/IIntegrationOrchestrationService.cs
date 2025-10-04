namespace UVP.ExternalIntegration.Business.Interfaces
{
    public interface IIntegrationOrchestrationService
    {
        Task<bool> ExecuteFullClearanceCycleAsync(int doaCandidateId, int candidateId, string integrationType);
        Task<bool> CheckAndProgressClearanceAsync(int doaCandidateId, int candidateId, string integrationType);
    }
}
