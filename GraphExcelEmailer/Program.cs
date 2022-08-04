using Newtonsoft.Json;
using System.Text;

string logfile = Path.GetTempPath() + "log." + DateTime.Now.ToString("yyyyMMddHHmmss") + ".txt";
StreamWriter sw = new(logfile);
sw.AutoFlush = true;
LogEntry("Program Started.");

LogEntry("Loading settings from appsettings.jason and secrets Azure KeyVault...");
var settings = Settings.LoadSettings();
LogEntry("Done loading settings from appsettings.jason and secrets Azure KeyVault.");

LogEntry("Establishing App-only authentication for Microsoft Graph...");
GraphHelper.EnsureGraphForAppOnlyAuth(settings);
LogEntry("Done establishing App-only authentication for Microsoft Graph.");

List<string> doNotSendEmails;
LogEntry("Getting list of do not send emails...");
doNotSendEmails = await GetListofDoNotSendEmails();
LogEntry("Done getting list of do not send emails.");

LogEntry("Start sending Replies to past emails...");
await SendManyReplyEmailsAsync();
LogEntry("Done sending replies to past emails.");

LogEntry("Start sending new emails...");
await SendManyEmailsAsync();
LogEntry("Done sending new emails.");

LogEntry("Program Ended.");
sw.Close();

await GraphHelper.SendReportMailAsync("Graph API Test Run Report", "See attachment.", logfile);

async Task<List<string>> GetListofDoNotSendEmails()
{
    // Get a list of emails that should not get another message, 
    // either because they are in the DoNotSend table,
    // or because they have received two messages already, 
    // which means the emails are in the Sent2emails table.
    
    List<string> list = new();
    LogEntry("Getting list of addresses included in the DoNotSend table...");
    var donotsenttable = await GraphHelper.GetTableRowsAsync("DoNotSend");
    foreach (var row in donotsenttable)
    {
        var aRow = JsonConvert.DeserializeObject<dynamic>(row.Values.RootElement.ToString());
        if (aRow == null) { continue; }
        string email = aRow[0][0];
        if (!list.Contains(email.ToLower())) { list.Add(email.ToLower()); }
    }
    LogEntry(donotsenttable.Count + " entries in the DoNotSend table.");
    LogEntry("Getting list of addresses that already received two emails...");
    var sent2emailstable = await GraphHelper.GetTableRowsAsync("Sent2emails");
    foreach (var row in sent2emailstable)
    {
        var aRow = JsonConvert.DeserializeObject<dynamic>(row.Values.RootElement.ToString());
        if (aRow == null) { continue; }
        string email = aRow[0][1];
        if (!list.Contains(email.ToLower())) { list.Add(email.ToLower()); }
    }
    LogEntry(sent2emailstable.Count + " entries in the Sent2emails table.");
    LogEntry(list.Count + " emails addresses will be ignored.");
    return list;
}

async Task SendManyEmailsAsync()
{
    try
    {
        int counter = 0;
        LogEntry("Get entries from the NewEmails table.");
        var rowsPage = await GraphHelper.GetTableRowsAsync("NewEmails");

        // Reverse the order of the row results, so we can delete from high to low row number.
        // This avoids changing a row numbers at runtime and causing problems on row delete.
        foreach (var row in rowsPage.Reverse())
        {
            var aRow = JsonConvert.DeserializeObject<dynamic>(row.Values.RootElement.ToString());
            if (aRow == null) { continue; }
            string name = aRow[0][0];
            string email = aRow[0][1];
            if (!doNotSendEmails.Contains(email.ToLower()))
            {
                // New emails get a custom InternetMessageId. 
                // This InternetMessageId will be used later to locate the message and reply to it.
                string msgid = "<" + Guid.NewGuid().ToString() + "@bcc.bz>";
                double sentdatetime = DateTime.Now.ToOADate();
                StringBuilder bodytext = new();
                bodytext.AppendLine("<p>Hello " + name.Trim() + ",<br><br>");
                bodytext.AppendLine("How are you?<br><br>");
                bodytext.AppendLine("Graph Tester<br>");
                bodytext.AppendLine("Intern<br>");
                bodytext.AppendLine("Better Computing Consulting<br>");
                bodytext.AppendLine("https://bcc.bz</p>");
                await GraphHelper.SendMailAsync(email, msgid, "Graph API test", bodytext.ToString());
                LogEntry("Sent message to email " + email + ". InternetMessageId: " + msgid);
                counter++;
                await AddSentEmailsTableRow(name, email, msgid, sentdatetime);
                LogEntry("Added message to email " + email + " to the SentEmails table.");
            }
            else
            {
                LogEntry("Skipping email to " + email + " as it has already received two emails or is in the do not send list.");
            }
            bool success = await GraphHelper.DeleteTableRowAsync("NewEmails", row);
            if (success)
            {
                LogEntry("Deleted entry with email " + email + " from the NewEmails table.");
            }
            else
            {
                LogEntry("Failed deleting entry with email " + email + " from the NewEmails table.");
            }
        }
        LogEntry(counter + " New emails were sent.");
    }
    catch (Exception ex)
    {
        LogEntry($"Error sending mail: {ex.Message}");
    }
}

async Task SendManyReplyEmailsAsync()
{
    try
    {
        int counter = 0;
        LogEntry("Get entries from the SentEmails table.");
        var rowsPage = await GraphHelper.GetTableRowsAsync("SentEmails");

        // Reverse the order of the row results, so we can delete from high to low row number.
        // This avoids changing a row numbers at runtime and causing problems on row delete.
        foreach (var row in rowsPage.Reverse())
        {
            var aRow = JsonConvert.DeserializeObject<dynamic>(row.Values.RootElement.ToString());
            if (aRow == null) { continue; }
            string name = aRow[0][0];
            string email = aRow[0][1];
            if (!doNotSendEmails.Contains(email.ToLower()))
            {
                string msgid = aRow[0][2];
                double sent1stdatetime = aRow[0][3];
                DateTime sent1stemaildatetime = DateTime.FromOADate(sent1stdatetime);

                // The program will not reply to an email unless it is at least 6 days old.
                if (!(sent1stemaildatetime < DateTime.Now.AddDays(-6)))
                {
                    LogEntry("Last email to " + email + " was not at least one week ago. Skipping it for now.");
                    continue;
                }
                double sent2nddatetime = DateTime.Now.ToOADate();

                // Locate the message by its InternetMessageId
                var messagePage = await GraphHelper.GetSentItemAsync(msgid);
                var message = messagePage.CurrentPage.Single();
                StringBuilder commenttext = new();
                commenttext.AppendLine("<p>Hello " + name.Trim() + ",<br><br>");
                commenttext.AppendLine("I have not hard from you. Are you okay?<br><br>");
                commenttext.AppendLine("Graph Tester<br>");
                commenttext.AppendLine("Intern<br>");
                commenttext.AppendLine("Better Computing Consulting<br>");
                commenttext.AppendLine("https://bcc.bz</p>");
                await GraphHelper.SendReplyAsync(message.Id, commenttext.ToString());
                LogEntry("Sent Reply message to email " + email + ". InternetMessageId: " + msgid);
                counter++;
                await AddSent2emailsTableRow(name, email, msgid, sent1stdatetime, sent2nddatetime);
                LogEntry("Added Reply message to email " + email + " to the Sent2emails table.");
            }
            else
            {
                LogEntry("Skipping reply email to " + email + " as it has already received two emails or is in the do not send list.");
            }
            bool success = await GraphHelper.DeleteTableRowAsync("SentEmails", row);
            if (success)
            {
                LogEntry("Deleted entry with email " + email + " from the SentEmails table.");
            }
            else
            {
                LogEntry("Failed deleting entry with email " + email + " from the SentEmails table.");
            }
        }
        LogEntry(counter + " Reply emails were sent.");
    }
    catch (Exception ex)
    {
        LogEntry($"Error sending Many Replies: {ex.Message}");
    }
}

async Task AddSentEmailsTableRow(string name, string email, string inetmsgid, double sentdaytime)
{
    try
    {
        dynamic[] arr = { name, email, inetmsgid, sentdaytime };
        dynamic[][] aRow = { arr };
        var row = await GraphHelper.AddTableRowAsync("SentEmails", aRow);
        var resRow = JsonConvert.DeserializeObject<dynamic>(row.Values.RootElement.ToString());
        if (resRow != null)
        {
            double d = resRow[0][3];
            DateTime sentdate = DateTime.FromOADate(d);
        }
    }
    catch (Exception ex)
    {
        LogEntry($"Error adding table row: {ex.Message}");
    }
}

async Task AddSent2emailsTableRow(string name, string email, string inetmsgid, double sent1stdaytime, double sent2nddatetime)
{
    try
    {
        dynamic[] arr = { name, email, inetmsgid, sent1stdaytime, sent2nddatetime };
        dynamic[][] aRow = { arr };
        var row = await GraphHelper.AddTableRowAsync("Sent2emails", aRow);
        var resRow = JsonConvert.DeserializeObject<dynamic>(row.Values.RootElement.ToString());
        if (resRow != null)
        {
            double sent1st = resRow[0][3];
            DateTime sent1stdate = DateTime.FromOADate(sent1st);
            double sent2nd = resRow[0][4];
            DateTime sent2nddate = DateTime.FromOADate(sent2nd);
        }
    }
    catch (Exception ex)
    {
        LogEntry($"Error adding table row: {ex.Message}");
    }
}
void LogEntry(string entry)
{
    sw.WriteLine("{0,-21}{1}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ":", entry);
    Console.WriteLine(entry);
}