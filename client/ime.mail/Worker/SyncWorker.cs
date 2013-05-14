using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Data.SQLite;
using System.Data;
using System;
using wos.rpc.core;
using System.Diagnostics;
using wos.rpc;
using wos.utils;
using wos.library;
using System.IO;

namespace ime.mail.Worker
{
    public class SyncWorker
    {
        private Thread thread = null;
        private static SyncWorker _instance = null;
        private bool isProcess = false;

        private List<ASObject> mailList = new List<ASObject>();
        private List<ASObject> mailTempList = new List<ASObject>();
        private IWorkInfo _workInfo;
        private StringBuilder sql = new StringBuilder();
        private string upload_mail_message = "pages/ime.mail/upload_mail_message.jsp";
        private int total = 0;
        private int progress = 0;

        public static SyncWorker instance
        {
            get
            {
                if (_instance == null)
                    _instance = new SyncWorker();
                return _instance;
            }
        }

        protected SyncWorker()
        {
            sql.Clear();
            sql.Append("select * from ML_Mail where is_synced=@is_synced or is_synced=@is_synced1");

            using (DataSet ds = new DataSet())
            {
                try
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection()))
                    {
                        cmd.Parameters.AddWithValue("@is_synced", 1);
                        cmd.Parameters.AddWithValue("@is_synced1", 0);
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
                            mailList.Add(mail);
                        }
                    }
                }
            }
            
        }

        public IWorkInfo WorkInfo
        {
            set
            {
                _workInfo = value;
                _workInfo.SetInfo("正在同步邮件...");
            }
        }

        public void Start()
        {
            thread = new Thread(run);
            thread.IsBackground = true;
            thread.Name = Guid.NewGuid().ToString();
            thread.Start();
        }

        public Thread Thread
        {
            get { return thread; }
        }

        /// <summary>
        /// 唤醒线程
        /// </summary>
        internal void Notify(int total)
        {
            if (isProcess)
                return;
            this.total = total + mailList.Count;
            if (thread == null)
                Start();
        }

        /// <summary>
        /// 重置 执行下次同步
        /// </summary>
        private void Reset()
        {
            mailTempList.Clear();
            sql.Clear();
            sql.Append("select * from ML_Mail_Temp");

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
                            mailTempList.Add(mail);
                        }
                    }
                }
            }
        }

        private void run()
        {
            isProcess = true;
            try
            {
                Reset();
                if (mailList.Count > 0)
                {
                    foreach (ASObject mail in mailList)
                    {
                        if(_workInfo != null)
                            _workInfo.SetProgress(total, progress++);
                        Remoting.call("MailManager.syncUserMail", new object[] { mail });

                        int is_synced_value = -1;
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
                                using (var client = new ime.mail.Worker.MailWorker.CookieAwareWebClient())
                                {
                                    client.Param = param;
                                    string uri = Desktop.getAbsoluteUrl(upload_mail_message);
                                    mail_file_path = mail_file_path.Replace("/", "\\");
                                    client.UploadFile(new Uri(uri), mail_file_path);
                                }
                                is_synced_value = 2;
                            }
                        }
                        if (is_synced_value != -1)
                        {
                            mail["is_synced"] = is_synced_value;
                            MailWorker.instance.updateMailRecord(mail, new string[] { "is_synced" });
                        }
                    }
                }

                if (mailTempList.Count > 0)
                {
                    object result = Remoting.call("MailManager.syncUserMails", new object[] { mailTempList });
                    if (result == null || (result as object[]) == null || (result as object[]).Length == 0)
                        return;

                    //在正式表中插入一条记录 删除临时表相对应的记录
                    object[] record = result as object[];
                    StringBuilder sb = new StringBuilder();
                    foreach (object r in record)
                    {
                        if (_workInfo != null)
                            _workInfo.SetProgress(total, progress++);
                        List<ASObject> list = mailTempList.Where(p => p.getString("mail_uid") == (r as string)).ToList();
                        if (list != null && list.Count > 0)
                        {
                            ASObject mail = list[0];
                            MailWorker.instance.dispatchMailEvent(ime.mail.Worker.MailWorker.Event.Create, mail, null);
                            sb.Clear();
                            sb.Append("delete from ML_Mail_Temp where id=@id");
                            using (SQLiteCommand cmd = new SQLiteCommand(sb.ToString(), DBWorker.GetConnection()))
                            {
                                cmd.Parameters.AddWithValue("@id", mail["id"]);
                                cmd.ExecuteNonQuery();
                            }
                            int is_synced_value = -1;
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
                                    using (var client = new ime.mail.Worker.MailWorker.CookieAwareWebClient())
                                    {
                                        client.Param = param;
                                        string uri = Desktop.getAbsoluteUrl(upload_mail_message);
                                        mail_file_path = mail_file_path.Replace("/", "\\");
                                        client.UploadFile(new Uri(uri), mail_file_path);
                                    }
                                    is_synced_value = 2;
                                }
                            }
                            if (is_synced_value != -1)
                            {
                                mail["is_synced"] = is_synced_value;
                                if (mail.ContainsKey("id"))
                                    mail.Remove("id");
                                MailWorker.instance.updateMailRecord(mail, new string[] { "is_synced" });
                            }
                        }
                    }

                    Reset();
                }
            }
            catch (Exception ex)
            {
                Debug.Write(ex.StackTrace);
            }

            thread = null;
            isProcess = false;
        }
    }
}
