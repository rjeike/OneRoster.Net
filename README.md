# OneRoster.Net
This is an ASP.Net Core application that:
1. Processes OneRoster CSV files from a known folder
1. Loads the CSV files and keeps track of Add / Update / Delete status of each record
1. Applies the changes to your system via a Web Service

The OneRoster CSV format is here:
[OneRoster CSV Formats](https://www.imsglobal.org/oneroster-v11-final-csv-tables)

# Benefits
The benefits include:
1. Handle business logic needed to load CSV files
1. Keeps track of the data over time
1. Provides an audit log of all changes applied to your system
1. Allows for an approval process over changes to be applied to your system

# Diagram

```
+---------------+      +---------------+       +---------------+ 
|  District     |  ==> | OneRoster.Net |  ==>  | Your LMS      |
+---------------+      +---------------+       +---------------+ 
```

# Processing Stages
1. Scheduled - a District is manually scheduled for processing via the UI
1. Queued - the background processor picked it up and queued it for processing
1. LoadProcessing - loading the CSV into SQL tables
1. Analyzing - data has been loaded, now looing for deleted records
1. PendingApproval - waiting for a human to approve the changes before applying them to the target LMS
1. Approved - human has approved, ready to be applied
1. Applying - processing the changes, calling the LMS web services
1. Finished

