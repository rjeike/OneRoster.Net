using Newtonsoft.Json;

namespace OneRosterSync.Net.Models
{
#pragma warning disable IDE1006 // Naming Styles
    public class CsvBaseObject
    {
        /// <summary>
        /// Unique identifier for the object
        /// </summary>
        [JsonProperty(Order = -2)]
        public string sourcedId { get; set; }

        /// <summary>
        /// 4.13.8.     StatusType
        /// active, tobedeleted, inactive
        /// Note: this project will determine deletion status automatically if this field is omitted
        /// </summary>
        [JsonProperty(Order = -2, NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string status { get; set; }

        /// <summary>
        /// Date the record was last modified
        /// </summary>
        [JsonProperty(Order = -2, NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string dateLastModified { get; set; }

        [JsonIgnore]
        public bool isDeleted => status?.ToLower() == "deleted" || status?.ToLower() == "tobedeleted";
    }

    /// <summary>
    /// Organizations (i.e. Schools that belong to the District)
    /// </summary>
    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class CsvOrg : CsvBaseObject
    {
        /// <summary>
        /// Name of the org
        /// </summary>
        public string name { get; set; }

        /// <summary>
        /// From section OneRoster spec 4.13.4. OrgType
        /// department, school, district, local, state, national
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// NCES ID National Center for Education Statistics) for the school/district.
        /// </summary>
        public string identifier { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class CsvAcademicSession : CsvBaseObject
    {
        /// <summary>
        /// Name of this academic session
        /// </summary>
        public string title { get; set; }

        /// <summary>
        /// 4.13.7.     SessionType
        /// gradingPeriod, semester, schoolYear, term
        /// </summary>
        public string type { get; set; }
        public string startDate { get; set; }
        public string endDate { get; set; }

        /// <summary>
        /// The school year for which the academic session contributes. 
        /// This year should be that in which the school year ends.
        /// (Format is: YYYY).
        /// </summary>
        public string schoolYear { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class CsvCourse : CsvBaseObject
    {
        public string schoolYearSourcedId { get; set; }
        public string title { get; set; }
        public string courseCode { get; set; }
        public string grades { get; set; }
        public string orgSourcedId { get; set; }
        public string subjects { get; set; }
        public string subjectCodes { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class CsvClass : CsvBaseObject
    {
        public string title { get; set; }
        public string courseSourcedId { get; set; }
        public string classCode { get; set; }
        public string classType { get; set; }
        public string schoolSourcedId { get; set; }
        public string termSourcedIds { get; set; }
        public string periods { get; set; }
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class CsvEnrollment : CsvBaseObject
    {
        public string classSourcedId { get; set; }
        public string schoolSourcedId { get; set; }
        public string userSourcedId { get; set; }
        public string role { get; set; }
        //Sandesh
        public string user_id { get; set; }
        public string nces_schoolid { get; set; }
        public string action => "enrollinschool";
    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class CsvUser : CsvBaseObject
    {
        public string enabledUser { get; set; }
        public string orgSourcedIds { get; set; }
        public string role { get; set; }
        public string username { get; set; }
        public string givenName { get; set; }
        public string familyName { get; set; }
        public string middleName { get; set; }
        public string password { get; set; }
        public string email { get; set; }
        public string grades { get; set; }
        public string identifier { get; set; }
        public string isLep { get; set; }
    }

    public class EnrollmentMap
    {
        //Sandesh
        //public string classTargetId { get; set; }
        //public string userTargetId { get; set; }
        public string user_id { get; set; }
        public string nces_schoolid { get; set; }
    }

    public class NCESMappingModel
    {
        public string ncesId { get; set; }
        public string stateSchoolId { get; set; }
    }

#pragma warning restore IDE1006 // Naming Styles
}
