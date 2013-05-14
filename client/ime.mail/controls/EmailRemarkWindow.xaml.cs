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

namespace ime.mail.controls
{
    /// <summary>
    /// EmailRemarkWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EmailRemarkWindow : Window
    {
        public EmailRemarkWindow()
        {
            InitializeComponent();

            btnOK.Click += btnOK_Click;
            btnCancel.Click += btnCancel_Click;
        }

        public string Text
        {
            set { txtText.Text = value; }
            get { return txtText.Text.Trim(); }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            btnOK.Click -= btnOK_Click;
            btnCancel.Click -= btnCancel_Click;
        }

        void btnOK_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
