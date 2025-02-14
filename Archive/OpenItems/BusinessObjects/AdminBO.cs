using System.Collections.Generic;
using System.Data.Entity.Core.Objects;
using System.Linq;
using OpenItems.Data;
using OpenItems.Properties;

namespace GSA.OpenItems.Web
{
    using System;
    using System.Data;
    using System.Configuration;
    using System.Data.SqlClient;
    using System.Text;
    using System.Net.Mail;
    using Data;


    /// <summary>
    /// Summary description for Admin
    /// </summary>
    public class AdminBO
    {
        private readonly IAdminDataLayer Dal;
        public AdminBO(IAdminDataLayer dal)
        {
            Dal = dal;
        }

        public IEnumerable<spGetLoadListNotArchived_Result> GetLoadListNotArchived()
        {
            return Dal.GetLoadListNotArchived();
        }

        public List<spGetLoadListWithActiveDueDate_Result> GetLoadListWithActiveDueDate()
        {
            return Dal.GetLoadListWithActiveDueDate().ToList();
        }

        public List<spGetLoadDetails_Result> GetLoadDetails(int iLoadID)
        {
            return Dal.GetLoadDetails(iLoadID).ToList();
        }

        public DataSet GetAllAttachmentTypes()
        {
            return Dal.GetAllAttachmentTypes();
        }

        public List<spGetDefaultJustifications_Result> GetDefaultJustificationTypes()
        {
            return Dal.GetDefaultJustificationTypes().ToList();
        }

        public List<spGetJustificationTypeByID_Result> GetJustificationTypeByID(int iID)
        {
            return Dal.GetJustificationTypeByID(iID).ToList();
        }

        public void ArchiveLoad(int iLoadID)
        {
            Dal.ArchiveLoad(iLoadID);
        }

        public void UnarchiveLoad(int iLoadID)
        {
           Dal.UnarchiveLoad(iLoadID);
        }

        public void SendDueDateReminder(string sLoadID, string sLoadDate, string sDueDate, out string sSentToR, out string sSentToOA, out string sSentToBDA)
        {
            //1. send email to all Reviewers
            //2. send email to Organization Administrators
            //3. send email to BD Administrators
            DataSet dsOItemsRevewers = Dal.GetPendingOItemsByUser(Convert.ToInt32(sLoadID));

            SendDueDateReminderToReviewers(sLoadID, sLoadDate, sDueDate, dsOItemsRevewers, out sSentToR);

            SendDueDateReminderToOrgAdmins(sLoadID, sLoadDate, sDueDate, dsOItemsRevewers, out sSentToOA);

            SendDueDateReminderToBDAdmins(sLoadID, sLoadDate, sDueDate, dsOItemsRevewers, out sSentToBDA);


        }

        public int SaveJustification(string sJustificationDescription, int iJustification, bool bDisplayAddOnField,
            string sAddOnDescription)
        {
            return Dal.SaveJustification(sJustificationDescription, iJustification, bDisplayAddOnField, sAddOnDescription);
        }

        private static void SendDueDateReminderToReviewers(string sLoadID, string sLoadDate, string sDueDate, DataSet dsOItemsRevewers, out string sSentTo)
        {
            var sRecipients = "";
            var sFileName = Settings.Default.DueDateReminderFile;
            var sPath = System.Web.Hosting.HostingEnvironment.MapPath("~/docs/");
            var sFullPath = sPath + sFileName;

            var str_subject = Utility.GetFileText(sFullPath, "SUBJECT");
            str_subject = str_subject.Replace("[p_DueDate]", String.Format("{0:MMM dd, yyyy}", sDueDate));

            var str_body = Utility.GetFileText(sFullPath, "BODY_REVIEWER");
            str_body = str_body.Replace("[p_LoadDate]", String.Format("{0:MMM dd, yyyy}", sLoadID));
            str_body = str_body.Replace("[p_DueDate]", String.Format("{0:MMM dd, yyyy}", sDueDate));
            str_body = str_body.Replace("_message_body", "");
            str_body = str_body.Replace("string", "");

            var dt = dsOItemsRevewers.Tables[0];

            foreach (DataRow dr in dt.Rows)
            {
                var items_count = (int)dr["ItemsCount"];
                var current_body = str_body.Replace("[p_ItemsCount]", items_count.ToString());

                var emails_arr = new string[1];
                emails_arr[0] = (string)dr["Email"];
                //emails_arr[0] = "sergei.mirosh@gsa.gov";

                var emailobj = new EmailUtility();
                emailobj.Subject = str_subject;
                emailobj.MessageBody = current_body;
                emailobj.To = emails_arr;
                emailobj.SendEmail();

                if (sRecipients == "")
                {
                    sRecipients = emails_arr[0];
                }
                else
                {
                    sRecipients = sRecipients + ", " + emails_arr[0];
                }

            }

            sSentTo = sRecipients;

        }


        private void SendDueDateReminderToOrgAdmins(string sLoadID, string sLoadDate, string sDueDate, DataSet dsOItemsRevewers, out string sSentTo)
        {
            //sSentTo = "Z";
            var sRecipients = "";

            var sFileName = Settings.Default.DueDateReminderFile;
            var sPath = System.Web.Hosting.HostingEnvironment.MapPath("~/docs/");
            var sFullPath = sPath + sFileName;

            var str_subject = Utility.GetFileText(sFullPath, "SUBJECT");
            str_subject = str_subject.Replace("[p_DueDate]", String.Format("{0:MMM dd, yyyy}", sDueDate));

            var str_body = Utility.GetFileText(sFullPath, "BODY_ORG_ADMIN");
            str_body = str_body.Replace("[p_LoadDate]", String.Format("{0:MMM dd, yyyy}", sLoadID));
            str_body = str_body.Replace("[p_DueDate]", String.Format("{0:MMM dd, yyyy}", sDueDate));
            str_body = str_body.Replace("_message_body", "");
            str_body = str_body.Replace("string", "");
            var dsAdmins = Dal.GetUsersByRole(UserRoles.urOrganizationAdmin);
            var dsNAItems = Dal.GetPendingNAOItemsByOrg(Convert.ToInt32(sLoadID)); //get not assigned items
            var dtNAItems = dsNAItems.Tables[0];

            //DataTable dt = dsOItemsRevewers.Tables[0];
            var dsPendingItems = Dal.GetPendingOItems(Convert.ToInt32(sLoadID)); //get all assigned pending items
            var dt = dsPendingItems.Tables[0];

            foreach (DataRow drAdmin in dsAdmins.Tables[0].Rows)
            {
                var organization = (string)drAdmin["Organization"];

                var emails_arr = new string[1];
                emails_arr[0] = (string)drAdmin["Email"];

                //build the list of OpenItems - Reviewers for the current Administrator/Organization:
                var dr_col = dt.Select("Organization = '" + organization + "'");

                var sb = new StringBuilder();
                sb.Append("<tr><td>");

                foreach (var dr in dr_col)
                {
                    sb.Append((string)dr["UserFullName"]);
                    sb.Append("</td><td /><td>");
                    sb.Append(((int)dr["ItemsCount"]).ToString());
                    sb.Append("</td></tr><tr><td>");
                }

                var dr_NA = dtNAItems.Select("ResponsibleOrg = '" + organization + "'");
                if (dr_NA.Length > 0)
                {
                    sb.Append("Not Assigned Items");
                    sb.Append("</td><td /><td>");
                    sb.Append(((int)dr_NA[0]["ItemsCount"]).ToString());
                    sb.Append("</td></tr>");
                }
                else
                    sb.Append("</td></tr>");

                var current_email_body = str_body.Replace("[p_TR_ReviewersList]", sb.ToString());

                var emailobj = new EmailUtility();
                emailobj.Subject = str_subject;
                emailobj.MessageBody = current_email_body;
                emailobj.To = emails_arr;
                emailobj.SendEmail();

                if (sRecipients == "")
                {
                    sRecipients = emails_arr[0];
                }
                else
                {
                    sRecipients = sRecipients + ", " + emails_arr[0];
                }
            }
            sSentTo = sRecipients;
        }

        private void SendDueDateReminderToBDAdmins(string sLoadID, string sLoadDate, string sDueDate, DataSet dsOItemsRevewers, out string sSentTo)
        {
            sSentTo = "";
            var sRecipients = "";
   
            var sFileName = Settings.Default.DueDateReminderFile;
            var sPath = System.Web.Hosting.HostingEnvironment.MapPath("~/docs/");
            var sFullPath = sPath + sFileName;

            var str_subject = Utility.GetFileText(sFullPath, "SUBJECT");
            str_subject = str_subject.Replace("[p_DueDate]", String.Format("{0:MMM dd, yyyy}", sDueDate));

            var str_body = Utility.GetFileText(sFullPath, "BODY_BD_ADMIN");
            str_body = str_body.Replace("[p_LoadDate]", String.Format("{0:MMM dd, yyyy}", sLoadID));
            str_body = str_body.Replace("[p_DueDate]", String.Format("{0:MMM dd, yyyy}", sDueDate));
            str_body = str_body.Replace("_message_body", "");
            str_body = str_body.Replace("string", "");

            var dsAdmins = Dal.GetUsersByRole(UserRoles.urBudgetDivisionAdmin);
            var dt = dsAdmins.Tables[0];

            var emails_arr = new string[dt.Rows.Count];

            for (var i = 0; i < dt.Rows.Count; i++)
            {
                emails_arr[i] = (string)dt.Rows[i]["Email"];

                if (sRecipients == "")
                {
                    sRecipients = emails_arr[i];
                }
                else
                {
                    sRecipients = sRecipients + ", " + emails_arr[i];
                }
            }

            //emails_arr[0] = "sergei.mirosh@gsa.gov";
            // emails_arr[1] = "vaishali.shah@gsa.gov";




            //build the list of OpenItems per Organization
            var pendingOItems = Dal.GetPendingOItemsByOrg(Convert.ToInt32(sLoadID)).ToList();
            var pendingDeobligate = Dal.GetPendingDeobligateByOrg(Convert.ToInt32(sLoadID)).ToList();

            var sb = new StringBuilder();
            sb.Append("<tr><td>");

            foreach (var pendingOItem in pendingOItems)
            {
                var organization = pendingOItem.Organization;
                var openItemsCount = pendingOItem.ItemsCount;
               

                var pendingDeobligateSelect = pendingDeobligate.Where(o => o.Organization == organization).ToList();
                var deobligate = pendingDeobligateSelect.Any() ? pendingDeobligateSelect.First().RecordsCount.GetValueOrDefault(0) : 0;

                sb.Append(organization);
                sb.Append("</td><td>");
                sb.Append(openItemsCount.ToString());
                sb.Append("</td><td>");
                sb.Append(deobligate.ToString());
                sb.Append("</td></tr>");
                sb.Append("<tr><td>");
            }

            sb.Append("</td></tr>");

            var current_email_body = str_body.Replace("[p_TR_OrgList]", sb.ToString());

            var emailobj = new EmailUtility();
            emailobj.Subject = str_subject;
            emailobj.MessageBody = current_email_body;
            emailobj.To = emails_arr;
            emailobj.SendEmail();

            sSentTo = sRecipients;

        }
        public static void SendEmailToBDAdminsOnReassignOrReroute(string sOldOrgCode, string sNewOrgCode, string sOldReviewer, string sNewReviewer, string sComments)
        {
        }
        public void SendCustomEmailToOrgAdmins(string sBody, string sSubject, out string sSentTo, out int iCnt)
        {
            var dsAdmins = Dal.GetUsersByRole(UserRoles.urOrganizationAdmin);

            var sSmtpServer = Settings.Default.SMTPServer;
            var sFrom = Settings.Default.MailSenderAddress;

            var oMessage = new System.Net.Mail.MailMessage();
            oMessage.From = new MailAddress(sFrom);
            oMessage.Subject = sSubject;
            oMessage.Body = sBody;
            oMessage.IsBodyHtml = true;

            iCnt = 0;
            foreach (DataRow drAdmin in dsAdmins.Tables[0].Rows)
            {
                var sTo = (string)drAdmin["Email"];
                var oTo = new MailAddress(sTo);
                oMessage.To.Add(oTo);
                iCnt = iCnt + 1;
            }

            var oClient = new SmtpClient(sSmtpServer);
            // Include credentials if the server requires them.
            oClient.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;

            // ****************** UNCOMMENT AFTER TESTING! *****************************
            oClient.Send(oMessage);
            // *************************************************************************

            sSentTo = oMessage.To.ToString();
        }

        public void SendCustomEmailToBDAdmins(string sBody, string sSubject, out string sSentTo, out int iCnt)
        {
         
            var dsAdmins = Dal.GetUsersByRole(UserRoles.urBudgetDivisionAdmin);
            var dt = dsAdmins.Tables[0];

            var sSmtpServer = Settings.Default.SMTPServer;
            var sFrom = Settings.Default.MailSenderAddress;

            var oMessage = new System.Net.Mail.MailMessage();
            oMessage.From = new MailAddress(sFrom);
            oMessage.Subject = sSubject;
            oMessage.Body = sBody;
            oMessage.IsBodyHtml = true;

            iCnt = 0;

            for (var i = 0; i < dt.Rows.Count; i++)
            {
                var sTo = (string)dt.Rows[i]["Email"];
                var oTo = new MailAddress(sTo);
                oMessage.To.Add(oTo);
                iCnt = iCnt + 1;
            }

            var oClient = new SmtpClient(sSmtpServer);
            // Include credentials if the server requires them.
            oClient.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;

            // ****************** UNCOMMENT AFTER TESTING! *****************************
            oClient.Send(oMessage);
            // *************************************************************************

            sSentTo = oMessage.To.ToString();

        }

        public void SendCustomEmailToAllULOUsers(string sBody, string sSubject, out string sSentTo, out int iCnt)
        {

            var dsAdmins = Dal.GetAllULOUsers();
            var dt = dsAdmins.Tables[0];

            var sSmtpServer = Settings.Default.SMTPServer;
            var sFrom = Settings.Default.MailSenderAddress;

            var oMessage = new System.Net.Mail.MailMessage();
            oMessage.From = new MailAddress(sFrom);
            oMessage.Subject = sSubject;
            oMessage.Body = sBody;
            oMessage.IsBodyHtml = true;

            iCnt = 0;

            for (var i = 0; i < dt.Rows.Count; i++)
            {
                var sTo = (string)dt.Rows[i]["Email"];
                var oTo = new MailAddress(sTo);
                oMessage.To.Add(oTo);
                iCnt = iCnt + 1;
            }

            var oClient = new SmtpClient(sSmtpServer);
            // Include credentials if the server requires them.
            oClient.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;

            // ****************** UNCOMMENT AFTER TESTING! *****************************
            oClient.Send(oMessage);
            // *************************************************************************

            sSentTo = oMessage.To.ToString();

        }


        public void SendCustomEmailToReviewers(string sBody, string sSubject, out string sSentTo, out int iCnt)
        {

            var dsUsers = Dal.GetUsersByRole(UserRoles.urReviewer);
            var dtUsers = dsUsers.Tables[0];

            var sSmtpServer = Settings.Default.SMTPServer;
            var sFrom = Settings.Default.MailSenderAddress;

            var oMessage = new System.Net.Mail.MailMessage();
            oMessage.From = new MailAddress(sFrom);
            oMessage.Subject = sSubject;
            oMessage.Body = sBody;
            oMessage.IsBodyHtml = true;

            iCnt = 0;

            foreach (DataRow dr in dtUsers.Rows)
            {
                var sTo = (string)dr["Email"];
                var oTo = new MailAddress(sTo);
                oMessage.To.Add(oTo);
                iCnt = iCnt + 1;
            }

            var oClient = new SmtpClient(sSmtpServer);
            // Include credentials if the server requires them.
            oClient.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;

            // ****************** UNCOMMENT AFTER TESTING! *****************************
            oClient.Send(oMessage);
            // *************************************************************************

            sSentTo = oMessage.To.ToString();
        }
        public void SendCustomEmail(string sTo, string sBody, string sSubject, out string sSentTo, out int iCnt)
        {
            iCnt = 0;
            sSentTo = "";

            var sSmtpServer = Settings.Default.SMTPServer;
            var sFrom = Settings.Default.MailSenderAddress;

            var oMessage = new System.Net.Mail.MailMessage();
            oMessage.From = new MailAddress(sFrom);
            oMessage.Subject = sSubject;
            oMessage.Body = sBody;
            oMessage.IsBodyHtml = true;

            var arrTo = sTo.Split(new char[] { ',' });
            iCnt = 0;

            foreach (var s in arrTo) //check for the duplicates and remove if any before sending 
            {
                if (s.Trim() != "" && s != null)
                {
                    var oTo = new MailAddress(s);
                    oMessage.To.Add(oTo);
                    iCnt = iCnt + 1; ;
                }
            }

            var oClient = new SmtpClient(sSmtpServer);
            // Include credentials if the server requires them.
            oClient.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials;

            //****************** UNCOMMENT AFTER TESTING! *****************************
            oClient.Send(oMessage);
            // *************************************************************************

            sSentTo = oMessage.To.ToString();
        }
        public string GetEmailByUserID(int iUserID)
        {
            var sEmail = "";
            try
            {
                var ds = Dal.GetUserEmailByUserID(iUserID);

                if (ds.Tables[0].Rows.Count > 0)
                {
                    sEmail = ds.Tables[0].Rows[0][0].ToString();
                }
                else
                {
                    sEmail = "";
                }
                return sEmail;
            }
            catch
            {
                return "";
            }
        }

        //SM_
        public string GetOrganizationNameByOrgCode(string sOrgCode)
        {
            var sOrgName = "";
            try
            {
                var orgNames = Dal.GetOrgNameByOrgCode(sOrgCode).ToList();

                if (orgNames.Any())
                {
                    sOrgName = orgNames.First();
                }
                else
                {
                    sOrgName = "";
                }
                return sOrgName;
            }
            catch
            {
                return "";
            }
        }

        //SM_
        public string GetLoadIDByItemID(int iItemID)
        {
            var sLoadID = "";
            try
            {
                var docNums = Dal.GetDocNumByItemID(iItemID).ToList();

                if (docNums.Any())
                {
                    sLoadID = docNums.First();
                }
                else
                {
                    sLoadID = "0";
                }

                return sLoadID;
            }
            catch
            {
                return "0";
            }
        }


    }
}
