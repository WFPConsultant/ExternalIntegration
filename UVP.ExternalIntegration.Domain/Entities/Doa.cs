namespace UVP.ExternalIntegration.Domain.Entities
{
    public class Doa
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OrganizationMission { get; set; } = string.Empty;
        public string DutyStationCode { get; set; } = string.Empty;
        public string DutyStationDescription { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? ExpectedEndDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
