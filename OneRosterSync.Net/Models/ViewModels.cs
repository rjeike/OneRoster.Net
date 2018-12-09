using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

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
}
