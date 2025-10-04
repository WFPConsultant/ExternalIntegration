namespace UVP.ExternalIntegration.Domain.DTOs.EarthMed
{
    public class DoaIntegrationModel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string OrganizationMission { get; set; } = string.Empty;
        public string DutyStationCode { get; set; } = string.Empty;
        public string DutyStationDescription { get; set; } = string.Empty;
        public DateTime? StartDate { get; set; }
        public DateTime? ExpectedEndDate { get; set; }
    }

    public class UserIntegrationModel
    {
        public long Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public DateTime? BirthDate { get; set; }
        public string EmailAddress { get; set; } = string.Empty;
        public string? NationalityCode { get; set; }
    }

    public class CandidateIntegrationModel
    {
        public long Id { get; set; }
        public string IndexNumber { get; set; } = string.Empty;
        public string? EmployeeType { get; set; }
        public string? OccupationGroup { get; set; }
        public string? FunctionalTitleCode { get; set; }
        public string? FunctionalTitleDescription { get; set; }
        public string? NationalityCode { get; set; }
        public string EmailAddress { get; set; } = string.Empty;
    }

    public class DoaCandidateIntegrationModel
    {
        public long Id { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string SequenceNumber { get; set; } = string.Empty;
        public string? ClearanceType { get; set; }
        public string? RequestStatus { get; set; }
        public DateTime? RequestDate { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public long DoaId { get; set; }
        public long CandidateId { get; set; }
    }
}
