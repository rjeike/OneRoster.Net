using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OneRosterSync.Net.Models
{
    /// <summary>
    /// Data posted to LMS API call
    /// </summary>
    public class ApiPostBase
    {
        static public ApiPostBase CreateApiPost(string entity, string json)
        {
            switch (entity)
            {
                case "Org": return new ApiPost<CsvOrg>(json);
                case "Course": return new ApiPost<CsvCourse>(json);
                case "AcademicSession": return new ApiPost<CsvAcademicSession>(json);
                case "Class": return new ApiPost<CsvClass>(json);
                case "User": return new ApiPost<CsvUser>(json);
                case "Enrollment": return new ApiPost<CsvEnrollment>(json);

                default:
                    throw new ArgumentException($"unsupported entity: {entity}");
            }
        }

        /// <summary>
        /// District identifier (in LMS sid)
        /// </summary>
        public string DistrictId { get; set; }

        /// <summary>
        /// Name of district (for debugging purpose only)
        /// </summary>
        public string DistrictName { get; set; }

        /// <summary>
        /// Status of record: "Added", "Modified", or "Deleted"
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Unique identifier for record in Source system
        /// </summary>
        public string SourceId { get; set; }

        /// <summary>
        /// Unique identifier for record in LMS side
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// Last time the record was seen in a CSV import
        /// </summary>
        public DateTime LastSeen { get; set; }

        /// <summary>
        /// Descriptive name of type of the data
        /// </summary>
        public string EntityType { get; protected set; }
    }

    public class ApiPost<T> : ApiPostBase where T : CsvBaseObject
    {
        public ApiPost()
        {
            EntityType = typeof(T).Name;
            if (EntityType.StartsWith("Csv"))
                EntityType = EntityType.Substring(3);
        }

        public ApiPost(string json) 
            : this()
        {
            Data = JsonConvert.DeserializeObject<T>(json);
        }

        /// <summary>
        /// The data for the record (straight from the CSV)
        /// </summary>
        public T Data { get; set; }
    }

    /*
    public class ApiPostOrg : ApiPost<CsvOrg> { }
    public class ApiPostCourse : ApiPost<CsvCourse> { }
    public class ApiPostAcademicSession : ApiPost<CsvAcademicSession> { }
    public class ApiPostClass : ApiPost<CsvClass> { }
    public class ApiPostUser : ApiPost<CsvUser> { }
    public class ApiPostEnrollment : ApiPost<CsvEnrollment> { }
    */

    /// <summary>
    /// Expected response from LMS API call
    /// </summary>
    public class ApiResponse
    {
        /// <summary>
        /// Was the LMS able to successfully process the API request?
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Unique Id of the record on the LMS side
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// If an error, an code (that only has meaning on the LMS side)
        /// </summary>
        public string ErrorCode { get; set; }

        /// <summary>
        /// Helpful error message from LMS
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
