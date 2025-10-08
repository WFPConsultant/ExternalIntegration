namespace UVP.ExternalIntegration.Domain.Entities
{
    public class DoaCandidateClearances
    {
        public long Id { get; set; }
        public long DoaCandidateId { get; set; }
        public string RecruitmentClearanceCode { get; set; } = string.Empty;
        public DateTime RequestedDate { get; set; }
        public string StatusCode { get; set; } = string.Empty;
        public string? LinkDetailRemarks { get; set; }
        public string? AdditionalRemarks { get; set; }
        public DateTime? CompletionDate { get; set; }
        public string? Outcome { get; set; }
        public DateTime UpdatedDate { get; set; }

        // Navigation property
        public virtual DoaCandidate? DoaCandidate { get; set; }
    }
}
