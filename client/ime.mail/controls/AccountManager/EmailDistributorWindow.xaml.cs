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
using wos.rpc.core;
using wos.utils;
using ime.controls;

namespace ime.mail.controls
{
    /// <summary>
    /// EmailDistributorWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EmailDistributorWindow : QWindow
    {
        private ASObject distribution_policy = new ASObject();
        private ASObject _acoount;

        private List<DictionaryItem> distributionModes = new List<DictionaryItem>()
        {
            new DictionaryItem(){label = "动态分配",value = "dynamic"},
            new DictionaryItem(){label = "静态分配",value = "static"}
        };

        public EmailDistributorWindow()
        {
            InitializeComponent();

            cboDistributionMode.ItemsSource = distributionModes;
            cboDistributionMode.DisplayMemberPath = "label";
            cboDistributionMode.SelectionChanged += cboDistributionMode_SelectionChanged;

            btnOK.Click += btnOK_Click;
            btnDistributor.Click += btnDistributor_Click;

            Masking.SetMask(txtOnlineUnreadTimeout, @"^[0-9]+$");
            Masking.SetMask(txtUnhandledTimeout, @"^[0-9]+$");
            Masking.SetMask(txtOfflineTimeout, @"^[0-9]+$");
            Masking.SetMask(txtOldUncontactTimeout, @"^[0-9]+$");
            Masking.SetMask(txtOldUnhandledTimeout, @"^[0-9]+$");
            Masking.SetMask(txtOldUnreadTimeout, @"^[0-9]+$");
        }

        public ASObject Account
        {
            set
            {
                if (value == null)
                    return;
                _acoount = value;
                ASObject o = value.getObject("distribution_policy");
                if (o != null)
                    ASObjectUtil.copyTo(o, distribution_policy);
            }
            get { return _acoount; }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            txtDistributor.Text = convertDistributorsString(getMultipleList(distribution_policy));

            if (distribution_policy.getString("distribution_mode") == "dynamic"
                || distribution_policy.getString("distribution_mode") == null
                || distribution_policy.getString("distribution_mode") == "")
                cboDistributionMode.SelectedIndex = 0;
            else if (distribution_policy.getString("distribution_mode") == "static")
                cboDistributionMode.SelectedIndex = 1;

            chkHideEmail.IsChecked = distribution_policy.getBoolean("is_hide_email");
            ASObject new_customer_policy = distribution_policy.getObject("new_customer_policy");
            if (new_customer_policy != null)
            {
                if (new_customer_policy.get("online_unread_timeout") != null)
                {
                    txtOnlineUnreadTimeout.Text = new_customer_policy.getString("online_unread_timeout");
                }
                else
                    chkOnlineUnreadTimeout.IsChecked = false;
                if (new_customer_policy.get("unhandled_timeout") != null)
                {
                    txtUnhandledTimeout.Text = new_customer_policy.getString("unhandled_timeout");
                }
                else
                    chkUnhandledTimeout.IsChecked = false;
                if (new_customer_policy.get("offline_timeout") != null)
                {
                    txtOfflineTimeout.Text = new_customer_policy.getString("offline_timeout");
                }
                else
                    chkOfflineTimeout.IsChecked = false;
            }

            ASObject old_customer_policy = distribution_policy.getObject("old_customer_policy");
            if (old_customer_policy != null)
            {
                if (old_customer_policy.get("unread_timeout") != null)
                {
                    txtOldUnreadTimeout.Text = old_customer_policy.getString("unread_timeout");
                }
                else
                    chkOldUnreadTimeout.IsChecked = false;
                if (old_customer_policy.get("unhandled_timeout") != null)
                {
                    txtOldUnhandledTimeout.Text = old_customer_policy.getString("unhandled_timeout");
                }
                else
                    chkOldUnhandledTimeout.IsChecked = false;
                if (old_customer_policy.get("uncontact_timeout") != null)
                {
                    txtOldUncontactTimeout.Text = old_customer_policy.getString("uncontact_timeout");
                }
                else
                    chkOldUncontactTimeout.IsChecked = false;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            cboDistributionMode.SelectionChanged -= cboDistributionMode_SelectionChanged;

            btnOK.Click -= btnOK_Click;
            btnDistributor.Click -= btnDistributor_Click;
        }

        void cboDistributionMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DictionaryItem item = cboDistributionMode.SelectedItem as DictionaryItem;
            if (item == null || Account == null)
                return;

            distribution_policy["distribution_mode"] = item.value;
            if (item.value.ToString() == "dynamic")
            {
                txtDistributionMode.Text = "根据设定条件，邮件的处理人按规则进行动态转移。";
                gbNewUser.IsEnabled = true;
                gbOldUser.IsEnabled = true;
            }
            else if (item.value.ToString() == "static")
            {
                txtDistributionMode.Text = "邮件的处理人始终保持不变。";
                gbNewUser.IsEnabled = false;
                gbOldUser.IsEnabled = false;
            }
        }

        void btnDistributor_Click(object sender, RoutedEventArgs e)
        {
            if (Account == null)
                return;

            PrincipalSelectWindow pwin = new PrincipalSelectWindow();
            pwin.setRootPath("/", false);
            pwin.multipleValue = getMultipleList(distribution_policy);
            pwin.Owner = this;
            if (pwin.ShowDialog() == true)
            {
                distribution_policy["distributor"] = convertDistributors(pwin.multipleValue);
                txtDistributor.Text = convertDistributorsString(convertDistributors(pwin.multipleValue));
            }
        }

        private List<ASObject> getMultipleList(ASObject policy)
        {
            List<ASObject> multipleList = null;
            object distributors = policy.get("distributor");
            if (distributors != null && distributors is object[])
            {
                multipleList = new List<ASObject>();
                foreach (object o in (object[])distributors)
                {
                    if (o is ASObject)
                    {
                        multipleList.Add((ASObject)o);
                    }
                }
            }
            else if (distributors != null && distributors is List<ASObject>)
            {
                multipleList = new List<ASObject>();
                foreach (ASObject o in (List<ASObject>)distributors)
                {
                    multipleList.Add(o);
                }
            }

            return multipleList;
        }

        /// <summary>
        /// distributor : [{id=xxx, name:"", loginId:"xxx@xxx.xxx"}],		//管理人员
        /// </summary>
        /// <param name="multipleValue"></param>
        /// <returns></returns>
        private List<ASObject> convertDistributors(List<ASObject> multipleValue)
        {
            if (multipleValue == null || multipleValue.Count == 0)
                return null;
            List<ASObject> distributors = new List<ASObject>();
            foreach (ASObject p in multipleValue)
            {
                ASObject o = new ASObject();
                o["id"] = p.get("id");
                o["name"] = p.get("name");
                o["loginId"] = p.getString("loginId") + "@" + p.getString("domainName");
                distributors.Add(o);
            }

            return distributors;
        }
        private string convertDistributorsString(List<ASObject> distributors)
        {
            if (distributors == null || distributors.Count == 0)
                return "";
            StringBuilder sb = new StringBuilder();
            foreach (ASObject m in distributors)
            {
                sb.Append(m.getString("name")).Append(",");
            }
            if (sb.ToString().LastIndexOf(",") != -1)
                sb.Remove(sb.ToString().LastIndexOf(","), 1);

            return sb.ToString();
        }

        void btnOK_Click(object sender, RoutedEventArgs e)
        {
            distribution_policy["is_hide_email"] = chkHideEmail.IsChecked.Value;
            DictionaryItem item = cboDistributionMode.SelectedItem as DictionaryItem;
            if (item.value.ToString() == "dynamic")
            {
                ASObject new_customer_policy = new ASObject();
                if (chkOnlineUnreadTimeout.IsChecked.Value)
                {
                    if (String.IsNullOrWhiteSpace(txtOnlineUnreadTimeout.Text))
                    {
                        ime.controls.MessageBox.Show("请设置新客户邮件未读转移的超时时间", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        txtOnlineUnreadTimeout.Focus();
                        return;
                    }
                    new_customer_policy["online_unread_timeout"] = txtOnlineUnreadTimeout.Text.Trim();
                }
                if (chkUnhandledTimeout.IsChecked.Value)
                {
                    if (String.IsNullOrWhiteSpace(txtUnhandledTimeout.Text))
                    {
                        ime.controls.MessageBox.Show("请设置新客户对已查看邮件因未及时处理而转移的超时时间", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        txtUnhandledTimeout.Focus();
                        return;
                    }
                    new_customer_policy["unhandled_timeout"] = txtUnhandledTimeout.Text.Trim();
                }
                if (chkOfflineTimeout.IsChecked.Value)
                {
                    if (String.IsNullOrWhiteSpace(txtOfflineTimeout.Text))
                    {
                        ime.controls.MessageBox.Show("请设置新客户因晚于其他用户上线而被转移邮件的超时时间", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        txtOfflineTimeout.Focus();
                        return;
                    }
                    new_customer_policy["offline_timeout"] = txtOfflineTimeout.Text.Trim();
                }
                if (new_customer_policy.Count > 0)
                    distribution_policy["new_customer_policy"] = new_customer_policy;


                ASObject old_customer_policy = new ASObject();
                if (chkOldUnreadTimeout.IsChecked.Value)
                {
                    if (String.IsNullOrWhiteSpace(txtOldUnreadTimeout.Text))
                    {
                        ime.controls.MessageBox.Show("请设置老用户因未读邮件转移的超时时间", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        txtOldUnreadTimeout.Focus();
                        return;
                    }
                    old_customer_policy["unread_timeout"] = txtOldUnreadTimeout.Text.Trim();
                }
                if (chkOldUnhandledTimeout.IsChecked.Value)
                {
                    if (String.IsNullOrWhiteSpace(txtOldUnhandledTimeout.Text))
                    {
                        ime.controls.MessageBox.Show("请设置老用户因未处理邮件转移的超时时间", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        txtOldUnhandledTimeout.Focus();
                        return;
                    }
                    old_customer_policy["unhandled_timeout"] = txtOldUnhandledTimeout.Text.Trim();
                }
                if (chkOldUncontactTimeout.IsChecked.Value)
                {
                    if (String.IsNullOrWhiteSpace(txtOldUncontactTimeout.Text))
                    {
                        ime.controls.MessageBox.Show("请设置老用户因未联系而转移跟踪人员的超时时间", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        txtOldUncontactTimeout.Focus();
                        return;
                    }
                    old_customer_policy["uncontact_timeout"] = txtOldUncontactTimeout.Text.Trim();
                }
                if (old_customer_policy.Count > 0)
                    distribution_policy["old_customer_policy"] = old_customer_policy;
            }

            _acoount["distribution_policy"] = distribution_policy;

            this.DialogResult = true;
            this.Close();
        }
    }
}
