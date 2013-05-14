using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows;

namespace ime.mail.controls
{
    public class SearchTextBox :TextBox
    {
        static SearchTextBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SearchTextBox), new FrameworkPropertyMetadata(typeof(SearchTextBox)));
        }

        Image ClearTextbox = null;
        Image SearchTextbox = null;

        public delegate void SearchEventHandler();
        public event SearchEventHandler SearchEvent;
        public delegate void ClearSearchEventHandler();
        public event ClearSearchEventHandler ClearSearchEvent;

        public SearchTextBox()
            : base()
        {
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            ClearTextbox = GetTemplateChild("PART_Clear") as Image;
            SearchTextbox = GetTemplateChild("PART_Search") as Image;
            if (ClearTextbox != null && SearchTextbox != null)
            {
                ClearTextbox.MouseDown += Clear_MouseDown;
                SearchTextbox.MouseDown += Search_MouseDown;
            }

            this.Unloaded += new RoutedEventHandler(SearchTextBox_Unloaded);
        }

        void SearchTextBox_Unloaded(object sender, RoutedEventArgs e)
        {
            if (ClearTextbox != null)
            {
                ClearTextbox.MouseDown -= Clear_MouseDown;
                SearchTextbox.MouseDown -= Search_MouseDown;
            }
        }

        void Clear_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            string str = this.Text;
            this.Clear();
            if (ClearSearchEvent != null && !String.IsNullOrWhiteSpace(str))
                ClearSearchEvent();
        }

        void Search_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SearchEvent != null && !String.IsNullOrWhiteSpace(this.Text))
                SearchEvent();
        }
    }
}
