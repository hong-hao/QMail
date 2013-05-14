using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ime.mail.Net.MIME;
using ime.mail.Net.IO;
using ime.mail.Net.Mail;
using ime.mail.Net.SMTP.Client;
using wos.rpc.core;
using ime.mail.Utils;
using wos.collections;
using wos.rpc;
using Newtonsoft.Json.Linq;
using wos.utils;
using System.Threading;
using System.Data.SQLite;
using System.Data;
using wos.library;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using ime.messenger;

namespace ime.mail.Worker
{
    /// <summary>
    /// 邮件发送
    /// </summary>
    public class MailSendWorker
    {
        private List<ASObject> Accounts = new List<ASObject>();//账号
        private List<ASObject> Contacts = new List<ASObject>();//联系人列表
        private Map<string, List<string>> AccountContact = new Map<string, List<string>>();//邮件账号和联系人的关系
        private static MailSendWorker _instance = null;

        private string store_path;
        private bool isProcess = false;//线程是否在运行中
        private bool isWait = false;//是否有邮件任务在等待发送
        private Map<long, List<ASObject>> PrincipalAccounts = new Map<long, List<ASObject>>();//人员账号

        public static MailSendWorker instance
        {
            get
            {
                if (_instance == null)
                    _instance = new MailSendWorker();
                return _instance;
            }
        }

        protected MailSendWorker()
        {
            this.store_path = System.IO.Path.Combine(Desktop.instance.ApplicationPath, "mail");

            InitData();
        }

        /// <summary>
        /// 初始化账号、联系人 邮件账号和联系人的关系
        /// </summary>
        private void InitData()
        {
            try
            {
                //获取账户列表
                object result = Remoting.call("MailManager.getUserMailAccounts", new object[] { });
                if (result != null)
                {
                    if (result is string)
                    {
                        JArray array = JArray.Parse(result as string);
                        object[] record = JsonUtil.toRawArray(array);
                        foreach (object o in record)
                        {
                            if (o is ASObject)
                                Accounts.Add((ASObject)o);
                        }
                    }
                }
                //获取联系人列表
                StringBuilder sb = new StringBuilder();
                sb.Append("from ML_Contact");

                result = Remoting.call("QueryService.executeEntityHQL", new object[] { sb.ToString() });
                if (result != null)
                {
                    object[] record = result as object[];
                    foreach (object o in record)
                    {
                        if (o is ASObject)
                            Contacts.Add(o as ASObject);
                    }
                }

                //获取账户和联系人的关系
                sb.Clear();
                sb.Append("select a1.account, a2.email from ML_MailEntry a0, ML_MailAccount a1, ML_Contact a2 where a1.id=a0.mail_account_id and a2.id=a0.customer_id and a1.is_enabled=1 and a1.owner_user_id=").Append(Desktop.instance.loginedPrincipal.id);

                result = Remoting.call("QueryService.executeEntityHQL", new object[] { sb.ToString() });
                if (result != null)
                {
                    object[] record = result as object[];
                    foreach (object o in record)
                    {
                        object[] _re = o as object[];
                        string key = _re[0] as string;
                        string value = _re[1] as string;
                        if (AccountContact.ContainsKey(key))
                        {
                            List<string> values = AccountContact[key];
                            if (!values.Contains(value))
                                values.Add(value);
                        }
                        else
                        {
                            List<string> values = new List<string>();
                            values.Add(value);
                            AccountContact[key] = values;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
            }
        }

        /// <summary>
        /// 重置数据
        /// </summary>
        public void Reset()
        {
            Accounts.Clear();
            Contacts.Clear();
            AccountContact.Clear();
            InitData();
        }

        /// <summary>
        /// 添加邮件到发送目录
        /// </summary>
        /// <param name="mail"></param>
        public void AddMail(object mail)
        {
            isWait = true;
        }

        /// <summary>
        /// 根据联系人查找账户
        /// </summary>
        /// <param name="contact"></param>
        /// <returns></returns>
        public List<ASObject> findAccountContact(string contact)
        {
            List<string> acc = new List<string>();
            string[] spilts = contact.Split(';');
            foreach (string s in spilts)
            {
                foreach (KeyValuePair<string, List<string>> entry in AccountContact)
                {
                    if (entry.Value.Contains(s))
                    {
                        if (!acc.Contains(entry.Key))
                            acc.Add(entry.Key);
                    }
                }
            }

            List<ASObject> accounts = new List<ASObject>();
            foreach (ASObject o in Accounts)
            {
                if (acc.Contains(o.getString("account")))
                    accounts.Add(o);
            }

            if (accounts.Count == 0)
                return Accounts;

            return accounts;
        }

        /// <summary>
        /// 根据账户查找账户所对应的对象
        /// </summary>
        /// <param name="mail_account"></param>
        /// <returns></returns>
        public ASObject findAccount(string mail_account)
        {
            foreach (ASObject o in Accounts)
            {
                if (mail_account == o.getString("account"))
                    return o;
            }
            return null;
        }

        public ASObject findPrincipalMailAccount(long principalId, string mail_account)
        {
            List<ASObject> list = null;
            if (PrincipalAccounts.ContainsKey(principalId))
            {
                list = PrincipalAccounts[principalId];
            }
            else
            {
                try
                {
                    //获取账户列表
                    object result = Remoting.call("MailManager.getPrincipalMailAccounts", new object[] { principalId });
                    if (result != null)
                    {
                        if (result is string)
                        {
                            list = new List<ASObject>();
                            JArray array = JArray.Parse(result as string);
                            object[] record = JsonUtil.toRawArray(array);
                            foreach (object o in record)
                            {
                                if (o is ASObject)
                                    list.Add((ASObject)o);
                            }

                            PrincipalAccounts.Add(principalId, list);
                        }
                    }
                }
                catch
                {
                    list = new List<ASObject>();
                }
            }

            foreach (ASObject o in list)
            {
                if (mail_account == o.getString("account"))
                    return o;
            }

            return null;
        }

        /// <summary>
        /// 获取待发邮件
        /// </summary>
        private List<ASObject> findOutBox()
        {
            try
            {
                List<ASObject> mailList = new List<ASObject>();
                StringBuilder sql = new StringBuilder();
                sql.Append("select * from ML_Mail where owner_user_id=@owner_user_id and folder=@folder and mail_type=@mail_type");

                SQLiteCommand cmd = null;
                DataSet ds = null;
                try
                {
                    cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection());
                    cmd.Parameters.AddWithValue("@owner_user_id", Desktop.instance.loginedPrincipal.id);
                    cmd.Parameters.AddWithValue("@folder", "OUTBOX");
                    cmd.Parameters.AddWithValue("@mail_type", (int)DBWorker.MailType.OutboxMail);
                    ds = new DataSet();
                    SQLiteDataAdapter q = new SQLiteDataAdapter(cmd);
                    q.Fill(ds);
                }
                finally
                {
                    if (cmd != null)
                        cmd.Dispose();
                }
                DataTable dt = ds.Tables[0];
                foreach (DataRow row in dt.Rows)
                {
                    ASObject mail = new ASObject();
                    foreach (DataColumn column in dt.Columns)
                    {
                        mail[column.ColumnName] = row[column];
                    }
                    mailList.Add(mail);
                }

                return mailList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
                return null;
            }
        }


        public void Start()
        {
            if (isProcess)
                return;
            Thread thread = new Thread(run);
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>
        /// 有定时器触发
        /// </summary>
        public void StartTime()
        {
            if (isProcess)
                return;
            Thread thread = new Thread(run_time);
            thread.IsBackground = true;
            thread.Start();
        }

        private void run()
        {
            try
            {
                isProcess = true;
                while (isWait)
                {
                    isWait = false;
                    traverse();
                }
                isProcess = false;
            }
            catch (Exception ex)
            {
                isProcess = false;
                System.Diagnostics.Debug.Write(ex.StackTrace);
            }
        }

        private void run_time()
        {
            try
            {
                isProcess = true;
                traverse();
                isProcess = false;
            }
            catch (Exception ex)
            {
                isProcess = false;
                System.Diagnostics.Debug.Write(ex.StackTrace);
            }
        }

        /// <summary>
        /// 遍历待发送邮件 执行发送
        /// </summary>
        private void traverse()
        {
            List<ASObject> mailList = findOutBox();
            if (mailList == null)
                throw new Exception("查询待发送邮件数据错误");
            foreach (ASObject mail in mailList)
            {
                try
                {
                    string contact_mail = mail["mail_account"] as string;
                    if (String.IsNullOrWhiteSpace(contact_mail))
                        continue;
                    ASObject from = findAccount(contact_mail);

                    if (from == null)
                        from = findPrincipalMailAccount(mail.getLong("operator_id"), contact_mail);

                    if (from == null)
                        continue;

                    string file = this.store_path + mail["mail_file"] as string;
                    if (!File.Exists(file))
                        continue;
                    Mail_Message mm = MailWorker.instance.ParseMail(mail["mail_file"] as string);

                    string[] to = (mail["mail_to"] as string).Trim().Split(';');
                    send(mm, from, to);

                    mail["folder"] = "SENDED";
                    mail["mail_type"] = (int)DBWorker.MailType.RecvMail;
                    mail["send_time"] = DateTimeUtil.now();
                    //发送完成后更新数据到已发送目录
                    MailWorker.instance.dispatchMailEvent(MailWorker.Event.Update, mail, new string[] { "folder", "mail_type", "send_time" });

                    if (!String.IsNullOrWhiteSpace(mail.getString("reviewer_id")) && !String.IsNullOrWhiteSpace(mail.getString("operator_id")))
                    {
                        //发送消息通知发送审核邮件的用户，邮件已经审核
                        try
                        {
                            object result = Remoting.call("MailManager.getPrincipal", new object[] { mail.getLong("operator_id") });
                            ASObject principal = result as ASObject;
                            if (principal != null)
                            {
                                StringBuilder sb = new StringBuilder();
                                sb.Append("<message type=\"MailAuditedDeliver\">")
                                    .Append("<subject>").Append("您有新的消息").Append("</subject>")
                                    .Append("<body>")
                                        .Append("<message uuid=\"").Append(mail.getString("uuid"))
                                        .Append("\" reviewer_id=\"").Append(mail.getString("reviewer_id"))
                                        .Append("\" operator_id=\"").Append(mail.getString("operator_id"))
                                        .Append("\">")
                                        .Append("您发起的审核邮件已经被").Append(mail.getString("reviewer_name")).Append("审核通过")
                                        .Append("</message>")
                                    .Append("</body>")
                                    .Append("<icon>Information</icon>")
                                .Append("</message>");
                                string targetUser = principal.getString("loginId") + "$" + principal.getString("domainName");
                                MessageManager.instance.sendMessage(sb.ToString(), targetUser);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.Write(ex.StackTrace);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                }
            }
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="mm">邮件对象</param>
        /// <param name="from">发送人</param>
        /// <param name="to">接收人</param>
        private void send(Mail_Message mm, ASObject from, string[] to)
        {
            using (MemoryStreamEx stream = new MemoryStreamEx(32000))
            {
                MIME_Encoding_EncodedWord headerwordEncoder = new MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding.Q, Encoding.UTF8);
                mm.ToStream(stream, headerwordEncoder, Encoding.UTF8);
                stream.Position = 0;

                SMTP_Client.QuickSendSmartHost(null, from.getString("send_address", "stmp.sina.com"), from.getInt("send_port", 25),
                    from.getBoolean("is_send_ssl", false), from.getString("account"), PassUtil.Decrypt(from.getString("password")),
                    from.getString("account"), to, stream);
            }
        }
    }
}
