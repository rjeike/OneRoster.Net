﻿using CsvHelper.Configuration;

namespace OneRosterSync.Net.Models
{
    sealed class CsvUserClassMap : ClassMap<CsvUser>
    {
        public CsvUserClassMap()
        {
            Map(m => m.sourcedId).Name("sourcedId", "sourcedid", "SOURCEDID", "sourceId");
            Map(m => m.orgSourcedIds).Name("orgSourcedIds", "ORGSOURCEDIDS", "orgSourceIds", "orgsourceids", "orgsourcedids");
            Map(m => m.status).Name("status", "STATUS", "Development");
            Map(m => m.dateLastModified).Name("dateLastModified", "DATELASTMODIFIED");
            Map(m => m.enabledUser).Name("enabledUser", "enableduser", "ENABLEDUSER");
            Map(m => m.role).Name("role", "ROLE");
            Map(m => m.username).Name("username", "USERNAME");
            Map(m => m.givenName).Name("givenName", "givenname", "GIVENNAME");
            Map(m => m.familyName).Name("familyName", "familyname", "FAMILYNAME");
            Map(m => m.middleName).Name("middleName", "middlename", "MIDDLENAME");
            Map(m => m.email).Name("email", "EMAIL");
            Map(m => m.grades).Name("grades", "GRADES");
            Map(m => m.password).Name("password", "PASSWORD");
        }
    }

    sealed class CsvOrgClassMap : ClassMap<CsvOrg>
    {
        public CsvOrgClassMap()
        {
            Map(m => m.sourcedId).Name("sourcedId", "sourcedid", "SOURCEDID", "sourceId");
            Map(m => m.name).Name("name", "NAME", "school", "SCHOOL");
            Map(m => m.status).Name("status", "STATUS", "Development");
            Map(m => m.dateLastModified).Name("dateLastModified", "DATELASTMODIFIED");
            Map(m => m.type).Name("type", "TYPE");
            Map(m => m.identifier).Name("identifier", "IDENTIFIER");
        }
    }
}