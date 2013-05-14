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
using wos.utils;
using ime.data.Grouping;
using wos.rpc.core;

namespace ime.mail.controls
{
	public class DataGridGroupRow : ContentControl, INotifyPropertyChanged
	{
		static DataGridGroupRow()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(DataGridGroupRow), new FrameworkPropertyMetadata(typeof(DataGridGroupRow)));
		}

		public DataGridGroupRow()
		{
			this.MouseLeftButtonDown += new MouseButtonEventHandler(DataGridGroupRow_MouseLeftButtonDown);
		}

		void DataGridGroupRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			DataGrid datagrid = WPFUtil.FindAncestor<DataGrid>(this);
			if (datagrid != null && datagrid.SelectedItems.Count > 0)
			{
				if (datagrid.SelectedItems.Count != datagrid.SelectedItems.OfType<ASObjectGroup>().Count())
				{
					if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
						return;
					datagrid.SelectedItems.Clear();
				}
				else
				{
					if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl))
						datagrid.SelectedItems.Clear();
				}
			}
			if (!this.IsSelected)
				this.IsSelected = true;
		}
		public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(DataGridGroupRow), new UIPropertyMetadata(false));
		public bool IsSelected
		{
			get { return (bool)GetValue(IsSelectedProperty); }
			set 
			{ 
				SetValue(IsSelectedProperty, value);
				OnPropertyChanged("IsSelected");
			}
		}

		public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register("IsExpanded", typeof(bool), typeof(DataGridGroupRow), new UIPropertyMetadata(false));
		public bool IsExpanded
		{
			get { return (bool)GetValue(IsExpandedProperty); }
			set 
			{ 
				SetValue(IsExpandedProperty, value);
				OnPropertyChanged("IsExpanded");
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string name)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}

	public class NullBollConverter : IValueConverter
	{
		#region Methods

		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
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
                return value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
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
                return value;
		}

		#endregion Methods
	}

    public class GroupNameConverter : IValueConverter
    {
        #region Methods

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return "未知分组";
            ASObject group = value as ASObject;
            string group_label = group.getString("$group_label");

            string group_field = group.getString("$group_field");

            if (String.IsNullOrWhiteSpace(group_label))
                return "未知分组";
            if (group_field == "customer_grade")
            {
                switch (group_label)
                {
                    case "0":
                        return "陌生客户";
                    case "1":
                        return "潜力客户";
                    case "2":
                        return "正式客户";
                    case "3":
                        return "重要客户";
                    case "4":
                        return "关键客户";
                }
            }
            else if (group_field == "handle_action")
            {
                switch (group_label)
                {
                    case "0":
                        return "未处理";
                    case "1":
                        return "无需回复";
                    case "2":
                        return "已回复";
                }
            }

            return group_label;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value;
        }

        #endregion Methods
    }
}
