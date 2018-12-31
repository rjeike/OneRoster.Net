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

        // LoadStatus
        public int Added { get; set; }
        public int Modified { get; set; }
        public int NoChange { get; set; }
        public int Deleted { get; set; }

        // SyncStatus
        public int Loaded { get; set; }
        public int ReadyToApply { get; set; }
        public int Applied { get; set; }
        public int AppliedFailed { get; set; }

        public int TotalRecords { get; set; }
    }
}
