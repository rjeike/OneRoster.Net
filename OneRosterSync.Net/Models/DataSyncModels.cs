using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OneRosterSync.Net.Models
{
    public class DataObject
    {
        public int Version { get; set; }

        public DateTime Created { get; set; }

        public DateTime Modified { get; set; }

        public void Touch()
        {
            Modified = DateTime.UtcNow;
            Version++;
        }

        public DataObject()
        {
            Version = 1;
            Created = Modified = DateTime.UtcNow;
        }
    }

    public enum LoadStatus
    {
        None = 0,
        Added = 1,
        Modified = 2,
        NoChange = 3,
        Deleted = 4,
    }

    public enum SyncStatus
    {
        /// <summary>
        /// No status applied; should not be used
        /// </summary>
        None = 0,

        /// <summary>
        /// Record has come in and data loaded into sync table.  Pending application to LMS.
        /// </summary>
        Loaded = 1,

        /// <summary>
        /// After data is loaded, it is analyzed for which records to apply, those that pass are marked ReadyToApply
        /// </summary>
        ReadyToApply = 2,

        /// <summary>
        /// After data is applied, records marked accordingly
        /// </summary>
        Applied = 3,

        /// <summary>
        /// Failures also tracked
        /// </summary>
        ApplyFailed = 4,

        /// <summary>
        /// Admin has rejected this change
        /// </summary>
        Rejected = 5,
    }

    public enum ProcessingStatus
    {
        None = 0,
        Scheduled = 1,
        Loading = 3,
        Analyzing = 4,
        PendingApproval = 5,
        Approved = 6,
        Applying = 7,
        Finished =100,
    }

    public class District : DataObject
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DistrictId { get; set; }

        [Required]
        public string Name { get; set; }

        public string TargetId { get; set; }

        // The time of day to process this district's files
        [DisplayName("Daily Processing Time (time of day)")]
        public TimeSpan? DailyProcessingTime { get; set; }

        // The time afterwhich the district should be processed
        // This should be updated to the next day's DailyProcessingTime
        // after each processing
        [DisplayName("Next Processing Time")]
        public DateTime? NextProcessingTime { get; set; }

        [DisplayName("Processing Status")]
        public ProcessingStatus ProcessingStatus { get; set; }

        [DisplayName("Base Path of CSV File")]
        public string BasePath { get; set; }

        [DisplayName("Approval Required?")]
        public bool IsApprovalRequired { get; set; }

        [DisplayName("Email List for processing")]
        public string EmailsEachProcess { get; set; }

        [DisplayName("Email List on changes")]
        public string EmailsOnChanges { get; set; }
    }

    public class DataSyncLine : DataObject
    {
        public DataSyncLine()
        {
            DataSyncHistoryDetails = new HashSet<DataSyncHistoryDetail>();
        }

        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DataSyncLineId { get; set; }

        [ForeignKey("DistrictId")]
        public virtual District District { get; set; }
        public int DistrictId { get; set; }

        public string Table { get; set; }
        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public string Data { get; set; }
        public string RawData { get; set; }
        public LoadStatus LoadStatus { get; set; }
        public SyncStatus SyncStatus { get; set; }
        public string Error { get; set; }
        public DateTime LastSeen { get; set; }

        [DisplayName("Include in sync processing?")]
        public bool IncludeInSync { get; set; }

        /// <summary>
        /// Kludge for tracking TargetIds of an Enrollment
        /// </summary>
        public string EnrollmentMap { get; set; }

        public virtual ICollection<DataSyncHistoryDetail> DataSyncHistoryDetails { get; set; }
    }

    public class DataSyncHistory : DataObject
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DataSyncHistoryId { get; set; }

        [ForeignKey("DistrictId")]
        public virtual District District { get; set; }
        public int DistrictId { get; set; }

        public DateTime Started { get; set; }
        public DateTime? Completed { get; set; }

        [DisplayName("Rows")]
        public int NumRows { get; set; }
        [DisplayName("Added")]
        public int NumAdded { get; set; }
        [DisplayName("Modified")]
        public int NumModified { get; set; }
        [DisplayName("Deleted")]
        public int NumDeleted { get; set; }
    }

    public class DataSyncHistoryDetail : DataObject
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DataSyncHistoryDetailId { get; set; }

        [ForeignKey("DataSyncHistoryId")]
        public virtual DataSyncHistory DataSyncHistory { get; set; }
        public int DataSyncHistoryId { get; set; }

        [ForeignKey("DataSyncLineId")]
        public virtual DataSyncLine DataSyncLine { get; set; }
        public int DataSyncLineId { get; set; }

        [DisplayName("Original Data")]
        public string DataOrig { get; set; }
        [DisplayName("New Data")]
        public string DataNew { get; set; }

        public LoadStatus LoadStatus { get; set; }

        public string Table { get; set; }
    }

    public class EnrollmentMap
    {
        public string classTargetId { get; set; }
        public string userTargetId { get; set; }
    }

}
