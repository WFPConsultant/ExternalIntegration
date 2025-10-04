namespace UVP.ExternalIntegration.Domain.Entities
{
    public class IntegrationInvocation
    {
        //public long IntegrationInvocationId { get; set; }
        //public string IntegrationType { get; set; } = string.Empty;
        //public string IntegrationOperation { get; set; } = string.Empty;
        //public string IntegrationStatus { get; set; } = string.Empty;
        //public string ReferenceId { get; set; } = string.Empty;
        //public string? ExternalReferenceId { get; set; }
        //public int AttemptCount { get; set; }
        //public DateTime? NextRetryTime { get; set; }
        //public DateTime CreatedOn { get; set; }
        //public string CreatedUser { get; set; } = string.Empty;
        //public DateTime UpdatedOn { get; set; }
        //public string UpdatedUser { get; set; } = string.Empty;

        public long IntegrationInvocationId { get; set; }
        //public int DoaCandidateId { get; set; }  // ADDED from Transaction.xlsx
        public string IntegrationType { get; set; } = string.Empty;
        public string IntegrationOperation { get; set; } = string.Empty;
        public string IntegrationStatus { get; set; } = string.Empty;
        //public string? ReferenceId { get; set; }  // Keep for backward compatibility (DoaCandidateId_CandidateId format)
       //public string? ExternalReferenceId { get; set; }
        public int AttemptCount { get; set; }
        public DateTime? NextRetryTime { get; set; }
        public DateTime CreatedOn { get; set; }
        public string CreatedUser { get; set; } = string.Empty;
        public DateTime? UpdatedOn { get; set; }
        public string? UpdatedUser { get; set; }
        public bool IsActive { get; set; } = true;  // ADDED

        //// Navigation property
        //public virtual DoaCandidate? DoaCandidate { get; set; }      
    }
}
