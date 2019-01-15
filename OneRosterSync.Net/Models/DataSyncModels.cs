using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OneRosterSync.Net.Authentication;

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

    public enum ProcessingStage
    {
        None = 0,
        Load = 1,
        Analyze = 2,
        Apply = 3,
    }

    public enum ProcessingAction
    {
        None = 0,
        Load = 1,
        LoadSample = 2,
        Analyze = 3,
        Apply = 4,
        FullProcess = 5,
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
    }

    public enum ProcessingStatus
    {
        None = 0,
        Loading = 2,
        LoadingDone = 3,
        Analyzing = 4,
        AnalyzingDone = 5,
        Applying = 7,
        ApplyingDone = 8,
    }

    public class District : DataObject
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DistrictId { get; set; }

        [Required]
        public string Name { get; set; }

        public string TargetId { get; set; }

        // The time of day to process this district's files
        [DisplayName("Daily Processing Time")]
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

        [DisplayName("Endpoint for LMS API")]
        public string LmsApiEndpoint { get; set; }

	    [DisplayName("Authenticator for LMS API")]
		public ApiAuthenticatorType LmsApiAuthenticatorType { get; set; }

	    [DisplayName("Authentication Data for LMS API")]
		public string LmsApiAuthenticationJsonData { get; set; }

        [DisplayName("Approval Required?")]
        public bool IsApprovalRequired { get; set; }

        [DisplayName("Emails - Processing ")]
        public string EmailsEachProcess { get; set; }

        [DisplayName("Email - Changes")]
        public string EmailsOnChanges { get; set; }

        [DisplayName("Next Processing")]
        public ProcessingAction ProcessingAction { get; set; }
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
        public string SourcedId { get; set; }
        public string TargetId { get; set; }
        public string Data { get; set; }

        [DisplayName("CSV data as JSON")]
        [DataType(DataType.MultilineText)]
        public string RawData { get; set; }

        [DisplayName("Load Status")]
        public LoadStatus LoadStatus { get; set; }

        [DisplayName("Sync Status")]
        public SyncStatus SyncStatus { get; set; }
        public string Error { get; set; }

        [DisplayName("Last Seen")]
        public DateTime LastSeen { get; set; }

        [DisplayName("Include in Sync?")]
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

        public DateTime? LoadStarted { get; set; }
        public DateTime? LoadCompleted { get; set; }

        public DateTime? AnalyzeStarted { get; set; }
        public DateTime? AnalyzeCompleted { get; set; }

        public DateTime? ApplyStarted { get; set; }
        public DateTime? ApplyCompleted { get; set; }

        public string LoadError { get; set; }
        public string AnalyzeError { get; set; }
        public string ApplyError { get; set; }

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

        [DisplayName("Data")]
        public string DataNew { get; set; }

        [DisplayName("Include in Sync")]
        public bool IncludeInSync { get; set; }

        [DisplayName("Load Status")]
        public LoadStatus LoadStatus { get; set; }

        [DisplayName("Sync Status")]
        public SyncStatus SyncStatus { get; set; }

        public string Table { get; set; }
    }
}