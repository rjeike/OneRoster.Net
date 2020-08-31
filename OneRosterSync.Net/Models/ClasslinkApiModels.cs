using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OneRosterSync.Net.Models
{
    public class ClassLinkUsers
    {
        public List<ClassLinkUser> users { get; set; } = new List<ClassLinkUser>();
    }

    public class ClassLinkUser : CsvUser
    {
        public new string[] grades { get; set; }
        public List<org> orgs { get; set; } = new List<org>();

        public class org
        {
            public string href { get; set; }
            public string sourcedId { get; set; }
            public string type { get; set; }
        }
    }

    public class ClassLinkOrgs
    {
        public List<ClassLinkOrg> orgs { get; set; } = new List<ClassLinkOrg>();
    }

    public class ClassLinkOrg : CsvOrg
    {
    }
}
