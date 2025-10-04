namespace UVP.ExternalIntegration.Domain.Entities
{
    public class IntegrationInvocationLog
    {
        //public long IntegrationInvocationLogId { get; set; }
        //public long IntegrationInvocationId { get; set; }
        //public string? RequestPayload { get; set; }
        //public string? ResponsePayload { get; set; }
        //public int? ResponseStatusCode { get; set; }
        //public string IntegrationStatus { get; set; } = string.Empty;
        //public DateTime RequestSentOn { get; set; }
        //public DateTime? ResponseReceivedOn { get; set; }
        //public long? ResponseTimeMs { get; set; }
        //public string? ErrorDetails { get; set; }
        //public DateTime CreatedOn { get; set; }
        //public string CreatedUser { get; set; } = string.Empty;

        //// Navigation property
        //public virtual IntegrationInvocation? IntegrationInvocation { get; set; }

        public long IntegrationInvocationLogId { get; set; }
        public long IntegrationInvocationId { get; set; }
        //public int DoaCandidateId { get; set; }      // ADDED from TransactionLog.xlsx
        public string? RequestPayload { get; set; }
        public string? ResponsePayload { get; set; }
        public int? ResponseStatusCode { get; set; }
        public string IntegrationStatus { get; set; } = string.Empty;
        public DateTime? RequestSentOn { get; set; }
        public DateTime? ResponseReceivedOn { get; set; }
        public long? ResponseTimeMs { get; set; }
        public string? ErrorDetails { get; set; }
        public int LogSequence { get; set; }          // ADDED - to track log order
        public DateTime CreatedOn { get; set; }
        public string CreatedUser { get; set; } = string.Empty;

        // Navigation properties
        public virtual IntegrationInvocation? IntegrationInvocation { get; set; }
        //public virtual DoaCandidate? DoaCandidate { get; set; }


    }
}
