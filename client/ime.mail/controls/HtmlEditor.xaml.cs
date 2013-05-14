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
using wos.library;
using wos.utils;
using wos.rpc.core;
using System.Timers;

namespace ime.mail.controls
{
    /// <summary>
    /// HtmlEditor.xaml 的交互逻辑
    /// </summary>
    public partial class HtmlEditor : UserControl
    {
        private HtmlEditorRuntime runtime = null;
        private string html = null;
        private bool isComplete = false;
        private static Timer interval;
        private int DEFAULT_DELAY = 200;

        public enum Event
        {
            Create,
            Update
        }

        public HtmlEditor()
        {
            InitializeComponent();

            runtime = new HtmlEditorRuntime(this);
            runtime.host = htmlEditor;

            htmlEditor.LoadCompleted += htmlCompleteHandler;
            htmlEditor.SizeChanged += htmlEditor_SizeChanged;

            this.Unloaded += HtmlEditor_Unloaded;
        }

        void HtmlEditor_Unloaded(object sender, RoutedEventArgs e)
        {
            htmlEditor.LoadCompleted -= htmlCompleteHandler;
            htmlEditor.SizeChanged -= htmlEditor_SizeChanged;

            if (interval != null)
            {
                interval.Stop();
                interval.Close();
                interval.Dispose();
            }
        }

        /// <summary>
        /// 获取或设置编辑器中的HTML内容。
        /// Get or set the html content.
        /// </summary>
        public string ContentHtml
        {
            set { html = value; }
            get { return runtime.call("GetText", new object[] { }) as string; }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            htmlEditor.Navigate(Desktop.getAbsoluteUrl("pages/ime.mail/editor.html"));
        }

        void htmlEditor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (isComplete)
            {
                this.Dispatcher.BeginInvoke((System.Action)delegate
                {
                    runtime.call("SizeChanged", new object[] { e.NewSize.Width - 5, e.NewSize.Height - 104 });
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        void htmlCompleteHandler(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
            mshtml.HTMLDocument dom = (mshtml.HTMLDocument)htmlEditor.Document;
            dom.documentElement.style.overflow = "hidden";
        }

        /// <summary>
        /// 选择图片
        /// </summary>
        /// <param name="val"></param>
        private void selectedImage(HtmlEditor.Event eventType, string val)
        {
            HtmlEditorImageWindow imgWin = new HtmlEditorImageWindow();
            imgWin.Owner = WPFUtil.FindAncestor<Window>(this);
            imgWin.EventType = eventType;
            if (eventType == Event.Update)
                imgWin.Img = val;
            imgWin.HtmlEditorImageChangedEvent += OnHtmlEditorImageChangedEvent;
            imgWin.ShowDialog();
            imgWin.HtmlEditorImageChangedEvent -= OnHtmlEditorImageChangedEvent;
        }

        void OnHtmlEditorImageChangedEvent(HtmlEditor.Event eventType, ASObject value)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<img src='").Append(value.getString("path")).Append("'");
            if(value.ContainsKey("width"))
                sb.Append(" width='").Append(value.getString("width")).Append("px'");
            if (value.ContainsKey("height"))
                sb.Append(" height='").Append(value.getString("height")).Append("px'");
            if(value.ContainsKey("alt"))
                sb.Append(" alt='").Append(value.getString("alt")).Append("'");
            sb.Append(" />");
            if(eventType == Event.Create)
                runtime.call("HtmlEditorInsertContent", new object[] { sb.ToString()});
            else if(eventType == Event.Update)
                runtime.call("HtmlEditorReplaceContent", new object[] { sb.ToString() });
        }

        public void onHtmlLoad()
        {
            isComplete = true;

            interval = new Timer(DEFAULT_DELAY);
            interval.AutoReset = true;
            interval.Elapsed += onInterval_Elapsed;
            interval.Start();

            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                runtime.call("LoadComplete", new object[] { });
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void onInterval_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (String.IsNullOrWhiteSpace(html) || !isComplete)
                return;

            interval.Stop();
            interval.Close();
            interval.Dispose();

            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                if (!String.IsNullOrWhiteSpace(html))
                    runtime.call("SetText", new object[] { html });
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        [System.Runtime.InteropServices.ComVisibleAttribute(true)]
        public class HtmlEditorRuntime
        {
            private HtmlEditor HtmlEditor;
            private WebBrowser webHost = null;

            public HtmlEditorRuntime(HtmlEditor HtmlEditor)
            {
                this.HtmlEditor = HtmlEditor;
            }

            public WebBrowser host
            {
                get { return webHost; }
                set { webHost = value; webHost.ObjectForScripting = this; }
            }

            public object call(string functionName, params object[] args)
            {
                object result = null;
                try
                {
                    result = this.webHost.InvokeScript(functionName, args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
                return result;
            }

            public void OnHtmlEditorInsertImage(params object[] args)
            {
                if (args == null || args.Length == 0)
                    return;
                string val = args[0] as string;
                if (val == null)
                    return;

                if(val.IndexOf("<img") == 0)
                    HtmlEditor.selectedImage(HtmlEditor.Event.Update, val);
                else if(String.IsNullOrWhiteSpace(val))
                    HtmlEditor.selectedImage(HtmlEditor.Event.Create, val);
            }

            public void OnHtmlLoad(params object[] args)
            {
                HtmlEditor.onHtmlLoad();
            }
        }
    }
}