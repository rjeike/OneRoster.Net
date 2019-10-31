using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OneRosterSync.Net.Models
{
    public class DistrictViewModel
    {
        public int DistrictId { get; set; }
        public string Name { get; set; }
        public string TimeOfDay { get; set; }
        public string ProcessingStatus { get; set; }
        public string Modified { get; set; }
        public int NumRecords { get; set; }
    }

    public class DataSyncLineReportLine
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int id { get; set; }

        public string Entity { get; set; }
        public int IncludeInSync { get; set; }
		public bool SyncEnabled { get; set; }

        // LoadStatus
        [DisplayName("Load: Added")]
        public int Added { get; set; }
        [DisplayName("Load: Modified")]
        public int Modified { get; set; }
        [DisplayName("Load: No Change")]
        public int NoChange { get; set; }
        [DisplayName("Load: Deleted")]
        public int Deleted { get; set; }

        // SyncStatus
        [DisplayName("Apply: Loaded")]
        public int Loaded { get; set; }
        [DisplayName("Apply: Ready")]
        public int ReadyToApply { get; set; }
        [DisplayName("Apply: Applied")]
        public int Applied { get; set; }
        [DisplayName("Apply: Failed")]
        public int AppliedFailed { get; set; }

        public int TotalRecords { get; set; }
    }

    public class DataSyncLineViewModel : DataObject
    {
        //public DataSyncLineViewModel()
        //{
        //    DataSyncHistoryDetails = new HashSet<DataSyncHistoryDetail>();
        //}

        //[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DataSyncLineId { get; set; }

        //[ForeignKey("DistrictId")]
        //public virtual District District { get; set; }
        public int DistrictId { get; set; }

        public string Table { get; set; }
        public string SourcedId { get; set; }
        public string TargetId { get; set; }
        public string Data { get; set; }

        [DisplayName("CSV data as JSON")]
        [DataType(DataType.MultilineText)]
        public string RawData { get; set; }
        public object DeserializedRawData { get; set; }

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
        public object DeserializedEnrollmentMap { get; set; }

        //public virtual ICollection<DataSyncHistoryDetail> DataSyncHistoryDetails { get; set; }
    }

    public class EnrollmentSyncLineViewModel : DataObject
    {
        //public DataSyncLineViewModel()
        //{
        //    DataSyncHistoryDetails = new HashSet<DataSyncHistoryDetail>();
        //}

        //[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int DataSyncLineId { get; set; }

        //[ForeignKey("DistrictId")]
        //public virtual District District { get; set; }
        public int DistrictId { get; set; }
        public string Table { get; set; }
        [DisplayName("Student Name")]
        public string Name { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string SchoolName { get; set; }

        //[DisplayName("CSV data as JSON")]
        //[DataType(DataType.MultilineText)]
        //public string RawData { get; set; }
        //public object DeserializedRawData { get; set; }

        //[DisplayName("Load Status")]
        //public LoadStatus LoadStatus { get; set; }

        [DisplayName("Sync Status")]
        public SyncStatus SyncStatus { get; set; }
        public string Error { get; set; }

        [DisplayName("Include in Sync?")]
        public bool IncludeInSync { get; set; }

        /// <summary>
        /// Kludge for tracking TargetIds of an Enrollment
        /// </summary>
        //public string EnrollmentMap { get; set; }
        //public object DeserializedEnrollmentMap { get; set; }

        //public virtual ICollection<DataSyncHistoryDetail> DataSyncHistoryDetails { get; set; }
    }
}
