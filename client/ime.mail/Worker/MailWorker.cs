using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using ime.mail.Net;
using ime.mail.Net.Mail;
using ime.mail.Net.MIME;
using wos.collections;
using wos.library;
using wos.rpc;
using wos.rpc.core;
using wos.utils;
using ime.mail.Net.IO;
using ime.mail.Net.SMTP.Client;
using ime.mail.Utils;

namespace ime.mail.Worker
{
	public class MailWorker : IRemotingHandler
	{
		public enum Event
		{
			Create,
			Update,
			Delete,
            Reset,
            MoveFolder,
            ClearSearch
		}
		public delegate void MailEventHandler(Event eventType, ASObject mail, string[] updateFields);
		public event MailEventHandler MailEvent;

		private static MailWorker _instance;
        private ASObject _blacks = null;//黑名单
        private string upload_mail_message = "pages/ime.mail/upload_mail_message.jsp";

        private Regex rgCharset = new Regex("(?<=<meta(.*)charset=)([^\"'>]*)", RegexOptions.IgnoreCase);

		public static MailWorker instance
		{
			get
			{
				if (_instance == null)
					_instance = new MailWorker();
				return _instance;
			}
		}

        /// <summary>
        /// 黑名单
        /// </summary>
        public ASObject Blacks
        {
            set { _blacks = value; }
            get { return _blacks; }
        }

		/// <summary>
		/// 保存邮件并同步邮件到服务器，采用异步方式，但必须保证执行顺序
		/// </summary>
		/// <param name="mail">邮件记录</param>
		/// <param name="fields">保存或同步的字段</param>
		public void updateMail(ASObject mail, string[] updateFields)
		{
			// 保存邮件到本地并同步到服务器
            try
            {
                if (mail == null || updateFields == null)
                    return;
                updateMailRecord(mail, updateFields);

                syncUserMail(mail);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
		}

		/// <summary>
		/// 触发邮件事件
		/// </summary>
		/// <param name="eventType">事件类型</param>
		/// <param name="mail">邮件记录</param>
		/// <param name="updateFields">更新的字段</param>
		public void dispatchMailEvent(Event eventType, ASObject mail, string[] updateFields)
		{
			if (MailEvent != null)
				MailEvent(eventType, mail, updateFields);
		}

        public bool MoveFolder(ASObject mail, string toFolder)
        {
            string folder = mail.getString("folder");
            if (toFolder == folder)
                return false;
            if (folder == "DRAFT" || folder == "OUTBOX" || folder == "DSBOX" || folder == "SENDED")
                return false;

            mail["folder"] = toFolder;

            updateMail(mail, new string[] { "folder" });

            return true;
        }

        public bool BlackList(ASObject mail, string type)
        {
            bool IsChange = false;
            string folder = mail.getString("folder");
            if (folder == "INBOX")
                IsChange = true;
            if (folder == "SPAM")
                IsChange = true;
            if (!IsChange)
                return false;

            string toFolder = null;
            if (type == "email_b")
            {
                Remoting.call("MailManager.addBlackList", new object[] { mail.getString("contact_mail"), null, mail.getString("mail_from_label") });
                toFolder = "SPAM";
            }
            else if (type == "email_w")
            {
                Remoting.call("MailManager.removeBlackList", new object[] { mail.getString("contact_mail"), null });
                toFolder = "INBOX";
            }
            else if (type == "domain_b")
            {
                string contact_mail = mail.getString("contact_mail");
                string[] spilts = contact_mail.Split('@');
                if (spilts.Length != 2)
                    return false;
                Remoting.call("MailManager.addBlackList", new object[] { null, spilts[1], mail.getString("mail_from_label") });
                toFolder = "SPAM";
            }
            else if (type == "domain_w")
            {
                string contact_mail = mail.getString("contact_mail");
                string[] spilts = contact_mail.Split('@');
                if (spilts.Length != 2)
                    return false;
                Remoting.call("MailManager.removeBlackList", new object[] { null, spilts[1] });
                toFolder = "INBOX";
            }

            if (folder == toFolder)
                return false;

            mail["folder"] = toFolder;

            updateMail(mail, new string[] { "folder" });

            return true;
        }

        public Mail_Message ParseMail(string mail_file)
        {
            string store_path = Desktop.instance.ApplicationPath + "/mail/";
            using (FileStream fs = new FileStream(store_path + mail_file, FileMode.Open))
            {
                Mail_Message m = Mail_Message.ParseFromStream(fs, Encoding.GetEncoding("GBK"));
                string charset = null;
                if (m.ContentType != null && m.ContentType.Param_Charset != null)
                    charset = m.ContentType.Param_Charset;
                if (charset != null && String.Compare(charset, "GBK", true) != 0 && String.Compare(charset, "gb2312", true) != 0)
                {
                    fs.Position = 0;
                    m = Mail_Message.ParseFromStream(fs, Encoding.GetEncoding(charset));
                }

                return m;
            }
        }

        public void saveMailRecord(ASObject record, string entityName = "ML_Mail")
        {
            string sql = @"INSERT INTO " + entityName + @" (
								uuid,
                                owner_user_id, 
                                message_id,
								subject,
								sender,
								mail_to,
								reply_to,
								mail_from,
								contact_mail,
								flags,
								cc,
								bcc,
								attachments,
								contents,
								text_body,
								html_body,
								create_time,
								send_time,
								mail_date,
								mail_type,
								mail_account,
								mail_file,
								reply_for,
								reply_header,
								folder,
								mail_uid,
								client_or_server,
								is_synced,
								mail_from_label,
								mail_to_label,
								priority,
                                is_seen,
								ip_from,
                                reviewer_id,
                                reviewer_name,
                                operator_id,
                                operator_name
							) 
							VALUES (
								@uuid,
                                @owner_user_id,
                                @message_id,
								@subject,
								@sender,
								@mail_to,
								@reply_to,
								@mail_from,
								@contact_mail,
								@flags,
								@cc,
								@bcc,
								@attachments,
								@contents,
								@text_body,
								@html_body,
								@create_time,
								@send_time,
								@mail_date,
								@mail_type,
								@mail_account,
								@mail_file,
								@reply_for,
								@reply_header,
								@folder,
								@mail_uid,
								@client_or_server,
								@is_synced,
								@mail_from_label,
								@mail_to_label,
								@priority,
                                @is_seen,
								@ip_from,
                                @reviewer_id,
                                @reviewer_name,
                                @operator_id,
                                @operator_name
						    )";
            SQLiteCommand cmd = null;
            try
            {
                cmd = new SQLiteCommand(sql, DBWorker.GetConnection());

                cmd.Parameters.AddWithValue("@uuid", record["uuid"]);
                cmd.Parameters.AddWithValue("@owner_user_id", record["owner_user_id"]);
                cmd.Parameters.AddWithValue("@message_id", record["message_id"]);
                cmd.Parameters.AddWithValue("@subject", record["subject"]);
                cmd.Parameters.AddWithValue("@sender", record["sender"]);
                cmd.Parameters.AddWithValue("@mail_to", record["mail_to"]);
                cmd.Parameters.AddWithValue("@mail_to_label", record["mail_to_label"]);
                cmd.Parameters.AddWithValue("@reply_to", record["reply_to"]);
                cmd.Parameters.AddWithValue("@mail_from", record["mail_from"]);
                cmd.Parameters.AddWithValue("@mail_from_label", record["mail_from_label"]);
                cmd.Parameters.AddWithValue("@contact_mail", record["contact_mail"]);
                cmd.Parameters.AddWithValue("@flags", record["flags"]);
                cmd.Parameters.AddWithValue("@cc", record["cc"]);
                cmd.Parameters.AddWithValue("@bcc", record["bcc"]);
                cmd.Parameters.AddWithValue("@attachments", record["attachments"]);
                cmd.Parameters.AddWithValue("@contents", record["contents"]);
                cmd.Parameters.AddWithValue("@text_body", record["text_body"]);
                cmd.Parameters.AddWithValue("@html_body", record["html_body"]);
                cmd.Parameters.AddWithValue("@create_time", record["create_time"]);
                cmd.Parameters.AddWithValue("@mail_date", record["mail_date"]);
                cmd.Parameters.AddWithValue("@send_time", record["send_time"]);
                cmd.Parameters.AddWithValue("@mail_type", record["mail_type"]);
                cmd.Parameters.AddWithValue("@mail_account", record["mail_account"]);
                cmd.Parameters.AddWithValue("@mail_file", record["mail_file"]);
                cmd.Parameters.AddWithValue("@reply_for", record["reply_for"]);
                cmd.Parameters.AddWithValue("@reply_header", record["reply_header"]);
                cmd.Parameters.AddWithValue("@folder", record["folder"]);
                cmd.Parameters.AddWithValue("@mail_uid", record["mail_uid"]);
                cmd.Parameters.AddWithValue("@client_or_server", record["client_or_server"]);
                cmd.Parameters.AddWithValue("@is_synced", record["is_synced"]);
                cmd.Parameters.AddWithValue("@is_seen", record["is_seen"]);
                cmd.Parameters.AddWithValue("@ip_from", record["ip_from"]);
                cmd.Parameters.AddWithValue("@priority", record["priority"]);
                cmd.Parameters.AddWithValue("@reviewer_id", record["reviewer_id"]);
                cmd.Parameters.AddWithValue("@reviewer_name", record["reviewer_name"]);
                cmd.Parameters.AddWithValue("@operator_id", record["operator_id"]);
                cmd.Parameters.AddWithValue("@operator_name", record["operator_name"]);

                cmd.ExecuteNonQuery();

                cmd.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
            }
            finally
            {
                if (cmd != null)
                {
                    cmd.Dispose();
                }
            }
        }

        /// <summary>
        /// 更新邮件记录
        /// </summary>
        /// <param name="mail"></param>
        /// <param name="updateFields"></param>
        public void updateMailRecord(ASObject mail, string[] updateFields)
        {
            if (mail == null || updateFields == null || updateFields.Length == 0)
                return;
            string sql = @"update ML_Mail set ";
            StringBuilder sb = new StringBuilder();
            sb.Append(sql);
            foreach (string field in updateFields)
            {
                sb.Append(field).Append("=@").Append(field).Append(",");
            }
            if (sb.ToString().LastIndexOf(",") != -1)
                sb.Remove(sb.ToString().LastIndexOf(","), 1);
            if(mail.ContainsKey("id"))
                sb.Append(" where id=@id");
            else
                sb.Append(" where uuid=@uuid");

            SQLiteCommand cmd = null;
            try
            {
                cmd = new SQLiteCommand(sb.ToString(), DBWorker.GetConnection());
                if (mail.ContainsKey("id"))
                    cmd.Parameters.AddWithValue("@id", mail["id"]);
                else
                    cmd.Parameters.AddWithValue("@uuid", mail["uuid"]);
                foreach (string field in updateFields)
                {
                    cmd.Parameters.AddWithValue("@" + field, mail[field]);
                }

                cmd.ExecuteNonQuery();

                cmd.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
            }
            finally
            {
                if (cmd != null)
                {
                    cmd.Dispose();
                }
            }
        }

        public void onRemotingCallback(string callUID, string methodName, object result, AsyncOption option)
        {
            switch (methodName)
            {
                case "MailManager.syncUserMail":
                    {
                        int is_synced_value = -1;
                        ASObject mail = option.asyncData as ASObject;
                        int is_synced = NumberUtil.parseInt(mail["is_synced"] == null ? "0" : mail["is_synced"].ToString());
                        if (is_synced == 0)
                        {
                            is_synced = 1;
                            is_synced_value = 1;
                        }
                        if (is_synced == 1)
                        {
                            //同步文件
                            string mail_file = mail["mail_file"] as string;
                            string store_path = System.IO.Path.Combine(new string[] { Desktop.instance.ApplicationPath, "mail" });
                            string mail_file_path = store_path + mail_file;
                            if (File.Exists(mail_file_path))
                            {
                                string uuid = mail["uuid"] as string;
                                Dictionary<string, string> param = new Dictionary<string, string>();
                                param["uuid"] = MailReceiveWorker.getFilePath(uuid);
                                using (var client = new CookieAwareWebClient())
                                {
                                    client.Param = param;
                                    string uri = Desktop.getAbsoluteUrl(upload_mail_message);
                                    mail_file_path = mail_file_path.Replace("/", "\\");
                                    client.UploadFileAsync(new Uri(uri), mail_file_path);
                                }
                                is_synced_value = 2;
                            }
                        }
                        if (is_synced_value != -1)
                        {
                            mail["is_synced"] = is_synced_value;
                            updateMailRecord(mail, new string[] { "is_synced" });
                        }
                    }
                    break;
            }
        }

        public void onRemotingException(string callUID, string methodName, string message, string code, ASObject exception, AsyncOption option)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }

        /// <summary>
        /// 与服务器同步邮件信息
        /// </summary>
        /// <param name="record"></param>
        public void syncUserMail(ASObject record)
        {
            if (record == null)
                return;
            AsyncOption option = new AsyncOption("MailManager.syncUserMail");
            option.asyncData = record;
            option.showWaitingBox = false;
            Remoting.call("MailManager.syncUserMail", new object[] { record }, this, option);
        }

        /// <summary>
        /// val是否在黑名单中存在
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        public bool IsExistBlack(string val)
        {
            if (_blacks == null || String.IsNullOrWhiteSpace(val))
                return false;
            if (_blacks.ContainsKey("email"))
            {
                object[] result = _blacks["email"] as object[];
                if (result == null)
                    return false;

                foreach (object value in result)
                {
                    string _val = value as string;
                    if (!String.IsNullOrWhiteSpace(_val))
                    {
                        if (val == _val)
                            return true;
                    }
                }
            }
            
            if (_blacks.ContainsKey("email_domain"))
            {
                object[] result = _blacks["email_domain"] as object[];
                if (result == null)
                    return false;

                foreach (object value in result)
                {
                    string _val = value as string;
                    if (!String.IsNullOrWhiteSpace(_val))
                    {
                        string[] splits = _val.Split('@');
                        if (splits.Length == 2)
                        {
                            if (val == splits[1])
                                return true;
                        }
                        else if (val == _val)
                            return true;
                    }
                }
            }

            return false;
        }

        public ASObject MapToASObject(Map<string, object> map)
        {
            ASObject aso = new ASObject();
            foreach (KeyValuePair<string, object> entry in map)
            {
                aso[entry.Key] = entry.Value;
            }

            return aso;
        }

        public class CookieAwareWebClient : WebClient
        {
            private Dictionary<string, string> param = null;

            public Dictionary<string, string> Param
            {
                set { param = value; }
            }

            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = null;
                StringBuilder sb = new StringBuilder();
                if (param != null)
                {
                    int i = 0;
                    sb.Append(address.ToString());
                    foreach (KeyValuePair<string, string> item in param)
                    {
                        if (i == 0)
                        {
                            sb.Append("?");
                            sb.Append(item.Key).Append("=").Append(HttpUtility.UrlEncode(item.Value, Encoding.UTF8));
                        }
                        else
                        {
                            sb.Append("&").Append(item.Key).Append("=").Append(HttpUtility.UrlEncode(item.Value, Encoding.UTF8));
                        }
                        i++;
                    }
                }
                if (sb.Length > 0)
                    request = base.GetWebRequest(new Uri(sb.ToString()));
                else
                    request = base.GetWebRequest(address);
                HttpWebRequest req = request as HttpWebRequest;
                if (req != null)
                {
                    req.CookieContainer = wos.rpc.Remoting.CookieContainer;
                }
                return request;
            }
        }

        private string getHtmlEncoding(string html)
        {
            try
            {
                string html_charset = null;

                wos.utils.Util.WithTimeout(() =>
                {
                    //解析html中的charset
                    Match match = null;
                    if (html.Length > 1000)
                        match = rgCharset.Match(html.Substring(0, 1000));
                    else
                        match = rgCharset.Match(html);
                    while (match.Success)
                    {
                        if (match.Groups.Count >= 3)
                        {
                            html_charset = match.Groups[2].Value;
                            if (!String.IsNullOrEmpty(html_charset))
                                break;
                        }
                        match = match.NextMatch();
                    }
                }, 300);
                if (html_charset == null && html.Length > 1000)
                {
                    wos.utils.Util.WithTimeout(() =>
                    {
                        //解析html中的charset
                        Match match = rgCharset.Match(html);
                        while (match.Success)
                        {
                            if (match.Groups.Count >= 3)
                            {
                                html_charset = match.Groups[2].Value;
                                if (!String.IsNullOrEmpty(html_charset))
                                    break;
                            }
                            match = match.NextMatch();
                        }
                    }, 500);
                }
                return html_charset;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void ParseMail(ASObject mailRecord)
        {
            string store_path = Desktop.instance.ApplicationPath + "/mail/";
            string file = mailRecord["mail_file"] as string;
            DirectoryInfo dirinfo = Directory.GetParent(store_path + file);
            if (!dirinfo.Exists)
                dirinfo.Create();

            if (!File.Exists(store_path + file))
            {
                using (WebClient w = new WebClient())
                {
                    w.DownloadFile(Desktop.getAbsoluteUrl("docroot/attachments" + file), store_path + file);
                }
            }

            string uid = mailRecord["uuid"] as string;
            dirinfo = Directory.GetParent(store_path + file);
            string dir = dirinfo.FullName + "/" + uid + ".parts";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (FileStream fs = new FileStream(store_path + file, FileMode.Open))
            {
                using (Mail_Message m = Mail_Message.ParseFromStream(fs, Encoding.GetEncoding("GBK")))
                {
                    string charset = null;
                    if (m.ContentType != null && m.ContentType.Param_Charset != null)
                        charset = m.ContentType.Param_Charset;
                    if (charset != null && String.Compare(charset, "GBK", true) != 0 && String.Compare(charset, "gb2312", true) != 0)
                    {
                        fs.Position = 0;
                        using (Mail_Message _m = Mail_Message.ParseFromStream(fs, Encoding.GetEncoding(charset)))
                        {
                            parseMIMEContent(_m, uid, dir, mailRecord);
                        }
                    }
                    else
                        parseMIMEContent(m, uid, dir, mailRecord);
                }
            }
        }

        private void parseMIMEContent(Mail_Message m, string uid, string dir, ASObject record)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement xml = doc.CreateElement("message");
            doc.AppendChild(xml);

            StringBuilder attachments = new StringBuilder();
            MIME_Entity[] entities = m.GetAllEntities(true);

            Map<string, string> content_id_file = new Map<string, string>();
            StringBuilder textHtml = new StringBuilder();
            textHtml.Append(@"<html><head><meta http-equiv=""content-type"" content=""text/html; charset=utf-8""></head><body>");
            bool hasText = false;

            foreach (MIME_Entity e in entities)
            {
                try
                {
                    if (e.Body.MediaType.ToLower() == MIME_MediaTypes.Text.html)
                        continue;
                    else if (e.Body.MediaType.ToLower() == MIME_MediaTypes.Text.plain)
                        continue;
                    else if (e.Body is MIME_b_SinglepartBase)
                    {
                        MIME_b_SinglepartBase p = (MIME_b_SinglepartBase)e.Body;
                        Stream data = p.GetDataStream();
                        string fPath = "";
                        string fileName = e.ContentType.Param_Name;
                        if (fileName == null)
                            fileName = Guid.NewGuid().ToString();
                        else
                            attachments.Append(fileName).Append(";");
                        fileName = fileName.Replace(' ', '_');
                        fPath = System.IO.Path.Combine(dir, fileName);
                        using (FileStream afs = File.Create(fPath))
                        {
                            Net_Utils.StreamCopy(data, afs, 4096);
                        }
                        data.Close();

                        string contentId = e.ContentID;
                        if (!String.IsNullOrEmpty(contentId))
                        {
                            contentId = contentId.Trim();
                            if (contentId.StartsWith("<"))
                                contentId = contentId.Substring(1);
                            if (contentId.EndsWith(">"))
                                contentId = contentId.Substring(0, contentId.Length - 1);
                            content_id_file.Add(contentId, fileName);
                        }

                        XmlElement part = doc.CreateElement("PART");
                        part.SetAttribute("type", "file");
                        part.SetAttribute("content-id", contentId);
                        part.SetAttribute("filename", fileName);
                        part.SetAttribute("description", e.ContentDescription);
                        if (e.ContentType != null)
                            part.SetAttribute("content-type", e.ContentType.ToString());
                        xml.AppendChild(part);
                    }
                }
                catch (Exception)
                {
                }
            }
            foreach (MIME_Entity e in entities)
            {
                try
                {
                    if (e.Body.MediaType.ToLower() == MIME_MediaTypes.Text.html)
                    {
                        string html = ((MIME_b_Text)e.Body).Text;

                        //处理html中的内嵌图片
                        if (content_id_file.Count > 0)
                        {
                            foreach (string key in content_id_file.Keys)
                            {
                                html = html.Replace("cid:" + key, content_id_file[key]);
                            }
                        }

                        XmlElement part = doc.CreateElement("PART");
                        part.SetAttribute("type", "html");
                        part.AppendChild(doc.CreateCDataSection(html));
                        xml.AppendChild(part);

                        string charset = "GBK";
                        if (e.ContentType != null && e.ContentType.Param_Charset != null)
                            charset = e.ContentType.Param_Charset;
                        else if (m.ContentType != null && m.ContentType.Param_Charset != null)
                            charset = m.ContentType.Param_Charset;

                        string html_charset = getHtmlEncoding(html);
                        if (html_charset == null)
                        {
                            int index = html.IndexOf("<head>", StringComparison.CurrentCultureIgnoreCase);
                            if (index != -1)
                            {
                                StringBuilder sb = new StringBuilder();
                                index = index + "<head>".Length;
                                sb.Append(html.Substring(0, index));
                                sb.Append(@"<meta http-equiv=""content-type"" content=""text/html; charset=").Append(charset).Append(@""">");
                                sb.Append(html.Substring(index));
                                html = sb.ToString();
                            }
                            else
                            {
                                StringBuilder sb = new StringBuilder();
                                sb.Append(@"<html><head><meta http-equiv=""content-type"" content=""text/html; charset=").Append(charset).Append(@""">");
                                sb.Append("</head><body>");
                                sb.Append(html);
                                sb.Append("</body></html>");
                                html = sb.ToString();
                            }
                            html_charset = charset;
                        }

                        Encoding encoding = null;
                        try
                        {
                            encoding = Encoding.GetEncoding(html_charset);
                        }
                        catch (Exception)
                        {
                        }
                        if (encoding == null)
                        {
                            try
                            {
                                encoding = Encoding.GetEncoding(charset);
                            }
                            catch (Exception)
                            {
                            }
                        }
                        if (encoding == null)
                            encoding = Encoding.UTF8;
                        StreamWriter hfs = new StreamWriter(dir + "/" + uid + ".html", false, encoding);
                        hfs.Write(html);
                        hfs.Close();
                    }
                    else if (e.Body.MediaType.ToLower() == MIME_MediaTypes.Text.plain)
                    {
                        XmlElement part = doc.CreateElement("PART");
                        part.SetAttribute("type", "text");
                        string text = ((MIME_b_Text)e.Body).Text;

                        part.AppendChild(doc.CreateCDataSection(text));
                        xml.AppendChild(part);

                        if (hasText)
                            textHtml.Append("<hr/>");
                        hasText = true;
                        text = text.Replace(" ", "&nbsp;");
                        text = text.Replace("\n", "<br/>");
                        textHtml.Append(text);
                    }
                }
                catch (Exception)
                {
                }
            }

            textHtml.Append("</body></html>");
            if (hasText)
            {
                try
                {
                    using (StreamWriter hfs = new StreamWriter(dir + "/" + uid + ".text.html", false, Encoding.UTF8))
                    {
                        hfs.Write(textHtml.ToString());
                    }
                }
                catch (Exception)
                {
                }
            }

            record["attachments"] = attachments.ToString();
            record["contents"] = doc.OuterXml;
        }

        /// <summary>
        /// 获取国家代码
        /// </summary>
        /// <param name="country"></param>
        /// <returns></returns>
        public string getCountryCode(string country)
        {
            try
            {
                ArrayList array = DictionaryLoader.getValueList("区域.国家代码");
                if (array == null || array.Count == 0)
                    return null;
                foreach (object code in array)
                {
                    if (code is DictionaryItem)
                    {
                        DictionaryItem item = code as DictionaryItem;
                        if (item.label is string && item.label.ToString().IndexOf(country) != -1)
                            return item.value as string;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
                return null;
            }
        }

        /// <summary>
        /// 发送阅读回折邮件
        /// </summary>
        /// <param name="sHtmlText">邮件内容</param>
        /// <param name="from">发送人</param>
        /// <param name="to">接收人</param>
        public void sendReceiptMail(string sHtmlText, string subject, ASObject from, string[] to)
        {
            using (MemoryStreamEx stream = new MemoryStreamEx(32000))
            {
                Mail_Message m = new Mail_Message();
                m.MimeVersion = "1.0";
                m.Date = DateTime.Now;
                m.MessageID = MIME_Utils.CreateMessageID();

                m.Subject = subject;
                StringBuilder sb = new StringBuilder();
                foreach (string p in to)
                {
                    if (sb.Length > 0)
                        sb.Append(",");
                    sb.Append(p);
                }
                m.To = Mail_t_AddressList.Parse(sb.ToString());

                //--- multipart/alternative -----------------------------------------------------------------------------------------
                MIME_h_ContentType contentType_multipartAlternative = new MIME_h_ContentType(MIME_MediaTypes.Multipart.alternative);
                contentType_multipartAlternative.Param_Boundary = Guid.NewGuid().ToString().Replace('-', '.');
                MIME_b_MultipartAlternative multipartAlternative = new MIME_b_MultipartAlternative(contentType_multipartAlternative);
                m.Body = multipartAlternative;

                //--- text/plain ----------------------------------------------------------------------------------------------------
                MIME_Entity entity_text_plain = new MIME_Entity();
                MIME_b_Text text_plain = new MIME_b_Text(MIME_MediaTypes.Text.plain);
                entity_text_plain.Body = text_plain;
                text_plain.SetText(MIME_TransferEncodings.QuotedPrintable, Encoding.UTF8, sHtmlText);
                multipartAlternative.BodyParts.Add(entity_text_plain);

                //--- text/html ------------------------------------------------------------------------------------------------------
                MIME_Entity entity_text_html = new MIME_Entity();
                MIME_b_Text text_html = new MIME_b_Text(MIME_MediaTypes.Text.html);
                entity_text_html.Body = text_html;
                text_html.SetText(MIME_TransferEncodings.QuotedPrintable, Encoding.UTF8, sHtmlText);
                multipartAlternative.BodyParts.Add(entity_text_html);

                MIME_Encoding_EncodedWord headerwordEncoder = new MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding.Q, Encoding.UTF8);
                m.ToStream(stream, headerwordEncoder, Encoding.UTF8);
                stream.Position = 0;

                SMTP_Client.QuickSendSmartHost(null, from.getString("send_address", "stmp.sina.com"), from.getInt("send_port", 25),
                    from.getBoolean("is_send_ssl", false), from.getString("account"), PassUtil.Decrypt(from.getString("password")),
                    from.getString("account"), to, stream);
            }
        }

        /// <summary>
        /// 默认时间差间隔
        /// </summary>
        public static long interval = 5000000;
    }
}
