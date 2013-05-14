using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using System.Xml.XPath;
using ime.mail.Worker;
using wos.collections;
using wos.extensions;
using wos.library;
using wos.rpc.core;
using wos.utils;

namespace ime.mail.controls
{
    /// <summary>
    /// MailBoxBar.xaml 的交互逻辑
    /// </summary>
    public partial class MailBoxBar : UserControl
    {
        public delegate void FolderChangedHandler(XElement folderXml);
        public event FolderChangedHandler FolderChangedEvent;

        private UnhandledMailProvider unhandledMailProvider = new UnhandledMailProvider();
        private XElement sxml = XElement.Parse(@"<root>
						                            <mailbox name=""AllMailBox"" label=""我的邮箱"" type=""group"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/folder.gif"" openIconUrl=""pack://application:,,,/ime.mail;component/Icons/folder-open.gif"">
							                            <folder count="""" name=""INBOX"" label=""收件箱"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail-close.png"" />
							                            <folder count="""" name=""OUTBOX"" label=""待发邮件"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail-waiting-send.png"" />
							                            <folder count="""" name=""DSBOX"" label=""待审邮件"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png"" />
							                            <folder count="""" name=""DRAFT"" label=""草稿"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail-draft-box.png""/>
							                            <folder count="""" name=""SPAM"" label=""垃圾邮件"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/spam-box.png""/>
							                            <folder count="""" name=""SENDED"" label=""已发送邮件"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail-send-box.png""/>
							                            <folder count="""" name=""DELETED"" label=""已删除邮件"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail-delete-box.png""/>
						                            </mailbox>
						                            <folder name=""Unhandled"" label=""未处理邮件"" type=""group"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/folder.gif"" openIconUrl=""pack://application:,,,/ime.mail;component/Icons/folder-open.gif"">
							                            <folder count="""" value=""0"" label=""2天"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
							                            <folder count="""" value=""1"" label=""4天"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
							                            <folder count="""" value=""2"" label=""1周"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
							                            <folder count="""" value=""3"" label=""半个月"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
							                            <folder count="""" value=""4"" label=""1个月"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
							                            <folder count="""" value=""5"" label=""3个月"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
							                            <folder count="""" value=""6"" label=""更长时间"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
						                            </folder>
						                            <!--
						                            <folder label=""长时间未联系用户"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png"">
							                        <folder label=""1周"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
							                        <folder label=""半个月"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
							                        <folder label=""1个月"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
							                        <folder label=""3个月"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
							                        <folder label=""半年"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
							                        <folder label=""1年"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
							                        <folder label=""更长时间"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail.png""/>
						                            </folder>
						                            -->
					                        </root>");

        public MailBoxBar()
        {
            InitializeComponent();

            MailWorker.instance.MailEvent += OnMailEvent;

            this.Unloaded += MailBoxBar_Unloaded;
        }

        void MailBoxBar_Unloaded(object sender, RoutedEventArgs e)
        {
            MailWorker.instance.MailEvent -= OnMailEvent;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            tree.ItemsSource = sxml.Elements();
        }

        public void Reset()
        {
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                try
                {
                    Map<string, long> countMap = FetchNewMailCount();
                    IEnumerable<XElement> folders = sxml.XPathSelectElements("/mailbox/folder");
                    foreach (XElement folderXml in folders)
                    {
                        string folder = folderXml.AttributeValue("name");
                        if (countMap.ContainsKey(folder))
                            folderXml.SetAttributeValue("count", "(" + countMap[folder] + ")");
                        else
                            folderXml.SetAttributeValue("count", "");
                    }
                    XElement dsboxXml = sxml.XPathSelectElement("/mailbox/folder[@name='DSBOX']");
                    string dsboxCount = FetchDsBoxMailCount();
                    if (dsboxCount != "0,0")
                        dsboxXml.SetAttributeValue("count", "(" + dsboxCount + ")");

                    countMap = unhandledMailProvider.FetchUnhandledMailCount();

                    folders = sxml.XPathSelectElements("/folder/folder");
                    foreach (XElement folderXml in folders)
                    {
                        string value = folderXml.AttributeValue("value");
                        if (countMap.ContainsKey(value))
                            folderXml.SetAttributeValue("count", "(" + countMap[value] + ")");
                        else
                            folderXml.SetAttributeValue("count", "");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private Map<string, long> FetchNewMailCount()
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("select folder, count(id) from ML_Mail where (is_seen is null or is_seen = 0) ")
               .Append("and owner_user_id = @owner_user_id ")
               .Append("group by folder");

            SQLiteCommand cmd = null;
            DataSet ds = null;
            try
            {
                cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection());
                cmd.Parameters.AddWithValue("@owner_user_id", Desktop.instance.loginedPrincipal.id);

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

            Map<string, long> result = new Map<string, long>();
            foreach (DataRow row in dt.Rows)
            {
                result[row[0].ToString()] = (long)row[1];
            }
            return result;
        }

        /// <summary>
        /// 获取待审核邮件
        /// </summary>
        /// <returns>[0]提交审核数，[1]等待审核数</returns>
        private string FetchDsBoxMailCount()
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("select owner_user_id, reviewer_id from ML_Mail where folder=@folder");

            SQLiteCommand cmd = null;
            DataSet ds = null;
            try
            {
                cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection());
                cmd.Parameters.AddWithValue("@folder", "DSBOX");

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
            long submitCount = 0;
            long waitCount = 0;
            foreach (DataRow row in dt.Rows)
            {
                if ((long)row[0] == Desktop.instance.loginedPrincipal.id && (long)row[1] == Desktop.instance.loginedPrincipal.id)
                    waitCount++;
                else if ((long)row[0] == Desktop.instance.loginedPrincipal.id)
                    submitCount++;
            }
            return submitCount + "," + waitCount;
        }

        private void onTreeSelection(object sender, RoutedEventArgs e)
        {
            TreeView tree = (TreeView)e.OriginalSource;
            if (tree.SelectedItem != null && FolderChangedEvent != null)
            {
                XElement folderXml = tree.SelectedItem as XElement;
                if (folderXml == null)
                    return;
                FolderChangedEvent(folderXml);
            }
        }

        void OnMailEvent(MailWorker.Event eventType, ASObject mail, string[] updateFields)
        {
            switch (eventType)
            {
                case MailWorker.Event.Delete:
                    return;
                case MailWorker.Event.Create:
                    {
                        if (mail == null)
                            return;
                        MailWorker.instance.saveMailRecord(mail);

                        string folder = mail.getString("folder");
                        XElement folderXml = sxml.XPathSelectElement("/mailbox/folder[@name='" + folder + "']");
                        string count = folderXml.AttributeValue("count");
                        if (IsBool(mail.get("is_seen")))
                        {
                            if (count == "(1)" || count == "")
                                folderXml.SetAttributeValue("count", "");
                            else
                            {
                                count = count.Replace("(", "").Replace(")", "");
                                folderXml.SetAttributeValue("count", "(" + (NumberUtil.toLong(count) - 1) + ")");
                            }
                        }
                        else
                        {
                            if (count == "")
                                folderXml.SetAttributeValue("count", "(1)");
                            else
                            {
                                count = count.Replace("(", "").Replace(")", "");
                                folderXml.SetAttributeValue("count", "(" + (NumberUtil.toLong(count) + 1) + ")");
                            }
                        }

                        if (mail["mail_date"] is DateTime && !IsBool(mail.get("is_handled")))
                        {
                            DateTime time = (DateTime)mail["mail_date"];
                            string value = unhandledMailProvider.JudgeTimePhase(time);
                            folderXml = sxml.XPathSelectElement("/folder/folder[@value='" + value + "']");
                            if (folderXml != null)
                            {
                                count = folderXml.AttributeValue("count");
                                if (count == "")
                                    folderXml.SetAttributeValue("count", "(1)");
                                else
                                {
                                    count = count.Replace("(", "").Replace(")", "");
                                    folderXml.SetAttributeValue("count", "(" + (NumberUtil.toLong(count) + 1) + ")");
                                }
                            }
                        }
                    }
                    break;
                case MailWorker.Event.Update:
                    {
                        if (mail == null || updateFields == null || updateFields.Length == 0)
                            return;

                        MailWorker.instance.updateMail(mail, updateFields);

                        bool is_seen = false;
                        bool is_handled = false;
                        foreach (string s in updateFields)
                        {
                            if (s == "is_seen")
                                is_seen = true;
                            else if (s == "is_handled")
                                is_handled = true;
                        }

                        if (is_seen)
                        {
                            string folder = mail.getString("folder");
                            XElement folderXml = sxml.XPathSelectElement("/mailbox/folder[@name='" + folder + "']");
                            string count = folderXml.AttributeValue("count");
                            if (IsBool(mail.get("is_seen")))
                            {
                                if (count == "(1)" || count == "")
                                    folderXml.SetAttributeValue("count", "");
                                else
                                {
                                    count = count.Replace("(", "").Replace(")", "");
                                    folderXml.SetAttributeValue("count", "(" + (NumberUtil.toLong(count) - 1) + ")");
                                }
                            }
                            else
                            {
                                if (count == "")
                                    folderXml.SetAttributeValue("count", "(1)");
                                else
                                {
                                    count = count.Replace("(", "").Replace(")", "");
                                    folderXml.SetAttributeValue("count", "(" + (NumberUtil.toLong(count) + 1) + ")");
                                }
                            }
                        }

                        if (is_handled)
                        {
                            DateTime time = (DateTime)mail["mail_date"];
                            string value = unhandledMailProvider.JudgeTimePhase(time);
                            XElement folderXml = sxml.XPathSelectElement("/folder/folder[@value='" + value + "']");
                            string count = folderXml.AttributeValue("count");
                            if (IsBool(mail.get("is_handled")))
                            {
                                if (count == "(1)" || count == "")
                                    folderXml.SetAttributeValue("count", "");
                                else
                                {
                                    count = count.Replace("(", "").Replace(")", "");
                                    folderXml.SetAttributeValue("count", "(" + (NumberUtil.toLong(count) - 1) + ")");
                                }
                            }
                            else
                            {
                                if (count == "")
                                    folderXml.SetAttributeValue("count", "(1)");
                                else
                                {
                                    count = count.Replace("(", "").Replace(")", "");
                                    folderXml.SetAttributeValue("count", "(" + (NumberUtil.toLong(count) + 1) + ")");
                                }
                            }
                        }
                    }
                    break;
                case MailWorker.Event.Reset:
                    this.Reset();
                    break;
                case MailWorker.Event.MoveFolder:
                    {
                        Map<string, long> countMap = unhandledMailProvider.FetchUnhandledMailCount();

                        IEnumerable<XElement> folders = sxml.XPathSelectElements("/folder/folder");
                        foreach (XElement folderXml in folders)
                        {
                            string value = folderXml.AttributeValue("value");
                            if (countMap.ContainsKey(value))
                                folderXml.SetAttributeValue("count", "(" + countMap[value] + ")");
                            else
                                folderXml.SetAttributeValue("count", "");
                        }
                    }
                    break;
                case MailWorker.Event.ClearSearch:
                    {
                        XElement folderXml = tree.SelectedItem as XElement;
                        if (folderXml == null)
                            return;
                        this.Dispatcher.BeginInvoke((Action)delegate
                        {
                            FolderChangedEvent(folderXml);
                        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }
                    break;
            }

            if (mail == null)
                return;

            if ("DSBOX" == mail.getString("folder") || "OUTBOX" == mail.getString("folder"))
            {
                XElement dsboxXml = sxml.XPathSelectElement("/mailbox/folder[@name='DSBOX']");
                string dsboxCount = FetchDsBoxMailCount();
                if (dsboxCount != "0,0")
                    dsboxXml.SetAttributeValue("count", "(" + dsboxCount + ")");
                else
                    dsboxXml.SetAttributeValue("count", "");
            }
        }

        private bool IsBool(object value)
        {
            if (value == null)
                return false;
            if (value is Boolean)
                return (bool)value;
            else if (NumberUtil.isNumber(value))
            {
                decimal d = NumberUtil.toNumber(value);
                return d > 0;
            }
            else
                return ASObjectUtil.toBoolean(value);
        }
    }
}
