using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ime.controls.QWindow;
using ime.mail.Worker;
using ime.notification;

namespace ime.mail.controls
{
	/// <summary>
	/// ProgressWindow.xaml 的交互逻辑
	/// </summary>
	public partial class ProgressWindow : QWindow, IWorkInfo
	{
        private bool _isWinHide = false;
		public ProgressWindow()
		{
			InitializeComponent();

			this.Closing += ProgressWindow_Closing;
		}

		void ProgressWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (!this.closed)
			{
				this.Hide();
				e.Cancel = true;
			}
		}

		public object worker;

		private bool closed = false;
		public bool IsClosed
		{
			get { return closed; }
		}

        public bool IsWinHide
        {
            set { _isWinHide = value; }
            get { return _isWinHide; }
        }

		#region IWorkInfo 成员

		public void SetProgress(int total, int progress)
		{
            if (_isWinHide)
                return;
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                progressBar.IsIndeterminate = false;
                progressBar.Maximum = total;
                progressBar.Value = progress;

                itemProgressBar.Maximum = 1;
                itemProgressBar.Value = 0;
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
		}
		//上一次显示子项进度的时间
		private long item_progress_prev_ticks = 0;
		public void SetItemProgress(int total, int progress)
		{
            if (_isWinHide)
                return;
			//判断当前时间与上次显示子项进度时间的间隔，如果小于100毫秒，则不显示进度，防止CPU占用过高
			if (item_progress_prev_ticks == 0)
				item_progress_prev_ticks = System.DateTime.UtcNow.Ticks;
			else
			{
				if (System.DateTime.UtcNow.Ticks - item_progress_prev_ticks < MailWorker.interval)
					return;
				item_progress_prev_ticks = System.DateTime.UtcNow.Ticks;
			}

            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                itemProgressBar.Maximum = total;
                itemProgressBar.Value = progress;
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
		}
		public void SetInfo(string info)
		{
            if (_isWinHide)
                return;
			this.Dispatcher.BeginInvoke((System.Action)delegate
			{
				text.Text = info;
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
		}

		public void SetStatus(bool isSucess, string info)
		{
            if (_isWinHide)
                return;
			this.Dispatcher.BeginInvoke((System.Action)delegate
			{
				text.Text = info;
				closed = true;
				if (!isSucess)
					icon.Source = new BitmapImage(new Uri("pack://application:,,,/ime.mail;component/Icons/error.png"));
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
		}

        //detailInfo的长度如果不做限制，则会带动Scroll的进度条卡主
		public void AddDetail(string detailInfo, Color color)
		{
			this.Dispatcher.BeginInvoke((System.Action)delegate
			{
				TextBlock textBlock = new TextBlock();
                textBlock.Text = (detailInfo.Length > 35) ? detailInfo.Substring(0, 35) + "..." : detailInfo;
				textBlock.Foreground = new SolidColorBrush(color);

				detail.Items.Add(textBlock);
				detail.ScrollIntoView(textBlock);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
		}
		public void CloseWindow()
		{
			this.Dispatcher.BeginInvoke((System.Action)delegate
			{
				this.closed = true;
				this.Close();
				this.worker = null;

                MailWorker.instance.dispatchMailEvent(MailWorker.Event.Reset, null, null);
                
                if (IsNewMail)
                {
                    ime.notification.NotifyMessage notifyMessage = new ime.notification.NotifyMessage();
                    notifyMessage.Type = "MailNewDeliver";
                    notifyMessage.Subject = "您有新邮件";
                    notifyMessage.Text = "您有新邮件";
                    NotificationCenter.DispatchMessage(null, notifyMessage, NotificationCenter.Stage.Receiving);
                }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
		}
		#endregion

		private void OnStop(object sender, RoutedEventArgs e)
		{
            if (worker != null)
            {
                if(worker is MailReceiveWorker)
                    ((MailReceiveWorker)worker).Stop();
            }
		}

		private void OnHide(object sender, RoutedEventArgs e)
		{
			this.Hide();
            IsWinHide = true;
		}

		private void OnShowDetail(object sender, RoutedEventArgs e)
		{
			if (detail.Visibility == System.Windows.Visibility.Visible)
				detail.Visibility = System.Windows.Visibility.Collapsed;
			else
				detail.Visibility = System.Windows.Visibility.Visible;
		}


        public bool IsNewMail
        {
            get;
            set;
        }
    }
}
