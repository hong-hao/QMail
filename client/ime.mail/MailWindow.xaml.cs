using System;
using System.Windows;
using System.Xml.Linq;
using com.kinorsoft.appworkshop;
using ime.controls;
using ime.controls.QWindow;
using ime.mail.controls;
using ime.mail.Net;
using ime.mail.Worker;
using wos.library;

namespace ime.mail
{
	/// <summary>
	/// MailWindow.xaml 的交互逻辑
	/// </summary>
	public partial class MailWindow : QWindow
	{
		private TitleTab titleTab;
		private const string TITLE = "QS Mail";
		private HttpListenerProxy proxy;

		public MailWindow()
		{
			InitializeComponent();

			if (!DBWorker.IsDBCreated())
				DBWorker.CreateDB();

            DBWorker.GetConnection();

            Desktop.toDesktopWindow(this, true);
			Desktop.instance.addApplicationWindow("ime.MailManager", this);

			string translatorHtml = ime.mail.Properties.Resources.translatorHtml;
			proxy = new HttpListenerProxy();
			proxy.Started += new HttpListenerProxy.StartedHandler(proxy_Started);
			proxy.ServerAddress = "http://www.bing.com";
			proxy.port = 7981;
			proxy.AddedInnerFiles.Add("/translator.html", translatorHtml);
			proxy.Start();

			Application.Current.Exit += Current_Exit;
		}

		void Current_Exit(object sender, ExitEventArgs e)
		{
			if (proxy != null)
				proxy.Stop();
            DBWorker.CloseConnection();
		}

		void proxy_Started()
		{
			this.Dispatcher.BeginInvoke(new Action(() =>
			{
				Translator translator = new Translator();
				translator.Width = 0;
				translator.Height = 0;
				root.Children.Add(translator);

				TranslatorFactory.Translator = translator;
			}));
		}

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            Desktop.instance.removeApplicationWindow("ime.MailManager");
            if (proxy != null)
                proxy.Stop();

            DBWorker.CloseConnection();
        }

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			XElement rootXml = new XElement("root");
			XElement xml = new XElement("item");
			xml.SetAttributeValue("label", "邮件管理");
			xml.SetAttributeValue("alink", "");
			xml.SetAttributeValue("iconUrl", "pack://application:,,,/ime.mail;component/Icons/mail.png");
			rootXml.Add(xml);

			xml = new XElement("item");
			xml.SetAttributeValue("label", "客户管理");
			xml.SetAttributeValue("alink", "");
			xml.SetAttributeValue("backgroundColor", "#008040");
			xml.SetAttributeValue("iconUrl", "pack://application:,,,/ime.mail;component/Icons/contact.png");
			rootXml.Add(xml);
			/*
			xml = new XElement("item");
			xml.SetAttributeValue("label", "邮件营销");
			xml.SetAttributeValue("alink", "");
			xml.SetAttributeValue("backgroundColor", "#004080");
			xml.SetAttributeValue("iconUrl", "pack://application:,,,/ime.mail;component/Icons/mail_marketing.png");
			rootXml.Add(xml);
			*/
			xml = new XElement("item");
			xml.SetAttributeValue("label", "日程安排");
			xml.SetAttributeValue("alink", "");
			xml.SetAttributeValue("backgroundColor", "#D57312");
			xml.SetAttributeValue("iconUrl", "pack://application:,,,/ime.mail;component/Icons/calendar.png");
			rootXml.Add(xml);

			titleTab = new TitleTab();
			titleTab.Margin = new Thickness(10, 0, 0, 0);
			titleTab.Xml = rootXml;
			titleTab.SelectedItemChanged += new TitleTab.SelectedItemChangedHandler(tab_SelectedItemChanged);
			this.AddHeadControl(titleTab, true);
			titleTab.SelectedItemText = "邮件管理";

			this.Title = "邮件管理" + " - " + TITLE;
		}
		private void tab_SelectedItemChanged(string selectedItemText)
		{
			TitleTabItem item = titleTab.GetItem(selectedItemText);
			if (item != null)
			{
				this.Title = selectedItemText + " - " + TITLE;
				this.Icon = item.Icon;
				if (selectedItemText == "邮件管理")
				{
					calendarApp.Visibility = System.Windows.Visibility.Collapsed;
					customerManager.Visibility = System.Windows.Visibility.Collapsed;
					mailView.Visibility = System.Windows.Visibility.Visible;
				}
				else if (selectedItemText == "客户管理")
				{
					calendarApp.Visibility = System.Windows.Visibility.Collapsed;
					mailView.Visibility = System.Windows.Visibility.Collapsed;
					customerManager.Visibility = System.Windows.Visibility.Visible;
					if (customerManager.Tag == null)
					{
						if (ApplicationProvider.DefaultProvider != null)
						{
							customerManager.Children.Add(ApplicationProvider.DefaultProvider.CreatePage("5E9AF7B4-8F4A-4F0E-B25A-F83CEB4A1D3E.0D1E4AA03C10"));
							customerManager.Tag = true;
						}
					}
				}
				else if (selectedItemText == "日程安排")
				{
					mailView.Visibility = System.Windows.Visibility.Collapsed;
					customerManager.Visibility = System.Windows.Visibility.Collapsed;
					calendarApp.Visibility = System.Windows.Visibility.Visible;
				}
			}
		}
    }
}
