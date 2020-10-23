using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OneRosterSync.Net.Models
{
    public class CleverUsers
    {
        public List<CleverUser> data { get; set; } = new List<CleverUser>();
        public List<NextLink> links { get; set; } = new List<NextLink>();
    }

    public class CleverUser
    {
        public CleverUserData data { get; set; }
    }

    public class CleverUserData
    {
        public string id { get; set; }
        public string sis_id { get; set; }
        public string email { get; set; }
        public CleverUserName name { get; set; }
        public string[] schools { get; set; }
        public string grade { get; set; }
        public string state_id { get; set; }

        [JsonProperty(Order = -2, NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string last_modified { get; set; }

        public class CleverUserName
        {
            public string first { get; set; }
            public string middle { get; set; }
            public string last { get; set; }
        }
    }

    public class CleverOrgs
    {
        public List<CleverOrg> data { get; set; } = new List<CleverOrg>();
        public List<NextLink> links { get; set; } = new List<NextLink>();
    }

    public class CleverOrg
    {
        public CleverOrgData data { get; set; }
    }

    public class CleverOrgData
    {
        public string id { get; set; }
        public string name { get; set; }
        public string school_number { get; set; }
        public string sis_id { get; set; }
        public string state_id { get; set; }

        [JsonProperty(Order = -2, NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string last_modified { get; set; }
    }

    public class NextLink
    {
        public string rel { get; set; }
        public string uri { get; set; }
    }
}
