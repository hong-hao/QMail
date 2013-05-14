using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml;
using System.Xml.Linq;
using ime.controls.QWindow;
using ime.data.Grouping;
using ime.mail.controls;
using ime.mail.Worker;
using mshtml;
using wos.extensions;
using wos.library;
using wos.rpc.core;
using wos.utils;

namespace ime.mail.views
{
    /// <summary>
    /// MailViewWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MailViewWindow : QWindow
    {
        public delegate void MailViewCloseHandler(ASObject mail);
        public event MailViewCloseHandler MailViewCloseEvent;

        ASObject mail = null;
        private XElement garbageXml = XElement.Parse(@"<root>
                                                            <folder action=""email_b"" label=""将发件人添加到黑名单"" iconUrl=""pack://application:,,,/ime.mail;component/views/Icons/agt_action_fail.png"" />
							                                <folder action=""domain_b"" label=""将发件人的域添加到黑名单"" iconUrl=""pack://application:,,,/ime.mail;component/views/Icons/agt_action_fail.png""/>
							                                <folder action=""domain_w"" label=""将发件人的域添加到白名单"" iconUrl=""pack://application:,,,/ime.mail;component/views/Icons/button_ok.png""/>
                                                            <folder action=""email_w"" label=""将发件人添加到白名单"" iconUrl=""pack://application:,,,/ime.mail;component/views/Icons/button_ok.png""/>
                                                        </root>");
        private XElement foldersXml = XElement.Parse(@"<root>
                                                            <folder name=""INBOX"" label=""收件箱"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail-close.png"" />
							                                <folder name=""SPAM"" label=""垃圾邮件"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/spam-box.png""/>
							                                <folder name=""DELETED"" label=""已删除邮件"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail-delete-box.png""/>
                                                        </root>");

        private XElement gradesXml = XElement.Parse(@"<root>
                                                            <grade value=""0"" label=""陌生客户"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/Grades/0.png"" />
							                                <grade value=""1"" label=""潜力客户"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/Grades/1.png""/>
							                                <grade value=""2"" label=""正式客户"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/Grades/2.png""/>
                                                            <grade value=""3"" label=""重要客户"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/Grades/3.png""/>
                                                            <grade value=""4"" label=""关键客户"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/Grades/4.png""/>
                                                        </root>");
        private XElement handlesXml = XElement.Parse(@"<root>
                                                            <grade value=""0"" label=""未处理"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/Handles/0.png"" />
							                                <grade value=""1"" label=""无需回复"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/Handles/1.png""/>
							                                <grade value=""2"" label=""已回复"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/Handles/2.png""/>
                                                        </root>");

        public MailViewWindow()
        {
            InitializeComponent();

            this.Width = System.Windows.SystemParameters.PrimaryScreenWidth * 0.55;
            this.Height = System.Windows.SystemParameters.PrimaryScreenHeight * 0.75;

            webBrowser.Navigating += webBrowser_Navigating;

            Desktop.toDesktopWindow(this, false);

            lbGarbage.DataContext = garbageXml;
            lbGarbage.SelectionChanged += lbGarbage_SelectionChanged;
            btnGarbage.Click += btnGarbage_Click;
            btnDelete.Click += btnDelete_Click;
            lbTransfer.DataContext = foldersXml;
            lbTransfer.SelectionChanged += lbTransfer_SelectionChanged;
            btnTransfer.Click += btnTransfer_Click;
            btnTransmit.Click += btnTransmit_Click;

            btnReply.Click += btnReply_Click;
            btnAllReply.Click += btnAllReply_Click;

            lbGrade.DataContext = gradesXml;
            lbGrade.SelectionChanged += lbGrade_SelectionChanged;
            btnGrade.Click += btnGrade_Click;

            lbHandle.DataContext = handlesXml;
            lbHandle.SelectionChanged += lbHandle_SelectionChanged;
            btnHandle.Click += btnHandle_Click;

            btnTranslateClose.Click += btnTranslateClose_Click;
        }

        public ASObject Mail
        {
            set { mail = value; }
        }

        public bool Editable
        {
            set
            {
                if (!value)
                {
                    btnGarbage.IsEnabled = false;
                    btnDelete.IsEnabled = false;
                    btnReply.IsEnabled = false;
                    btnAllReply.IsEnabled = false;
                    btnTransmit.IsEnabled = false;
                    btnTransfer.IsEnabled = false;

                    btnGrade.IsEnabled = false;
                    btnHandle.IsEnabled = false;
                }
            }
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

            webBrowser.Navigating -= webBrowser_Navigating;

            lbGarbage.SelectionChanged -= lbGarbage_SelectionChanged;
            btnGarbage.Click -= btnGarbage_Click;
            btnDelete.Click -= btnDelete_Click;

            lbTransfer.SelectionChanged -= lbTransfer_SelectionChanged;
            btnTransfer.Click -= btnTransfer_Click;
            btnTransmit.Click -= btnTransmit_Click;

            btnReply.Click -= btnReply_Click;
            btnAllReply.Click -= btnAllReply_Click;

            lbGrade.SelectionChanged -= lbGrade_SelectionChanged;
            btnGrade.Click -= btnGrade_Click;

            lbHandle.SelectionChanged -= lbHandle_SelectionChanged;
            btnHandle.Click -= btnHandle_Click;

            btnTranslateClose.Click -= btnTranslateClose_Click;
        }

        void webBrowser_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            string url = e.Uri.ToString();
            if (url != "about:blank" && !url.StartsWith("file:///"))
            {
                //TODO 过滤掉使用当前服务器中地址的情况
                e.Cancel = true;
                try
                {
                    //用默认浏览器打开链接
                    System.Diagnostics.Process.Start(url);
                }
                catch (Exception)
                {
                }
            }
        }
        public string CleanInvalidXmlChars(string text)
        {
            string re = @"[\x00-\x1F]+";
            return System.Text.RegularExpressions.Regex.Replace(text, re, "");
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (mail == null)
                return;

            showMail(mail);
        }

        void btnGarbage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MailWorker.instance.MoveFolder(mail, "SPAM"))
                {
                    this.Dispatcher.BeginInvoke((System.Action)delegate
                    {
                        if (MailViewCloseEvent != null)
                            MailViewCloseEvent(mail);
                        this.Close();
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
                ime.controls.MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void lbGarbage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XElement garbageXml = lbGarbage.SelectedItem as XElement;
            if (garbageXml == null)
                return;
            btnGarbage.CloseDropDown();
            lbGarbage.SelectedIndex = -1;

            try
            {
                if (MailWorker.instance.BlackList(mail, garbageXml.AttributeValue("action")))
                {
                    this.Dispatcher.BeginInvoke((System.Action)delegate
                    {
                        if (MailViewCloseEvent != null)
                            MailViewCloseEvent(mail);
                        this.Close();
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
                ime.controls.MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MailWorker.instance.MoveFolder(mail, "DELETED"))
                {
                    this.Dispatcher.BeginInvoke((System.Action)delegate
                    {
                        if (MailViewCloseEvent != null)
                            MailViewCloseEvent(mail);
                        this.Close();
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
                ime.controls.MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void btnTransmit_Click(object sender, RoutedEventArgs e)
        {
            if (mail == null || mail is ASObjectGroup)
                return;

            MailSendWindow win = new MailSendWindow();
            win.Mail_Type = MailSendWindow.MailType.Transmit;
            win.Mail = mail;
            win.Show();

            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                this.Close();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        void lbTransfer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XElement folderXml = lbTransfer.SelectedItem as XElement;
            if (folderXml == null)
                return;
            btnTransfer.CloseDropDown();
            lbTransfer.SelectedIndex = -1;

            try
            {
                if (MailWorker.instance.MoveFolder(mail, folderXml.AttributeValue("name")))
                {
                    this.Dispatcher.BeginInvoke((System.Action)delegate
                    {
                        if (MailViewCloseEvent != null)
                            MailViewCloseEvent(mail);
                        this.Close();
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
                ime.controls.MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void btnTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (!btnTransfer.IsOpen)
                btnTransfer.IsOpen = true;
        }

        void btnAllReply_Click(object sender, RoutedEventArgs e)
        {
            if ("INBOX" != mail.getString("folder"))
                return;
            MailSendWindow win = new MailSendWindow();
            win.Mail_Type = MailSendWindow.MailType.AllReply;
            win.Mail = mail;
            win.Show();

            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                this.Close();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        void btnReply_Click(object sender, RoutedEventArgs e)
        {
            if ("INBOX" != mail.getString("folder"))
                return;
            MailSendWindow win = new MailSendWindow();
            win.Mail_Type = MailSendWindow.MailType.Reply;
            win.Mail = mail;
            win.Show();

            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                this.Close();
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        void btnGrade_Click(object sender, RoutedEventArgs e)
        {
            if (!btnGrade.IsOpen)
                btnGrade.IsOpen = true;
        }

        void btnHandle_Click(object sender, RoutedEventArgs e)
        {
            if (!btnHandle.IsOpen)
                btnHandle.IsOpen = true;
        }

        void lbGrade_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XElement xml = lbGrade.SelectedItem as XElement;
            if (xml == null)
                return;
            btnGrade.CloseDropDown();
            lbGrade.SelectedIndex = -1;

            if (mail == null || mail is ASObjectGroup)
                return;
            if ("INBOX" != mail.getString("folder"))
                return;
            string old = mail.getString("customer_grade", "");
            if (xml.AttributeValue("value").Equals(old))
                return;

            mail["customer_grade"] = xml.AttributeValue("value");
            MailWorker.instance.dispatchMailEvent(MailWorker.Event.Update, mail, new string[] { "customer_grade" });

            int customer_grade = mail.getInt("customer_grade");

            GradeLabel.Text = getGradeLabel(customer_grade);
            GradeImage.Source = getGradeImage(customer_grade);
        }

        void lbHandle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XElement item = lbHandle.SelectedItem as XElement;
            if (item == null)
                return;
            btnHandle.CloseDropDown();
            lbHandle.SelectedIndex = -1;

            if (mail == null || mail is ASObjectGroup)
                return;
            if ("INBOX" != mail.getString("folder"))
                return;
            int old = mail.getInt("handle_action");
            if (old.Equals(item.AttributeValue("value")))
                return;

            mail["handle_action"] = item.AttributeValue("value");
            if ((item.AttributeValue("value") == "1" || item.AttributeValue("value") == "2") && IsBool(mail["is_handled"]))
                MailWorker.instance.dispatchMailEvent(MailWorker.Event.Update, mail, new string[] { "handle_action" });
            else if (item.AttributeValue("value") == "0" && IsBool(mail["is_handled"]))
            {
                mail["is_handled"] = false;
                MailWorker.instance.dispatchMailEvent(MailWorker.Event.Update, mail, new string[] { "handle_action", "is_handled", "is_handled" });
            }
            else if ((item.AttributeValue("value") == "1" || item.AttributeValue("value") == "2") && !IsBool(mail["is_handled"]))
            {
                mail["is_handled"] = true;
                MailWorker.instance.dispatchMailEvent(MailWorker.Event.Update, mail, new string[] { "handle_action", "is_handled", "is_handled" });
            }
            int handle_action = mail.getInt("handle_action");
            HandleActionLabel.Text = getHandleAction(handle_action);
            HandleActionImage.Source = getHandleActionImage(handle_action);
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

        private void showMail(ASObject mail)
        {
            try
            {
                webBrowser.Navigate("about:blank");
                attachments.Children.Clear();

                Subject.Text = mail["subject"] as string;
                DateTime date = (DateTime)mail["mail_date"];
                if (date != null)
                    Date.Text = date.ToString("yyyy-MM-dd HH:mm");
                else
                    Date.Text = "";

                Sender.Text = mail["mail_from_label"] as string;

                int customer_grade = mail.getInt("customer_grade");

                GradeLabel.Text = getGradeLabel(customer_grade);
                GradeImage.Source = getGradeImage(customer_grade);

                int handle_action = mail.getInt("handle_action");
                HandleActionLabel.Text = getHandleAction(handle_action);
                HandleActionImage.Source = getHandleActionImage(handle_action);

                /*
                string remark = mail.getString("remark");
                if (String.IsNullOrWhiteSpace(remark))
                    txtRemark.Visibility = System.Windows.Visibility.Collapsed;
                else
                    txtRemark.Text = remark;
                 * */

                string contents = mail["contents"] as string;
                if (!String.IsNullOrEmpty(contents))
                {
                    XmlDocument contentsXml = new XmlDocument();
                    contentsXml.LoadXml(CleanInvalidXmlChars(contents));
                    string htmlfile = null;
                    bool hasText = false;
                    string root_path;

                    root_path = Desktop.instance.ApplicationPath + "/mail/" + mail["mail_file"];
                    DirectoryInfo dirinfo = Directory.GetParent(root_path);
                    root_path = dirinfo.FullName + "/" + mail["uuid"] + ".parts/";

                    foreach (XmlElement xml in contentsXml.GetElementsByTagName("PART"))
                    {
                        string type = xml.GetAttribute("type");
                        if (type == "html")
                        {
                            htmlfile = root_path + mail["uuid"] + ".html";
                        }
                        else if (type == "text")
                        {
                            hasText = true;
                        }
                        else if (type == "file")
                        {
                            if (String.IsNullOrEmpty(xml.GetAttribute("content-id")))
                            {
                                AttachmentItem item = new AttachmentItem();
                                item.SetAttachment(root_path + xml.GetAttribute("filename"));
                                attachments.Children.Add(item);
                            }
                        }
                    }

                    if (!String.IsNullOrEmpty(htmlfile))
                    {
                        if (File.Exists(htmlfile))
                        {
                            Uri uri = new Uri("file:///" + htmlfile);
                            webBrowser.Navigate(uri);
                        }
                    }
                    else if (hasText)
                    {
                        string textfile = root_path + mail["uuid"] + ".text.html";
                        if (File.Exists(textfile))
                        {
                            Uri uri = new Uri("file:///" + textfile);
                            webBrowser.Navigate(uri);
                        }
                    }
                }

                txtFrom.Text = mail.getString("country_from");
                txtArea.Text = (String.IsNullOrWhiteSpace(mail.getString("area_from")) ? "" : mail.getString("area_from"));
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Write(e.StackTrace);
                ime.controls.MessageBox.Show(e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnTranslate(object sender, RoutedEventArgs e)
        {
            showTranslate();

            gridTranslate.IsBusy = true;
            var doc = webBrowser.Document;
            if (tobTranslate.IsChecked.Value && TranslatorFactory.Translator != null && doc != null && doc is HTMLDocument && ((HTMLDocument)doc).documentElement != null)
            {
                string text = (((mshtml.HTMLDocument)(doc)).documentElement).innerText;
                if (!String.IsNullOrEmpty(text))
                {
                    try
                    {
                        TranslatorFactory.Translator.BeginTranslate(text, new Action<string>((string result) =>
                        {
                            if (!String.IsNullOrWhiteSpace(result))
                            {
                                txtTitle.Text = "邮件" + this.mail.getString("subject") + "翻译结果:";
                                txtTranslate.Text = result;
                            }

                            gridTranslate.IsBusy = false;
                        }));
                    }
                    catch (Exception ex)
                    {
                        gridTranslate.IsBusy = false;
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                    }
                }
                else
                {
                    gridTranslate.IsBusy = false;
                    hideTranslate();
                }
            }
            else
            {
                gridTranslate.IsBusy = false;
                hideTranslate();
            }
        }

        private void showTranslate()
        {
            gridTranslate.Visibility = System.Windows.Visibility.Visible;
            gridSplitter.Visibility = System.Windows.Visibility.Visible;

            rootRow0.Height = new GridLength(0, GridUnitType.Auto);
            rootRow1.Height = new GridLength(0, GridUnitType.Auto);
            rootRow2.Height = new GridLength(1, GridUnitType.Star);
            rootRow3.Height = new GridLength(0, GridUnitType.Auto);
            rootRow4.Height = new GridLength(1, GridUnitType.Star);
        }

        private void hideTranslate()
        {
            gridTranslate.Visibility = System.Windows.Visibility.Collapsed;
            gridSplitter.Visibility = System.Windows.Visibility.Collapsed;

            rootRow0.Height = new GridLength(0, GridUnitType.Auto);
            rootRow1.Height = new GridLength(0, GridUnitType.Auto);
            rootRow2.Height = new GridLength(1, GridUnitType.Star);
            rootRow3.Height = new GridLength(0, GridUnitType.Auto);
            rootRow4.Height = new GridLength(0, GridUnitType.Auto);
        }

        void btnTranslateClose_Click(object sender, RoutedEventArgs e)
        {
            tobTranslate.IsChecked = false;

            hideTranslate();
        }

        private string getGradeLabel(int grade)
        {
            if (grade == 0)
                return "陌生客户";
            else if (grade == 1)
                return "潜力客户";
            else if (grade == 2)
                return "正式客户";
            else if (grade == 3)
                return "重要客户";
            else if (grade == 4)
                return "关键客户";
            else
                return "";
        }

        private BitmapImage getGradeImage(int grade)
        {
            if (grade == 0)
                return new BitmapImage(new Uri("pack://application:,,,/ime.mail;component/Icons/Grades/0.png"));
            else if (grade == 1)
                return new BitmapImage(new Uri("pack://application:,,,/ime.mail;component/Icons/Grades/1.png"));
            else if (grade == 2)
                return new BitmapImage(new Uri("pack://application:,,,/ime.mail;component/Icons/Grades/2.png"));
            else if (grade == 3)
                return new BitmapImage(new Uri("pack://application:,,,/ime.mail;component/Icons/Grades/3.png"));
            else if (grade == 4)
                return new BitmapImage(new Uri("pack://application:,,,/ime.mail;component/Icons/Grades/4.png"));
            else
                return new BitmapImage(new Uri("pack://application:,,,/ime.mail;component/Icons/Grades/0.png"));
        }

        private string getHandleAction(int handle_action)
        {
            if (handle_action == 0)
                return "未处理";
            else if (handle_action == 1)
                return "无须回复";
            else if (handle_action == 2)
                return "已回复";
            else
                return "";
        }

        private BitmapImage getHandleActionImage(int handle)
        {
            if (handle == 0)
                return new BitmapImage(new Uri("pack://application:,,,/ime.mail;component/Icons/Handles/0.png"));
            else if (handle == 1)
                return new BitmapImage(new Uri("pack://application:,,,/ime.mail;component/Icons/Handles/1.png"));
            else if (handle == 2)
                return new BitmapImage(new Uri("pack://application:,,,/ime.mail;component/Icons/Handles/2.png"));
            else
                return new BitmapImage(new Uri("pack://application:,,,/ime.mail;component/Icons/Handles/0.png"));
        }
    }
}