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
using System.ComponentModel;
using System.IO;
using ime.mail.Utils;
using System.Xml.Linq;
using System.Diagnostics;
using wos.extensions;
using Microsoft.Win32;
using wos.utils;

namespace ime.mail.controls
{
	/// <summary>
	/// AttachmentItem.xaml 的交互逻辑
	/// </summary>
	public partial class AttachmentItem : UserControl, INotifyPropertyChanged
	{
		private static FileToIconConverter iconConverter = new FileToIconConverter();
        private XElement menuXml = XElement.Parse(@"<menus>
                                                        <menu action=""open"" icon=""pack://application:,,,/ime.mail;component/controls/Icons/open.png"" text="" 打 开 ""/>
                                                        <menu action=""saveAs"" icon=""pack://application:,,,/ime.mail;component/controls/Icons/save-as.png"" text="" 另 存 为 ""/>
                                                    </menus>");

		public AttachmentItem()
		{
			InitializeComponent();
		}
		public event PropertyChangedEventHandler PropertyChanged;

		private string _text;
		public string Text
		{
			get { return _text; }
			set { _text = value; OnPropertyChanged("Text"); }
		}

		private object _icon;
		public object Icon
		{
			get { return _icon; }
			set { _icon = value; OnPropertyChanged("Icon"); }
		}

		private string _file;
		public string File
		{
			get { return _file; }
			set { _file = value; OnPropertyChanged("File"); }
		}

		protected virtual void OnPropertyChanged(string propertyName)
		{
			if (PropertyChanged == null) 
				return;

			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}

		public void SetAttachment(string file)
		{
			this.File = file;
			try
			{
				this.Icon = iconConverter.GetImage(file, 16);
			}
			catch (Exception)
			{
			}
			this.Text = System.IO.Path.GetFileName(file);
		}

        public bool Editable
        {
            set
            {
                if(value)
                    VisualStateManager.GoToElementState(this.LayoutRoot, "edit", true);
                else
                    VisualStateManager.GoToElementState(this.LayoutRoot, "nedit", true);
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            sbButton.Click += onOpen;
            btnAci.Click += onOpen;
            removeImage.MouseDown += onMouseButtonDownHandle;
            lbMenus.DataContext = menuXml;
            lbMenus.SelectionChanged += lbMenus_SelectionChanged;

            this.Unloaded += AttachmentItem_Unloaded;
        }

        void AttachmentItem_Unloaded(object sender, RoutedEventArgs e)
        {
            sbButton.Click -= onOpen;
            btnAci.Click -= onOpen;
            lbMenus.SelectionChanged -= lbMenus_SelectionChanged;
            removeImage.MouseDown -= onMouseButtonDownHandle;
        }

        void lbMenus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            XElement el = lbMenus.SelectedItem as XElement;
            if (el == null)
            {
                sbButton.Dispatcher.BeginInvoke((System.Action)delegate
                {
                    sbButton.CloseDropDown();
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                return;
            }
            switch (el.AttributeValue("action"))
            {
                case "open":
                    lbMenus.Dispatcher.BeginInvoke((System.Action)delegate
                    {
                        lbMenus.SelectedIndex = -1;
                        sbButton.CloseDropDown();
                        lbMenus.Dispatcher.BeginInvoke((System.Action)delegate
                        {
                            if (!String.IsNullOrWhiteSpace(this.File) && System.IO.File.Exists(this.File))
                                Process.Start(this.File);
                        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    break;
                case "saveAs":
                    lbMenus.Dispatcher.BeginInvoke((System.Action)delegate
                    {
                        lbMenus.SelectedIndex = -1;
                        sbButton.CloseDropDown();
                        lbMenus.Dispatcher.BeginInvoke((System.Action)delegate
                        {
                            SaveAs(this.File);
                        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    break;
            }
        }

        void onMouseButtonDownHandle(object sender, MouseButtonEventArgs e)
        {
            WrapPanel wrapPanel = WPFUtil.FindAncestor<WrapPanel>(e.OriginalSource as DependencyObject);
            if (wrapPanel == null)
                return;
            wrapPanel.Children.Remove(this);
        }

        void onOpen(object sender, RoutedEventArgs e)
        {
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                sbButton.CloseDropDown();
                this.Dispatcher.BeginInvoke((System.Action)delegate
                {
                    if (!String.IsNullOrWhiteSpace(this.File) && System.IO.File.Exists(this.File))
                    {
                        try
                        {
                            Process.Start(this.File);
                        }
                        catch (Exception ex)
                        {
                            Debug.Write(ex.StackTrace);
                            ime.controls.MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void SaveAs(string file)
        {
            string fileName = System.IO.Path.GetFileName(file);
            string fileExt = System.IO.Path.GetExtension(file).Substring(1);

            SaveFileDialog dlg = new SaveFileDialog();

            dlg.DefaultExt = fileExt.ToLower();
            dlg.FileName = fileName;
            dlg.Title = "文件另存为";
            dlg.Filter = fileExt.ToUpper() + "文件|*." + fileExt.ToLower();
            if (dlg.ShowDialog() == true)
            {
                this.Dispatcher.BeginInvoke((System.Action)delegate
                {
                    try
                    {
                        System.IO.File.Copy(this.File, dlg.FileName, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.Write(ex.StackTrace);
                        ime.controls.MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }
	}
}
