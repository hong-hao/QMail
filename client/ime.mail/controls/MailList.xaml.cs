using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml.Linq;
using ime.data.Grouping;
using ime.mail.views;
using ime.mail.Worker;
using wos.collections;
using wos.extensions;
using wos.library;
using wos.rpc;
using wos.rpc.core;
using wos.utils;

namespace ime.mail.controls
{
    /// <summary>
    /// MailList.xaml 的交互逻辑
    /// </summary>
    public partial class MailList : UserControl, IRemotingHandler
    {
        public delegate void SelectedChangedHandler(ASObject selectedMail);
        public event SelectedChangedHandler SelectedChangedEvent;

        private MailDateProvider dateProvider = new MailDateProvider();
        private FieldGroupProvider fieldGroupProvider = new FieldGroupProvider();
        private UnhandledMailProvider unhandledMailProvider = new UnhandledMailProvider();
        private DsBoxMailProvider dsBoxMailProvider = new DsBoxMailProvider();

        private string currentFolder = "INBOX";
        private XElement parentXml = null;
        private XElement folderXml = null;

        private bool _isSearch = false;//是否是搜索

        private List<DictionaryItem> searchTargets = new List<DictionaryItem>()
        {
            new DictionaryItem(){ label = "本地搜索", value = 1},
            new DictionaryItem(){ label = "全域搜索", value = 2}
        };

        private List<DictionaryItem> searchTypes = new List<DictionaryItem>()
        {
            new DictionaryItem(){ label = "按标题搜索", value = 1},
            new DictionaryItem(){ label = "按内容搜索", value = 2},
            new DictionaryItem(){ label = "按发件人搜索", value = 3}
        };

        public MailList()
        {
            InitializeComponent();

            dataGrid.LoadingRow += DataGrid_LoadingRow;
            dataGrid.SelectionChanged += dataGrid_SelectionChanged;
            dataGrid.Sorting += dataGrid_Sorting;
            dataGrid.MouseDoubleClick += dataGrid_MouseDoubleClick;

            cboSearch.ItemsSource = searchTargets;
            cboSearch.DisplayMemberPath = "label";
            cboSearch.SelectedIndex = 0;

            cboSearchType.ItemsSource = searchTypes;
            cboSearchType.DisplayMemberPath = "label";
            cboSearchType.SelectedIndex = 0;

            txtSearch.PreviewKeyUp += onSearch;
            txtSearch.SearchEvent += txtSearch_SearchEvent;
            txtSearch.ClearSearchEvent += txtSearch_ClearSearchEvent;

            MailWorker.instance.MailEvent += OnMailEvent;

            this.Unloaded += MailList_Unloaded;
        }

        void MailList_Unloaded(object sender, RoutedEventArgs e)
        {
            dataGrid.LoadingRow -= DataGrid_LoadingRow;
            dataGrid.SelectionChanged -= dataGrid_SelectionChanged;
            dataGrid.Sorting -= dataGrid_Sorting;
            dataGrid.MouseDoubleClick -= dataGrid_MouseDoubleClick;

            txtSearch.PreviewKeyUp -= onSearch;
            txtSearch.SearchEvent -= txtSearch_SearchEvent;
            txtSearch.ClearSearchEvent -= txtSearch_ClearSearchEvent;

            MailWorker.instance.MailEvent -= OnMailEvent;
        }

        public bool IsSearch
        {
            get { return _isSearch; }
        }

        void OnMailEvent(MailWorker.Event eventType, ASObject mail, string[] updateFields)
        {
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                if (_isSearch)
                    return;
                if (mail == null)
                {
                    this.Dispatcher.BeginInvoke((System.Action)delegate
                    {
                        dataGrid.Items.Refresh();
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    return;
                }
                if (currentFolder == mail.getString("folder"))
                {
                    Collection<ASObject> data = dataGrid.ItemsSource as Collection<ASObject>;
                    if (data == null)
                        return;
                    if (eventType == MailWorker.Event.Create)
                    {
                        if (data is GroupCollection<ASObject>)
                            ((GroupCollection<ASObject>)data).AddToGroup(mail);
                        else
                            data.Add(mail);
                    }
                    else if (eventType == MailWorker.Event.Delete)
                    {
                        if (parentXml != null && parentXml.AttributeValue("name") == "AllMailBox")
                            return;
                        if (data is GroupCollection<ASObject>)
                            ((GroupCollection<ASObject>)data).RemoveFromGroup(mail);
                        else
                            data.Remove(mail);
                    }

                    this.Dispatcher.BeginInvoke((System.Action)delegate
                    {
                        dataGrid.Items.Refresh();
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        string is_seenSort = "asc";
        string prioritySort = "asc";
        string dateSort = "asc";
        void dataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (parentXml == null || folderXml == null || _isSearch)
            {
                e.Handled = true;
                return;
            }
            if (parentXml.AttributeValue("name") == "AllMailBox")
            {
                if (e.Column.Header.ToString() == "发件人")
                {
                    dataGrid.ItemsSource = fieldGroupProvider.GetMails(currentFolder, "contact_mail", "mail_from_label");
                }
                else if (e.Column.Header.ToString() == "日期")
                {
                    dataGrid.ItemsSource = dateProvider.GetMails(currentFolder);
                }
                else if (e.Column.Header is Image)
                {
                    Image header = e.Column.Header as Image;
                    if (header.ToolTip.ToString() == "是否已阅读")
                    {
                        dataGrid.ItemsSource = GetMails(currentFolder, "is_seen", is_seenSort);
                        if (is_seenSort == "asc")
                            is_seenSort = "desc";
                        else is_seenSort = "asc";
                    }
                    else if (header.ToolTip.ToString() == "优先级")
                    {
                        dataGrid.ItemsSource = GetMails(currentFolder, "priority", prioritySort);
                        if (prioritySort == "asc")
                            prioritySort = "desc";
                        else prioritySort = "asc";
                    }
                    else if (header.ToolTip.ToString() == "客户等级")
                    {
                        var comparer = new DataComparer<ASObject>(delegate(ASObject p, ASObject p1)
                        {
                            return p1.getInt("$group_field_value", -1).CompareTo(p.getInt("$group_field_value", -1));
                        });
                        GroupCollection<ASObject> result = fieldGroupProvider.GetMails(currentFolder, "customer_grade");
                        List<ASObject> sorted = result.OrderBy(p => p, comparer).ToList();

                        for (int i = 0; i < sorted.Count(); i++)
                            result.Move(result.IndexOf(sorted[i]), i);

                        dataGrid.ItemsSource = result;
                    }
                    else if (header.ToolTip.ToString() == "是否已处理")
                    {
                        dataGrid.ItemsSource = fieldGroupProvider.GetMails(currentFolder, "handle_action");
                    }
                }
            }
            else if (parentXml.AttributeValue("name") == "Unhandled")
            {
                if (e.Column.Header.ToString() == "日期")
                {
                    dataGrid.ItemsSource = unhandledMailProvider.GetMails(folderXml.AttributeValue("value"), "mail_date", dateSort);
                    if (dateSort == "asc")
                        dateSort = "desc";
                    else dateSort = "asc";
                }
                else if (e.Column.Header is Image)
                {
                    Image header = e.Column.Header as Image;
                    if (header.ToolTip.ToString() == "是否已阅读")
                    {
                        dataGrid.ItemsSource = unhandledMailProvider.GetMails(folderXml.AttributeValue("value"), "is_seen", is_seenSort);
                        if (is_seenSort == "asc")
                            is_seenSort = "desc";
                        else is_seenSort = "asc";
                    }
                    else if (header.ToolTip.ToString() == "优先级")
                    {
                        dataGrid.ItemsSource = unhandledMailProvider.GetMails(folderXml.AttributeValue("value"), "priority", prioritySort);
                        if (prioritySort == "asc")
                            prioritySort = "desc";
                        else prioritySort = "asc";
                    }
                }
            }

            e.Handled = true;
        }

        private class DataComparer<T> : IComparer<T>
        {
            private readonly Comparison<T> comparison;

            public DataComparer(Comparison<T> comparison)
            {
                this.comparison = comparison;
            }

            #region IComparer<T> Members

            public int Compare(T x, T y)
            {
                return comparison.Invoke(x, y);
            }

            #endregion
        }

        void dataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ASObject mail = dataGrid.SelectedItem as ASObject;
            if (mail == null || mail is ASObjectGroup)
                return;
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                if (SelectedChangedEvent != null)
                    SelectedChangedEvent(mail);

                this.Dispatcher.BeginInvoke((System.Action)delegate
                {
                    if (_isSearch)
                        return;
                    object is_seen = mail["is_seen"];

                    if (!IsBool(is_seen))
                    {
                        mail["is_seen"] = true;
                        MailWorker.instance.dispatchMailEvent(MailWorker.Event.Update, mail, new string[] { "is_seen" });
                    }
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        void dataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridRow row = WPFUtil.FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row == null)
                return;
            ASObject mail = row.DataContext as ASObject;
            if (mail == null || mail is ASObjectGroup)
                return;

            if (_isSearch)
            {
                MailViewWindow win = new MailViewWindow();
                win.Mail = mail;
                win.Editable = false;
                win.Owner = WPFUtil.FindAncestor<Window>(this);
                win.Show();
            }
            else
            {
                if (mail.getString("folder") == "DRAFT")
                {
                    MailSendWindow win = new MailSendWindow();
                    win.Mail_Type = MailSendWindow.MailType.Draft;
                    win.Mail = mail;
                    win.Owner = WPFUtil.FindAncestor<Window>(this);
                    win.Show();
                }
                else if (mail.getString("folder") == "DSBOX")
                {
                    if (mail.getLong("reviewer_id") == Desktop.instance.loginedPrincipal.id)
                    {
                        MailSendWindow win = new MailSendWindow();
                        win.Mail_Type = MailSendWindow.MailType.Dsbox;
                        win.Mail = mail;
                        win.Owner = WPFUtil.FindAncestor<Window>(this);
                        win.Show();
                    }
                    else
                    {
                        MailViewWindow win = new MailViewWindow();
                        win.Mail = mail;
                        win.Editable = false;
                        win.Owner = WPFUtil.FindAncestor<Window>(this);
                        win.Show();
                    }
                }
                else
                {
                    MailViewWindow win = new MailViewWindow();
                    win.Mail = mail;
                    win.Owner = WPFUtil.FindAncestor<Window>(this);
                    win.MailViewCloseEvent += OnMailViewCloseEvent;
                    win.Show();
                }
            }
        }

        void OnMailViewCloseEvent(ASObject mail)
        {
            GroupCollection<ASObject> gcolls = dataGrid.ItemsSource as GroupCollection<ASObject>;
            if (gcolls != null && gcolls.Contains(mail))
                gcolls.RemoveFromGroup(mail);
            else
            {
                Collection<ASObject> colls = dataGrid.ItemsSource as Collection<ASObject>;
                if (colls != null)
                    colls.Remove(mail);
            }

            MailWorker.instance.dispatchMailEvent(MailWorker.Event.MoveFolder, null, null);
        }

        /// <summary>
        /// 移动邮件到XX目录
        /// </summary>
        public void MoveFolder(string toFolder)
        {
            if (_isSearch)
                return;
            ASObject mail = dataGrid.SelectedItem as ASObject;
            if (mail == null || mail is ASObjectGroup)
                return;
            try
            {
                if (MailWorker.instance.MoveFolder(mail, toFolder))
                {
                    GroupCollection<ASObject> gcolls = dataGrid.ItemsSource as GroupCollection<ASObject>;
                    if (gcolls != null)
                        gcolls.RemoveFromGroup(mail);
                    else
                    {
                        Collection<ASObject> colls = dataGrid.ItemsSource as Collection<ASObject>;
                        if (colls != null)
                            colls.Remove(mail);
                    }

                    MailWorker.instance.dispatchMailEvent(MailWorker.Event.MoveFolder, null, null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
                ime.controls.MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void BlackList(string type = null)
        {
            if (type == null || _isSearch)
                return;
            ASObject mail = dataGrid.SelectedItem as ASObject;
            if (mail == null || mail is ASObjectGroup)
                return;

            try
            {
                if (MailWorker.instance.BlackList(mail, type))
                {
                    GroupCollection<ASObject> gcolls = dataGrid.ItemsSource as GroupCollection<ASObject>;
                    if (gcolls != null)
                        gcolls.RemoveFromGroup(mail);
                    else
                    {
                        Collection<ASObject> colls = dataGrid.ItemsSource as Collection<ASObject>;
                        if (colls != null)
                            colls.Remove(mail);
                    }

                    MailWorker.instance.dispatchMailEvent(MailWorker.Event.MoveFolder, null, null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
                ime.controls.MessageBox.Show(ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 回复邮件
        /// </summary>
        public void Reply(bool all = false)
        {
            if (_isSearch)
                return;
            ASObject mail = dataGrid.SelectedItem as ASObject;
            if (mail == null || mail is ASObjectGroup)
                return;
            if ("INBOX" != mail.getString("folder"))
                return;
            MailSendWindow win = new MailSendWindow();
            if (all)
                win.Mail_Type = MailSendWindow.MailType.AllReply;
            else
                win.Mail_Type = MailSendWindow.MailType.Reply;
            win.Mail = mail;
            win.Owner = WPFUtil.FindAncestor<Window>(this);
            win.Show();
        }

        /// <summary>
        /// 转发
        /// </summary>
        public void Transmit()
        {
            if (_isSearch)
                return;
            ASObject mail = dataGrid.SelectedItem as ASObject;
            if (mail == null || mail is ASObjectGroup)
                return;
            
            MailSendWindow win = new MailSendWindow();
            win.Mail_Type = MailSendWindow.MailType.Transmit;
            win.Mail = mail;
            win.Owner = WPFUtil.FindAncestor<Window>(this);
            win.Show();
        }

        public void ShowMailList(XElement folderXml)
        {
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                try
                {
                    _isSearch = false;

                    dgOperatorName.Visibility = System.Windows.Visibility.Collapsed;
                    dgReviewerName.Visibility = System.Windows.Visibility.Collapsed;
                    dataGrid.ItemsSource = null;
                    this.folderXml = folderXml;
                    parentXml = folderXml.Parent;
                    if (parentXml.AttributeValue("name") == "AllMailBox")
                    {
                        Mouse.OverrideCursor = Cursors.Wait;

                        string folder = folderXml.AttributeValue("name");
                        currentFolder = folder;
                        if (currentFolder == "DSBOX")
                        {
                            dgOperatorName.Visibility = System.Windows.Visibility.Visible;
                            dgReviewerName.Visibility = System.Windows.Visibility.Visible;
                            dataGrid.ItemsSource = dsBoxMailProvider.GetMails(currentFolder);
                        }
                        else
                            dataGrid.ItemsSource = dateProvider.GetMails(currentFolder);
                    }
                    else if (parentXml.AttributeValue("name") == "Unhandled")
                    {
                        Mouse.OverrideCursor = Cursors.Wait;

                        currentFolder = "INBOX";
                        string value = folderXml.AttributeValue("value");
                        dataGrid.ItemsSource = unhandledMailProvider.GetMails(value);
                    }

                    Collection<ASObject> colls = dataGrid.ItemsSource as Collection<ASObject>;
                    if (colls == null || colls.Count == 0)
                    {
                        if (SelectedChangedEvent != null)
                            SelectedChangedEvent(null);
                        return;
                    }

                    foreach (ASObject o in colls)
                    {
                        if (o is ASObjectGroup)
                            continue;
                        else
                        {
                            dataGrid.SelectedItem = o;
                            break;
                        }
                    }
                }
                finally
                {
                    Mouse.OverrideCursor = null;
                }
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            DataGridRow row = e.Row;
            ASObject item = row.DataContext as ASObject;
            if (item is ASObjectGroup)
                return;

            GroupCollection<ASObject> colls = dataGrid.ItemsSource as GroupCollection<ASObject>;
            if (colls != null)
                e.Row.Header = getDataIndex(item);
            else
                e.Row.Header = e.Row.GetIndex() + 1;
        }

        private int getDataIndex(ASObject item)
        {
            int groupIndex = 0;
            GroupCollection<ASObject> colls = dataGrid.ItemsSource as GroupCollection<ASObject>;
            int index = colls.IndexOf(item) + 1;
            foreach (ASObject o in colls)
            {
                if (o is ASObjectGroup)
                    groupIndex++;
                else if (item == o)
                    break;
            }
            return index - groupIndex;
        }

        private void IsSeen_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isSearch)
                return;
            ASObject data = ((FrameworkElement)sender).DataContext as ASObject;
            if (data == null)
                return;
            object is_seen = data["is_seen"];

            if (IsBool(is_seen))
            {
                data["is_seen"] = false;
                e.Handled = true;
                dataGrid.SelectedIndex = -1;
                MailWorker.instance.dispatchMailEvent(MailWorker.Event.Update, data, new string[] { "is_seen" });
            }
        }

        private bool IsBool(object value)
        {
            if (value == null)
                return false;
            if (value is Boolean)
                return (bool)value;
            else if (NumberUtil.isNumber(value))
            {
                decimal d = NumberUtil.toNumber(value);
                return d > 0;
            }
            else
                return ASObjectUtil.toBoolean(value);
        }

        private ObservableCollection<ASObject> GetMails(string folder, string labelField, string desc)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("select * from ML_Mail where folder = @folder ")
               .Append("and owner_user_id = @owner_user_id ")
               .Append("order by ").Append(labelField).Append(" ").Append(desc);

            SQLiteCommand cmd = null;
            DataSet ds = null;
            try
            {
                cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection());
                cmd.Parameters.AddWithValue("@folder", folder);
                cmd.Parameters.AddWithValue("@owner_user_id", Desktop.instance.loginedPrincipal.id);

                ds = new DataSet();
                SQLiteDataAdapter q = new SQLiteDataAdapter(cmd);
                q.Fill(ds);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }
            DataTable dt = ds.Tables[0];

            ObservableCollection<ASObject> result = new ObservableCollection<ASObject>();
            foreach (DataRow row in dt.Rows)
            {
                ASObject mail = new ASObject();
                foreach (DataColumn column in dt.Columns)
                {
                    object value = row[column];
                    if (value is System.DBNull)
                        mail[column.ColumnName] = null;
                    else
                        mail[column.ColumnName] = value;
                }
                result.Add(mail);
            }
            return result;
        }

        void onSearch(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            Search();
        }

        void txtSearch_SearchEvent()
        {
            Search();
        }

        void txtSearch_ClearSearchEvent()
        {
            MailWorker.instance.dispatchMailEvent(MailWorker.Event.ClearSearch, null, null);
        }

        private void Search()
        {
            DictionaryItem item = cboSearch.SelectedItem as DictionaryItem;
            if (item == null)
                return;

            DictionaryItem item_type = cboSearchType.SelectedItem as DictionaryItem;
            if (item_type == null)
                return;

            if (String.IsNullOrWhiteSpace(txtSearch.Text) || txtSearch.Text.Trim() == "搜索邮件")
                return;

            string type = "subject";
            switch (item_type.value.ToString())
            {
                case "1":
                    type = "subject";
                    break;
                case "2":
                    type = "contents";
                    break;
                case "3":
                    type = "mail_from_label";
                    break;
            }

            if (item.value.Equals(1))
            {
                this.Dispatcher.BeginInvoke((System.Action)delegate
                {
                    dataGrid.ItemsSource = GetSearchMails("INBOX", type, txtSearch.Text.Trim());
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            else if (item.value.Equals(2))
            {
                this.Dispatcher.BeginInvoke((System.Action)delegate
                {
                    Remoting.call("MailManager.getSearchMails", new object[] { "INBOX", type, txtSearch.Text.Trim() }, this);
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }

            _isSearch = true;
        }

        private ObservableCollection<ASObject> GetSearchMails(string folder, string labelField, string search)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("select * from ML_Mail where folder = @folder ")
               .Append("and owner_user_id = @owner_user_id ")
               .Append("and ").Append(labelField).Append(" like '%").Append(search).Append("%'");

            SQLiteCommand cmd = null;
            DataSet ds = null;
            try
            {
                cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection());
                cmd.Parameters.AddWithValue("@folder", folder);
                cmd.Parameters.AddWithValue("@owner_user_id", Desktop.instance.loginedPrincipal.id);

                ds = new DataSet();
                SQLiteDataAdapter q = new SQLiteDataAdapter(cmd);
                q.Fill(ds);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }
            DataTable dt = ds.Tables[0];

            ObservableCollection<ASObject> result = new ObservableCollection<ASObject>();
            foreach (DataRow row in dt.Rows)
            {
                ASObject mail = new ASObject();
                foreach (DataColumn column in dt.Columns)
                {
                    object value = row[column];
                    if (value is System.DBNull)
                        mail[column.ColumnName] = null;
                    else
                        mail[column.ColumnName] = value;
                }
                result.Add(mail);
            }
            return result;
        }

        public void onRemotingCallback(string callUID, string methodName, object result, AsyncOption option)
        {
            switch (methodName)
            {
                case "MailManager.getSearchMails":
                    dataGrid.ItemsSource = null;
                    if (result == null)
                        return;
                    object[] record = result as object[];
                    ObservableCollection<ASObject> collection = new ObservableCollection<ASObject>();
                    foreach (object o in record)
                    {
                        collection.Add(o as ASObject);
                    }

                    dataGrid.ItemsSource = collection;
                    break;
            }
        }

        public void onRemotingException(string callUID, string methodName, string message, string code, ASObject exception, AsyncOption option)
        {
            System.Diagnostics.Debug.Write(message);
        }

        private void DataGrid_CheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cbx = e.OriginalSource as CheckBox;
            if(cbx == null)
                return;
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                ASObject mail = dataGrid.SelectedItem as ASObject;
                if (mail == null)
                    return;
                mail["is_checked"] = cbx.IsChecked.Value;
                this.Dispatcher.BeginInvoke((System.Action)delegate
                {
                    dataGrid.Items.Refresh();
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }

    public class FieldGroupProvider : IGroupDataProvider<ASObject>
    {
        private string folder;
        private string groupField, labelField;

        public FieldGroupProvider()
        {
        }
        public GroupCollection<ASObject> GetMails(string folder, string field)
        {
            this.folder = folder;
            this.groupField = field;

            StringBuilder sql = new StringBuilder();
            sql.Append("select ").Append(field).Append(", count(id) from ML_Mail where folder = @folder ")
               .Append("and owner_user_id = @owner_user_id ")
               .Append("group by ")
               .Append(field);

            SQLiteCommand cmd = null;
            DataSet ds = null;
            try
            {
                cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection());
                cmd.Parameters.AddWithValue("@folder", folder);
                cmd.Parameters.AddWithValue("@owner_user_id", Desktop.instance.loginedPrincipal.id);

                ds = new DataSet();
                SQLiteDataAdapter q = new SQLiteDataAdapter(cmd);
                q.Fill(ds);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }
            DataTable dt = ds.Tables[0];

            GroupCollection<ASObject> result = new GroupCollection<ASObject>(this);
            foreach (DataRow row in dt.Rows)
            {
                ASObjectGroup group = new ASObjectGroup(result, false);
                group.ChildrenCount = (int)(long)row[1];
                group["$group_field"] = field;
                group["$group_label"] = row[0].ToString();
                group["$group_field_value"] = row[0].ToString();
                group["$children_count"] = "(" + group.ChildrenCount + ")";
                group["$mail_folder"] = folder;
                result.Add(group);
            }
            result.Sort(p => p["$group_label"]);
            return result;
        }
        public GroupCollection<ASObject> GetMails(string folder, string groupField, string labelField)
        {
            this.folder = folder;
            this.groupField = groupField;
            this.labelField = labelField;

            StringBuilder sql = new StringBuilder();
            sql.Append("select ").Append(groupField).Append(",").Append(labelField)
               .Append(", count(id) from ML_Mail where folder = @folder ")
               .Append("and owner_user_id = @owner_user_id ")
               .Append("group by ")
               .Append(groupField).Append(",").Append(labelField);

            SQLiteCommand cmd = null;
            DataSet ds = null;
            try
            {
                cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection());
                cmd.Parameters.AddWithValue("@folder", folder);
                cmd.Parameters.AddWithValue("@owner_user_id", Desktop.instance.loginedPrincipal.id);

                ds = new DataSet();
                SQLiteDataAdapter q = new SQLiteDataAdapter(cmd);
                q.Fill(ds);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }
            DataTable dt = ds.Tables[0];

            GroupCollection<ASObject> result = new GroupCollection<ASObject>(this);
            Map<string, ASObjectGroup> recordMap = new Map<string, ASObjectGroup>();
            string groupValue;
            foreach (DataRow row in dt.Rows)
            {
                groupValue = row[0].ToString();
                if (recordMap.ContainsKey(groupValue))
                {
                    ASObjectGroup group = recordMap[groupValue];
                    group.ChildrenCount += (int)(long)row[2];
                    group["$children_count"] = "(" + group.ChildrenCount + ")";
                    if (String.IsNullOrEmpty(group.getString("$group_label")))
                        group["$group_label"] = row[1].ToString();
                }
                else
                {
                    ASObjectGroup group = new ASObjectGroup(result, false);
                    group.ChildrenCount = (int)(long)row[2];
                    group["$group_field"] = groupField;
                    group["$label_field"] = labelField;
                    group["$group_field_value"] = row[0];
                    group["$group_label"] = row[1].ToString();
                    group["$children_count"] = "(" + group.ChildrenCount + ")";
                    group["$mail_folder"] = folder;

                    result.Add(group);
                    recordMap[groupValue] = group;
                }
            }
            result.Sort(p => p["$group_label"]);
            return result;
        }
        public Collection<ASObject> GetChildren(ASObject group)
        {
            string field = group.getString("$group_field");
            string folder = group.getString("$mail_folder");
            StringBuilder sql = new StringBuilder();
            sql.Append("select * from ML_Mail where folder = '")
               .Append(folder).Append("' and ");
            if (String.IsNullOrWhiteSpace(group.getString("$group_field_value")))
                sql.Append(field).Append(" is null and ");
            else
                sql.Append(field).Append(" = @field and ");
            sql.Append("owner_user_id = @owner_user_id ");

            SQLiteCommand cmd = null;
            DataSet ds = null;
            try
            {
                cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection());
                if (!String.IsNullOrWhiteSpace(group.getString("$group_field_value")))
                    cmd.Parameters.AddWithValue("@field", group["$group_field_value"]);
                cmd.Parameters.AddWithValue("@owner_user_id", Desktop.instance.loginedPrincipal.id);
                ds = new DataSet();
                SQLiteDataAdapter q = new SQLiteDataAdapter(cmd);
                q.Fill(ds);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }
            DataTable dt = ds.Tables[0];

            Collection<ASObject> data = new Collection<ASObject>();
            foreach (DataRow row in dt.Rows)
            {
                ASObject mail = new ASObject();
                foreach (DataColumn column in dt.Columns)
                {
                    object value = row[column];
                    if (value is System.DBNull)
                        mail[column.ColumnName] = null;
                    else
                        mail[column.ColumnName] = value;
                }
                data.Add(mail);
            }
            group["$children_count"] = "(" + data.Count + ")";
            return data;
        }

        public void AddToGroup(GroupCollection<ASObject> collection, ASObject obj)
        {
            object value = obj[this.groupField];

            IEnumerable<ASObjectGroup> groups = collection.OfType<ASObjectGroup>();
            int index;
            foreach (ASObjectGroup group in groups)
            {
                if (group[this.groupField] == value)
                {
                    if (group.IsExpanded)
                    {
                        index = collection.IndexOf(group);
                        collection.Insert(index + group.ChildrenCount, obj);
                    }
                    group.Children.Add(obj);
                    group.ChildrenCount += 1;
                    return;
                }
            }
            //未找到匹配的分组，则插入新分组
            ASObjectGroup newGroup = new ASObjectGroup(collection, true);
            newGroup.ChildrenCount = 1;
            newGroup["$group_field"] = groupField;
            newGroup["$label_field"] = labelField;
            newGroup["$group_field_value"] = value;
            newGroup["$group_label"] = obj[labelField];
            newGroup["$children_count"] = "(" + newGroup.ChildrenCount + ")";
            newGroup["$mail_folder"] = folder;

            collection.Add(newGroup);
            collection.Add(obj);
        }
    }

    public class MailDateProvider : IGroupDataProvider<ASObject>
    {
        class TimePhase
        {
            public string name;		//分段名称
            public DateTime begin;	//起始时间
            public DateTime end;	//结束时间
            public Collection<ASObject> data = new Collection<ASObject>(); //符合时间范围的数据列表
        }
        //时间分段
        private List<TimePhase> timePhases;
        //时间分段的起始时间点
        private DateTime timePhaseBegin;
        //一周中是否从周日开始
        private bool sundayFirst = false;
        private string folder;

        public MailDateProvider(bool sundayFirst = false)
        {
            this.sundayFirst = sundayFirst;
        }
        /// <summary>
        /// 初始化时间分段
        /// </summary>
        private void InitTimePhases()
        {
            DateTime now = DateTimeUtil.now();
            if (now.Month >= 2)
                timePhaseBegin = new DateTime(now.Year, now.Month - 1, 1);
            else
                timePhaseBegin = new DateTime(now.Year - 1, 12, 1);

            timePhases = new List<TimePhase>();
            StringBuilder sb = new StringBuilder();
            TimePhase phase;

            //生成今天的时间段
            DateTime today = new DateTime(now.Year, now.Month, now.Day);
            phase = new TimePhase();
            phase.name = sb.Clear().Append("今天(").Append(now.ToString("MM月dd日")).Append(")").ToString();
            phase.begin = today;
            phase.end = now.AddDays(1);
            timePhases.Add(phase);

            DateTime currentBeginTime = today;

            //生成昨天的时间段
            DateTime yesterday = today.Subtract(new TimeSpan(24, 0, 0));
            if (today.Day > 1)	//如果今天是1号，则不生成昨天，直接生成上个月
            {
                if (GetWeekDay(yesterday) < GetWeekDay(now))
                {
                    phase = new TimePhase();
                    phase.name = sb.Clear().Append("昨天(").Append(yesterday.ToString("MM月dd日")).Append(")").ToString();
                    phase.begin = yesterday;
                    phase.end = new DateTime(now.Year, now.Month, now.Day);
                    timePhases.Add(phase);

                    currentBeginTime = yesterday;
                }
            }

            //生成两天前的时间段
            int weekDay = GetWeekDay(today);
            DateTime bYesterday = yesterday.Subtract(new TimeSpan(24, 0, 0));
            DateTime weekBegin = today.Subtract(new TimeSpan(24 * (weekDay - 1), 0, 0));
            if (today.Day > 2)	//如果今天是2号以前，则不生成两天前，直接生成上个月
            {
                if (weekDay == 3)
                {
                    phase = new TimePhase();
                    phase.name = sb.Clear().Append("前天(").Append(bYesterday.ToString("MM月dd日")).Append(")").ToString();
                    phase.begin = bYesterday;
                    phase.end = yesterday;
                    timePhases.Add(phase);

                    currentBeginTime = bYesterday;
                }
                else if (weekDay > 3)
                {
                    phase = new TimePhase();
                    phase.name = "两天前";
                    phase.begin = weekBegin;
                    phase.end = yesterday;
                    timePhases.Add(phase);

                    currentBeginTime = weekBegin;
                }
            }

            //生成上周的时间段
            DateTime begin = weekBegin.Subtract(new TimeSpan(24 * 7, 0, 0));
            if (begin.Month == today.Month || weekBegin.Subtract(new TimeSpan(0, 0, 1)).Month == today.Month) //如果上周的起始时间与本周在同一月，则生成“上周”
            {
                phase = new TimePhase();
                phase.name = "上周";
                phase.begin = begin;
                phase.end = currentBeginTime;
                timePhases.Add(phase);

                currentBeginTime = begin;
            }

            //生成两周前
            DateTime monthBegin = new DateTime(today.Year, today.Month, 1);
            if (monthBegin < currentBeginTime)
            {
                phase = new TimePhase();
                phase.name = "两周前";
                phase.begin = monthBegin;
                phase.end = currentBeginTime;
                timePhases.Add(phase);

                currentBeginTime = monthBegin;
            }

            //生成上个月
            phase = new TimePhase();
            phase.name = "上个月";
            phase.begin = timePhaseBegin;
            phase.end = currentBeginTime;
            timePhases.Add(phase);

            //生成一个月以前的
            phase = new TimePhase();
            phase.name = "一个月以前的";
            phase.begin = DateTime.MinValue;
            phase.end = timePhaseBegin;
            timePhases.Add(phase);
        }
        /// <summary>
        /// 获取指定日期是一周的第几天
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        private int GetWeekDay(DateTime date)
        {
            if (sundayFirst)
            {
                switch (date.DayOfWeek)
                {
                    case DayOfWeek.Sunday: return 1;
                    case DayOfWeek.Monday: return 2;
                    case DayOfWeek.Tuesday: return 3;
                    case DayOfWeek.Wednesday: return 4;
                    case DayOfWeek.Thursday: return 5;
                    case DayOfWeek.Friday: return 6;
                    case DayOfWeek.Saturday: return 7;
                }
            }
            else
            {
                switch (date.DayOfWeek)
                {
                    case DayOfWeek.Monday: return 1;
                    case DayOfWeek.Tuesday: return 2;
                    case DayOfWeek.Wednesday: return 3;
                    case DayOfWeek.Thursday: return 4;
                    case DayOfWeek.Friday: return 5;
                    case DayOfWeek.Saturday: return 6;
                    case DayOfWeek.Sunday: return 7;
                }
            }
            return 0;
        }

        /// <summary>
        /// 将数据放到对应的时间段中
        /// </summary>
        /// <param name="dataSet">数据集</param>
        /// <param name="dateField">数据集中记录的时间字段</param>
        private void ApplyTimePhase(Collection<ASObject> dataSet, string dateField)
        {
            Object field;
            DateTime date;
            TimePhase unknowPhase = new TimePhase();
            unknowPhase.begin = DateTime.MinValue;
            unknowPhase.end = DateTime.MinValue;
            unknowPhase.name = "未知时间";
            bool found;
            foreach (ASObject data in dataSet)
            {
                field = data[dateField];
                found = false;
                if (field is DateTime)
                {
                    date = (DateTime)data[dateField];
                    foreach (TimePhase phase in timePhases)
                    {
                        if (date >= phase.begin && date < phase.end)
                        {
                            phase.data.Add(data);
                            found = true;
                            break;
                        }
                    }
                }
                if (found != true)
                {
                    unknowPhase.data.Add(data);
                }
            }
            if (unknowPhase.data.Count > 0)
            {
                timePhases.Add(unknowPhase);
            }
        }

        /// <summary>
        /// 构建按时间分组的邮件数据
        /// </summary>
        /// <returns></returns>
        public GroupCollection<ASObject> GetMails(string folder)
        {
            this.folder = folder;

            InitTimePhases();

            GroupCollection<ASObject> result = new GroupCollection<ASObject>(this);

            //获取最近一个月之内的邮件
            DateTime end = DateTimeUtil.now();
            DateTime begin = timePhaseBegin;
            Collection<ASObject> mails = FetchMail(folder, DateTime.MinValue, end);

            ASObjectGroup group;
            if (mails.Count > 0)
            {
                ApplyTimePhase(mails, "mail_date");
                foreach (TimePhase phase in timePhases)
                {
                    if (phase.data.Count > 0)
                    {
                        if (phase.begin == DateTime.MinValue)
                            group = new ASObjectGroup(result, false);
                        else
                            group = new ASObjectGroup(result, true);
                        if(group.IsExpanded)
                            group.Children = phase.data;
                        group.ChildrenCount = phase.data.Count;
                        group["$begin_time"] = phase.begin;
                        group["$end_time"] = phase.end;
                        group["$group_label"] = phase.name;
                        group["$children_count"] = "(" + group.ChildrenCount + ")";
                        group["$mail_folder"] = folder;

                        result.Add(group);
                        if (group.IsExpanded)
                            result.InsertRange(result.Count, phase.data);
                    }
                }
            }
            /*
            int count = FetchMailCount(folder, DateTime.MinValue, begin);
            if (count > 0)
            {
                group = new ASObjectGroup(result, false);
                group.ChildrenCount = count;
                group["$group_label"] = "一个月以前的邮件";
                group["$children_count"] = "(" + group.ChildrenCount + ")";
                group["$end_time"] = begin;
                group["$mail_folder"] = folder;

                result.Add(group);
            }*/

            return result;
        }
        private int FetchMailCount(string folder, DateTime begin, DateTime end)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("select count(id) from ML_Mail where folder = '").Append(folder).Append("' ")
               .Append("and owner_user_id = @owner_user_id ");
            if (begin != DateTime.MinValue)
                sql.Append("and mail_date > @begin_time ");
            else if (end != DateTime.MinValue)
                sql.Append("and mail_date <= @end_time ");

            SQLiteCommand cmd = null;
            DataSet ds = null;
            try
            {
                cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection());
                cmd.Parameters.AddWithValue("@owner_user_id", Desktop.instance.loginedPrincipal.id);
                if (begin != DateTime.MinValue)
                    cmd.Parameters.AddWithValue("@begin_time", begin);
                else if (end != DateTime.MinValue)
                    cmd.Parameters.AddWithValue("@end_time", end);

                ds = new DataSet();
                SQLiteDataAdapter q = new SQLiteDataAdapter(cmd);
                q.Fill(ds);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }
            DataTable dt = ds.Tables[0];
            return (int)((long)dt.Rows[0][0]);
        }
        private Collection<ASObject> FetchMail(string folder, DateTime begin, DateTime end)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("select * from ML_Mail where folder = '").Append(folder).Append("' ")
               .Append("and owner_user_id = @owner_user_id ");
            if (begin != DateTime.MinValue)
                sql.Append("and mail_date > @begin_time ");
            else if (end != DateTime.MinValue)
                sql.Append("and mail_date <= @end_time ");
            sql.Append(" order by mail_date desc");

            SQLiteCommand cmd = null;
            DataSet ds = null;
            try
            {
                cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection());
                cmd.Parameters.AddWithValue("@owner_user_id", Desktop.instance.loginedPrincipal.id);
                if (begin != DateTime.MinValue)
                    cmd.Parameters.AddWithValue("@begin_time", begin);
                else if (end != DateTime.MinValue)
                    cmd.Parameters.AddWithValue("@end_time", end);

                ds = new DataSet();
                SQLiteDataAdapter q = new SQLiteDataAdapter(cmd);
                q.Fill(ds);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }
            Collection<ASObject> data = new Collection<ASObject>();
            DataTable dt = ds.Tables[0];

            object value;
            foreach (DataRow row in dt.Rows)
            {
                ASObject mail = new ASObject();
                foreach (DataColumn column in dt.Columns)
                {
                    value = row[column];
                    if (value is System.DBNull)
                        mail[column.ColumnName] = null;
                    else
                        mail[column.ColumnName] = value;
                }
                data.Add(mail);
            }
            return data;
        }
        public Collection<ASObject> GetChildren(ASObject group)
        {
            string folder = group.getString("$mail_folder");

            DateTime begin = DateTime.MinValue, end = DateTime.MinValue;

            if (group.ContainsKey("$begin_time"))
                begin = (DateTime)group["$begin_time"];
            if (group.ContainsKey("$end_time"))
                end = (DateTime)group["$end_time"];

            Collection<ASObject> children = FetchMail(folder, begin, end);
            group["$children_count"] = "(" + children.Count + ")";
            return children;
        }

        private TimePhase FindTimePhase(DateTime date)
        {
            foreach (TimePhase phase in timePhases)
            {
                if (date >= phase.begin && date < phase.end)
                {
                    return phase;
                }
            }

            return null;
        }

        private ASObjectGroup FindASObjectGroup(TimePhase phase, GroupCollection<ASObject> collection)
        {
            IEnumerable<ASObjectGroup> groups = collection.OfType<ASObjectGroup>();
            foreach (ASObjectGroup group in groups)
            {
                DateTime begin = (DateTime)group["$begin_time"];
                DateTime end = (DateTime)group["$end_time"];
                if (phase.begin >= begin && phase.end <= end)
                {
                    return group;
                }
            }

            return null;
        }

		public void AddToGroup(GroupCollection<ASObject> collection, ASObject obj)
		{
            object field = obj["mail_date"];
            DateTime date;
			TimePhase phaseTo = null;
            ASObjectGroup group = null;
            if (field is DateTime)
            {
                date = (DateTime)field;

                phaseTo = FindTimePhase(date);

                if (phaseTo == null)//存放到一个月以前
                    return;

                group = FindASObjectGroup(phaseTo, collection);
                if (group == null)
                {
                    phaseTo.data.Add(obj);

                    ASObjectGroup newGroup = null;
                    if (phaseTo.begin == DateTime.MinValue)
                        newGroup = new ASObjectGroup(collection, false);
                    else
                        newGroup = new ASObjectGroup(collection, true);
                    if (newGroup.IsExpanded)
                        newGroup.Children = phaseTo.data;
                    newGroup.ChildrenCount = phaseTo.data.Count;
                    newGroup["$begin_time"] = phaseTo.begin;
                    newGroup["$end_time"] = phaseTo.end;
                    newGroup["$group_label"] = phaseTo.name;
                    newGroup["$children_count"] = "(" + newGroup.ChildrenCount + ")";
                    newGroup["$mail_folder"] = folder;

                    collection.Add(newGroup);
                    if (newGroup.IsExpanded)
                        collection.InsertRange(collection.Count, phaseTo.data);
                    return;
                }

                int index = collection.IndexOf(group);

                if (group.IsExpanded)
                {
                    group.Children.Add(obj);
                    collection.Insert(index + group.ChildrenCount, obj);
                }
                group.ChildrenCount += 1;
            }
            /*
			object field = obj["mail_date"];
			DateTime date;
			TimePhase phaseTo = null, unknowPhase = null;
			if (field is DateTime)
			{
				date = (DateTime)field;
				foreach (TimePhase phase in timePhases)
				{
					if (date >= phase.begin && date < phase.end)
					{
						phaseTo = phase;
						phaseTo.data.Add(obj);
						break;
					}
					if (phase.begin == DateTime.MinValue && phase.end == DateTime.MinValue)
						unknowPhase = phase;
				}
			}
			if (phaseTo == null && unknowPhase != null)
			{
				unknowPhase.data.Add(obj);
				phaseTo = unknowPhase;
			}

			if (phaseTo != null)
			{
				int index;
				IEnumerable<ASObjectGroup> groups = collection.OfType<ASObjectGroup>();
				foreach (ASObjectGroup group in groups)
				{
					if (phaseTo.data == group.Children)
					{
						if (group.IsExpanded)
						{
							index = collection.IndexOf(group);
							collection.Insert(index + group.ChildrenCount, obj);
						}
						group.Children.Add(obj);
						group.ChildrenCount += 1;
						return;
					}
				}
				//未找到匹配的分组，说明时间段还没有插入，则寻找相应的插入位置
				index = -1;
				foreach (ASObjectGroup group in groups)
				{
					if (group["$end_time"] != null)
					{
						date = (DateTime)group["$end_time"];
						if (date != DateTime.MinValue && phaseTo.begin >= date)
						{
							index = collection.IndexOf(group);
						}
					}
				}
				if (index == -1)
					index = collection.Count;

				ASObjectGroup newGroup = new ASObjectGroup(collection, true);
				newGroup.Children = phaseTo.data;
				newGroup.ChildrenCount = phaseTo.data.Count;
				newGroup["$group_label"] = phaseTo.name;
				newGroup["$children_count"] = "(" + newGroup.ChildrenCount + ")";
				newGroup["$mail_folder"] = folder;

				collection.Add(newGroup);
				collection.InsertRange(index, phaseTo.data);
			}
			else
			{
				unknowPhase = new TimePhase();
				unknowPhase.begin = DateTime.MinValue;
				unknowPhase.end = DateTime.MinValue;
				unknowPhase.name = "未知时间";
				unknowPhase.data.Add(obj);
				timePhases.Add(unknowPhase);

				ASObjectGroup group = new ASObjectGroup(collection, true);
				group.Children = unknowPhase.data;
				group.ChildrenCount = unknowPhase.data.Count;
				group["$group_label"] = unknowPhase.name;
				group["$children_count"] = "(" + group.ChildrenCount + ")";
				group["$mail_folder"] = folder;

				collection.Add(group);
				collection.InsertRange(collection.Count, unknowPhase.data);
			}*/
		}
	}
}
