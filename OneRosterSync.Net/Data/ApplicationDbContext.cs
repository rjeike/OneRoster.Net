using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OneRosterSync.Net.Models;

namespace OneRosterSync.Net.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
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

            base.OnModelCreating(builder);
        }

    }
}
