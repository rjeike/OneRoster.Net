using Newtonsoft.Json;

namespace OneRosterSync.Net.Models
{
#pragma warning disable IDE1006 // Naming Styles
    public class CsvBaseObject
    {
        public string sourcedId { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string status { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string dateLastModified { get; set; }

        [JsonIgnore]
        public bool isDeleted => status?.ToLower() == "deleted";
    }

    public class CsvOrg : CsvBaseObject
    {
        public string name { get; set; }
        public string type { get; set; }
        public string identifier { get; set; }
    }

    public class CsvAcademicSession : CsvBaseObject
    {
        public string title { get; set; }
        public string type { get; set; }
        public string startDate { get; set; }
        public string endDate { get; set; }
        public string schoolYear { get; set; }
    }

    public class CsvCourse : CsvBaseObject
    {
        //public string schoolYearSourcedId { get; set; }
        public string title { get; set; }
        public string courseCode { get; set; }
        //public string grades { get; set; }
        public string orgSourcedId { get; set; }
        //public string subjects { get; set; }
        //public string subjectCodes { get; set; }
    }

    public class CsvClass : CsvBaseObject
    {
        public string title { get; set; }
        public string courseSourcedId { get; set; }
        public string classCode { get; set; }
        public string classType { get; set; }
        public string schoolSourcedId { get; set; }
        public string termSourcedIds { get; set; }
    }

    public class CsvEnrollment : CsvBaseObject
    {
        public string classSourcedId { get; set; }
        public string schoolSourcedId { get; set; }
        public string userSourcedId { get; set; }
        public string role { get; set; }
    }

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
    }
#pragma warning restore IDE1006 // Naming Styles
}
