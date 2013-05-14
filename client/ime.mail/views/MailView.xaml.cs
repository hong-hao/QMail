using System;
using System.Collections.Generic;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using ime.mail.controls;
using ime.mail.Worker;
using ime.notification;
using wos.extensions;
using wos.rpc.core;
using wos.utils;

namespace ime.mail.views
{
	/// <summary>
	/// MailView.xaml 的交互逻辑
	/// </summary>
    public partial class MailView : UserControl
    {
        private ProgressWindow progressWindow = null;
        private string MailNewDeliver = "MailNewDeliver";
        /// <summary>
        /// 接受邮件账号列表
        /// </summary>
        private List<ASObject> recvs = new List<ASObject>();
        private List<ASObject> joinList = new List<ASObject>();
        private XElement foldersXml = XElement.Parse(@"<root>
                                                    <folder name=""INBOX"" label=""收件箱"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail-close.png"" />
							                        <folder name=""SPAM"" label=""垃圾邮件"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/spam-box.png""/>
							                        <folder name=""DELETED"" label=""已删除邮件"" iconUrl=""pack://application:,,,/ime.mail;component/Icons/mail-delete-box.png""/>
                                                </root>");
        private XElement garbageXml = XElement.Parse(@"<root>
                                                    <folder action=""email_b"" label=""将发件人添加到黑名单"" iconUrl=""pack://application:,,,/ime.mail;component/views/Icons/agt_action_fail.png"" />
							                        <folder action=""domain_b"" label=""将发件人的域添加到黑名单"" iconUrl=""pack://application:,,,/ime.mail;component/views/Icons/agt_action_fail.png""/>
							                        <folder action=""domain_w"" label=""将发件人的域添加到白名单"" iconUrl=""pack://application:,,,/ime.mail;component/views/Icons/button_ok.png""/>
                                                    <folder action=""email_w"" label=""将发件人添加到白名单"" iconUrl=""pack://application:,,,/ime.mail;component/views/Icons/button_ok.png""/>
                                                </root>");

        private static Timer interval;
        private int DEFAULT_DELAY = 5 * 60 * 1000;	//5分钟刷新一次;

        public MailView()
        {
            InitializeComponent();

            mailBoxBar.FolderChangedEvent += mailBoxBar_FolderChangedEvent;
            mailList.SelectedChangedEvent += mailList_SelectedChangedEvent;

            btnNewMail.Click += btnNewMail_Click;
            btnGarbage.Click += btnGarbage_Click;
            btnDelete.Click += btnDelete_Click;
            btnTransfer.Click += btnTransfer_Click;

            lbTransfer.DataContext = foldersXml;
            lbTransfer.SelectionChanged += lbTransfer_SelectionChanged;

            lbGarbage.DataContext = garbageXml;
            lbGarbage.SelectionChanged += lbGarbage_SelectionChanged;

            btnReply.Click += btnReply_Click;
            btnAllReply.Click += btnAllReply_Click;
            btnTransmit.Click += btnTransmit_Click;

            this.Unloaded += MailView_Unloaded;

            NotificationCenter.Instance.AddMessageListener(MailNewDeliver, onNewMail);
        }

        void MailView_Unloaded(object sender, RoutedEventArgs e)
        {
            mailBoxBar.FolderChangedEvent -= mailBoxBar_FolderChangedEvent;
            mailList.SelectedChangedEvent -= mailList_SelectedChangedEvent;

            btnNewMail.Click -= btnNewMail_Click;
            btnGarbage.Click -= btnGarbage_Click;
            btnDelete.Click -= btnDelete_Click;
            btnTransfer.Click -= btnTransfer_Click;

            lbTransfer.SelectionChanged -= lbTransfer_SelectionChanged;
            lbGarbage.SelectionChanged -= lbGarbage_SelectionChanged;

            btnReply.Click -= btnReply_Click;
            btnAllReply.Click -= btnAllReply_Click;
            btnTransmit.Click -= btnTransmit_Click;

            NotificationCenter.Instance.RemoveMessageListener(MailNewDeliver, onNewMail);

            interval.Elapsed -= onInterval_Elapsed;
            interval.Stop();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            interval = new Timer(DEFAULT_DELAY);
            interval.AutoReset = true;
            interval.Elapsed += onInterval_Elapsed;
            interval.Start();

            OnSendAndRecv(null, null);
        }

        private void onInterval_Elapsed(object sender, ElapsedEventArgs e)
        {
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                try
                {
                    //发送线程启动处理待发送邮件
                    MailSendWorker.instance.StartTime();

                    if (progressWindow != null && !progressWindow.IsClosed)
                    {
                        progressWindow.IsWinHide = false;
                        progressWindow.Show();
                    }
                    else
                    {
                        MailReceiveWorker worker = new MailReceiveWorker(DateTime.MinValue);

                        progressWindow = new ProgressWindow();
                        progressWindow.IsWinHide = true;
                        progressWindow.Owner = WPFUtil.FindAncestor<Window>(this);
                        progressWindow.worker = worker;
                        worker.Start(progressWindow);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        void btnGarbage_Click(object sender, RoutedEventArgs e)
        {
            mailList.MoveFolder("SPAM");
        }

        void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            mailList.MoveFolder("DELETED");
        }

        void btnTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (!btnTransfer.IsOpen)
                btnTransfer.IsOpen = true;
        }

        void btnReply_Click(object sender, RoutedEventArgs e)
        {
            mailList.Reply();
        }

        void btnAllReply_Click(object sender, RoutedEventArgs e)
        {
            mailList.Reply(true);
        }

        void btnTransmit_Click(object sender, RoutedEventArgs e)
        {
            mailList.Transmit();
        }

        void mailList_SelectedChangedEvent(ASObject selectedMail)
        {
            if (selectedMail == null)
            {
                mailReader.root.Visibility = System.Windows.Visibility.Hidden;
                return;
            }

            mailReader.ShowMail(selectedMail, mailList.IsSearch);
        }

        void mailBoxBar_FolderChangedEvent(System.Xml.Linq.XElement folderXml)
        {
            XElement parentXml = folderXml.Parent;
            if (parentXml.AttributeValue("name") == "Unhandled")
                changeCurrentState("INBOX");
            else
                changeCurrentState(folderXml.AttributeValue("name"));
			mailList.ShowMailList(folderXml);
        }

        void lbTransfer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XElement folderXml = lbTransfer.SelectedItem as XElement;
            if (folderXml == null)
                return;
            btnTransfer.CloseDropDown();
            lbTransfer.SelectedIndex = -1;
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                mailList.MoveFolder(folderXml.AttributeValue("name"));
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        void lbGarbage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XElement garbageXml = lbGarbage.SelectedItem as XElement;
            if (garbageXml == null)
                return;
            btnGarbage.CloseDropDown();
            lbGarbage.SelectedIndex = -1;
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                mailList.BlackList(garbageXml.AttributeValue("action"));
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void OnSendAndRecv(object sender, RoutedEventArgs e)
        {
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                try
                {
                    //发送线程启动处理待发送邮件
                    MailSendWorker.instance.StartTime();

                    if (progressWindow != null && !progressWindow.IsClosed)
                    {
                        progressWindow.IsWinHide = false;
                        progressWindow.Show();
                    }
                    else
                    {
                        MailReceiveWorker worker = new MailReceiveWorker(DateTime.MinValue);

                        progressWindow = new ProgressWindow();
                        progressWindow.IsWinHide = false;
                        progressWindow.Owner = WPFUtil.FindAncestor<Window>(this);
                        progressWindow.worker = worker;
                        worker.Start(progressWindow);
                        progressWindow.Show();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.Write(ex.StackTrace);
                }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void btnNewMail_Click(object sender, RoutedEventArgs e)
        {
            MailSendWindow win = new MailSendWindow();
            win.Owner = WPFUtil.FindAncestor<Window>(this);
            win.Show();
        }

        private void OnAccountManager(object sender, RoutedEventArgs e)
        {
            AccountManagerWindow win = new AccountManagerWindow();
            win.Owner = WPFUtil.FindAncestor<Window>(this);
            if (win.ShowDialog() == true)
            {
                this.Dispatcher.BeginInvoke((System.Action)delegate
                {
                    try
                    {
                        if (progressWindow != null && !progressWindow.IsClosed)
                        {
                            progressWindow.IsWinHide = false;
                            progressWindow.Show();
                        }
                        else
                        {
                            MailReceiveWorker worker = new MailReceiveWorker(DateTime.MinValue, win.InitImportList);

                            progressWindow = new ProgressWindow();
                            progressWindow.Owner = WPFUtil.FindAncestor<Window>(this);
                            progressWindow.worker = worker;
                            worker.Start(progressWindow);
                            progressWindow.Show();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.Write(ex.StackTrace);
                    }
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        private void onNewMail(string sender, ime.notification.NotifyMessage e, NotificationCenter.Stage stage)
        {
            if (stage == NotificationCenter.Stage.Receiving)
                e.Show();
        }

        private void changeCurrentState(string state)
        {
            switch (state)
            {
                case "INBOX":
                    VisualStateManager.GoToElementState(this.LayoutRoot, "INBOX", true);
                    break;
                case "OUTBOX":
                    VisualStateManager.GoToElementState(this.LayoutRoot, "OUTBOX", true);
                    break;
                case "DSBOX":
                    VisualStateManager.GoToElementState(this.LayoutRoot, "DSBOX", true);
                    break;
                case "DRAFT":
                    VisualStateManager.GoToElementState(this.LayoutRoot, "DRAFT", true);
                    break;
                case "SPAM":
                    VisualStateManager.GoToElementState(this.LayoutRoot, "SPAM", true);
                    break;
                case "SENDED":
                    VisualStateManager.GoToElementState(this.LayoutRoot, "SENDED", true);
                    break;
                case "DELETED":
                    VisualStateManager.GoToElementState(this.LayoutRoot, "DELETED", true);
                    break;
            }
        }
    }
}
