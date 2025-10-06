using Microsoft.EntityFrameworkCore;
using UVP.ExternalIntegration.Domain.Entities;

namespace UVP.ExternalIntegration.Repository.Context
{
    public class UVPDbContext : DbContext
    {
        public UVPDbContext(DbContextOptions<UVPDbContext> options) : base(options)
        {
        }

        public DbSet<IntegrationEndpointConfiguration> IntegrationEndpointConfigurations { get; set; }
        public DbSet<IntegrationInvocation> IntegrationInvocations { get; set; }
        public DbSet<IntegrationInvocationLog> IntegrationInvocationLogs { get; set; }
        public DbSet<DoaCandidate> DoaCandidates { get; set; }
        public DbSet<Candidate> Candidates { get; set; }
        public DbSet<DoaCandidateClearances> DoaCandidateClearances { get; set; }
        public DbSet<DoaCandidateClearancesOneHR> DoaCandidateClearancesOneHR { get; set; }
        public DbSet<Doa> Doas { get; set; }
        public DbSet<User> Users { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // IntegrationEndpointConfiguration
            modelBuilder.Entity<IntegrationEndpointConfiguration>(entity =>
            {
                entity.HasKey(e => e.IntegrationEndpointId);
                entity.Property(e => e.IntegrationType).HasMaxLength(50).IsRequired();
                entity.Property(e => e.IntegrationOperation).HasMaxLength(100).IsRequired();
                entity.Property(e => e.BaseUrl).HasMaxLength(500).IsRequired();
                entity.Property(e => e.PathTemplate).HasMaxLength(500).IsRequired();
                entity.Property(e => e.HttpMethod).HasMaxLength(10).IsRequired();
                entity.Property(e => e.CreatedUser).HasMaxLength(100);
                entity.Property(e => e.UpdatedUser).HasMaxLength(100);
                entity.HasIndex(e => new { e.IntegrationType, e.IntegrationOperation, e.IsActive });
            });

            // IntegrationInvocation
            modelBuilder.Entity<IntegrationInvocation>(entity =>
            {
                entity.HasKey(e => e.IntegrationInvocationId);
                entity.Property(e => e.IntegrationType).HasMaxLength(50).IsRequired();
                entity.Property(e => e.IntegrationOperation).HasMaxLength(100).IsRequired();
                entity.Property(e => e.IntegrationStatus).HasMaxLength(50).IsRequired();
                // Make ReferenceId optional (nullable) — previously marked .IsRequired()
                //entity.Property(e => e.ReferenceId).HasMaxLength(200);
                //entity.Property(e => e.ExternalReferenceId).HasMaxLength(200);
                entity.Property(e => e.CreatedUser).HasMaxLength(100);
                entity.Property(e => e.UpdatedUser).HasMaxLength(100);
                entity.HasIndex(e => e.IntegrationStatus);
                //entity.HasIndex(e => e.ReferenceId);
            });

            // IntegrationInvocationLog
            modelBuilder.Entity<IntegrationInvocationLog>(entity =>
            {
                entity.HasKey(e => e.IntegrationInvocationLogId);
                entity.Property(e => e.IntegrationStatus).HasMaxLength(50).IsRequired();
                entity.Property(e => e.CreatedUser).HasMaxLength(100);
                entity.HasIndex(e => e.IntegrationInvocationId);

                entity.HasOne(e => e.IntegrationInvocation)
                    .WithMany()
                    .HasForeignKey(e => e.IntegrationInvocationId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // DoaCandidate
            modelBuilder.Entity<DoaCandidate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Department).HasMaxLength(100);
                entity.Property(e => e.RequestorName).HasMaxLength(200);
                entity.Property(e => e.RequestorEmail).HasMaxLength(200);
                entity.Property(e => e.Status).HasMaxLength(50);
            });

            // Candidate
            modelBuilder.Entity<Candidate>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.MiddleName).HasMaxLength(100);
                entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Gender).HasMaxLength(10);
                entity.Property(e => e.CountryOfBirth).HasMaxLength(100);
                entity.Property(e => e.CountryOfBirthISOCode).HasMaxLength(3);
                entity.Property(e => e.Nationality).HasMaxLength(100);
                entity.Property(e => e.NationalityISOCode).HasMaxLength(3);
            });

            // DoaCandidateClearances
            modelBuilder.Entity<DoaCandidateClearances>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.RecruitmentClearanceCode).HasMaxLength(50).IsRequired();
                entity.Property(e => e.StatusCode).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Outcome).HasMaxLength(50);
                entity.HasIndex(e => e.DoaCandidateId);

                entity.HasOne(e => e.DoaCandidate)
                    .WithMany()
                    .HasForeignKey(e => e.DoaCandidateId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // DoaCandidateClearancesOneHR
            modelBuilder.Entity<DoaCandidateClearancesOneHR>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DoaCandidateClearanceId).HasMaxLength(100);
                entity.Property(e => e.RVCaseId).HasMaxLength(100);
                entity.HasIndex(e => new { e.DoaCandidateId, e.CandidateId });

                entity.HasOne(e => e.DoaCandidate)
                    .WithMany()
                    .HasForeignKey(e => e.DoaCandidateId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Candidate)
                    .WithMany()
                    .HasForeignKey(e => e.CandidateId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            // Doa
            modelBuilder.Entity<Doa>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200);
                entity.Property(e => e.OrganizationMission).HasMaxLength(200);
                entity.Property(e => e.DutyStationCode).HasMaxLength(50);
                entity.Property(e => e.DutyStationDescription).HasMaxLength(200);
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.MiddleName).HasMaxLength(100);
                entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Gender).HasMaxLength(10);
                entity.Property(e => e.PersonalEmail).HasMaxLength(200);
                entity.Property(e => e.NationalityISOCode).HasMaxLength(3);
            });
        }
    }
}
