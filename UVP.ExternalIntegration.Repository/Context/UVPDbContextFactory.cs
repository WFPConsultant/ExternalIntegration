using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace UVP.ExternalIntegration.Repository.Context
{
    public class UVPDbContextFactory : IDesignTimeDbContextFactory<UVPDbContext>
    {
        public UVPDbContext CreateDbContext(string[] args)
        {
            // Build config to read connection string
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            var connectionString = config.GetConnectionString("DefaultConnection");

            var optionsBuilder = new DbContextOptionsBuilder<UVPDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new UVPDbContext(optionsBuilder.Options);
        }
    }
}