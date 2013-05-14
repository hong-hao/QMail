using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using ime.controls.QWindow;
using ime.mail.controls.AccountManager;
using wos.rpc;
using System.Collections.ObjectModel;
using wos.rpc.core;
using ime.controls;
using wos.utils;
using Newtonsoft.Json.Linq;
using ime.mail.Utils;
using wos.library;

namespace ime.mail.controls
{
	/// <summary>
	/// AccountManagerWindow.xaml 的交互逻辑
	/// </summary>
	public partial class AccountManagerWindow : QWindow, IRemotingHandler
	{
        private List<ASObject> createList = new List<ASObject>();
        private List<ASObject> updateList = new List<ASObject>();
        private List<ASObject> removeList = new List<ASObject>();
        private List<ASObject> _initImportList = new List<ASObject>();

        private ObservableCollection<ASObject> Accounts = new ObservableCollection<ASObject>();
        private List<String> recvServerTypes = new List<string>()
        {
            "POP3","IMAP"
        };

		public AccountManagerWindow()
		{
			InitializeComponent();

            Masking.SetMask(txtRecvPort, @"^[0-9]+$");
            Masking.SetMask(txtSendPort, @"^[0-9]+$");
		}

        public List<ASObject> InitImportList
        {
            get { return _initImportList; }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            listAccount.ItemsSource = Accounts;
            listAccount.SelectionChanged += listAccount_SelectionChanged;

            cboRecvServerType.ItemsSource = recvServerTypes;
            cboRecvServerType.SelectionChanged += cboRecvServerType_SelectionChanged;

            password.PasswordChanged += password_PasswordChanged;
            txtRecvAddress.TextChanged += txtRecvAddress_TextChanged;
            txtRecvPort.TextChanged += txtRecvPort_TextChanged;
            txtSendAddress.TextChanged += txtSendAddress_TextChanged;
            txtSendPort.TextChanged += txtSendPort_TextChanged;
            txtName.TextChanged += txtName_TextChanged;

            chkRecvSSL.Click += chkRecvSSL_Click;
            chkSendSSL.Click += chkSendSSL_Click;

            btnOK.Click += btnOK_Click;

            try
            {
                 Remoting.call("MailManager.getAllMailAccounts", new object[] { }, this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
                this.Dispatcher.BeginInvoke((System.Action)delegate
                {
                    ime.controls.MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            if (createList.Count > 0 || updateList.Count > 0 || removeList.Count > 0)
            {
                if (ime.controls.MessageBox.Show("数据已经变更，确认关闭窗口吗？", "提示",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            btnAddAccount.Click -= OnAddAccount;
            btnDelAccount.Click -= OnDelAccount;
            btnAddUser.Click -= OnAddUser;
            btnSetPolicy.Click -= OnSetPolicy;

            listAccount.SelectionChanged -= listAccount_SelectionChanged;
            cboRecvServerType.SelectionChanged -= cboRecvServerType_SelectionChanged;

            password.PasswordChanged -= password_PasswordChanged;
            txtRecvAddress.TextChanged -= txtRecvAddress_TextChanged;
            txtRecvPort.TextChanged -= txtRecvPort_TextChanged;
            txtSendAddress.TextChanged -= txtSendAddress_TextChanged;
            txtSendPort.TextChanged -= txtSendPort_TextChanged;

            chkRecvSSL.Click -= chkRecvSSL_Click;
            chkSendSSL.Click -= chkSendSSL_Click;

            btnOK.Click -= btnOK_Click;
        }

        void cboRecvServerType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            object[] result = e.AddedItems as object[];
            if (result == null)
                return;

            lbRecvServer.Content = "接收邮件服务器地址(" + result[0] + ")：";
            ASObject account = listAccount.SelectedItem as ASObject;
            if (account == null)
                return;
            long recv_type = (result[0] as string) == "POP3" ? 1 : 2;
            if (account.getLong("recv_type") != recv_type)
            {
                account["recv_type"] = recv_type;
                AddUpdate(account);
            }
        }

        void listAccount_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ASObject account = listAccount.SelectedItem as ASObject;
            if (account == null)
                return;
            txtEMail.Text = account.getString("account");
            txtName.Text = account.getString("name");
            if (account.getString("password") != null && account.getString("password") != "")
            {
                password.Password = PassUtil.Decrypt(account.getString("password"));
            }
            else
            {
                password.Password = "";
            }
            if (account.getLong("recv_type") == 1 || account.getLong("recv_type") == 0)
            {
                cboRecvServerType.SelectedIndex = 0;
                account["recv_type"] = 1;
            }
            else if (account.getLong("recv_type") == 2)
            {
                cboRecvServerType.SelectedIndex = 1;
                account["recv_type"] = 2;
            }

            account["recv_address"] = txtRecvAddress.Text = account.getString("recv_address", "pop.sina.com");
            account["recv_port"] = txtRecvPort.Text = account.getString("recv_port", "110");
            account["send_address"] = txtSendAddress.Text = account.getString("send_address", "smtp.sina.com");
            account["send_port"] = txtSendPort.Text = account.getString("send_port", "25");

            account["is_recv_ssl"] = chkRecvSSL.IsChecked = account.getBoolean("is_recv_ssl");
            account["is_send_ssl"] = chkSendSSL.IsChecked = account.getBoolean("is_send_ssl");

            if (chkRecvSSL.IsChecked.Value && account.getString("recv_port", "110") == "110")
                txtRecvPort.Text = "995";
            if (chkSendSSL.IsChecked.Value && account.getString("send_port", "25") == "25")
                txtSendPort.Text = "465";

            ASObject distribution_policy = account.getObject("distribution_policy");
            txtExcepUsers.Text = convertManagersString(getMultipleList(distribution_policy));
        }

        void txtName_TextChanged(object sender, TextChangedEventArgs e)
        {
            ASObject account = listAccount.SelectedItem as ASObject;
            if (account == null)
                return;

            string name = txtName.Text.Trim();

            if (account.getString("name") != name)
            {
                account["name"] = name;
                AddUpdate(account);
            }
        }

        void password_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ASObject account = listAccount.SelectedItem as ASObject;
            if (account == null)
                return;
            if (String.IsNullOrWhiteSpace(password.Password))
                return;
            string pwd = password.Password;
            pwd = PassUtil.Encrypt(pwd);
            if (account.getString("password") != pwd)
            {
                account["password"] = pwd;
                AddUpdate(account);
            }
        }

        void txtSendPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            ASObject account = listAccount.SelectedItem as ASObject;
            if (account == null)
                return;
            string send_port = txtSendPort.Text.Trim();
            if (account.getString("send_port") != send_port)
            {
                account["send_port"] = NumberUtil.toLong(send_port);
                AddUpdate(account);
            }
        }

        void txtSendAddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            ASObject account = listAccount.SelectedItem as ASObject;
            if (account == null)
                return;

            string send_address = txtSendAddress.Text.Trim();
            if (account.getString("send_address") != send_address)
            {
                account["send_address"] = send_address;
                AddUpdate(account);
            }
        }

        void txtRecvPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            ASObject account = listAccount.SelectedItem as ASObject;
            if (account == null)
                return;

            string recv_port = txtRecvPort.Text.Trim();
            if (account.getString("recv_port") != recv_port)
            {
                account["recv_port"] = NumberUtil.toLong(recv_port);
                AddUpdate(account);
            }
        }

        void txtRecvAddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            ASObject account = listAccount.SelectedItem as ASObject;
            if (account == null)
                return;

            string recv_address = txtRecvAddress.Text.Trim();
            if (account.getString("recv_address") != recv_address)
            {
                account["recv_address"] = recv_address;
                AddUpdate(account);
            }
        }

        void chkSendSSL_Click(object sender, RoutedEventArgs e)
        {
            ASObject account = listAccount.SelectedItem as ASObject;
            if (account == null)
                return;
            bool is_send_ssl = chkSendSSL.IsChecked.Value;
            if (account.getBoolean("is_send_ssl") != is_send_ssl)
            {
                account["is_send_ssl"] = is_send_ssl;
                AddUpdate(account);
                if(is_send_ssl)
                    txtSendPort.Text = "465";
                else
                    txtSendPort.Text = "25";
            }
        }

        void chkRecvSSL_Click(object sender, RoutedEventArgs e)
        {
            ASObject account = listAccount.SelectedItem as ASObject;
            if (account == null)
                return;
            bool is_recv_ssl = chkRecvSSL.IsChecked.Value;
            if (account.getBoolean("is_recv_ssl") != is_recv_ssl)
            {
                account["is_recv_ssl"] = is_recv_ssl;
                AddUpdate(account);
                if(is_recv_ssl)
                    txtRecvPort.Text = "995";
                else
                    txtRecvPort.Text = "110";
            }
        }

		private void OnSetPolicy(object sender, RoutedEventArgs e)
		{
            ASObject account = listAccount.SelectedItem as ASObject;
            if (account == null)
                return;

            EmailDistributorWindow ewin = new EmailDistributorWindow();
            ewin.Owner = this;
            ewin.Account = account;
            if (ewin.ShowDialog() == true)
            {
                AddUpdate(account);
            }
		}

		private void OnAddUser(object sender, RoutedEventArgs e)
		{
            ASObject account = listAccount.SelectedItem as ASObject;
            if (account == null)
                return;

            ASObject distribution_policy = account.getObject("distribution_policy");
                
            PrincipalSelectWindow pwin = new PrincipalSelectWindow();
            pwin.setRootPath("/", false);
            pwin.multipleValue = getMultipleList(distribution_policy);
            pwin.Owner = this;
            if (pwin.ShowDialog() == true)
            {
                if (distribution_policy == null)
                    distribution_policy = new ASObject();
                account["distribution_policy"] = distribution_policy;
                distribution_policy["managers"] = convertManagers(pwin.multipleValue);
                string excepUsers = convertManagersString(convertManagers(pwin.multipleValue));
                if (excepUsers != txtExcepUsers.Text.Trim())
                {
                    txtExcepUsers.Text = excepUsers;
                    AddUpdate(account);
                }
            }
		}

        private List<ASObject> getMultipleList(ASObject policy)
        {
            if (policy == null)
                return null;
            List<ASObject> multipleList = null;
            object managers = policy.get("managers");
            if (managers != null && managers is object[])
            {
                multipleList = new List<ASObject>();
                foreach (object o in (object[])managers)
                {
                    if (o is ASObject)
                    {
                        multipleList.Add((ASObject)o);
                    }
                }
            }
            else if (managers != null && managers is List<ASObject>)
            {
                multipleList = new List<ASObject>();
                foreach (ASObject o in (List<ASObject>)managers)
                {
                    multipleList.Add(o);
                }
            }

            return multipleList;
        }

        /// <summary>
        /// managers : [{id=xxx, name:"", loginId:"xxx@xxx.xxx"}],		//管理人员
        /// </summary>
        /// <param name="multipleValue"></param>
        /// <returns></returns>
        private List<ASObject> convertManagers(List<ASObject> multipleValue)
        {
            if (multipleValue == null || multipleValue.Count == 0)
                return null;
            List<ASObject> managers = new List<ASObject>();
            foreach (ASObject p in multipleValue)
            {
                ASObject o = new ASObject();
                o["id"] = p.get("id");
                o["name"] = p.get("name");
                o["loginId"] = p.getString("loginId") + "@" + p.getString("domainName");
                managers.Add(o);
            }

            return managers;
        }
        private string convertManagersString(List<ASObject> managers)
        {
            if (managers == null || managers.Count == 0)
                return "";
            StringBuilder sb = new StringBuilder();
            foreach (ASObject m in managers)
            {
                sb.Append(m.getString("name")).Append(",");
            }
            if (sb.ToString().LastIndexOf(",") != -1)
                sb.Remove(sb.ToString().LastIndexOf(","), 1);

            return sb.ToString();
        }

		private void OnAddAccount(object sender, RoutedEventArgs e)
		{
			NewAccountWindow win = new NewAccountWindow();
			win.Owner = this;
            if (win.ShowDialog() == true)
            {
                ASObject account = win.Account;
                if (account == null)
                    return;

                Accounts.Add(account);

                listAccount.SelectedItem = account;
                if(!account.ContainsKey("id"))
                    createList.Add(account);
            }
		}

        private void OnDelAccount(object sender, RoutedEventArgs e)
        {
            ASObject account = listAccount.SelectedItem as ASObject;
            if (account == null)
                return;
            if (ime.controls.MessageBox.Show("确定要移除账号【" + account.getString("name") + "】吗？", "提示",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Accounts.Remove(account);
                if (Accounts.Count > 0)
                {
                    listAccount.SelectedIndex = 0;
                }
                else
                {
                    listAccount.SelectedIndex = -1;

                    txtEMail.Text = "";
                    txtName.Text = "";
                    password.Password = "";
                    cboRecvServerType.SelectedIndex = 0;

                    txtRecvAddress.Text = "";
                    txtRecvPort.Text = "";
                    txtSendAddress.Text = "";
                    txtSendPort.Text = "";
                    txtExcepUsers.Text = "";

                    chkRecvSSL.IsChecked = false;
                    chkSendSSL.IsChecked = false;
                }

                if (account.ContainsKey("id"))
                {
                    removeList.Add(account);
                    if (createList.Contains(account))
                        createList.Remove(account);
                    if (updateList.Contains(account))
                        updateList.Remove(account);
                }
            }
        }

        private void AddUpdate(ASObject account)
        {
            if (account.ContainsKey("id") && !updateList.Contains(account))
                updateList.Add(account);
        }

        private bool IsExist(string account)
        {
            IEnumerable<ASObject> list = Accounts.Where(ac => ac.getString("account") == account);
            if (list.Count() > 0)
                return true;
            else
                return false;
        }

        private void cb_getMailAccounts(object result)
        {
            if (result == null)
                return;

            Accounts.Clear();
            if (!(result is string))
                return;
            JArray array = JArray.Parse(result as string);
            object[] record = JsonUtil.toRawArray(array);
            foreach (object o in record)
            {
                if (o is ASObject)
                    Accounts.Add((ASObject)o);
            }
        }

        public void onRemotingCallback(string callUID, string methodName, object result, AsyncOption option)
        {
            switch (methodName)
            {
                case "MailManager.getAllMailAccounts":
                    cb_getMailAccounts(result);
                    break;
            }
        }

		public void onRemotingException(string callUID, string methodName, string message, string code, wos.rpc.core.ASObject exception, AsyncOption option)
        {
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                ime.controls.MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        void btnOK_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ChangeData();

                if (_initImportList.Count > 0)
                {
                    if (ime.controls.MessageBox.Show("确认对新增帐户执行邮件初始导入？", "提示", MessageBoxButton.YesNo,
                        MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        this.DialogResult = true;
                    }
                }

                this.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
                ime.controls.MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangeData()
        {
            if (createList.Count > 0)
            {
                try
                {
                    foreach (ASObject account in createList)
                    {
                        ASObject policy = new ASObject();
                        ASObject ac = new ASObject();
                        ASObjectUtil.copyTo(account, ac);
                        if (ac.ContainsKey("distribution_policy"))
                        {
                            ac.Remove("distribution_policy");
                            policy["distribution_policy"] = account.getObject("distribution_policy");
                        }

                        Remoting.call("MailManager.createMailAccount", new object[] { ac, ASObjectUtil.toJObject(policy).ToString() });

                        _initImportList.Add(account);
                    }
                    createList.Clear();
                }
                catch (Exception ex)
                {
                    createList.Clear();
                    throw ex;
                }
            }

            if (updateList.Count > 0)
            {
                try
                {
                    foreach (ASObject account in updateList)
                    {
                        ASObject policy = new ASObject();
                        ASObject ac = new ASObject();
                        ASObjectUtil.copyTo(account, ac);
                        if (ac.ContainsKey("distribution_policy"))
                        {
                            ac.Remove("distribution_policy");
                            policy["distribution_policy"] = account.getObject("distribution_policy");
                        }

                        Remoting.call("MailManager.updateMailAccount", new object[] { ac, ASObjectUtil.toJObject(policy).ToString() });
                    }

                    updateList.Clear();
                }
                catch (Exception ex)
                {
                    updateList.Clear();
                    throw ex;
                }
            }

            if (removeList.Count > 0)
            {
                try
                {
                    foreach (ASObject account in removeList)
                    {
                        Remoting.call("MailManager.removeMailAccount", new object[] { account.getLong("id") });
                    }

                    removeList.Clear();
                }
                catch (Exception ex)
                {
                    removeList.Clear();
                    throw ex;
                }
            }

            Remoting.call("MailManager.reloadMailAccount", new object[] { }, this);
        }
    }

    public class EmailAccountDataMultiValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values == null)
                return "";
           
            ASObject account = values[1] as ASObject;
            if (account == null)
                return "";
            return account.getString("name") + "(" + account.getString("account") + ")";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
