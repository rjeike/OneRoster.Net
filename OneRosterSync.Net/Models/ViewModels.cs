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
}
