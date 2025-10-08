namespace UVP.ExternalIntegration.Domain.Entities
{
    public class DoaCandidateClearancesOneHR
    {
        public long Id { get; set; }
        public long DoaCandidateId { get; set; }
        public long CandidateId { get; set; }
        public long DoaId { get; set; }
        public string? DoaCandidateClearanceId { get; set; }
        public string? RVCaseId { get; set; }
        public DateTime RequestedDate { get; set; }
        public bool IsCompleted { get; set; }
        public int Retry { get; set; }
        public DateTime? CompletionDate { get; set; }

        // Navigation properties
        public virtual DoaCandidate? DoaCandidate { get; set; }
        public virtual Candidate? Candidate { get; set; }
    }
}
