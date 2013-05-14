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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Timers;

namespace ime.mail.controls
{
	/// <summary>
	/// Translator.xaml 的交互逻辑
	/// </summary>
	public partial class Translator : UserControl
	{
		private bool isOK = false;
		private Timer timer = new Timer(1000);
		private Action<string> callback;
		private int trycount = 0, trytime = 5;
		public Translator()
		{
			InitializeComponent();

			webBrowser.LoadCompleted += new LoadCompletedEventHandler(webBrowser_LoadCompleted);
			webBrowser.Navigate("http://localhost:7981/translator.html");
			timer.AutoReset = true;
			timer.Elapsed += new ElapsedEventHandler(timer_Elapsed);
		}

		void timer_Elapsed(object sender, ElapsedEventArgs e)
		{
			webBrowser.Dispatcher.Invoke(new Action(() =>
			{
				string result = "";
				try
				{
					result = webBrowser.InvokeScript("getResult") as string;
				}
				catch (Exception)
				{
					timer.Stop();
					if (callback != null)
						callback.Invoke(result);
					return;
				}

				if (!String.IsNullOrEmpty(result))
				{
					timer.Stop();
					if (callback != null)
						callback.Invoke(result);
				}
				else
				{
					trycount++;
					if (trycount > trytime)
					{
						timer.Stop();
						if (callback != null)
							callback.Invoke("");
					}
				}
			}));
		}

		void webBrowser_LoadCompleted(object sender, NavigationEventArgs e)
		{
			isOK = true;
		}
		public void BeginTranslate(string text, Action<string> callback)
		{
			if (isOK == false)
				throw new Exception("翻译功能尚未加载。");

			this.callback = callback;
			trycount = 0;
			try
			{
				webBrowser.InvokeScript("translate", text);
				timer.Start();
			}
			catch (Exception)
			{
			}
		}
	}

	public class TranslatorFactory
	{
		public static Translator Translator
		{
			get;
			set;
		}
	}
}
