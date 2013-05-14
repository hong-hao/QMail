using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml;
using System.Xml.Linq;
using ime.data.Grouping;
using ime.mail.Worker;
using mshtml;
using Newtonsoft.Json.Linq;
using wos.extensions;
using wos.library;
using wos.rpc.core;
using wos.utils;
using ime.mail.Net.Mail;


namespace ime.mail.controls
{
	/// <summary>
	/// MailView.xaml 的交互逻辑
	/// </summary>
	public partial class MailReader : UserControl
	{
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

        private ASObject mail = null;

		public MailReader()
		{
			InitializeComponent();
			
			webBrowser.Navigating += webBrowser_Navigating;
			root.Visibility = System.Windows.Visibility.Hidden;

            lbGrade.DataContext = gradesXml;
            lbGrade.SelectionChanged += lbGrade_SelectionChanged;
            btnGrade.Click += btnGrade_Click;

            lbHandle.DataContext = handlesXml;
            lbHandle.SelectionChanged += lbHandle_SelectionChanged;
            btnHandle.Click += btnHandle_Click;

            btnRemark.Click += btnRemark_Click;
            btnTranslateClose.Click += btnTranslateClose_Click;

            this.Unloaded += MailReader_Unloaded;
		}

        void MailReader_Unloaded(object sender, RoutedEventArgs e)
        {
            webBrowser.Navigating -= webBrowser_Navigating;

            btnGrade.Click -= btnGrade_Click;
            lbGrade.SelectionChanged -= lbGrade_SelectionChanged;
            btnHandle.Click -= btnHandle_Click;
            lbHandle.SelectionChanged -= lbHandle_SelectionChanged;

            btnRemark.Click -= btnRemark_Click;
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

        public void ShowMail(ASObject mail, bool isSearch = false)
		{
            btnGrade.IsEnabled = !isSearch;
            btnRemark.IsEnabled = !isSearch;
            btnHandle.IsEnabled = !isSearch;

            if (mail.getString("folder") != "INBOX" && !isSearch)
            {
                btnGrade.IsEnabled = false;
                btnRemark.IsEnabled = false;
                btnHandle.IsEnabled = false;
            }

            hideTranslate();

            tobTranslate.IsChecked = false;

            this.mail = mail;
            try
            {
                root.Visibility = System.Windows.Visibility.Visible;
                Mouse.OverrideCursor = Cursors.Wait;

                webBrowser.Navigate("about:blank");
                attachments.Children.Clear();

                txtSubject.Text = mail["subject"] as string;
                DateTime date = (DateTime)mail["mail_date"];
                if (date != null)
                    txtDate.Text = date.ToString("yyyy-MM-dd HH:mm");
                else
                    txtDate.Text = "";

                txtSender.Text = mail["mail_from_label"] as string;

                int customer_grade = mail.getInt("customer_grade");

                txtGradeLabel.Text = getGradeLabel(customer_grade);
                imgGrade.Source = getGradeImage(customer_grade);

                int handle_action = mail.getInt("handle_action");
                txtHandleAction.Text = getHandleAction(handle_action);
                imgHandleAction.Source = getHandleActionImage(handle_action);

                string contents = mail["contents"] as string;
                string file = Desktop.instance.ApplicationPath + "/mail/" + mail["mail_file"];
                if (String.IsNullOrEmpty(contents) || !File.Exists(file))
                {
                    if (!isSearch)
                    {
                        MailWorker.instance.ParseMail(mail);
                        MailWorker.instance.updateMailRecord(mail, new string[] { "attachments", "contents" });
                    }
                    else
                        MailWorker.instance.ParseMail(mail);
                    contents = mail["contents"] as string;
                }
                XmlDocument contentsXml = new XmlDocument();
                if (!String.IsNullOrEmpty(contents))
                {
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

                string country_from = mail["country_from"] as string;
                object ip_from = mail["ip_from"];
                if (String.IsNullOrWhiteSpace(country_from))
                {
                    //var remote_ip_info = {"ret":1,"start":"210.75.64.0","end":"210.75.95.255","country":"\u4e2d\u56fd","province":"\u4e0a\u6d77","city":"\u4e0a\u6d77","district":"","isp":"","type":"\u4f01\u4e1a","desc":"\u4e0a\u6d77\u901a\u7528\u6c7d\u8f66\u516c\u53f8"};
                    string url = "http://int.dpool.sina.com.cn/iplookup/iplookup.php?format=js&ip=";
                    country_from = FileLoader.loadFile(url + ip_from, null);
                    string begin = "{";
                    string end = "}";
                    country_from = country_from.Substring(country_from.IndexOf(begin));
                    country_from = country_from.Substring(0, country_from.IndexOf(end) + 1);
                    ASObject json = JsonUtil.toASObject(JObject.Parse(country_from));
                    if (json.getInt("ret") == -1)
                    {
                        mail["country_from"] = "未知";
                        if (!isSearch)
                            MailWorker.instance.updateMail(mail, new string[] { "country_from" });
                    }
                    else
                    {
                        mail["country_from"] = json.getString("country");//MailWorker.instance.getCountryCode(json.getString("country"));
                        string area_from = String.IsNullOrWhiteSpace(json.getString("province")) ? "" : json.getString("province");
                        if (!String.IsNullOrWhiteSpace(area_from) && !String.IsNullOrWhiteSpace(json.getString("city")))
                            area_from += "/" + json.getString("city");
                        if (!String.IsNullOrWhiteSpace(area_from))
                            mail["area_from"] = area_from;
                        if (!isSearch)
                            MailWorker.instance.updateMail(mail, new string[] { "country_from", "area_from" });
                    }
                }

                txtFrom.Text = mail.getString("country_from");
                txtArea.Text = (String.IsNullOrWhiteSpace(mail.getString("area_from")) ? "" : mail.getString("area_from"));

                //检查阅读回折

                if (mail.getString("flags") == "RECEIPT")
                {
                    string to = mail.getString("contact_mail");
                    this.Dispatcher.BeginInvoke((System.Action)delegate
                    {
                        if (ime.controls.MessageBox.Show("是否发送阅读回折？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            String subject = mail.getString("subject");
                            date = (DateTime)mail["mail_date"];
                            if (date == null)
                                date = DateTime.Now;
                            string source = mail.getString("mail_account");
                            StringBuilder sb = new StringBuilder();
                            sb.Append("<p>这是邮件收条, ").Append(date.ToString("yyyy-MM-dd HH:mm:ss")).Append("，发给")
                                .Append(source).Append(" 主题为 ").Append(subject).Append(" 的信件已被接受")
                                .Append("<br /><br />此收条只表明收件人的计算机上曾显示过此邮件</p>");
                            ASObject from = MailSendWorker.instance.findAccount(source);
                            if (from != null)
                            {
                                MailWorker.instance.sendReceiptMail(sb.ToString(), "Re:" + subject, from, new string[] { to });
                                mail["flags"] = "RECENT";
                                MailWorker.instance.updateMail(mail, new string[] { "flags" });
                            }
                        }
                        else
                        {
                            mail["flags"] = "RECENT";
                            MailWorker.instance.updateMail(mail, new string[] { "flags" });
                        }
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.Write(e.StackTrace);
                ime.controls.MessageBox.Show(e.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
			finally
			{
				Mouse.OverrideCursor = null;
			}
		}

		public static string ConvertExtendedASCII(string HTML)
		{
			StringBuilder ret = new StringBuilder();
			char[] s = HTML.ToCharArray();

			foreach (char c in s)
			{
				if (Convert.ToInt32(c) > 127)
					ret.Append("&#").Append(Convert.ToInt32(c)).Append(";");
				else
					ret.Append(c);
			}

			return ret.ToString();
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
            rootRow1.Height = new GridLength(1, GridUnitType.Star);
            rootRow2.Height = new GridLength(0, GridUnitType.Auto);
            rootRow3.Height = new GridLength(1, GridUnitType.Star);
        }

        private void hideTranslate()
        {
            gridTranslate.Visibility = System.Windows.Visibility.Collapsed;
            gridSplitter.Visibility = System.Windows.Visibility.Collapsed;

            rootRow0.Height = new GridLength(0, GridUnitType.Auto);
            rootRow1.Height = new GridLength(1, GridUnitType.Star);
            rootRow2.Height = new GridLength(0, GridUnitType.Auto);
            rootRow3.Height = new GridLength(0, GridUnitType.Auto);
        }

        void btnTranslateClose_Click(object sender, RoutedEventArgs e)
        {
            tobTranslate.IsChecked = false;

            hideTranslate();
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

            txtGradeLabel.Text = getGradeLabel(customer_grade);
            imgGrade.Source = getGradeImage(customer_grade);
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
                MailWorker.instance.dispatchMailEvent(MailWorker.Event.Delete, mail, null);
            }
            int handle_action = mail.getInt("handle_action");
            txtHandleAction.Text = getHandleAction(handle_action);
            imgHandleAction.Source = getHandleActionImage(handle_action);
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

        void btnRemark_Click(object sender, RoutedEventArgs e)
        {
            if (mail == null || mail is ASObjectGroup)
                return;

            EmailRemarkWindow win = new EmailRemarkWindow();
            win.Text = mail["remark"] as string;
            win.Owner = WPFUtil.FindAncestor<Window>(this);
            if (win.ShowDialog() == true)
            {
                mail["remark"] = win.Text;
                MailWorker.instance.dispatchMailEvent(MailWorker.Event.Update, mail, new string[] { "remark" });
            }
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
