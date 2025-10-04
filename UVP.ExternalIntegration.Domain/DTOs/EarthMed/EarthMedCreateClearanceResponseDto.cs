using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UVP.ExternalIntegration.Domain.DTOs.EarthMed
{
    public class EarthMedCreateClearanceResponseDto
    {
        [JsonProperty("IsSuccess")]
        public bool IsSuccess { get; set; }

        [JsonProperty("Result")]
        public EarthMedResultDto? Result { get; set; }
    }

    public class EarthMedResultDto
    {
        [JsonProperty("Id")]
        public int Id { get; set; }

        [JsonProperty("IndexNumber")]
        public string? IndexNumber { get; set; }

        [JsonProperty("FirstName")]
        public string? FirstName { get; set; }

        [JsonProperty("MiddleName")]
        public string? MiddleName { get; set; }

        [JsonProperty("LastName")]
        public string? LastName { get; set; }

        [JsonProperty("DateOfBirth")]
        public DateTime? DateOfBirth { get; set; }

        [JsonProperty("Gender")]
        public string? Gender { get; set; }

        [JsonProperty("EmployeeType")]
        public string? EmployeeType { get; set; }

        [JsonProperty("NationalityCode")]
        public string? NationalityCode { get; set; }

        [JsonProperty("Organization")]
        public string? Organization { get; set; }

        [JsonProperty("OccupationGroup")]
        public string? OccupationGroup { get; set; }

        [JsonProperty("FunctionalTitleCode")]
        public string? FunctionalTitleCode { get; set; }

        [JsonProperty("FunctionalTitleDescription")]
        public string? FunctionalTitleDescription { get; set; }

        [JsonProperty("EmailAddress")]
        public string? EmailAddress { get; set; }

        [JsonProperty("DutyStationCode")]
        public string? DutyStationCode { get; set; }

        [JsonProperty("DutyStationDescription")]
        public string? DutyStationDescription { get; set; }

        [JsonProperty("ReferenceNumber")]
        public string? ReferenceNumber { get; set; }

        [JsonProperty("SequenceNumber")]
        public string? SequenceNumber { get; set; }

        [JsonProperty("ClearanceType")]
        public string? ClearanceType { get; set; }

        [JsonProperty("RequestStatus")]
        public string? RequestStatus { get; set; }

        [JsonProperty("RequestDate")]
        public DateTime? RequestDate { get; set; }

        [JsonProperty("DestinationDutyStationCode")]
        public string? DestinationDutyStationCode { get; set; }

        [JsonProperty("DestinationDutyStationDescription")]
        public string? DestinationDutyStationDescription { get; set; }

        [JsonProperty("StartDate")]
        public DateTime? StartDate { get; set; }

        [JsonProperty("EndDate")]
        public DateTime? EndDate { get; set; }

        [JsonProperty("CreatedDate")]
        public DateTime? CreatedDate { get; set; }
    }
}
