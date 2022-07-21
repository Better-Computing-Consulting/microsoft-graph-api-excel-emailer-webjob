using Newtonsoft.Json;
using System.Text;

var settings = Settings.LoadSettings();

GraphHelper.EnsureGraphForAppOnlyAuth(settings);

List<string> doNotSendEmails; 
doNotSendEmails = await GetListofDoNotSendEmails();

await SendManyReplyEmailsAsync();
await SendManyEmailsAsync();

async Task<List<string>> GetListofDoNotSendEmails()
{
    List<string> list = new();
    var donotsenttable = await GraphHelper.GetTableRowsAsync("DoNotSend");
    foreach (var row in donotsenttable)
    {
        var aRow = JsonConvert.DeserializeObject<dynamic>(row.Values.RootElement.ToString());
        if (aRow == null) { continue; }
        string email = aRow[0][0];
        if (!list.Contains(email.ToLower())) { list.Add(email.ToLower()); }
    }
    var sent2emailstable = await GraphHelper.GetTableRowsAsync("Sent2emails");
    foreach (var row in sent2emailstable)
    {
        var aRow = JsonConvert.DeserializeObject<dynamic>(row.Values.RootElement.ToString());
        if (aRow == null) { continue; }
        string email = aRow[0][1];
        if (!list.Contains(email.ToLower())) { list.Add(email.ToLower()); }
    }
    return list;
}

async Task SendManyEmailsAsync()
{
    try
    {
        Console.WriteLine();
        var rowsPage = await GraphHelper.GetTableRowsAsync("NewEmails");
        foreach (var row in rowsPage.Reverse())
        {
            var aRow = JsonConvert.DeserializeObject<dynamic>(row.Values.RootElement.ToString());
            if (aRow == null) { continue; }
            string name = aRow[0][0];
            string email = aRow[0][1];
            if (!doNotSendEmails.Contains(email.ToLower()))
            {
                string msgid = "<" + Guid.NewGuid().ToString() + "@bcc.bz>";
                double sentdatetime = DateTime.Now.ToOADate();
                StringBuilder bodytext = new();
                bodytext.AppendLine("<p>Hello " + name.Trim() + ",<br><br>");
                bodytext.AppendLine("How are you?<br><br>");
                bodytext.AppendLine("Graph Tester<br>");
                bodytext.AppendLine("Intern<br>");
                bodytext.AppendLine("Better Computing Consulting<br>");
                bodytext.AppendLine("https://bcc.bz<br>");
                await GraphHelper.SendMailAsync(email, msgid, "Graph API test", bodytext.ToString());
                await AddSentEmailsTableRow(name, email, msgid, sentdatetime);
            }
            else
            {
                Console.WriteLine("Skipping email to " + email + " as it has already received two emails or is in the do not send list.");
            }
            bool success = await GraphHelper.DeleteTableRowAsync("NewEmails", row);
            if (success)
            {
                Console.WriteLine("Deleted row index: " + row.Index.ToString());
            }
            else
            {
                Console.WriteLine("Failed Deleting row index: " + row.Index.ToString());
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error sending mail: {ex.Message}");
    }
}

async Task SendManyReplyEmailsAsync()
{
    try
    {
        Console.WriteLine();
        var rowsPage = await GraphHelper.GetTableRowsAsync("SentEmails");
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
                if (!(sent1stemaildatetime < DateTime.Now.AddDays(-6)))
                {
                    Console.WriteLine("Last email to " + email + " was not at least one week ago. Skipping it for now.");
                    continue;
                }
                double sent2nddatetime = DateTime.Now.ToOADate();
                var messagePage = await GraphHelper.GetSentItemAsync(msgid);
                var message = messagePage.CurrentPage.Single();
                StringBuilder commenttext = new();
                commenttext.AppendLine("<p>Hello " + name.Trim() + ",<br><br>");
                commenttext.AppendLine("I have not hard from you. Are you okay?.<br><br>");
                commenttext.AppendLine("Graph Tester<br>");
                commenttext.AppendLine("Intern<br>");
                commenttext.AppendLine("Better Computing Consulting<br>");
                commenttext.AppendLine("https://bcc.bz<br>");
                await GraphHelper.SendReplyAsync(message.Id, commenttext.ToString());
                await AddSent2emailsTableRow(name, email, msgid, sent1stdatetime, sent2nddatetime);

            }
            else
            {
                Console.WriteLine("Skipping reply email to " + email + " as it has already received two emails or is in the do not send list.");
            }
            bool success = await GraphHelper.DeleteTableRowAsync("SentEmails", row);
            if (success)
            {
                Console.WriteLine("Deleted row index: " + row.Index.ToString());
            }
            else
            {
                Console.WriteLine("Failed Deleting row index: " + row.Index.ToString());
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error sending Many Replies: {ex.Message}");
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
            Console.WriteLine("{0,-5}{1, -30}{2,-40}{3,-50}{4}", row.Index, resRow[0][0], resRow[0][1], resRow[0][2], sentdate.ToString());
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error adding table row: {ex.Message}");
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
            Console.WriteLine("{0,-3}{1, -30}{2,-40}{3,-50}{4,-25}{5}", row.Index, resRow[0][0], resRow[0][1], resRow[0][2], sent1stdate.ToString(), sent2nddate.ToString());
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error adding table row: {ex.Message}");
    }
}