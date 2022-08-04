using Azure.Identity;
using Microsoft.Graph;
using Newtonsoft.Json;

class GraphHelper
{
    private static Settings? _settings;

    private static ClientSecretCredential? _clientSecretCredential;

    private static GraphServiceClient? _appClient;

    public static void EnsureGraphForAppOnlyAuth(Settings settings)
    {
        _settings = settings;

        _ = _settings ?? throw new System.NullReferenceException("Settings cannot be null");

        if (_clientSecretCredential == null)
        {
            _clientSecretCredential = new ClientSecretCredential(_settings.TenantId, _settings.ClientId, _settings.ClientSecret);
        }

        if (_appClient == null)
        {
            _appClient = new GraphServiceClient(_clientSecretCredential, new[] { "https://graph.microsoft.com/.default" });
        }
    }

    public static async Task SendReportMailAsync(string subject, string body, string attachmentPath)
    {
        _ = _appClient ?? throw new System.NullReferenceException("Graph has not been initialized for app-only auth");
        _ = _settings ?? throw new System.NullReferenceException("Settings cannot be null");

        IMessageAttachmentsCollectionPage attachments = new MessageAttachmentsCollectionPage()
        {
            new FileAttachment
            {
                Name = Path.GetFileName(attachmentPath),
                ContentType = "text/plain",
                ContentBytes = System.IO.File.ReadAllBytes(attachmentPath)
            }
        };
        var message = new Message
        {
            Subject = subject,
            Body = new ItemBody
            {
                Content = body,
                ContentType = BodyType.Text
            },
            ToRecipients = new Recipient[]
            {
                new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = _settings.ADUser,
                    }
                }
            },
            Attachments = attachments
        };
        await _appClient.Users[_settings.ADUser]
            .SendMail(message)
            .Request()
            .PostAsync();
    }

    public static async Task SendMailAsync(string recipient, string inetMsgID, string subject, string body)
    {
        _ = _appClient ?? throw new System.NullReferenceException("Graph has not been initialized for app-only auth");
        _ = _settings ?? throw new System.NullReferenceException("Settings cannot be null");

        var message = new Message
        {
            Subject = subject,
            InternetMessageId = inetMsgID,
            Body = new ItemBody
            {
                Content = body,
                ContentType = BodyType.Html
            },
            ToRecipients = new Recipient[]
        {
                new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = recipient.Trim(),
                    }
                }
        }
        };
        await _appClient.Users[_settings.ADUser]
            .SendMail(message)
            .Request()
            .PostAsync();
    }

    public static async Task SendReplyAsync(string msgID, string comment)
    {
        _ = _appClient ?? throw new System.NullReferenceException("Graph has not been initialized for app-only auth");
        _ = _settings ?? throw new System.NullReferenceException("Settings cannot be null");

        await _appClient.Users[_settings.ADUser].Messages[msgID]
            .ReplyAll(null, comment)
            .Request()
            .PostAsync();
    }

    public static Task<IMailFolderMessagesCollectionPage> GetSentItemAsync(string inetmsgid)
    {
        _ = _appClient ?? throw new System.NullReferenceException("Graph has not been initialized for app-only auth");
        _ = _settings ?? throw new System.NullReferenceException("Settings cannot be null");

        return _appClient.Users[_settings.ADUser].MailFolders["sentitems"].Messages
            .Request()
            .Select(m => new { m.Id })
            .Filter("(internetMessageId eq '" + inetmsgid + "')")
            .GetAsync();
    }

    public static Task<IWorkbookTableRowsCollectionPage> GetTableRowsAsync(string tableid)
    {
        _ = _appClient ?? throw new System.NullReferenceException("Graph has not been initialized for app-only auth");
        _ = _settings ?? throw new System.NullReferenceException("Settings cannot be null");

        return _appClient.Users[_settings.ADUser].Drive.Root.ItemWithPath(_settings.DocumentPath).Workbook.Tables[tableid].Rows
            .Request()
            .Select(r => new { r.Index, r.Values })
            .GetAsync();
    }

    public static Task<WorkbookTableRow> AddTableRowAsync(string tableid, dynamic[][] values)
    {
        _ = _appClient ?? throw new System.NullReferenceException("Graph has not been initialized for app-only auth");
        _ = _settings ?? throw new System.NullReferenceException("Settings cannot be null");

        var workbookTableRow = new WorkbookTableRow
        {
            Values = System.Text.Json.JsonDocument.Parse(JsonConvert.SerializeObject(values, Formatting.Indented))
        };
        return _appClient.Users[_settings.ADUser].Drive.Root.ItemWithPath(_settings.DocumentPath).Workbook.Tables[tableid].Rows
            .Request()
            .Header("Prefer", "respond-async")
            .AddAsync(workbookTableRow);
    }

    public static async Task<bool> DeleteTableRowAsync(string tableid, WorkbookTableRow row)
    {
        _ = _appClient ?? throw new System.NullReferenceException("Graph has not been initialized for app-only auth");
        _ = _settings ?? throw new System.NullReferenceException("Settings cannot be null");

        var requestUrl = _appClient.Users[_settings.ADUser].Drive.Root.ItemWithPath(_settings.DocumentPath).Workbook.Tables[tableid].Rows.ItemAt(row.Index.GetValueOrDefault()).Request().RequestUrl;
        HttpRequestMessage hrm = new(HttpMethod.Delete, requestUrl);
        await _appClient.AuthenticationProvider.AuthenticateRequestAsync(hrm);
        HttpResponseMessage response = await _appClient.HttpProvider.SendAsync(hrm);
        return response.IsSuccessStatusCode;
    }
}