using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using ime.controls.QWindow;
using ime.mail.Net.Mail;
using ime.mail.Net.MIME;
using ime.mail.controls;
using System.IO;
using Microsoft.Win32;
using wos.library;
using wos.rpc.core;
using wos.rpc;
using Newtonsoft.Json.Linq;
using wos.utils;
using wos.collections;
using System.Windows.Data;
using ime.mail.Net.IO;
using ime.mail.Net.SMTP.Client;
using ime.mail.Utils;
using ime.mail.Worker;
using System.Web;
using ime.mail.Net;
using System.Text.RegularExpressions;

namespace ime.mail.views
{
    /// <summary>
    /// MailSendWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MailSendWindow : QWindow
    {
        public enum MailType
        {
            Default,
            AllReply,
            Reply,
            Draft,
            Transmit,
            Dsbox
        }
        private bool isHtml = true;
        private ASObject reviewer = null;//审核人员列表
        private List<ASObject> ccList = new List<ASObject>();//抄送人列表
        private ASObject from = null; //邮件发送人
        private string store_path;

        private ASObject _mail = null;
        private Mail_Message Mail_Message = null;
        private ASObject _saveMail = null;

        public MailSendWindow()
        {
            InitializeComponent();
            this.Width = System.Windows.SystemParameters.PrimaryScreenWidth * 0.55;
            this.Height = System.Windows.SystemParameters.PrimaryScreenHeight * 0.75;

            this.store_path = System.IO.Path.Combine(Desktop.instance.ApplicationPath, "mail");
            Desktop.toDesktopWindow(this, false);
        }

        public ASObject Mail
        {
            set { _mail = value; }
        }

        public MailType Mail_Type
        {
            set;
            get;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            btnAddAttachement.Click += btnAddAttachement_Click;
            btnTo.Click += btnTo_Click;
            btnAudit.Click += btnAudit_Click;
            btnCc.Click += btnCc_Click;
            txtTo.TextChanged += txtTo_TextChanged;
            cboFrom.SelectionChanged += cboFrom_SelectionChanged;
            btnSave.Click += btnSave_Click;
            btnSendMail.Click += btnSendMail_Click;
            btnAttachments.Click += btnAddAttachement_Click;
            txtAudit.KeyUp += txtAudit_KeyUp;
            txtCc.KeyUp += txtCc_KeyUp;

            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                if (_mail == null)
                    return;
                showMail(_mail);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void OnTextHtml(object sender, RoutedEventArgs e)
        {
            isHtml = !isHtml;

            if (isHtml == true)
            {
                TextHtmlIcon.Source = new BitmapImage(new Uri("pack://application:,,,/ime.mail;component/Icons/text.png"));
                TextHtmlText.Text = "纯文本";
                txtEditor.Visibility = System.Windows.Visibility.Collapsed;
                htmlEditor.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                TextHtmlIcon.Source = new BitmapImage(new Uri("pack://application:,,,/ime.mail;component/Icons/html.png"));
                TextHtmlText.Text = "HTML";
                txtEditor.Visibility = System.Windows.Visibility.Visible;
                htmlEditor.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private string convertAddressList(List<ASObject> addresss)
        {
            StringBuilder sb = new StringBuilder();
            foreach (ASObject entry in addresss)
            {
                string email = entry.getString("email");
                if (!String.IsNullOrWhiteSpace(email))
                    sb.Append(email).Append(";");
            }
            if (sb.ToString().LastIndexOf(";") != -1)
                sb.Remove(sb.ToString().LastIndexOf(";"), 1);

            return sb.ToString();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            this.ShowInTaskbar = true;
            this.Owner = null;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            btnAddAttachement.Click -= btnAddAttachement_Click;
            btnTo.Click -= btnTo_Click;
            btnAudit.Click -= btnAudit_Click;
            btnCc.Click -= btnCc_Click;
            txtTo.TextChanged -= txtTo_TextChanged;
            cboFrom.SelectionChanged -= cboFrom_SelectionChanged;
            btnSave.Click -= btnSave_Click;
            btnSendMail.Click -= btnSendMail_Click;
            btnAttachments.Click -= btnAddAttachement_Click;

            txtAudit.KeyUp -= txtAudit_KeyUp;
            txtCc.KeyUp -= txtCc_KeyUp;
        }

        void btnTo_Click(object sender, RoutedEventArgs e)
        {
            string url = "alink://ime.AppWindow/EntitySelectWindow?";
            ALink alink = ALink.parseALink(url);
            ASObject param = new ASObject();
            param["pageUID"] = "5E9AF7B4-8F4A-4F0E-B25A-F83CEB4A1D3E.0D1E4AA03C10";
            param["entityUID"] = "5E9AF7B4-8F4A-4F0E-B25A-F83CEB4A1D3E.2613B247720A";

            Action<List<ASObject>> act = new Action<List<ASObject>>(delegate(List<ASObject> selectedEntities)
            {
                StringBuilder sb = new StringBuilder();
                foreach (ASObject entry in selectedEntities)
                {
                    sb.Append(entry.get("email")).Append(";");
                }
                if (sb.ToString().LastIndexOf(";") != -1)
                    sb.Remove(sb.ToString().LastIndexOf(";"), 1);
                if (sb.Length > 0)
                    txtTo.Text = sb.ToString();
                if (!String.IsNullOrWhiteSpace(txtTo.Text))
                    changeContains();
            });
            param["entitySelectHandler"] = act;
            alink.param = param;
            Desktop.openALinkWindow(alink);
        }

        void btnAddAttachement_Click(object sender, RoutedEventArgs e)
        {
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                OpenFileDialog ofld = new OpenFileDialog();
                ofld.Title = "选择文件";
                ofld.Multiselect = true;
                ofld.Filter = "所有文件*.*|*.*";
                if (ofld.ShowDialog() == true)
                {
                    string[] filenames = ofld.FileNames;
                    foreach (string filename in filenames)
                    {
                        if (isExist(filename))
                            continue;
                        AttachmentItem item = new AttachmentItem();
                        item.Editable = false;
                        item.SetAttachment(filename);
                        attachments.Children.Add(item);
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        void btnSave_Click(object sender, RoutedEventArgs e)
        {
            using (MemoryStreamEx stream = new MemoryStreamEx(32000))
            {
                try
                {
                    if (Mail_Message == null)
                        Mail_Message = createMessage();

                    updateMessage(Mail_Message);
                    MIME_Encoding_EncodedWord headerwordEncoder = new MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding.Q, Encoding.UTF8);
                    Mail_Message.ToStream(stream, headerwordEncoder, Encoding.UTF8);

                    if (_saveMail == null)
                    {
                        //存放邮件到草稿目录 DRAFT
                        StringBuilder sb = new StringBuilder();
                        string uuid = Guid.NewGuid().ToString();
                        sb.Append(MailReceiveWorker.getFilePath(uuid)).Append("/").Append(uuid).Append(".eml");
                        string file = sb.ToString();
                        DirectoryInfo dir = Directory.GetParent(store_path + file);
                        if (!dir.Exists)
                            dir.Create();

                        Mail_Message.ToFile(store_path + file, headerwordEncoder, Encoding.UTF8);
                        _saveMail = saveMail(null, Mail_Message, uuid, file, (int)DBWorker.MailType.DraftMail, "DRAFT");

                        MailWorker.instance.dispatchMailEvent(MailWorker.Event.Create, _saveMail, null);
                        MailWorker.instance.syncUserMail(_saveMail);
                    }
                    else
                    {
                        string file = _saveMail["mail_file"] as string;
                        string uuid = _saveMail["uuid"] as string;
                        Mail_Message.ToFile(store_path + file, headerwordEncoder, Encoding.UTF8);
                        _saveMail = saveMail(_saveMail, Mail_Message, uuid, file, (int)DBWorker.MailType.DraftMail, "DRAFT");

                        MailWorker.instance.dispatchMailEvent(MailWorker.Event.Update, _saveMail, getUpdateFields().ToArray());
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                    this.Dispatcher.BeginInvoke((System.Action)delegate
                    {
                        ime.controls.MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
        }

        void btnAudit_Click(object sender, RoutedEventArgs e)
        {
            PrincipalSelectWindow pwin = new PrincipalSelectWindow(true);
            pwin.setRootPath("/", false);
            pwin.Owner = this;
            if (pwin.ShowDialog() == true)
            {
                reviewer = pwin.SingleValue;
                txtAudit.Text = reviewer.getString("name");
            }
        }

        void btnCc_Click(object sender, RoutedEventArgs e)
        {
            PrincipalSelectWindow pwin = new PrincipalSelectWindow();
            pwin.setRootPath("/", false);
            pwin.multipleValue = ccList;
            pwin.Owner = this;
            if (pwin.ShowDialog() == true)
            {
                ccList.Clear();
                if (pwin.multipleValue.Count > 0)
                    ccList = pwin.multipleValue;
                string excepUsers = convertManagersString(pwin.multipleValue, "email");
                if (excepUsers != txtCc.Text.Trim())
                {
                    txtCc.Text = excepUsers;
                }
            }
        }

        void btnSendMail_Click(object sender, RoutedEventArgs e)
        {
            using (MemoryStreamEx stream = new MemoryStreamEx(32000))
            {
                try
                {
                    if (String.IsNullOrWhiteSpace(txtTo.Text))
                        throw new Exception("收件人不能为空！");

                    if (Mail_Message == null)
                        Mail_Message = createMessage();

                    updateMessage(Mail_Message);

                    if ((Mail_Type == MailType.AllReply || Mail_Type == MailType.Reply) && _mail != null)
                        Mail_Message.InReplyTo = _mail["message_id"] as string;

                    MIME_Encoding_EncodedWord headerwordEncoder = new MIME_Encoding_EncodedWord(MIME_EncodedWordEncoding.Q, Encoding.UTF8);
                    Mail_Message.ToStream(stream, headerwordEncoder, Encoding.UTF8);

                    int mail_type = (int)DBWorker.MailType.OutboxMail;
                    string folder = "OUTBOX";
                    if (reviewer != null)
                    {
                        mail_type = (int)DBWorker.MailType.SendMail;
                        folder = "DSBOX";
                    }
                    if (_saveMail == null)
                    {
                        StringBuilder sb = new StringBuilder();
                        string uuid = Guid.NewGuid().ToString();
                        sb.Append(MailReceiveWorker.getFilePath(uuid)).Append("/").Append(uuid).Append(".eml");
                        string file = sb.ToString();
                        DirectoryInfo dir = Directory.GetParent(store_path + file);
                        if (!dir.Exists)
                            dir.Create();

                        Mail_Message.ToFile(store_path + file, headerwordEncoder, Encoding.UTF8);
                        _saveMail = saveMail(null, Mail_Message, uuid, file, mail_type, folder);
                        if (mail_type == (int)DBWorker.MailType.SendMail)
                        {
                            _saveMail["reviewer_id"] = reviewer.getLong("id");
                            _saveMail["reviewer_name"] = reviewer.getString("name");
                            _saveMail["operator_id"] = Desktop.instance.loginedPrincipal.id;
                            _saveMail["operator_name"] = Desktop.instance.loginedPrincipal.name;
                        }
                        MailWorker.instance.dispatchMailEvent(MailWorker.Event.Create, _saveMail, null);

                        if (mail_type == (int)DBWorker.MailType.SendMail)
                        {
                            MailWorker.instance.syncUserMail(_saveMail);
                        }
                    }
                    else
                    {
                        List<string> list = getUpdateFields();
                        string file = _saveMail["mail_file"] as string;
                        string uuid = _saveMail["uuid"] as string;
                        Mail_Message.ToFile(store_path + file, headerwordEncoder, Encoding.UTF8);
                        _saveMail = saveMail(_saveMail, Mail_Message, uuid, file, mail_type, folder);
                        if (mail_type == (int)DBWorker.MailType.SendMail)
                        {
                            _saveMail["reviewer_id"] = reviewer.getLong("id");
                            _saveMail["reviewer_name"] = reviewer.getString("name");
                            list.Add("reviewer_id");
                            list.Add("reviewer_name");
                            if (String.IsNullOrWhiteSpace(_saveMail.getString("operator_id")))
                            {
                                _saveMail["operator_id"] = Desktop.instance.loginedPrincipal.id;
                                _saveMail["operator_name"] = Desktop.instance.loginedPrincipal.name;
                                list.Add("operator_id");
                                list.Add("operator_name");
                            }
                        }
                        MailWorker.instance.dispatchMailEvent(MailWorker.Event.Update, _saveMail, list.ToArray());
                    }

                    if (Mail_Type == MailType.AllReply || Mail_Type == MailType.Reply)
                    {
                        _mail["handle_action"] = 2;
                        _mail["is_handled"] = true;

                        MailWorker.instance.dispatchMailEvent(MailWorker.Event.Update, _mail, new string[] { "handle_action", "is_handled" });
                    }
                    if (mail_type == (int)DBWorker.MailType.OutboxMail)
                    {
                        MailSendWorker.instance.AddMail(_saveMail);
                        MailSendWorker.instance.Start();
                    }

                    this.Close();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                    this.Dispatcher.BeginInvoke((System.Action)delegate
                    {
                        ime.controls.MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
        }

        private string convertManagersString(List<ASObject> managers, string field)
        {
            if (managers == null || managers.Count == 0)
                return "";
            StringBuilder sb = new StringBuilder();
            foreach (ASObject m in managers)
            {
                sb.Append(m.getString(field)).Append(";");
            }
            if (sb.ToString().LastIndexOf(";") != -1)
                sb.Remove(sb.ToString().LastIndexOf(";"), 1);

            return sb.ToString();
        }

        protected override void OnPreviewDragEnter(DragEventArgs e)
        {
            base.OnPreviewDragEnter(e);

            e.Effects = getDragDropEffects(e);
            e.Handled = true;
        }

        protected override void OnPreviewDragOver(DragEventArgs e)
        {
            base.OnPreviewDragOver(e);

            e.Effects = getDragDropEffects(e);
            e.Handled = true;
        }

        private DragDropEffects getDragDropEffects(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
            {
                string[] filenames = (string[])e.Data.GetData(DataFormats.FileDrop, true);
                foreach (string filename in filenames)
                {
                    if (File.Exists(filename))
                        return DragDropEffects.All;
                    else
                        return DragDropEffects.None;
                }

                return DragDropEffects.None;
            }
            else
                return DragDropEffects.None;
        }

        protected override void OnPreviewDrop(DragEventArgs e)
        {
            base.OnPreviewDrop(e);

            if (e.Data.GetDataPresent(DataFormats.FileDrop, true))
            {
                string[] filenames = (string[])e.Data.GetData(DataFormats.FileDrop, true);
                foreach (string filename in filenames)
                {
                    if (isExist(filename))
                        continue;
                    AttachmentItem item = new AttachmentItem();
                    item.Editable = false;
                    item.SetAttachment(filename);
                    attachments.Children.Add(item);
                }
            }
            e.Handled = true;
        }

        /// <summary>
        /// 判断文件是否已经在附件列表中
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private bool isExist(string file)
        {
            if (attachments.Children.OfType<AttachmentItem>().Count() > 0)
            {
                foreach (AttachmentItem item in attachments.Children.OfType<AttachmentItem>())
                {
                    if (System.IO.Path.GetFileName(file) == System.IO.Path.GetFileName(item.File))
                        return true;
                }
            }

            return false;
        }


        void cboFrom_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cboFrom.SelectedItem == null)
                return;
            if (cboFrom.Visibility == System.Windows.Visibility.Visible)
            {
                from = cboFrom.SelectedItem as ASObject;
            }
        }

        void txtTo_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(txtTo.Text))
                changeContains();
        }

        void txtCc_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                txtCc.Clear();
                ccList.Clear();
            }
        }

        void txtAudit_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Delete)
            {
                txtAudit.Clear();
                reviewer = null;
            }
        }

        /// <summary>
        /// 变更联系人
        /// </summary>
        private void changeContains()
        {
            List<ASObject> accounts = MailSendWorker.instance.findAccountContact(txtTo.Text);
            if (accounts.Count > 1)
            {
                spFrom.Visibility = System.Windows.Visibility.Visible;
                cboFrom.ItemsSource = accounts;
            }
            else if (accounts.Count == 1)
            {
                spFrom.Visibility = System.Windows.Visibility.Collapsed;
                cboFrom.SelectedIndex = -1;
                from = accounts[0];
            }
        }

        private string getMailBoxLabel(Mail_t_Mailbox mailBox)
        {
            if (!String.IsNullOrEmpty(mailBox.DisplayName))
                return mailBox.DisplayName;
            else
                return mailBox.LocalPart;
        }

        private ASObject saveMail(ASObject record, Mail_Message m, string uid, string file, int mail_type, string folder)
        {
            if (record == null)
                record = new ASObject();

            record["uuid"] = uid;
            if(String.IsNullOrWhiteSpace(record.getString("owner_user_id")))
                record["owner_user_id"] = wos.library.Desktop.instance.loginedPrincipal.id;
            try
            {
                string subject = m.Subject;
                if (subject == null)
                    subject = "";
                else if (subject.IndexOf("&#") != -1)
                {
                    subject = HttpUtility.HtmlDecode(subject);
                }
                record["subject"] = subject;
            }
            catch (Exception)
            {
                record["subject"] = "";
            }
            record["sender"] = m.Sender == null ? "" : m.Sender.ToString();
            try
            {
                record["mail_to"] = m.To == null ? "" : m.To.ToString();
                if (m.To != null && m.To.Mailboxes.Count() > 0)
                    record["mail_to_label"] = getMailBoxLabel(m.To.Mailboxes[0]);
            }
            catch (Exception)
            {
                record["mail_to"] = "";
            }
            record["reply_to"] = m.ReplyTo == null ? "" : m.ReplyTo.ToString();
            try
            {
                record["mail_from"] = m.From == null ? "" : m.From.ToString();
                if (m.From != null && m.From.Count > 0)
                {
                    record["mail_from_label"] = getMailBoxLabel(m.From[0]);
                    record["contact_mail"] = m.From[0].Address;
                }
            }
            catch (Exception)
            {
                record["mail_from"] = "";
                record["contact_mail"] = "";
            }
            record["flags"] = "RECENT";
            record["mail_type"] = mail_type;
            record["folder"] = folder;
            record["cc"] = m.Cc == null ? "" : m.Cc.ToString();
            record["bcc"] = m.Bcc == null ? "" : m.Bcc.ToString();
            record["is_seen"] = true;
            record["message_id"] = m.MessageID;
            record["create_time"] = DateTimeUtil.now();
            if(String.IsNullOrWhiteSpace(record.getString("mail_date")))
                record["mail_date"] = DateTimeUtil.now();
            if (mail_type == (int)DBWorker.MailType.SendMail)
                record["send_time"] = DateTimeUtil.now();
            if (from != null)
                record["mail_account"] = from.getString("account");
            record["mail_file"] = file;
            record["reply_for"] = null;
            record["reply_header"] = null;
            record["mail_uid"] = uid;
            record["client_or_server"] = "client";
            record["is_synced"] = (short)0;
            record["priority"] = m.Priority;
            if (m.Received != null && m.Received.Count() > 0)
            {
                try
                {
                    if (m.Received[0].From_TcpInfo != null)
                        record["ip_from"] = m.Received[0].From_TcpInfo.IP.ToString();
                }
                catch (Exception)
                {
                }
            }
            return record;
        }

        private List<string> getUpdateFields()
        {
            List<string> list = new List<string>();
            string fields = @"uuid,is_seen,is_synced,owner_user_id,message_id,subject,sender,mail_to,reply_to,mail_from,contact_mail,flags,cc,bcc,mail_type,mail_account,mail_file,reply_for,reply_header,folder,mail_from_label,mail_to_label,priority,ip_from,mail_date";
            foreach (string s in fields.Split(','))
                list.Add(s);
            return list;
        }

        private void showMail(ASObject mail)
        {
            try
            {
                if (Mail_Type == MailType.Dsbox)
                {
                    btnSave.IsEnabled = false;
                    btnAudit.IsEnabled = false;
                }

                txtTo.TextChanged -= txtTo_TextChanged;
                cboFrom.SelectionChanged -= cboFrom_SelectionChanged;

                string mail_account = mail["mail_account"] as string;
                from = MailSendWorker.instance.findAccount(mail_account);

                if (from == null)
                {
                    txtTo.TextChanged += txtTo_TextChanged;
                    cboFrom.SelectionChanged += cboFrom_SelectionChanged;
                }

                string file = mail.getString("mail_file");
                StringBuilder sb = new StringBuilder();
                Mail_Message = MailWorker.instance.ParseMail(file);
                if (Mail_Type == MailType.Transmit)
                    txtSubject.Text = "Fw:" + mail["subject"] as string;
                else if (Mail_Type == MailType.Reply || Mail_Type == MailType.AllReply)
                {
                    txtSubject.Text = "Re:" + mail["subject"] as string;
                }
                else
                    txtSubject.Text = mail["subject"] as string;

                sb.Clear();
                Mail_t_AddressList addresses = Mail_Message.Cc;
                if (addresses != null)
                {
                    foreach (Mail_t_Mailbox mailbox in addresses.Mailboxes)
                    {
                        sb.Append(mailbox.Address).Append(";");
                    }
                    if (sb.ToString().LastIndexOf(";") != -1)
                        sb.Remove(sb.ToString().LastIndexOf(";"), 1);

                    txtCc.Text = sb.ToString();
                }

                if (Mail_Type == MailType.Draft || Mail_Type == MailType.Dsbox)
                    txtTo.Text = mail["mail_to"] as string;
                else if (Mail_Type != MailType.Transmit)
                    txtTo.Text = mail["contact_mail"] as string;

                if (Mail_Type != MailType.Transmit)
                {
                    //审核人
                    if (mail.ContainsKey("reviewer_id") && !String.IsNullOrWhiteSpace(mail.getString("reviewer_id")))
                    {
                        txtAudit.Text = mail.getString("reviewer_name");
                    }
                }

                sb.Clear();
                string uid = mail["uuid"] as string;
                DirectoryInfo dirinfo = Directory.GetParent(store_path + file);
                string dir = dirinfo.FullName + "/" + uid + ".parts";

                if (Mail_Type == MailType.Transmit)
                {
                    sb.Append(getTransmitVm(mail, Mail_Message));
                    sb.Append("<blockquote id=\"isReplyContent\" style=\"PADDING-LEFT: 1ex; MARGIN: 0px 0px 0px 0.8ex; BORDER-LEFT: #ccc 1px solid\">");
                }
                else if (Mail_Type == MailType.Reply || Mail_Type == MailType.AllReply)
                {
                    sb.Append(getReplyVm(mail, Mail_Message));
                    sb.Append("<blockquote id=\"isReplyContent\" style=\"PADDING-LEFT: 1ex; MARGIN: 0px 0px 0px 0.8ex; BORDER-LEFT: #ccc 1px solid\">");
                }

                string textHtml = parseMIMEContent(Mail_Message, dir);
                sb.Append(textHtml);

                if (Mail_Type == MailType.Transmit || Mail_Type == MailType.Reply || Mail_Type == MailType.AllReply)
                {
                    sb.Append("</blockquote>");
                }

                htmlEditor.ContentHtml = sb.ToString();

                if (Mail_Type == MailType.Draft || Mail_Type == MailType.Dsbox)
                {
                    _saveMail = _mail;
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Write(e.StackTrace);
                ime.controls.MessageBox.Show(e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string getTransmitVm(ASObject mail, Mail_Message m)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<br/><br/><br/><br/><hr/>-------- 转发邮件信息 --------<br/>");
            DateTime date = (DateTime)mail["mail_date"];
            if (date == null)
                date = DateTime.Now;

            sb.Append("发件人: ").Append(mail["mail_from"]).Append("<br/>");
            sb.Append("发送日期: ").Append(date.ToString("yyyy-MM-dd HH:mm:ss")).Append("<br/>");
            sb.Append("收件人: ").Append(mail["mail_to"]).Append("<br/>");
            Mail_t_AddressList addresses = m.Cc;
            if (addresses != null)
            {
                sb.Append("抄送人: ");
                foreach (Mail_t_Mailbox mailbox in addresses.Mailboxes)
                {
                    sb.Append(mailbox.Address).Append(";");
                }
                if (sb.ToString().LastIndexOf(";") != -1)
                    sb.Remove(sb.ToString().LastIndexOf(";"), 1);
                sb.Append("<br/>");
            }

            sb.Append("主题: ").Append(mail["subject"]).Append("<br/>");

            return sb.ToString();
        }

        private string getReplyVm(ASObject mail, Mail_Message m)
        {
            StringBuilder sb = new StringBuilder();
            DateTime date = (DateTime)mail["mail_date"];
            if (date == null)
                date = DateTime.Now;
            sb.Append("<br/><br/><br/><br/><hr/><div style=\"line-height:1.7;color:#000000;font-size:14px;font-family:arial\">");
            sb.Append("在 ").Append(date.ToString("yyyy-MM-dd HH:mm:ss")).Append("，").Append(mail["mail_from"]).Append(" 写道：").Append("<br/>");
            sb.Append("</div>");

            return sb.ToString();
        }

        private string parseMIMEContent(Mail_Message m, string dir)
        {
            MIME_Entity[] entities = m.GetAllEntities(true);
            if (entities == null)
                return "";
            Map<string, string> content_id_file = new Map<string, string>();
            StringBuilder textHtml = new StringBuilder();
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

                        string fPath = "";
                        string fileName = e.ContentType.Param_Name;
                        if (fileName == null)
                            fileName = Guid.NewGuid().ToString();
                        fileName = fileName.Replace(' ', '_');
                        fPath = System.IO.Path.Combine(dir, fileName);
                        if (!File.Exists(fPath))
                        {
                            using (Stream data = p.GetDataStream())
                            {
                                using (FileStream afs = File.Create(fPath))
                                {
                                    Net_Utils.StreamCopy(data, afs, 4096);
                                }
                            }
                        }

                        string contentId = e.ContentID;
                        if (!String.IsNullOrEmpty(contentId))
                        {
                            contentId = contentId.Trim();
                            if (contentId.StartsWith("<"))
                                contentId = contentId.Substring(1);
                            if (contentId.EndsWith(">"))
                                contentId = contentId.Substring(0, contentId.Length - 1);
                            content_id_file.Add(contentId, fPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
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

                        textHtml.Append(html);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }

            return textHtml.ToString();
        }

        private Mail_Message createMessage()
        {
            Mail_Message m = new Mail_Message();
            m.MimeVersion = "1.0";
            m.Date = DateTime.Now;
            m.MessageID = MIME_Utils.CreateMessageID();

            updateMessage(m);
            return m;
        }

        private void updateMessage(Mail_Message m)
        {
            if (from != null)
                m.From = Mail_t_MailboxList.Parse(from.getString("account"));
            m.To = Mail_t_AddressList.Parse(txtTo.Text);
            if (!String.IsNullOrWhiteSpace(txtSubject.Text))
                m.Subject = txtSubject.Text;
            if (!String.IsNullOrWhiteSpace(txtCc.Text))
                m.Cc = Mail_t_AddressList.Parse(txtCc.Text.Trim());

            if (Receipt.IsChecked == true)
            {
                m.DispositionNotificationTo = Mail_t_MailboxList.Parse(from.getString("account"));
            }
            if (Importance.IsChecked == true)
            {
                m.Priority = "urgent";
            }

            string sHtmlText = htmlEditor.ContentHtml;
            List<string> sUrlList = getHtmlImageUrlList(sHtmlText);

            if (sUrlList.Count > 0 || attachments.Children.OfType<AttachmentItem>().Count() > 0)
            {
                //--- multipart/mixed -------------------------------------------------------------------------------------------------
                MIME_h_ContentType contentType_multipartMixed = new MIME_h_ContentType(MIME_MediaTypes.Multipart.mixed);
                contentType_multipartMixed.Param_Boundary = Guid.NewGuid().ToString().Replace('-', '.');
                MIME_b_MultipartMixed multipartMixed = new MIME_b_MultipartMixed(contentType_multipartMixed);
                m.Body = multipartMixed;

                //--- multipart/alternative -----------------------------------------------------------------------------------------
                MIME_Entity entity_mulipart_alternative = new MIME_Entity();
                MIME_h_ContentType contentType_multipartAlternative = new MIME_h_ContentType(MIME_MediaTypes.Multipart.alternative);
                contentType_multipartAlternative.Param_Boundary = Guid.NewGuid().ToString().Replace('-', '.');
                MIME_b_MultipartAlternative multipartAlternative = new MIME_b_MultipartAlternative(contentType_multipartAlternative);
                entity_mulipart_alternative.Body = multipartAlternative;
                multipartMixed.BodyParts.Add(entity_mulipart_alternative);

                //--- text/plain ----------------------------------------------------------------------------------------------------
                MIME_Entity entity_text_plain = new MIME_Entity();
                MIME_b_Text text_plain = new MIME_b_Text(MIME_MediaTypes.Text.plain);
                entity_text_plain.Body = text_plain;
                text_plain.SetText(MIME_TransferEncodings.QuotedPrintable, Encoding.UTF8, txtEditor.Text);
                multipartAlternative.BodyParts.Add(entity_text_plain);

                // Create attachment etities: --- applactation/octet-stream -------------------------
                foreach (AttachmentItem item in attachments.Children.OfType<AttachmentItem>())
                {
                    multipartMixed.BodyParts.Add(Mail_Message.CreateAttachment(item.File));
                }

                foreach (string url in sUrlList)
                {
                    MIME_Entity img_entity = Mail_Message.CreateImage(url);
                    string contentID = Guid.NewGuid().ToString().Replace("-", "$");
                    img_entity.ContentID = "<" + contentID + ">";
                    sHtmlText = sHtmlText.Replace(url, "cid:" + contentID);
                    multipartMixed.BodyParts.Add(img_entity);
                }

                //--- text/html ------------------------------------------------------------------------------------------------------
                MIME_Entity entity_text_html = new MIME_Entity();
                MIME_b_Text text_html = new MIME_b_Text(MIME_MediaTypes.Text.html);
                entity_text_html.Body = text_html;
                text_html.SetText(MIME_TransferEncodings.QuotedPrintable, Encoding.UTF8, sHtmlText);
                multipartAlternative.BodyParts.Add(entity_text_html);
            }
            else
            {
                //--- multipart/alternative -----------------------------------------------------------------------------------------
                MIME_h_ContentType contentType_multipartAlternative = new MIME_h_ContentType(MIME_MediaTypes.Multipart.alternative);
                contentType_multipartAlternative.Param_Boundary = Guid.NewGuid().ToString().Replace('-', '.');
                MIME_b_MultipartAlternative multipartAlternative = new MIME_b_MultipartAlternative(contentType_multipartAlternative);
                m.Body = multipartAlternative;

                //--- text/plain ----------------------------------------------------------------------------------------------------
                MIME_Entity entity_text_plain = new MIME_Entity();
                MIME_b_Text text_plain = new MIME_b_Text(MIME_MediaTypes.Text.plain);
                entity_text_plain.Body = text_plain;
                text_plain.SetText(MIME_TransferEncodings.QuotedPrintable, Encoding.UTF8, txtEditor.Text);
                multipartAlternative.BodyParts.Add(entity_text_plain);

                //--- text/html ------------------------------------------------------------------------------------------------------
                MIME_Entity entity_text_html = new MIME_Entity();
                MIME_b_Text text_html = new MIME_b_Text(MIME_MediaTypes.Text.html);
                entity_text_html.Body = text_html;
                text_html.SetText(MIME_TransferEncodings.QuotedPrintable, Encoding.UTF8, sHtmlText);
                multipartAlternative.BodyParts.Add(entity_text_html);
            }
        }

        /// <summary>
        /// 取得HTML中所有本地图片的 URL。
        /// </summary>
        /// <param name="sHtmlText">HTML代码</param>
        /// <returns>图片的URL列表</returns>
        private List<string> getHtmlImageUrlList(string sHtmlText)
        {
            // 定义正则表达式用来匹配 img 标签
            Regex regImg = new Regex(@"<img\b[^<>]*?\bsrc[\s\t\r\n]*=[\s\t\r\n]*[""']?[\s\t\r\n]*(?<imgUrl>[^""']*)[^<>]*?/?[\s\t\r\n]*>", RegexOptions.IgnoreCase);

            // 搜索匹配的字符串
            MatchCollection matches = regImg.Matches(sHtmlText);

            List<string> sUrlList = new List<string>();

            // 取得匹配项列表
            foreach (Match match in matches)
            {
                string imgUrl = match.Groups["imgUrl"].Value;
                if (imgUrl.IndexOf("http://") == 0 || imgUrl.IndexOf("https://") == 0)
                    continue;
                sUrlList.Add(imgUrl);
            }

            return sUrlList;
        }
    }

    public class EmailAccountDataValueConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return "";
            ASObject account = value as ASObject;
            if (account == null)
                return "";
            return account.getString("name") + "(" + account.getString("account") + ")";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value;
        }
    }
}
