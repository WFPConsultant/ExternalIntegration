namespace UVP.ExternalIntegration.Domain.Entities
{
    public class DoaCandidate
    {
        public int Id { get; set; }
        public int DoaId { get; set; }
        public int CandidateId { get; set; }
        public string? Department { get; set; }
        public string? RequestorName { get; set; }
        public string? RequestorEmail { get; set; }
        public string? Status { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
