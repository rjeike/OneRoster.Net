# OneRosterSync.Net
OneRosterSync.Net is an ASP.Net Core application that:
1. Processes OneRoster CSV files 
1. Loads the CSV files and keeps track of Add / Update / Delete status of each record
1. Applies the changes to your system via a Web Service

The OneRoster CSV format is here:
[OneRoster CSV Formats](https://www.imsglobal.org/oneroster-v11-final-csv-tables)

# Benefits
The benefits of OneRoster.Net include:
1. Implements the logic of safely parsing, validating, and processing CSV files from the District.
1. Keeps track of the current state of the District system and only calls your LMS with updates when data changes occur.
1. Allows you to specifiy which Courses to map as you are likely interested in a subset of the District's data.
1. Maintains persistent tracking of the mapping between IDs in the District system and your System.
1. Finds deleted records without having to rely on the District to identify them.
1. Allows for an approval process over changes to be applied to your system; you don't have to blindly trust the data feed and worry about bad data corrupting your LMS.
1. Provides an audit log of all changes applied to your system
1. Email notification of successfully processing and/or errors

# Diagram
Roster data flows through OneRoster.Net:
```
+---------------+      +---------------+       +---------------+ 
|  District     |  ==> | OneRoster.Net |  ==>  | Your LMS      |
+---------------+      +---------------+       +---------------+ 
```

# Processing Stages
The processing occurs in three stages:

## 1. Loading
CSV files are processed and loaded into the OneRoster.Net database

## 2. Analyzing
After data is loaded, it is analyzed:
1. Deleted records are identified
1. Mapping between records is established (e.g. Class ==> Course, Enrollment ==> User and Class)
1. Determine what records should be included in the Sync with your LMS

## 3. Applying
The records that need to be Synced are walked and the API on your LMS that you expose are called.

# Getting Started
...Section TBD...

# Deployment (Temporary)
Temporary instructions till this is dockerized.

```
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=1Roster.Net" -p 1433:1433 microsoft/mssql-server-linux
dotnet ef database update
dotnet run --environment="Development"
```
