using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using wos.rpc.core;
using wos.extensions;
using wos.rpc;
using wos.utils;
using System.Diagnostics;
using System.Collections.ObjectModel;
using wos.library;
using ime.controls.QWindow;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;

namespace ime.mail.controls
{
    /// <summary>
    /// PrincipalSelectWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PrincipalSelectWindow : QWindow
    {
        private ObservableCollection<PrincipalSelectFieldNode> PrincipalList = new ObservableCollection<PrincipalSelectFieldNode>();//人员列表		
        private string strRootPath;
        private bool domain = false;
        private bool single = false;//单选

        private List<ASObject> depPriMultipleList = null;//多选列表

        public PrincipalSelectWindow(bool single = false)
        {
            InitializeComponent();
            Desktop.toDesktopWindow(this, false);
            this.single = single;
            if (!single)
                treeView.ItemTemplate = this.FindResource("CheckBoxItemTemplate") as HierarchicalDataTemplate;
            else
                treeView.ItemTemplate = this.FindResource("ItemTemplate") as HierarchicalDataTemplate;

            treeView.ItemsSource = PrincipalList;
            
            btnOK.IsEnabled = false;
        }

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            e.Handled = true;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            btnCanel.Click -= btnCanel_Click;
            btnOK.Click -= btnOK_Click;
            treeView.RemoveHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(onNodeOpen));
            treeView.RemoveHandler(TreeView.SelectedItemChangedEvent, new RoutedEventHandler(onSelectedItemChanged));

            if (!single)
            {
                treeView.RemoveHandler(CheckBox.CheckedEvent, new RoutedEventHandler(onItemCheckBoxChecked));
                treeView.RemoveHandler(CheckBox.UncheckedEvent, new RoutedEventHandler(onItemCheckBoxChecked));
            }
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            btnCanel.Click += btnCanel_Click;
            btnOK.Click += btnOK_Click;

            treeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(onNodeOpen));
            treeView.AddHandler(TreeView.SelectedItemChangedEvent, new RoutedEventHandler(onSelectedItemChanged));

            if (!single)
            {
                treeView.AddHandler(CheckBox.CheckedEvent, new RoutedEventHandler(onItemCheckBoxChecked));
                treeView.AddHandler(CheckBox.UncheckedEvent, new RoutedEventHandler(onItemCheckBoxChecked));
            }
            this.Dispatcher.BeginInvoke((System.Action)delegate
            {
                try
                {
                    if (domain)
                    {
                        object result = Remoting.call("DomainService.getDomainTree", new object[] { });
                        cb_getDomainTree(result);
                    }
                    else
                    {
                        object result = Remoting.call("DepartmentService.findDepartmentGroup", new object[] { strRootPath });
                        cb_findDepartmentGroup(result as ASObject);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.StackTrace);
                    ime.controls.MessageBox.Show(ex.Message, "错误提示", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                this.Dispatcher.BeginInvoke((System.Action)delegate
                {
                    OpenAll(treeView);
                    if (!single)
                    {
                        this.Dispatcher.BeginInvoke((System.Action)delegate
                        {
                            if (depPriMultipleList != null)
                            {
                                setMultipleChecked(PrincipalList, depPriMultipleList);
                            }
                        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void OpenAll(TreeView tree)
        {
            int index = tree.Items.Count;
            for (int i = 0; i < index; i++)
            {
                TreeViewItem parent = tree.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (parent == null)
                    continue;
                if (!parent.IsExpanded)
                    parent.IsExpanded = true;
                parent.UpdateLayout();

                PrincipalSelectFieldNode node = parent.DataContext as PrincipalSelectFieldNode;
                if (node != null && node.type == "D" && node.IsLoading)
                    OpenChild(parent);
            }
        }

        private void OpenChild(TreeViewItem parent)
        {
            int index = parent.Items.Count;
            for (int i = 0; i < index; i++)
            {
                TreeViewItem child = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (child == null)
                    continue;
                if (!child.IsExpanded)
                    child.IsExpanded = true;
                child.UpdateLayout();

                PrincipalSelectFieldNode node = child.DataContext as PrincipalSelectFieldNode;
                if (node != null && node.type == "D" && node.IsLoading)
                    OpenChild(child);
            }
        }

        public void setRootPath(string path, bool muchDomain = false)
        {
            this.strRootPath = path;
            this.domain = muchDomain;
        }

        private void getChildDep(int groupId, PrincipalSelectFieldNode node)
        {
            object result = Remoting.call("DepartmentService.getChildren", new object[] { groupId });
            if (result == null)
                return;

            object[] data = result as object[];

            PrincipalSelectFieldNode childNode = null;
            for (int i = 0; i < data.Length; i++)
            {
                ASObject record = data[i] as ASObject;

                childNode = new PrincipalSelectFieldNode();
                childNode.id = record.getLong("id");
                childNode.Label = record.getString("name");
                childNode.type = "D";
                childNode.entity = record;
                childNode.parent = node;

                node.Children.Add(childNode);
            }
        }

        private void getChildPri(int groupId, PrincipalSelectFieldNode node)
        {
            object result = Remoting.call("PrincipalService.getPrincipals", new object[] { groupId, 0, 99999 });
            if (result == null)
                return;

            object[] data = result as object[];

            PrincipalSelectFieldNode childNode = null;
            for (int i = 0; i < data.Length; i++)
            {
                ASObject record = data[i] as ASObject;

                childNode = new PrincipalSelectFieldNode(false);
                childNode.id = record.getLong("id");
                childNode.Label = record.getString("name");
                childNode.type = "P";
                childNode.entity = record;
                childNode.parent = node;
                childNode.IsLoading = true;

                node.Children.Add(childNode);
            }
        }

        private bool findActiveNote(XElement xml)
        {
            if (xml.AttributeValue("active") == "true")
            {
                getOrganizationRoot(NumberUtil.parseInt(xml.AttributeValue("id")));
                return true;
            }
            IEnumerable<XElement> xmlChild = xml.Elements();

            for (int i = 0; i < xmlChild.Count(); i++)
            {
                if (findActiveNote(xmlChild.ElementAt(i)))
                {
                    return true;
                }
            }
            return false;
        }

        private void getOrganizationRoot(int domainId)
        {
            object result = Remoting.call("DepartmentService.getOrganizationRoot", new object[] { domainId });
            cb_getOrganizationRoot(result);
        }

        private void cb_findDepartmentGroup(ASObject data)
        {
            PrincipalSelectFieldNode node = new PrincipalSelectFieldNode();
            node.id = data.getLong("id");
            if (data.getString("name") == "%DepartmentRoot%")
                node.Label = "部门";
            else
                node.Label = data.getString("name");
            node.type = "D";
            node.entity = data;
            PrincipalList.Add(node);
        }

        private void cb_getDomainTree(object data)
        {
            if (data is string)
            {
                XElement xml = XElement.Parse(data as string);
                if (xml.Elements().Count() == 0)
                {
                    object result = Remoting.call("DepartmentService.findDepartmentGroup", new object[] { strRootPath });
                    cb_findDepartmentGroup(result as ASObject);
                }
                else
                {
                    this.findActiveNote(xml);
                }
            }
        }

        private void cb_getOrganizationRoot(object data)
        {
            ASObject record = data as ASObject;
            PrincipalSelectFieldNode node = new PrincipalSelectFieldNode();
            node.Label = "部门";
            node.id = record.getLong("departmentRootId");
            node.type = "D";

            PrincipalList.Add(node);
        }

        private void onNodeOpen(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = (TreeViewItem)e.OriginalSource;
            PrincipalSelectFieldNode node = (PrincipalSelectFieldNode)item.DataContext;

            if (!node.IsLoading && node.type == "D")
            {
                try
                {
                    node.clear();
                    int groupId = (int)node.id;
                    this.getChildDep(groupId, node);
                    this.getChildPri(groupId, node);

                    node.IsLoading = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    ime.controls.MessageBox.Show(ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void onSelectedItemChanged(object sender, RoutedEventArgs e)
        {
            if (!single)
                return;

            if (treeView.SelectedItem == null)
                return;
            this.onSelectChange(treeView.SelectedItem as PrincipalSelectFieldNode);
        }

        private void onSelectChange(PrincipalSelectFieldNode node)
        {
            if (node.type == "P")
                btnOK.IsEnabled = true;
            else
                btnOK.IsEnabled = false;
        }

        private void onItemCheckBoxChecked(object sender, RoutedEventArgs e)
        {
            btnOK.IsEnabled = isNodeChecked(PrincipalList);
        }

        private bool isNodeChecked(ObservableCollection<PrincipalSelectFieldNode> nodes, string type = "P")
        {
            foreach (PrincipalSelectFieldNode child in nodes)
            {
                if (child.IsChecked == true && child.type == type)
                    return true;
                else
                    if (isNodeChecked(child.Children))
                        return true;
            }
            return false;
        }

        private void createMultipleList(ObservableCollection<PrincipalSelectFieldNode> nodes, string type = "P")
        {
            foreach (PrincipalSelectFieldNode child in nodes)
            {
                if (child.IsChecked == true && child.type == type)
                    depPriMultipleList.Add(child.entity);
                else
                    createMultipleList(child.Children);
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (!single)
            {
                if (depPriMultipleList == null)
                    depPriMultipleList = new List<ASObject>();
                if (depPriMultipleList.Count != 0)
                    depPriMultipleList.Clear();
                createMultipleList(PrincipalList);
            }

            this.DialogResult = true;
            this.Close();
        }

        private void btnCanel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// 多选列表值
        /// </summary>
        public List<ASObject> multipleValue
        {
            set 
            {
                if (value == null || value.Count == 0)
                    return;
                depPriMultipleList = new List<ASObject>();
                foreach (ASObject o in value)
                {
                    depPriMultipleList.Add(o);
                }
            }
            get { return depPriMultipleList; }
        }

        /// <summary>
        /// 在多选的状态下设置选中值
        /// </summary>
        /// <param name="list"></param>
        /// <param name="array"></param>
        private void setMultipleChecked(ObservableCollection<PrincipalSelectFieldNode> nodes, List<ASObject> list, string type = "P")
        {
            foreach (PrincipalSelectFieldNode child in nodes)
            {
                if (child.type == type)
                {
                    ASObject entity = child.entity;
                    foreach (ASObject o in list)
                    {
                        if (entity.getLong("id") == o.getLong("id"))
                        {
                            child.IsChecked = true;
                            btnOK.IsEnabled = true;
                            break;
                        }
                    }
                }
                else
                    setMultipleChecked(child.Children, list);
            }
        }

        public ASObject SingleValue
        {
            get { return (treeView.SelectedItem as PrincipalSelectFieldNode).entity; }
        }
    }

    public class PrincipalSelectFieldNode : INotifyPropertyChanged
    {
        private string _label;
        public long id = 0;
        public ASObject entity = null;
        public PrincipalSelectFieldNode parent = null;
        public string type = "";
        private bool? _isChecked = false;

        private ObservableCollection<PrincipalSelectFieldNode> _children = null;

        public PrincipalSelectFieldNode(bool loading = true)
        {
            this.Children = new ObservableCollection<PrincipalSelectFieldNode>();

            if (loading == true)
            {
                PrincipalSelectFieldNode dummy = new PrincipalSelectFieldNode(false);
                dummy.parent = this;
                dummy.Label = "正在加载数据...";
                _children.Add(dummy);
            }
        }
        public void clear()
        {
            if (_children != null)
                _children.Clear();
        }

        private bool _loading;
        public bool IsLoading
        {
            get { return _loading; }
            set { _loading = value; this.OnPropertyChanged("Children"); }
        }
        public string Label
        {
            get { return _label; }
            set { _label = value; this.OnPropertyChanged("Label"); }
        }

        public ObservableCollection<PrincipalSelectFieldNode> Children
        {
            get { return _children; }
            set { _children = value; }
        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        #region INotifyPropertyChanged 成员

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region // IsChecked
        public bool? IsChecked
        {
            get { return _isChecked; }
            set { this.SetIsChecked(value, true, true); }
        }

        void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (value == _isChecked)
                return;

            _isChecked = value;

            if (updateChildren && _isChecked.HasValue)
            {
                //this.Children.ForEach(c => c.SetIsChecked(_isChecked, true, false));
                foreach (PrincipalSelectFieldNode c in Children)
                    c.SetIsChecked(_isChecked, true, false);
            }

            if (updateParent && parent != null)
                parent.VerifyCheckState();

            this.OnPropertyChanged("IsChecked");
        }

        void VerifyCheckState()
        {
            bool? state = null;
            for (int i = 0; i < this.Children.Count; ++i)
            {
                bool? current = this.Children[i].IsChecked;
                if (i == 0)
                {
                    state = current;
                }
                else if (state != current)
                {
                    state = null;
                    break;
                }
            }
            this.SetIsChecked(state, false, true);
        }

        #endregion // IsChecked
    }

    public class PrincipalSelectFieldConverter : IValueConverter
    {
        #region IValueConverter 成员

        object IValueConverter.Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (targetType == typeof(ImageSource))
            {
                if (value == null)
                    return null;

                PrincipalSelectFieldNode xml = value as PrincipalSelectFieldNode;
                switch (xml.type)
                {
                    case "D":
                        return new BitmapImage(new Uri(@"pack://application:,,,/ime.mail;component/controls/PrincipalSelectField/Icons/role.png"));
                    case "P":
                        return new BitmapImage(new Uri(@"pack://application:,,,/ime.mail;component/controls/PrincipalSelectField/Icons/User.png"));
                }
            }

            return null;
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
        #endregion
    }
}
