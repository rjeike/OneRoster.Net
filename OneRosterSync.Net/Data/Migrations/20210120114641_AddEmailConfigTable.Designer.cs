﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OneRosterSync.Net.Data;

namespace OneRosterSync.Net.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20210120114641_AddEmailConfigTable")]
    partial class AddEmailConfigTable
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.1.14-servicing-32113")
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken();

                    b.Property<string>("Name")
                        .HasMaxLength(256);

                    b.Property<string>("NormalizedName")
                        .HasMaxLength(256);

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique()
                        .HasName("RoleNameIndex")
                        .HasFilter("[NormalizedName] IS NOT NULL");

                    b.ToTable("AspNetRoles");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("ClaimType");

                    b.Property<string>("ClaimValue");

                    b.Property<string>("RoleId")
                        .IsRequired();

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetRoleClaims");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUser", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("AccessFailedCount");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken();

                    b.Property<string>("Email")
                        .HasMaxLength(256);

                    b.Property<bool>("EmailConfirmed");

                    b.Property<bool>("LockoutEnabled");

                    b.Property<DateTimeOffset?>("LockoutEnd");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(256);

                    b.Property<string>("NormalizedUserName")
                        .HasMaxLength(256);

                    b.Property<string>("PasswordHash");

                    b.Property<string>("PhoneNumber");

                    b.Property<bool>("PhoneNumberConfirmed");

                    b.Property<string>("SecurityStamp");

                    b.Property<bool>("TwoFactorEnabled");

                    b.Property<string>("UserName")
                        .HasMaxLength(256);

                    b.HasKey("Id");

                    b.HasIndex("NormalizedEmail")
                        .HasName("EmailIndex");

                    b.HasIndex("NormalizedUserName")
                        .IsUnique()
                        .HasName("UserNameIndex")
                        .HasFilter("[NormalizedUserName] IS NOT NULL");

                    b.ToTable("AspNetUsers");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("ClaimType");

                    b.Property<string>("ClaimValue");

                    b.Property<string>("UserId")
                        .IsRequired();

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserClaims");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.Property<string>("LoginProvider");

                    b.Property<string>("ProviderKey");

                    b.Property<string>("ProviderDisplayName");

                    b.Property<string>("UserId")
                        .IsRequired();

                    b.HasKey("LoginProvider", "ProviderKey");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserLogins");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.Property<string>("UserId");

                    b.Property<string>("RoleId");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetUserRoles");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.Property<string>("UserId");

                    b.Property<string>("LoginProvider");

                    b.Property<string>("Name");

                    b.Property<string>("Value");

                    b.HasKey("UserId", "LoginProvider", "Name");

                    b.ToTable("AspNetUserTokens");
                });

            modelBuilder.Entity("OneRosterSync.Net.Models.DataSyncHistory", b =>
                {
                    b.Property<int>("DataSyncHistoryId")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<DateTime?>("AnalyzeCompleted");

                    b.Property<string>("AnalyzeError");

                    b.Property<DateTime?>("AnalyzeStarted");

                    b.Property<DateTime?>("ApplyCompleted");

                    b.Property<string>("ApplyError");

                    b.Property<DateTime?>("ApplyStarted");

                    b.Property<DateTime>("Created");

                    b.Property<int>("DistrictId");

                    b.Property<DateTime?>("LoadCompleted");

                    b.Property<string>("LoadError");

                    b.Property<DateTime?>("LoadStarted");

                    b.Property<DateTime>("Modified");

                    b.Property<int>("NumAdded");

                    b.Property<int>("NumDeleted");

                    b.Property<int>("NumModified");

                    b.Property<int>("NumRows");

                    b.Property<DateTime>("Started");

                    b.Property<int>("Version");

                    b.HasKey("DataSyncHistoryId");

                    b.HasIndex("DistrictId");

                    b.ToTable("DataSyncHistories");
                });

            modelBuilder.Entity("OneRosterSync.Net.Models.DataSyncHistoryDetail", b =>
                {
                    b.Property<int>("DataSyncHistoryDetailId")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<DateTime>("Created");

                    b.Property<string>("DataNew");

                    b.Property<int>("DataSyncHistoryId");

                    b.Property<int>("DataSyncLineId");

                    b.Property<bool>("IncludeInSync");

                    b.Property<int>("LoadStatus");

                    b.Property<DateTime>("Modified");

                    b.Property<int>("SyncStatus");

                    b.Property<string>("Table");

                    b.Property<int>("Version");

                    b.HasKey("DataSyncHistoryDetailId");

                    b.HasIndex("DataSyncHistoryId");

                    b.HasIndex("DataSyncLineId");

                    b.ToTable("DataSyncHistoryDetails");
                });

            modelBuilder.Entity("OneRosterSync.Net.Models.DataSyncLine", b =>
                {
                    b.Property<int>("DataSyncLineId")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<DateTime>("Created");

                    b.Property<string>("Data");

                    b.Property<int>("DistrictId");

                    b.Property<string>("EnrollmentMap");

                    b.Property<string>("Error");

                    b.Property<string>("ErrorCode");

                    b.Property<bool>("IncludeInSync");

                    b.Property<DateTime>("LastSeen");

                    b.Property<int>("LoadStatus");

                    b.Property<DateTime>("Modified");

                    b.Property<string>("RawData");

                    b.Property<string>("SourcedId");

                    b.Property<int>("SyncStatus");

                    b.Property<string>("Table");

                    b.Property<string>("TargetId");

                    b.Property<int>("Version");

                    b.HasKey("DataSyncLineId");

                    b.HasIndex("DistrictId", "Table", "SourcedId")
                        .IsUnique()
                        .HasFilter("[Table] IS NOT NULL AND [SourcedId] IS NOT NULL");

                    b.ToTable("DataSyncLines");
                });

            modelBuilder.Entity("OneRosterSync.Net.Models.District", b =>
                {
                    b.Property<int>("DistrictId")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("ApiError");

                    b.Property<string>("BasePath");

                    b.Property<string>("ClassLinkConsumerKey");

                    b.Property<string>("ClassLinkConsumerSecret");

                    b.Property<string>("ClassLinkOrgsApiUrl");

                    b.Property<string>("ClassLinkUsersApiUrl");

                    b.Property<string>("CleverOAuthToken");

                    b.Property<DateTime>("Created");

                    b.Property<string>("CronExpression")
                        .IsRequired()
                        .HasMaxLength(50);

                    b.Property<TimeSpan?>("DailyProcessingTime");

                    b.Property<string>("EmailFieldNameForUserAPI");

                    b.Property<string>("EmailsEachProcess");

                    b.Property<string>("EmailsOnChanges");

                    b.Property<DateTime?>("FTPFilesLastLoadedOn");

                    b.Property<string>("FTPPassword");

                    b.Property<string>("FTPPath");

                    b.Property<string>("FTPUsername");

                    b.Property<bool>("IsApiValidated");

                    b.Property<bool>("IsApprovalRequired");

                    b.Property<bool>("IsCsvBased");

                    b.Property<DateTime?>("LastSyncedOn");

                    b.Property<string>("LmsAcademicSessionEndPoint")
                        .IsRequired();

                    b.Property<string>("LmsApiAuthenticationJsonData");

                    b.Property<int>("LmsApiAuthenticatorType");

                    b.Property<string>("LmsApiBaseUrl");

                    b.Property<string>("LmsClassEndPoint")
                        .IsRequired();

                    b.Property<string>("LmsCourseEndPoint")
                        .IsRequired();

                    b.Property<string>("LmsEnrollmentEndPoint")
                        .IsRequired();

                    b.Property<string>("LmsOrgEndPoint")
                        .IsRequired();

                    b.Property<string>("LmsUserEndPoint")
                        .IsRequired();

                    b.Property<DateTime>("Modified");

                    b.Property<string>("NCESDistrictID")
                        .IsRequired()
                        .HasMaxLength(7);

                    b.Property<string>("Name")
                        .IsRequired();

                    b.Property<DateTime?>("NextProcessingTime");

                    b.Property<bool>("NightlySyncEnabled");

                    b.Property<string>("PasswordFieldNameForUserAPI")
                        .IsRequired();

                    b.Property<int>("ProcessingAction");

                    b.Property<int>("ProcessingStatus");

                    b.Property<bool>("ReadyForNightlySync");

                    b.Property<int?>("RosteringApiSource");

                    b.Property<bool>("StopCurrentAction");

                    b.Property<bool>("SyncAcademicSessions");

                    b.Property<bool>("SyncClasses");

                    b.Property<bool>("SyncCourses");

                    b.Property<bool>("SyncEnrollment");

                    b.Property<bool>("SyncOrgs");

                    b.Property<bool>("SyncUsers");

                    b.Property<string>("TargetId");

                    b.Property<string>("UsersLastDateModified");

                    b.Property<int>("Version");

                    b.HasKey("DistrictId");

                    b.ToTable("Districts");
                });

            modelBuilder.Entity("OneRosterSync.Net.Models.DistrictFilter", b =>
                {
                    b.Property<int>("ID")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<DateTime>("Created");

                    b.Property<int>("DistrictId");

                    b.Property<int>("FilterType");

                    b.Property<string>("FilterValue");

                    b.Property<DateTime>("Modified");

                    b.Property<bool>("ShouldBeApplied");

                    b.Property<int>("Version");

                    b.HasKey("ID");

                    b.ToTable("DistrictFilters");
                });

            modelBuilder.Entity("OneRosterSync.Net.Models.EmailConfig", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<string>("Bcc");

                    b.Property<string>("Cc");

                    b.Property<string>("DisplayName");

                    b.Property<string>("From");

                    b.Property<string>("Host");

                    b.Property<string>("Password");

                    b.Property<string>("Subject");

                    b.Property<string>("To");

                    b.HasKey("Id");

                    b.ToTable("EmailConfigs");
                });

            modelBuilder.Entity("OneRosterSync.Net.Models.NCESMapping", b =>
                {
                    b.Property<int>("ID")
                        .ValueGeneratedOnAdd()
                        .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

                    b.Property<DateTime>("Created");

                    b.Property<DateTime>("Modified");

                    b.Property<string>("NCESId");

                    b.Property<string>("StateID");

                    b.Property<int>("Version");

                    b.HasKey("ID");

                    b.ToTable("NCESMappings");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole")
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityUser")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityUser")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole")
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityUser")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityUser")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("OneRosterSync.Net.Models.DataSyncHistory", b =>
                {
                    b.HasOne("OneRosterSync.Net.Models.District", "District")
                        .WithMany()
                        .HasForeignKey("DistrictId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("OneRosterSync.Net.Models.DataSyncHistoryDetail", b =>
                {
                    b.HasOne("OneRosterSync.Net.Models.DataSyncHistory", "DataSyncHistory")
                        .WithMany()
                        .HasForeignKey("DataSyncHistoryId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("OneRosterSync.Net.Models.DataSyncLine", "DataSyncLine")
                        .WithMany("DataSyncHistoryDetails")
                        .HasForeignKey("DataSyncLineId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("OneRosterSync.Net.Models.DataSyncLine", b =>
                {
                    b.HasOne("OneRosterSync.Net.Models.District", "District")
                        .WithMany()
                        .HasForeignKey("DistrictId")
                        .OnDelete(DeleteBehavior.Restrict);
                });
#pragma warning restore 612, 618
        }
    }
}
