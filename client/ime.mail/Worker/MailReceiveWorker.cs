using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;
using ime.mail.Net;
using ime.mail.Net.IMAP;
using ime.mail.Net.IMAP.Client;
using ime.mail.Net.Mail;
using ime.mail.Net.POP3.Client;
using ime.mail.Utils;
using ime.messenger;
using ime.notification;
using Newtonsoft.Json.Linq;
using wos.extensions;
using wos.library;
using wos.rpc;
using wos.rpc.core;
using wos.utils;
using System.Diagnostics;

namespace ime.mail.Worker
{
    public class MailReceiveWorker : wos.rpc.IRemotingHandler
    {
        private MailAccount mailAccount;
        private List<string> uids;
        private DateTime uidBeginTime;
        private IWorkInfo workInfo;
        private string store_path;
        private int imap_total = 0, imap_progress = 0;
        private List<IMAP_r_u_Fetch> imap_recv_messages;
        private IMAP_r_u_Fetch current_imap_fetch;
        private bool hasError = false;
        private Thread thread;

        private List<ASObject> recvs = new List<ASObject>();
        private List<ASObject> joinList = new List<ASObject>();
        private List<string> pubIds = new List<string>();
        private bool is_handled = false;
        private List<ASObject> initImports = null;
        private string SUBSCRIBE = "subscribe";
        private Thread acceptThread;
        private System.Timers.Timer interval = null;
        private System.Timers.Timer stopInterval = null;
        private int DEFAULT_DELAY = 10 * 1000;
        private bool isStop = false;

        public MailReceiveWorker(DateTime uidBeginTime, List<ASObject> initImports = null)
        {
            this.initImports = initImports;
            if (initImports != null)
                is_handled = true;
            this.uidBeginTime = uidBeginTime;
            this.store_path = Desktop.instance.ApplicationPath + "/mail/";

            NotificationCenter.Instance.AddMessageListener(SUBSCRIBE, onSubscribe);
        }

        public void Start(IWorkInfo workInfo)
        {
            this.workInfo = workInfo;

            thread = new Thread(run);
            thread.Name = Guid.NewGuid().ToString();
            thread.IsBackground = true;
            thread.Start();
        }

        public void Stop()
        {
            isStop = true;
            try
            {
                if (thread != null)
                    thread.Abort();
            }
            catch (Exception ex)
            {
                Debug.Write(ex.StackTrace);
            }

            if (stopInterval != null)
            {
                stopInterval.Elapsed -= onStopInterval_Elapsed;
                stopInterval.Stop();
            }

            if (interval != null)
            {
                interval.Elapsed -= onInterval_Elapsed;
                interval.Stop();
            }

            NotificationCenter.Instance.RemoveMessageListener(SUBSCRIBE, onSubscribe);

            workInfo.CloseWindow();
        }

        private void run()
        {
            using (SQLiteCommand cmd = new SQLiteCommand("delete from ML_Mail_Temp", DBWorker.GetConnection()))
            {
                cmd.ExecuteNonQuery();
            }
            if (initImports != null)
            {
                if (initImports.Count == 0)
                {
                    workInfo.CloseWindow();
                    return;
                }
                execute(initImports);
            }
            else
            {
                syncMailBoxView();
            }
        }

        private void syncMailBoxView()
        {
            //同步邮件视图
            Remoting.call("MailManager.getMailBoxView", new object[] { }, this);
            //同步移除邮件
            Remoting.call("MailManager.getMoveMailBoxView", new object[] { Desktop.instance.loginedPrincipal.id }, this);
        }

        private void syncMailServer()
        {
            try
            {
                //访问MailManager.getBlackList() 获取黑名单列表
                object result = Remoting.call("MailManager.getBlackList", new object[] { });
                MailWorker.instance.Blacks = result as ASObject;
            }
            catch (Exception ex)
            {
                Debug.Write(ex.StackTrace);
            }

            //访问MailManager.getUserMailAccounts() 获取接受邮件的账户列表
            AsyncOption option = new AsyncOption("MailManager.getUserMailAccounts");
            option.showWaitingBox = false;
            Remoting.call("MailManager.getUserMailAccounts", new object[] { }, this, option);
        }

        private void onSubscribe(string sender, ime.notification.NotifyMessage e, NotificationCenter.Stage stage)
        {
            if (isJoinAccept)
                return;
            if (stage == NotificationCenter.Stage.Receiving)
            {
                XElement xml = e.Body as XElement;
                if (xml == null)
                    return;
                XElement subscribe = xml.Element("subscribe");
                if (subscribe == null)
                    return;

                if (subscribe.AttributeValue("principal") == Desktop.instance.loginedPrincipal.loginId)
                    return;

                string type = subscribe.AttributeValue("type");
                if (type == "accept_mail")
                {
                    workInfo.SetInfo(subscribe.AttributeValue("principal_name") + "邮件账户" + subscribe.AttributeValue("account") + subscribe.AttributeValue("subject"));
                    workInfo.SetProgress(NumberUtil.parseInt(subscribe.AttributeValue("total")), NumberUtil.parseInt(subscribe.AttributeValue("count")));
                    if (subscribe.AttributeValue("detail") == "true")
                        workInfo.AddDetail(subscribe.AttributeValue("principal_name") + "邮件账户" + subscribe.AttributeValue("subject"), Colors.Black);
                }
                else if (type == "accept_mail_stram")
                {
                    workInfo.SetItemProgress(NumberUtil.parseInt(subscribe.AttributeValue("total")), NumberUtil.parseInt(subscribe.AttributeValue("count")));
                }
            }
        }

        public void onRemotingCallback(string callUID, string methodName, object result, AsyncOption option)
        {
            switch (methodName)
            {
                case "MailManager.getMailBoxView":
                    {
                        ASObject record = result as ASObject;
                        if (record == null || record.Count == 0)
                        {
                            syncMailServer();
                            return;
                        }

                        List<string> uids = new List<string>();
                        using (DataSet ds = new DataSet())
                        {
                            string query = "select mail_uid from ML_Mail where owner_user_id=@owner_user_id";
                            using (SQLiteCommand cmd = new SQLiteCommand(query, DBWorker.GetConnection()))
                            {
                                cmd.Parameters.AddWithValue("@owner_user_id", Desktop.instance.loginedPrincipal.id);
                                using (SQLiteDataAdapter q = new SQLiteDataAdapter(cmd))
                                {
                                    q.Fill(ds);
                                }
                            }

                            if (ds.Tables.Count > 0)
                            {
                                using (DataTable dt = ds.Tables[0])
                                {
                                    foreach (DataRow row in dt.Rows)
                                    {
                                        if (String.IsNullOrWhiteSpace(row[0] as string))
                                            continue;
                                        uids.Add(row[0].ToString());
                                    }
                                }
                            }
                        }
                        List<ASObject> createList = new List<ASObject>();
                        foreach (KeyValuePair<string, object> val in record)
                        {
                            if (uids.Contains(val.Key))
                                continue;
                            createList.Add(val.Value as ASObject);
                        }
                        if (createList.Count > 0)
                        {
                            workInfo.IsNewMail = true;
                            Thread t = new Thread(() =>
                            {
                                AutoResetEvent reset = new AutoResetEvent(false);
                                int total = createList.Count;
                                int progress = 0;
                                try
                                {
                                    workInfo.SetInfo("正在从服务器获取邮件视图。。。");
                                    foreach (ASObject mail in createList)
                                    {
                                        workInfo.SetProgress(total, progress++);
                                        workInfo.AddDetail("从服务器同步邮件-" + mail.getString("subject"), Colors.Black);
                                        MailWorker.instance.dispatchMailEvent(MailWorker.Event.Create, mail, null);

                                        reset.WaitOne(200);
                                    }
                                }
                                catch (Exception e)
                                {
                                    System.Diagnostics.Debug.WriteLine(e.Message);
                                }
                                reset.Set();
                                syncMailServer();
                            });
                            t.IsBackground = true;
                            t.Start();
                        }else
                            syncMailServer();
                    }
                    break;
                case "MailManager.getUserMailAccounts":
                    {
                        if (result == null || !(result is string))
                        {
                            workInfo.CloseWindow();
                            return;
                        }

                        recvs.Clear();

                        JArray array = JArray.Parse(result as string);
                        object[] record = JsonUtil.toRawArray(array);
                        foreach (object o in record)
                        {
                            recvs.Add(o as ASObject);
                        }
                        if (recvs.Count > 0)
                        {
                            interval = new System.Timers.Timer(DEFAULT_DELAY);
                            interval.AutoReset = true;
                            interval.Elapsed += onInterval_Elapsed;
                            interval.Start();

                            stopInterval = new System.Timers.Timer(2000);
                            stopInterval.AutoReset = true;
                            stopInterval.Elapsed += onStopInterval_Elapsed;
                            stopInterval.Start();

                            execute(recvs);
                        }
                        else
                            workInfo.CloseWindow();
                    }
                    break;
                case "MailManager.getMoveMailBoxView":
                    {
                        if (result == null || (result as object[]) == null)
                            return;

                        object[] record = result as object[];
                        if (record.Length == 0)
                            return;

                        StringBuilder sb = new StringBuilder();
                        foreach (string s in record)
                        {
                            sb.Append("'").Append(s).Append("',");
                        }
                        sb.Remove(sb.Length - 1, 1);
                        //删除被移动的邮件在本地的文件
                        List<ASObject> files = new List<ASObject>();
                        string sql = "select mail_uid, mail_file from ML_Mail where mail_uid in (" + sb.ToString() + ")";
                        using (DataSet ds = new DataSet())
                        {
                            try
                            {
                                using (SQLiteCommand cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection()))
                                {
                                    using (SQLiteDataAdapter q = new SQLiteDataAdapter(cmd))
                                    {
                                        q.Fill(ds);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.Write(ex.StackTrace);
                            }
                            if (ds.Tables.Count > 0)
                            {
                                using (DataTable dt = ds.Tables[0])
                                {
                                    foreach (DataRow row in dt.Rows)
                                    {
                                        ASObject mail = new ASObject();
                                        foreach (DataColumn column in dt.Columns)
                                        {
                                            mail[column.ColumnName] = row[column];
                                        }
                                        files.Add(mail);
                                    }
                                }
                            }
                        }

                        if (files.Count > 0)
                        {
                            foreach (ASObject file in files)
                            {
                                if (File.Exists(Path.Combine(store_path, file.getString("mail_file"))))
                                {
                                    File.Delete(Path.Combine(store_path, file.getString("mail_file")));
                                    if (Directory.Exists(Path.Combine(store_path, file.getString("mail_uid"))))
                                    {
                                        Directory.Delete(Path.Combine(store_path, file.getString("mail_uid")), true);
                                    }
                                }
                            }
                        }

                        //删除被移动的邮件
                        sql = "delete from ML_Mail where mail_uid in (" + sb.ToString() + ")";
                        try
                        {
                            using (var cmd = new SQLiteCommand(sql, DBWorker.GetConnection()))
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.Write(ex.StackTrace);
                        }

                        MailWorker.instance.dispatchMailEvent(MailWorker.Event.Reset, null, null);
                    }
                    break;
            }
        }

        public void onRemotingException(string callUID, string methodName, string message, string code, ASObject exception, AsyncOption option)
        {
            System.Diagnostics.Debug.WriteLine(message);
        }

        private void execute(List<ASObject> recvs)
        {
            if (recvs.Count > 0)
            {
                ASObject ac = recvs[0];
                object value = Remoting.call("MailManager.requestRecvTask", new object[] { ac.getString("account") });
                if (value == null || String.IsNullOrWhiteSpace(value as string))
                {
                    recvs.RemoveAt(0);
                    execute(recvs);
                    return;
                }

                string recvTask = value as string;
                string[] splits = recvTask.Split(':');
                if (splits.Length != 2)
                {
                    recvs.RemoveAt(0);
                    execute(recvs);
                    return;
                }

                string pubId = splits[1];
                if (splits[0] == "OK")
                {
                    workInfo.SetInfo("正在连接服务器... 请稍后");
                    acceptThread = new Thread(() => recvMaill(ac, pubId));
                    acceptThread.IsBackground = true;
                    acceptThread.Start();
                }
                else if (splits[0] == "JOIN")
                {
                    joinList.Add(ac);
                    recvs.RemoveAt(0);
                    execute(recvs);
                }
            }
        }

        private bool isJoinAccept = false;
        private void onInterval_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //判断是否有等待的邮件账号 有 订阅（标记） 否 接收（标记）
            if (joinList.Count() > 0 && !isJoinAccept)
            {
                ASObject ac = joinList[0];
                object value = Remoting.call("MailManager.requestRecvTask", new object[] { ac.getString("account") });
                if (value == null || String.IsNullOrWhiteSpace(value as string))
                {
                    joinList.RemoveAt(0);
                    return;
                }

                string recvTask = value as string;
                string[] splits = recvTask.Split(':');
                if (splits.Length != 2)
                {
                    joinList.RemoveAt(0);
                    return;
                }
                string pubId = splits[1];
                if (splits[0] == "OK")
                {
                    isJoinAccept = true;
                    workInfo.SetInfo("正在连接服务器... 请稍后");
                    acceptThread = new Thread(() => recvMaill(ac, pubId, true));
                    acceptThread.IsBackground = true;
                    acceptThread.Start();
                }
                else if (splits[0] == "JOIN")
                {
                    if (!pubIds.Contains(pubId))
                    {
                        MessageManager.instance.subscribeMessage(pubId);
                        pubIds.Add(pubId);
                    }
                }
            }
        }

        private void onStopInterval_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (recvs.Count == 0 && joinList.Count == 0)
            {
                stopInterval.Stop();

                if (!hasError)
                {
                    workInfo.SetStatus(true, "邮件已全部接收完成。");
                    workInfo.CloseWindow();
                }
                else
                {
                    workInfo.SetStatus(false, "邮件已全部接收完成，但至少有一封邮件发生错误，相关内容请查看详细信息。");
                }
            }
        }

        private void error(string msg)
        {
            workInfo.SetStatus(false, msg);
        }

        private void recvMaill(ASObject ac, string pubId, bool isJoin = false)
        {
            string account = ac.getString("account");
            try
            {
                if (!pubIds.Contains(pubId))
                {
                    MessageManager.instance.subscribeMessage(pubId);
                    pubIds.Add(pubId);
                }
                executeRecvMaill(ac, pubId);
                AsyncOption option = new AsyncOption("MailManager.recvTaskFinished");
                option.showWaitingBox = false;
                Remoting.call("MailManager.recvTaskFinished", new object[] { account }, this, option);
                if (pubIds.Contains(pubId))
                {
                    MessageManager.instance.endPublish(pubId);
                    pubIds.Remove(pubId);
                }
            }
            catch (Exception ex)
            {
                error(ex.Message);
                return;
            }
            finally
            {
                AsyncOption option = new AsyncOption("MailManager.recvTaskFinished");
                option.showWaitingBox = false;
                Remoting.call("MailManager.recvTaskFinished", new object[] { account }, this, option);
                if (pubIds.Contains(pubId))
                {
                    MessageManager.instance.endPublish(pubId);
                    pubIds.Remove(pubId);
                }
            }

            if (!isJoin)
            {
                recvs.Remove(ac);
                execute(recvs);
            }
            else
            {
                joinList.Remove(ac);
                isJoinAccept = false;
            }
        }

        private void executeRecvMaill(ASObject ac, string pubId)
        {
            mailAccount = new MailAccount();

            mailAccount.pubId = pubId;
            mailAccount.account = ac.getString("account");
            mailAccount.name = ac.getString("name");
            mailAccount.recv_server = ac.getString("recv_address");
            mailAccount.recv_port = ac.getInt("recv_port");
            mailAccount.recv_type = (ac.getInt("recv_type") == 1 ? MailAccount.RECV_TYPE.POP3 : MailAccount.RECV_TYPE.IMAP);
            mailAccount.password = PassUtil.Decrypt(ac.getString("password"));
            mailAccount.recv_ssl = ac.getBoolean("is_recv_ssl");

            uids = new List<string>();
            /*
            DataSet ds = DBWorker.ExecuteQuery("select mail_uid from ML_Mail where mail_account = '" + mailAccount.account + "'");
            if (ds.Tables.Count > 0)
            {
                DataTable dt = ds.Tables[0];
                foreach (DataRow row in dt.Rows)
                {
                    if (String.IsNullOrWhiteSpace(row[0] as string))
                        continue;
                    uids.Add((string)row[0]);
                }
            }*/
            //获取账户对应的所有UIDs
            object result = Remoting.call("MailManager.getMailAccountUids", new object[] { mailAccount.account });
            object[] record = result as object[];
            if (record != null && record.Length > 0)
            {
                foreach (object r in record)
                {
                    uids.Add(r as string);
                }
            }

            try
            {
                if (mailAccount.recv_type == MailAccount.RECV_TYPE.POP3)
                {
                    pop3RecvMail();
                }
                else if (mailAccount.recv_type == MailAccount.RECV_TYPE.IMAP)
                {
                    imapRecvMail();
                }

                if (hasError)
                    throw new Exception("邮件已全部接收完成，但至少有一封邮件发生错误，相关内容请查看详细信息。");
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        private void pop3RecvMail()
        {
            using (var pop3_client = new POP3_Client())
            {
                pop3_client.Connect(mailAccount.recv_server, mailAccount.recv_port, mailAccount.recv_ssl);
                pop3_client.Login(mailAccount.account, mailAccount.password);

                int total = pop3_client.Messages.Count;
                workInfo.SetInfo("正在接收邮件列表... 请稍后");
                List<POP3Mail> mails = new List<POP3Mail>();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < pop3_client.Messages.Count; i++)
                {
                    if (isStop)
                        return;
                    workInfo.SetProgress(total, i);

                    POP3_ClientMessage pop3m = pop3_client.Messages[i];
                    try
                    {
                        if (uids.Contains(pop3m.UID))
                            continue;

                        //--通知其他订阅人--//
                        XElement el = new XElement("subscribe");
                        el.SetAttributeValue("total", total);
                        el.SetAttributeValue("count", i);
                        el.SetAttributeValue("account", mailAccount.account);
                        el.SetAttributeValue("type", "accept_mail");
                        el.SetAttributeValue("detail", "false");
                        el.SetAttributeValue("principal", Desktop.instance.loginedPrincipal.loginId);
                        el.SetAttributeValue("principal_name", Desktop.instance.loginedPrincipal.name);
                        el.SetAttributeValue("subject", "正在接收邮件列表...");
                        MessageManager.instance.publishMessage(mailAccount.pubId, "subscribe", el.ToString());
                        //-----------//

                        string charset = "GBK";
                        if (pop3m.HeaderToByte() == null)
                            return;
                        Mail_Message m = Mail_Message.ParseFromByte(pop3m.HeaderToByte());
                        //过滤黑名单
                        if (m.From != null && m.From.Count > 0)
                        {
                            if (MailWorker.instance.IsExistBlack(m.From[0].Address))
                                continue;
                        }

                        if (m.ContentType != null && m.ContentType.Param_Charset != null)
                            charset = m.ContentType.Param_Charset;

                        if (m.Date == DateTime.MinValue && IsMailExists(mailAccount.account, pop3m.UID))
                        {
                            continue;
                        }
                        else if (m.Date < uidBeginTime)
                        {
                            continue;
                        }
                        POP3Mail mail = new POP3Mail();
                        if (m.Subject != null && m.Subject.IndexOf("&#") != -1)
                            m.Subject = HttpUtility.HtmlDecode(m.Subject);

                        mail.subject = m.Subject;
                        mail.message = pop3m;
                        mail.charset = charset;
                        mails.Add(mail);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.Write(ex.StackTrace);

                        workInfo.AddDetail("邮件处理错误：" + ex.Message + ", uid = " + pop3m.UID, Colors.Red);
                        hasError = true;
                    }
                }

                if (mails.Count == 0)
                    return;

                workInfo.SetInfo("正在接收邮件... 请稍后");
                int count = 0;
                total = mails.Count;
                foreach (POP3Mail mail in mails)
                {
                    if (isStop)
                        return;
                    workInfo.SetProgress(total, count);
                    count++;
                    try
                    {
                        workInfo.AddDetail("正在接收邮件：" + mail.subject, Colors.Black);

                        //--通知其他订阅人--//
                        XElement el = new XElement("subscribe");
                        el.SetAttributeValue("total", total);
                        el.SetAttributeValue("count", count);
                        el.SetAttributeValue("account", mailAccount.account);
                        el.SetAttributeValue("type", "accept_mail");
                        el.SetAttributeValue("detail", "true");
                        el.SetAttributeValue("principal", Desktop.instance.loginedPrincipal.loginId);
                        el.SetAttributeValue("principal_name", Desktop.instance.loginedPrincipal.name);
                        el.SetAttributeValue("subject", "正在接收邮件：" + mail.subject);
                        MessageManager.instance.publishMessage(mailAccount.pubId, "subscribe", el.ToString());
                        //-----------//

                        string uid = mail.message.UID;
                        sb.Length = 0;
                        sb.Append(getFilePath(uid)).Append("/").Append(uid).Append(".eml");
                        string file = sb.ToString();

                        DirectoryInfo dir = Directory.GetParent(store_path + file);
                        if (!dir.Exists)
                            dir.Create();

                        using (FileStream fs = new FileStreamWrap(store_path + file, mail.message.Size, workInfo, mailAccount.pubId))
                        {
                            mail.message.MessageToStream(fs);
                        }
                        Mail_Message m = null;
                        using (FileStream fs = new FileStream(store_path + file, FileMode.Open))
                        {
                            m = Mail_Message.ParseFromStream(fs, Encoding.GetEncoding(mail.charset));
                        }

                        ASObject record = saveMail(m, uid, uid, file);

                        try
                        {
                            MailWorker.instance.saveMailRecord(record, "ML_Mail_Temp");

                            try
                            {
                                //唤醒Syncworker
                                SyncWorker.instance.Notify(total);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(ex.Message);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.Write(ex.StackTrace);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);

                        workInfo.AddDetail("邮件处理错误：" + ex.Message + ", uid = " + mail.message.UID, Colors.Red);
                        hasError = true;
                    }
                }

                if (mails.Count > 0)
                {
                    SyncWorker.instance.WorkInfo = workInfo;
                    if (SyncWorker.instance.Thread != null)
                        SyncWorker.instance.Thread.Join();
                }
            }
        }

        private void imapRecvMail()
        {
            using (var imap_client = new IMAP_Client())
            {
                imap_client.Connect(mailAccount.recv_server, mailAccount.recv_port, mailAccount.recv_ssl);

                // Call Capability even if you don't care about capabilities, it also controls IMAP client features.
                imap_client.Capability();

                imap_client.Login(mailAccount.account, mailAccount.password);
                imap_client.SelectFolder("INBOX");
                try
                {
                    imap_total = imap_client.SelectedFolder.MessagesCount;

                    workInfo.SetInfo("正在接收邮件列表... 请稍后");

                    imap_recv_messages = new List<IMAP_r_u_Fetch>();

                    imap_client.Fetch(
                        false,
                        IMAP_t_SeqSet.Parse("1:*"),
                        new IMAP_t_Fetch_i[]{
                        new IMAP_t_Fetch_i_Envelope(),
                        new IMAP_t_Fetch_i_Flags(),
                        new IMAP_t_Fetch_i_InternalDate(),
                        new IMAP_t_Fetch_i_Rfc822Size(),
                        new IMAP_t_Fetch_i_Uid()
                    },
                        this.callback_fetch_message_items
                    );

                    if (imap_recv_messages.Count == 0)
                        return;
                    workInfo.SetInfo("正在接收邮件... 请稍后");

                    imap_total = imap_recv_messages.Count;
                    int count = 1;
                    imap_client.FetchGetStoreStream += imap_client_FetchGetStoreStream;
                    foreach (IMAP_r_u_Fetch reps in imap_recv_messages)
                    {
                        if (isStop)
                            return;
                        workInfo.SetProgress(imap_total, count);
                        current_imap_fetch = reps;

                        string text = null;
                        if (reps.Envelope != null)
                        {
                            if (reps.Envelope.Subject != null && reps.Envelope.Subject.IndexOf("&#") != -1)
                                workInfo.AddDetail("正在接收邮件：" + (text = HttpUtility.HtmlDecode(reps.Envelope.Subject)), Colors.Black);
                            else
                                workInfo.AddDetail("正在接收邮件：" + (text = reps.Envelope.Subject), Colors.Black);
                        }

                        //--通知其他订阅人--//
                        XElement el = new XElement("subscribe");
                        el.SetAttributeValue("total", imap_total);
                        el.SetAttributeValue("count", count);
                        el.SetAttributeValue("account", mailAccount.account);
                        el.SetAttributeValue("type", "accept_mail");
                        el.SetAttributeValue("detail", "true");
                        el.SetAttributeValue("principal", Desktop.instance.loginedPrincipal.loginId);
                        el.SetAttributeValue("principal_name", Desktop.instance.loginedPrincipal.name);
                        el.SetAttributeValue("subject", "正在接收邮件：" + text);
                        MessageManager.instance.publishMessage(mailAccount.pubId, "subscribe", el.ToString());
                        //-----------//

                        imap_client.Fetch(
                            true,
                            IMAP_t_SeqSet.Parse(reps.UID.UID.ToString()),
                            new IMAP_t_Fetch_i[]{
                            new IMAP_t_Fetch_i_Rfc822Header(),
                            new IMAP_t_Fetch_i_Rfc822()
                        },
                            this.callback_fetch_message
                        );
                        count++;

                        try
                        {
                            //唤醒Syncworker
                            SyncWorker.instance.Notify(imap_total);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(ex.Message);
                        }
                    }
                    imap_client.FetchGetStoreStream -= imap_client_FetchGetStoreStream;

                    if (imap_recv_messages.Count > 0)
                    {
                        SyncWorker.instance.WorkInfo = workInfo;
                        if (SyncWorker.instance.Thread != null)
                            SyncWorker.instance.Thread.Join();
                    }
                }
                catch (Exception ex)
                {
                    workInfo.SetStatus(false, "邮件接收过程中发生错误：" + ex.Message);
                    hasError = true;
                }
            }
        }

        void imap_client_FetchGetStoreStream(object sender, IMAP_Client_e_FetchGetStoreStream e)
        {
            try
            {
                if (e.DataItem is IMAP_t_Fetch_r_i_Rfc822)
                {
                    e.Stream = new StreamWrap((e.DataItem as IMAP_t_Fetch_r_i_Rfc822).Stream, current_imap_fetch.Rfc822Size.Size, workInfo);
                }
            }
            catch (Exception)
            {
            }
        }
        private void callback_fetch_message_items(object sender, EventArgs<IMAP_r_u> e)
        {
            try
            {
                if (isStop)
                    return;
                imap_progress++;
                workInfo.SetProgress(imap_total, imap_progress);

                if (e.Value is IMAP_r_u_Fetch)
                {
                    IMAP_r_u_Fetch fetchResp = (IMAP_r_u_Fetch)e.Value;
                    string mail_uid = fetchResp.UID.UID.ToString();
                    if (uids.Contains(mail_uid))
                        return;
                    if (fetchResp.InternalDate == null && IsMailExists(mailAccount.account, mail_uid))
                    {
                        return;
                    }
                    else if (fetchResp.InternalDate.Date < uidBeginTime)
                    {
                        return;
                    }

                    imap_recv_messages.Add(fetchResp);

                    //--通知其他订阅人--//
                    XElement el = new XElement("subscribe");
                    el.SetAttributeValue("total", imap_total);
                    el.SetAttributeValue("count", imap_progress);
                    el.SetAttributeValue("account", mailAccount.account);
                    el.SetAttributeValue("type", "accept_mail");
                    el.SetAttributeValue("detail", "false");
                    el.SetAttributeValue("principal", Desktop.instance.loginedPrincipal.loginId);
                    el.SetAttributeValue("principal_name", Desktop.instance.loginedPrincipal.name);
                    el.SetAttributeValue("subject", "正在接收邮件列表...");
                    MessageManager.instance.publishMessage(mailAccount.pubId, "subscribe", el.ToString());
                    //-----------//
                }
            }
            catch (Exception ex)
            {
                workInfo.AddDetail("邮件处理错误：" + ex.Message, Colors.Red);
                hasError = true;
            }
        }
        private void callback_fetch_message(object sender, EventArgs<IMAP_r_u> e)
        {
            if (isStop)
                return;
            if (e.Value is IMAP_r_u_Fetch)
            {
                try
                {
                    IMAP_r_u_Fetch fetchResp = (IMAP_r_u_Fetch)e.Value;

                    string mail_uid = fetchResp.UID.UID.ToString();

                    Mail_Message mime = null;

                    fetchResp.Rfc822Header.Stream.Position = 0;
                    mime = Mail_Message.ParseFromStream(fetchResp.Rfc822Header.Stream);
                    fetchResp.Rfc822Header.Stream.Dispose();

                    if (mime.Date == DateTime.MinValue && IsMailExists(mailAccount.account, mail_uid))
                    {
                        return;
                    }
                    else if (mime.Date < uidBeginTime)
                    {
                        return;
                    }

                    string charset = "GBK";
                    if (mime.ContentType != null && mime.ContentType.Param_Charset != null)
                        charset = mime.ContentType.Param_Charset;

                    fetchResp.Rfc822.Stream.Position = 0;
                    mime = Mail_Message.ParseFromStream(fetchResp.Rfc822.Stream, Encoding.GetEncoding(charset));

                    StringBuilder sb = new StringBuilder();
                    string uid = Guid.NewGuid().ToString();
                    sb.Append(getFilePath(uid)).Append("/").Append(uid).Append(".eml");
                    string file = sb.ToString();

                    DirectoryInfo dir = Directory.GetParent(store_path + file);
                    if (!dir.Exists)
                        dir.Create();

                    // save message
                    using (FileStream fs = new FileStream(store_path + file, FileMode.Create))
                    {
                        fetchResp.Rfc822.Stream.Position = 0;
                        Net_Utils.StreamCopy(fetchResp.Rfc822.Stream, fs, 4096);
                    }

                    fetchResp.Rfc822.Stream.Dispose();

                    ASObject record = saveMail(mime, uid, mail_uid, file);

                    try
                    {
                        MailWorker.instance.saveMailRecord(record, "ML_Mail_Temp");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                    }
                }
                catch (ThreadAbortException ex)
                {
                    throw ex;
                }
                catch (Exception ex)
                {
                    workInfo.AddDetail("邮件处理错误：" + ex.Message + " uid = " + ((IMAP_r_u_Fetch)e.Value).UID.ToString(), Colors.Red);
                    hasError = true;
                }
            }
        }

        private string getMailBoxLabel(Mail_t_Mailbox mailBox)
        {
            if (!String.IsNullOrEmpty(mailBox.DisplayName))
                return mailBox.DisplayName;
            else
                return mailBox.LocalPart;
        }

        private ASObject saveMail(Mail_Message m, string uid, string mail_uid, string file)
        {
            workInfo.IsNewMail = true;
            DirectoryInfo dirinfo = Directory.GetParent(store_path + file);
            string dir = dirinfo.FullName + "/" + uid + ".parts";
            Directory.CreateDirectory(dir);

            ASObject record = new ASObject();

            record.Add("uuid", uid);
            record.Add("owner_user_id", wos.library.Desktop.instance.loginedPrincipal.id);
            try
            {
                string subject = m.Subject;
                if (subject == null)
                    subject = "";
                else if (subject.IndexOf("&#") != -1)
                {
                    subject = HttpUtility.HtmlDecode(subject);
                }
                record.Add("subject", subject);
            }
            catch (Exception)
            {
                record.Add("subject", "");
            }
            try
            {
                record.Add("sender", m.Sender == null ? "" : m.Sender.ToString());
            }
            catch (Exception)
            {
                record.Add("sender", "");
            }
            try
            {
                record.Add("mail_to", m.To == null ? "" : m.To.ToString());
                if (m.To != null && m.To.Mailboxes.Count() > 0)
                    record.Add("mail_to_label", getMailBoxLabel(m.To.Mailboxes[0]));
            }
            catch (Exception)
            {
                record.Add("mail_to", "");
            }
            try
            {
                record.Add("reply_to", m.ReplyTo == null ? "" : m.ReplyTo.ToString());
            }
            catch (Exception)
            {
                record.Add("reply_to", "");
            }
            try
            {
                record.Add("mail_from", m.From == null ? "" : m.From.ToString());
                if (m.From != null && m.From.Count > 0)
                {
                    record.Add("mail_from_label", getMailBoxLabel(m.From[0]));
                    record.Add("contact_mail", m.From[0].Address);
                }
            }
            catch (Exception)
            {
                record.Add("mail_from", "");
                record.Add("contact_mail", "");
            }
            //阅读回折
            if (m.DispositionNotificationTo != null && m.DispositionNotificationTo.Count > 0)
                record.Add("flags", "RECEIPT");
            else
                record.Add("flags", "RECENT");

            try
            {
                if (Setting.IsSpamMail(m))
                {
                    record.Add("mail_type", (int)DBWorker.MailType.SpamMail);
                    record.Add("folder", "SPAM");
                }
                else
                {
                    record.Add("mail_type", (int)DBWorker.MailType.RecvMail);
                    record.Add("folder", "INBOX");
                }
            }
            catch (Exception)
            {
                record.Add("mail_type", (int)DBWorker.MailType.RecvMail);
                record.Add("folder", "INBOX");
            }
            try
            {
                record.Add("cc", m.Cc == null ? "" : m.Cc.ToString());
            }
            catch (Exception)
            {
                record.Add("cc", "");
            }
            try
            {
                record.Add("bcc", m.Bcc == null ? "" : m.Bcc.ToString());
            }
            catch (Exception)
            {
                record.Add("bcc", "");
            }

            record.Add("message_id", m.MessageID == null ? uid : m.MessageID);
            record.Add("create_time", DateTimeUtil.now());
            record.Add("mail_date", m.Date == DateTime.MinValue ? DateTimeUtil.now() : m.Date);
            record.Add("send_time", null);
            record.Add("mail_account", mailAccount.account);
            record.Add("mail_file", file);
            record.Add("reply_for", null);
            record.Add("reply_header", null);
            record.Add("mail_uid", mail_uid);
            record.Add("client_or_server", "client");
            record.Add("is_synced", (short)0);
            record.Add("is_handled", is_handled);
            record.Add("priority", m.Priority);
            if (m.Received != null && m.Received.Count() > 0)
            {
                try
                {
                    if (m.Received[0].From_TcpInfo != null)
                        record.Add("ip_from", m.Received[0].From_TcpInfo.IP.ToString());
                }
                catch (Exception)
                {
                }
            }
            return record;
        }

        public static String getFilePath(String fileName)
        {
            int hashcode = fileName.GetHashCode();
            int c1, c2;
            c1 = (hashcode & 0x0000FF00) >> 11;
            c2 = (hashcode & 0x000000FF) >> 3;
            if (c1 < 0)
                c1 += 256;

            StringBuilder sb = new StringBuilder();

            sb.Append("/c");
            sb.Append(c1);
            sb.Append("/c");
            sb.Append(c2);

            return sb.ToString();
        }

        private bool IsMailExists(string account, string uid)
        {
            try
            {
                StringBuilder sql = new StringBuilder();
                sql.Append("select id from ML_Mail_Temp where mail_account = '")
                   .Append(account)
                   .Append("' and mail_uid = '")
                   .Append(uid)
                   .Append("'");
                DataSet ds = DBWorker.ExecuteQuery(sql.ToString());

                if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    return true;
                return false;
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }

    public class POP3Mail
    {
        public string subject;
        public POP3_ClientMessage message;
        public string charset;
    }
    public class FileStreamWrap : FileStream
    {
        private int size = 0;
        private IWorkInfo workInfo;
        private int progress = 0;
        private string pubId;
        private long item_subscribe_prev_ticks = System.DateTime.UtcNow.Ticks;

        public FileStreamWrap(string file, int size, IWorkInfo workInfo, string pubId = null)
            : base(file, FileMode.Create)
        {
            this.size = size;
            this.workInfo = workInfo;
            this.pubId = pubId;
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            base.Write(buffer, offset, count);
            progress += count;
            workInfo.SetItemProgress(size, progress);

            if ((System.DateTime.UtcNow.Ticks - item_subscribe_prev_ticks) > MailWorker.interval && !String.IsNullOrWhiteSpace(pubId))
            {
                item_subscribe_prev_ticks = System.DateTime.UtcNow.Ticks;
                //--通知其他订阅人--//
                XElement el = new XElement("subscribe");
                el.SetAttributeValue("total", size);
                el.SetAttributeValue("count", progress);
                el.SetAttributeValue("type", "accept_mail_stram");
                el.SetAttributeValue("principal", Desktop.instance.loginedPrincipal.loginId);
                el.SetAttributeValue("principal_name", Desktop.instance.loginedPrincipal.name);
                MessageManager.instance.publishMessage(pubId, "subscribe", el.ToString());
                //-----------//
            }
        }
    }
    public class StreamWrap : Stream
    {
        private int size = 0;
        private Stream stream;
        private IWorkInfo workInfo;
        private int progress = 0;
        private string pubId;
        private long item_subscribe_prev_ticks = System.DateTime.UtcNow.Ticks;

        public StreamWrap(Stream stream, int size, IWorkInfo workInfo, string pubId = null)
        {
            this.stream = stream;
            this.size = size;
            this.workInfo = workInfo;
            this.pubId = pubId;
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Write(buffer, offset, count);
            progress += count;
            workInfo.SetItemProgress(size, progress);

            if ((System.DateTime.UtcNow.Ticks - item_subscribe_prev_ticks > MailWorker.interval) && !String.IsNullOrWhiteSpace(pubId))
            {
                item_subscribe_prev_ticks = System.DateTime.UtcNow.Ticks;
                //--通知其他订阅人--//
                XElement el = new XElement("subscribe");
                el.SetAttributeValue("total", size);
                el.SetAttributeValue("count", progress);
                el.SetAttributeValue("type", "accept_mail_stram");
                el.SetAttributeValue("principal", Desktop.instance.loginedPrincipal.loginId);
                el.SetAttributeValue("principal_name", Desktop.instance.loginedPrincipal.name);
                MessageManager.instance.publishMessage(pubId, "subscribe", el.ToString());
                //-----------//
            }
        }

        public override bool CanRead
        {
            get { return stream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return stream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return stream.CanWrite; }
        }

        public override void Flush()
        {
            stream.Flush();
        }

        public override long Length
        {
            get { return stream.Length; }
        }

        public override long Position
        {
            get
            {
                return stream.Position;
            }
            set
            {
                stream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }
    }
}