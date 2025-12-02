Alerts

1.	For each module there will be different type of alerts.
2.	Alerts are user specific (HR) not organization specific
3.	HR can able to setup preferences based on the requirement., and also
he can able to setup preference based the Tax Term Specific
Below the screen shot for Alert setting for HR
 




Each module will have different type of alerts:

Personnel File
•	Missing Documents

Projects
•	Expiring Documents
•	Pending Action/Expired Documents

PAF
•	Expiring Documents
•	Missing Documents
•	Pending Action/Expired Documents
•	Sharing of Documents
•	LCA Job Title and employee designation comparison
•	LCA Wage Rate and employee Ofered salary comparison

EAD
•	Pending Action/Expired Documents
•	Expiring Work Authorization Documents

Form I-983
•	Pending Action/Expired Documents
•	Sharing of Documents

L-1 Visa
•	Pending Action/Expired Documents
•	Expiring Work Authorization Documents

E-3 Visa
•	Pending Action/Expired Documents
•	Expiring Work Authorization Documents

TN Visa
•	Pending Action/Expired Documents
•	Expiring Work Authorization Documents

H-1B
•	Expiring Work Authorization Documents
•	Sharing of Documents
•	Expiring Documents
•	Missing Documents
•	Pending Action/Expired Documents

Form I-9
•	Pending Action/Expired Documents
•	Expiring Documents
•	

Timesheets
•	Timesheet Approval Reminder
•	Leave Approval Reminder



Also the HR can able to set the preferred time interval to receive alerts digest

The whole data we are saving in the 
    OrganizationAlertSettings collection

 

NewAlertSettings for W2 Employees
NewAlertSettings1099 for 1099 Employees
NewAlertSettingsC2C for C2C employee
 
Code base: AlertSettingController.cs

HR Manager

 1.For Adding and updating alert settings.
POST : Alerts/Settings/AddOrUpdate

 


2. For Getting the alerts settings
GET:Alert/Settings

 
Employee:
1.For Adding and updating alert settings.
    Post:Employee/AlertSettings/AddOrUpdate
 

2. For Getting the alerts settings
    GET: Employee/{empId}/GetAlertSettings
 



Till now we have discussed about how to store the alert settings .,
Based on the alert settings, we need to trigger the alerts for the respective user. Here one point to be mentioned that, 
   We will be having mainly two types of alerts.
1.	Immediate alerts
 	These alerts will be triggered on the time of action done.
for example when timesheet submitted by Employee., HR will receive the alert immediately.

2.	Scheduler Alerts
 	These alerts will be triggered through specific time interval. For example, If Employee not submitted the timesheet with in the timesheet cycle., we are daily sending the alerts to Employee until he submit the timesheet.


How the Alerts will be triggered - Code base :
   For Immediate alerts we are call the service after the action done.
   For scheduler alerts we are using scheduler jobs

For this we have web jobs
   


 


From Compliance Alerts., we will be Process the alerts.

We will get the Alerts Settings data for all organizations and will process one by one
  

 


Lets check one module.,
 Timesheets:
 
First, we will get all the data related to 
•	The organization related timesheet data.,
•	HR details
•	Already triggered alert list
•	Employee list
So here we are comparing the data and getting the timesheet not submitted employees and sending the alerts to them.


We are storing the alerts data in Elastic DB.
NewAlertElastic.cs
   public class NewAlertElastic : BaseType
    {
        [Text(Name = "id")]
        public virtual string _id { get; set; }
        [Text(Name = "AlertId")]
        public virtual string AlertId { get; set; }
        [Text(Name = "CreatedDateTime")]
        public virtual DateTime CreatedDateTime { get; set; }
        [Text(Name = "ModifiedDateTime")]
        public virtual DateTime ModifiedDateTime { get; set; }
        [Text(Name = "CreatedBy")]
        public virtual ElasticUserAccount CreatedBy { get; set; }
        [Text(Name = "ModifiedBy")]
        public virtual ElasticUserAccount ModifiedBy { get; set; }
        [Text(Name = "Name")]
        public virtual string Name { get; set; }
        [Text(Name = "SourceId")]
        public virtual string SourceId { get; set; }
        [Text(Name = "Description")]
        public virtual string Description { get; set; }
        [Text(Name = "RedirectionType")]
        public virtual string RedirectionType { get; set; }
        [Text(Name = "AlertExpiryDate")]
        public virtual DateTime AlertExpiryDate { get; set; }
        [Text(Name = "IsDeleted")]
        public virtual bool IsDeleted { get; set; }
        [Text(Name = "RepeatInterval")]
        public virtual int RepeatInterval { get; set; }
        [Text(Name = "RepeatIntervalInHours")]
        public virtual int RepeatIntervalInHours { get; set; }
        [Text(Name = "EmployeeId")]
        public virtual string EmployeeId { get; set; }
        [Text(Name = "AlertCategory")]
        public virtual AlertCategory AlertCategory { get; set; }
        [Text(Name = "MissingDataList")]
        public virtual IEnumerable<MissingData> MissingDataList { get; set; }
        [Text(Name = "AlertsPreferenceTypes")]
        public virtual AlertsPreferenceTypes AlertsPreferenceTypes { get; set; }
        [Text(Name = "AlertPreference")]
        public virtual AlertsPreferenceTypes AlertPreference { get; set; }
        [Text(Name = "AlertGroup")]
        public virtual AlertGroup AlertGroup { get; set; }
        [Text(Name = "AlertType")]
        public virtual AlertType AlertType { get; set; }
        [Text(Name = "IsRead")]
        public virtual bool IsRead { get; set; }
        [Text(Name = "AlertClassification")]
        public virtual AlertClassification AlertClassification { get; set; }
        [Text(Name = "DocType")]
        public virtual DocType DocType { get; set; }
        [Text(Name = "DocumentGroup")]
        public virtual DocumentGroup DocumentGroup { get; set; }
        [Text(Name = "SnoozeInterval")]
        public virtual SnoozeInterval SnoozeInterval { get; set; }
        [Text(Name = "SnoozeDateTime")]
        public virtual DateTime SnoozeDateTime { get; set; }
        [Text(Name = "Recepient")]
        public virtual ElasticUserAccount Recepient { get; set; }
        [Text(Name = "IsAlert")]
        public virtual bool IsAlert { get; set; }
        [Text(Name = "IsMail")]
        public virtual bool IsMail { get; set; }
        [Text(Name = "HireType")]
        public virtual string HireType { get; set; }
    }



Once we store the data in Elastic DB., We are triggering the Notification to Employee by using SignalR

SignalR:
https://learn.microsoft.com/en-us/aspnet/signalr/overview/getting-started/introduction-to-signalr

 

