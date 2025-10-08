namespace UVP.ExternalIntegration.Domain.Entities
{
    public class User
    {
        public long Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public DateTime DateOfBirth { get; set; }
        public string? PersonalEmail { get; set; }
        public string? NationalityISOCode { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? UpdatedDate { get; set; }
    }
}
