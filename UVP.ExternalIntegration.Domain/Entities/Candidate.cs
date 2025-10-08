namespace UVP.ExternalIntegration.Domain.Entities
{
    public class Candidate
    {
        public long Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public string? CountryOfBirth { get; set; }
        public string? CountryOfBirthISOCode { get; set; }
        public string? Nationality { get; set; }
        public string? NationalityISOCode { get; set; }
        public DateTime DateOfBirth { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public long UserId { get; set; }
    }
}
