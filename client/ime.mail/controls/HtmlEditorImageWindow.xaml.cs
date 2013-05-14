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
using wos.library;
using ime.controls;
using Microsoft.Win32;
using System.IO;
using wos.rpc.core;
using System.Xml.Linq;
using wos.extensions;

namespace ime.mail.controls
{
    /// <summary>
    /// HtmlEditorImageWindow.xaml 的交互逻辑
    /// </summary>
    public partial class HtmlEditorImageWindow : QWindow
    {
        List<DictionaryItem> sources = new List<DictionaryItem>()
        {
            new DictionaryItem{label="本地图片", value="localhost"},
            new DictionaryItem{label="网路图片", value="http"}
        };

        public delegate void HtmlEditorImageChangedHandler(HtmlEditor.Event eventType, ASObject value);
        public event HtmlEditorImageChangedHandler HtmlEditorImageChangedEvent;

        private ASObject value = null;
        private string img = null;

        public HtmlEditorImageWindow()
        {
            InitializeComponent();

            Desktop.toDesktopWindow(this, false);

            cboSource.ItemsSource = sources;
            cboSource.DisplayMemberPath = "label";
            cboSource.SelectionChanged += cboSource_SelectionChanged;
            cboSource.SelectedIndex = 0;

            Masking.SetMask(txtWidth, @"^-?\d*$");
            Masking.SetMask(txtHeight, @"^-?\d*$");

            btnSelected.Click += btnSelected_Click;
            txtPath.TextChanged += txtPath_TextChanged;
            DataObject.AddPastingHandler(txtPath, OnPaste);

            btnOK.Click += btnOK_Click;
            btnCancel.Click += btnCancel_Click;

            this.PreviewKeyUp += HtmlEditorImageWindow_PreviewKeyUp;
        }


        public HtmlEditor.Event EventType
        {
            set;
            get;
        }

        public string Img
        {
            set
            {
                if (String.IsNullOrWhiteSpace(value))
                    return;
                img = value;
                try
                {
                    XElement imgXml = XElement.Parse(img);
                    string src = imgXml.AttributeValue("src");
                    if (String.IsNullOrWhiteSpace(src))
                        return;
                    if (src.IndexOf("http://") == 0 || src.IndexOf("https://") == 0)
                        cboSource.SelectedIndex = 1;
                    else
                        cboSource.SelectedIndex = 0;

                    txtPath.Text = src;

                    txtWidth.Text = imgXml.AttributeValue("width").Trim();
                    txtHeight.Text = imgXml.AttributeValue("height").Trim();
                    txtAlt.Text = imgXml.AttributeValue("alt").Trim();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            btnSelected.Click -= btnSelected_Click;
            cboSource.SelectionChanged -= cboSource_SelectionChanged;
            DataObject.RemovePastingHandler(txtPath, OnPaste);

            txtPath.TextChanged -= txtPath_TextChanged;
            btnOK.Click -= btnOK_Click;
            btnCancel.Click -= btnCancel_Click;

            this.PreviewKeyUp -= HtmlEditorImageWindow_PreviewKeyUp;
        }

        void HtmlEditorImageWindow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnOK_Click(null, null);
            }
        }

        void cboSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DictionaryItem item = e.AddedItems[0] as DictionaryItem;
            if(item == null)
                return;
            if ("http".Equals(item.value))
                btnSelected.Visibility = System.Windows.Visibility.Collapsed;
            else if ("localhost".Equals(item.value))
                btnSelected.Visibility = System.Windows.Visibility.Visible;
        }

        void btnSelected_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofld = new OpenFileDialog();
            ofld.Title = "选择图片";
            ofld.Multiselect = false;
            ofld.Filter = "图片|*.gif;*.jpg;*jpeg;*.tiff";
            if (ofld.ShowDialog() == true)
            {
                this.Dispatcher.BeginInvoke((System.Action)delegate
                {
                    txtPath.Text = ofld.FileName;
                    imgView.Source = getBitmapImage(ofld.FileName);
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            var isText = e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.Text, true);
            if (!isText)
            {
                e.CancelCommand();
                e.Handled = true;
                return;
            }

            var text = e.SourceDataObject.GetData(DataFormats.Text) as string;
            if (!isConform(text))
            {
                e.CancelCommand();
                e.Handled = true;
                return;
            }

            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                imgView.Source = getBitmapImage(text);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        void txtPath_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = txtPath.Text.Trim();
            if (String.IsNullOrWhiteSpace(text))
                return;

            if (!isConform(text))
            {
                imgView.Source = null;
                return;
            }
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                imgView.Source = getBitmapImage(text);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private bool isConform(string val)
        {
            DictionaryItem item = cboSource.SelectedItem as DictionaryItem;
            if (item == null)
                return false;

            if ("http".Equals(item.value) && val.IndexOf("http://") != 0)
                return false;
            else if ("http".Equals(item.value) && val.IndexOf("https://") != 0)
                return false;
            else if ("localhost".Equals(item.value))
            {
                if (!File.Exists(val))
                    return false;

                string ext = new FileInfo(val).Extension;
                if ("*.gif;*.jpg;*jpeg;*.tiff".IndexOf(ext) == -1)
                    return false;
            }

            return true;
        }


        private BitmapImage getBitmapImage(string val)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(val, UriKind.RelativeOrAbsolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                image.EndInit();
                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return null;
            }
        }

        void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DictionaryItem item = cboSource.SelectedItem as DictionaryItem;
            if (imgView.Source != null)
            {
                value = new ASObject();
                value["path"] = txtPath.Text.Trim();
                if(!string.IsNullOrWhiteSpace(txtWidth.Text))
                    value["width"] = txtWidth.Text.Trim();
                if (!string.IsNullOrWhiteSpace(txtHeight.Text))
                    value["height"] = txtHeight.Text.Trim();
                if (!string.IsNullOrWhiteSpace(txtAlt.Text))
                    value["alt"] = txtAlt.Text.Trim();

                if (this.HtmlEditorImageChangedEvent != null)
                    this.HtmlEditorImageChangedEvent(EventType, value);
                
            }

            this.Close();
        }
    }
}
