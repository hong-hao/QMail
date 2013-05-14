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
using ime.controls;
using System.Text.RegularExpressions;
using wos.rpc;
using wos.rpc.core;
using wos.utils;
using Newtonsoft.Json.Linq;

namespace ime.mail.controls.AccountManager
{
	/// <summary>
	/// NewAccountWindow.xaml 的交互逻辑
	/// </summary>
	public partial class NewAccountWindow : QWindow
	{
        private string EmailRegEx = @"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$";
        private ASObject _account = null;

		public NewAccountWindow()
		{
			InitializeComponent();
		}

        public ASObject Account
        {
            get { return _account; }
        }

        /// <summary>
        /// 验证是否是email
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public bool IsValidEmailAddress(string s)
        {
            Regex regEx = new Regex(EmailRegEx, RegexOptions.IgnoreCase);
            return regEx.IsMatch(s);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            btnOK.Click -= OnOK;
            btnCancel.Click -= OnCancel;
        }

		private void OnOK(object sender, RoutedEventArgs e)
		{
            if (!IsValidEmailAddress(txtAccount.Text.Trim()))
            {
                ime.controls.MessageBox.Show("请输入准确的邮件账号", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (String.IsNullOrWhiteSpace(txtName.Text))
            {
                ime.controls.MessageBox.Show("请输入显示名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                object value = Remoting.call("MailManager.existMailAccount", new object[] { txtAccount.Text.Trim() });
                if (value == null)
                    throw new Exception("服务器连接被断开");

                ASObject info = JsonUtil.toASObject(JObject.Parse(value as string));
                if (info.getBoolean("isExist"))
                {
                    if (info.getBoolean("is_enabled"))
                        throw new Exception("账号已存在");
                    else
                    {
                        if (ime.controls.MessageBox.Show("账号已存在，但尚未启用，是否现在启用？", "提示",
                            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                        {
                            value = Remoting.call("MailManager.enableMailAccount", new object[] { txtAccount.Text.Trim() });
                            _account = JsonUtil.toASObject(JObject.Parse(value as string));
                        }
                    }
                }
                else
                {
                    _account = new ASObject();
                    _account["account"] = txtAccount.Text.Trim();
                    _account["name"] = txtName.Text.Trim();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
                ime.controls.MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            this.DialogResult = true;
            this.Close();
		}

		private void OnCancel(object sender, RoutedEventArgs e)
		{
            this.DialogResult = false;
            this.Close();
		}
	}
}
