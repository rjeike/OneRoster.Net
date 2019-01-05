using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();
            var builder = new DbContextOptionsBuilder<ApplicationDbContext>();
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            builder.UseSqlServer(connectionString);
            return new ApplicationDbContext(builder.Options);
        }
    }

    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Save a diagram of the current database schema
        /// Creates a file called "OneRoster.dgml"
        /// </summary>
        /// <param name="directoryPath">base directory to save file</param>
        public void SaveDgmlFile(string directoryPath)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(directoryPath, "OneRoster.dgml"), this.AsDgml(), System.Text.Encoding.UTF8);
        }

        public new int SaveChanges()
        {
            try
            {
                // touch all the modified entities
                ChangeTracker.Entries<DataObject>()
                    .Where(e => e.State == EntityState.Modified)
                    .Select(e => e.Entity)
                    .ToList()
                    .ForEach(o => o.Touch());
                return base.SaveChanges();
            }
            catch (Exception)
            {
                throw;
            }
            /*
             catch (System.Data.Entity.Validation.DbEntityValidationException e)
             {
                var sb = new System.Text.StringBuilder();
                foreach (var eve in e.EntityValidationErrors)
                {
                   sb.AppendFormat(@"Entity of type [{0}] in state [{1}] has the following validation errors:", 
                      eve.Entry.Entity.GetType().Name, eve.Entry.State);
                   foreach (var ve in eve.ValidationErrors)
                      sb.AppendFormat(@"- Property: [{0}], Error: [{1}]", ve.PropertyName, ve.ErrorMessage);
                }

                throw;
             */
        }


        public DbSet<District> Districts { get; set; }
        public DbSet<DataSyncLine> DataSyncLines { get; set; }
        public DbSet<DataSyncHistory> DataSyncHistories { get; set; }
        public DbSet<DataSyncHistoryDetail> DataSyncHistoryDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<DataSyncLine>()
                .HasOne(line => line.District)
                .WithMany()
                .HasForeignKey(line => line.DistrictId)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DataSyncHistory>()
                .HasOne(history => history.District)
                .WithMany()
                .HasForeignKey(history => history.DistrictId)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DataSyncHistoryDetail>()
                .HasOne(detail => detail.DataSyncHistory)
                .WithMany()
                .HasForeignKey(detail => detail.DataSyncHistoryId)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DataSyncHistoryDetail>()
                .HasOne(detail => detail.DataSyncLine)
                .WithMany(line => line.DataSyncHistoryDetails)
                .HasForeignKey(detail => detail.DataSyncLineId)
                .IsRequired(true)
                .OnDelete(DeleteBehavior.Cascade);

            // Enforce that sourcedId is unique within a given District/Table
            builder.Entity<DataSyncLine>()
                .HasIndex(l => new { l.DistrictId, l.Table, l.SourcedId })
                .IsUnique();

            base.OnModelCreating(builder);
        }
    }
}
