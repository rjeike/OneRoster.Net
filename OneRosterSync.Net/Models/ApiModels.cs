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
        /// <summary>
        /// District identifier (in LMS sid)
        /// </summary>
        public string DistrictId { get; set; }

        /// <summary>
        /// Name of district (for debugging purpose only)
        /// </summary>
        public string DistrictName { get; set; }

        /// <summary>
        /// Status of record: "Added", "Modified", "Deleted", "NoChange", or "None"
        /// This field should be used only as a Hint.  An Added record may already exist in the LMS,
        /// likewise a Modified record may not yet have been created.
        /// The LMS should rely on the presence of the TargetId field to determine if the user is "new"
        /// to the LMS or not.
        /// Finally, a Deleted record should be "deactivated", not physically deleted from the LMS
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Unique identifier for record in Source system
        /// </summary>
        public string SourcedId { get; set; }

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

    public class ApiEnrollmentPost : ApiPost<CsvEnrollment>
    {
        public ApiEnrollmentPost() 
            : base()
        {
        }

        public ApiEnrollmentPost(string json)
            : base(json)
        {
        }

        /// <summary>
        /// Kludge for tracking TargetIds of an Enrollment
        /// </summary>
        public EnrollmentMap EnrollmentMap { get; set; }
    }

	public class ApiClassPost : ApiPost<CsvClass>
	{
		public ApiClassPost()
			: base()
		{
		}

		public ApiClassPost(string json)
			: base(json)
		{
		}

		public string CourseTargetId { get; set; }
		public string TermTargetId { get; set; }
		public string SchoolTargetId { get; set; }
	}


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
