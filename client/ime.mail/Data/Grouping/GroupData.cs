using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using wos.collections;
using System.Collections;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using wos.rpc.core;

namespace ime.data.Grouping
{
	public interface IGroupDataProvider<T>
	{
		/// <summary>
		/// 获取分组的子元素
		/// </summary>
		/// <param name="group"></param>
		/// <returns></returns>
		Collection<T> GetChildren(T group);

		/// <summary>
		/// 将对象加入到对应的分组对象中
		/// </summary>
		/// <param name="collection"></param>
		/// <param name="obj"></param>
		void AddToGroup(GroupCollection<T> collection, T obj);
	}
	public class GroupCollection<T> : ObservableCollection<T>
	{
		private IGroupDataProvider<T> dataProvider;

		public GroupCollection(IGroupDataProvider<T> dataProvider)
		{
			this.dataProvider = dataProvider;
		}
		public void AddToGroup(T obj)
		{
			dataProvider.AddToGroup(this, obj);
		}
		public void RemoveFromGroup(T obj)
		{
			IEnumerable<IGroupData<T>> groups = this.OfType<IGroupData<T>>();
			foreach (IGroupData<T> group in groups)
			{
				if (group.Children != null && group.Children.Contains(obj))
				{
					group.Children.Remove(obj);
					group.ChildrenCount--;

					if (this.Contains(obj))
						this.Remove(obj);

                    if (group.ChildrenCount == 0)
                        this.Remove((T)group);
					break;
				}
			}
		}
		public void ExpandGroup(IGroupData<T> group)
		{
			int index = this.IndexOf((T)group);
			Collection<T> children = group.Children;
			if (children == null)
			{
				children = dataProvider.GetChildren((T)group);
				group.Children = children;
			}
			InsertRange(index + 1, children);
		}
		public void CloseGroup(IGroupData<T> group)
		{
			int index = this.IndexOf((T)group);
			RemoveRange(index + 1, group.ChildrenCount);
		}

		public void RemoveRange(int index, int count)
		{
            try
            {
                this.CheckReentrancy();
                var items = this.Items as List<T>;
                items.RemoveRange(index, count);
                OnReset();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }
		}

		public void InsertRange(int index, IEnumerable<T> collection)
		{
            try
            {
                this.CheckReentrancy();
                var items = this.Items as List<T>;
                items.InsertRange(index, collection);
                OnReset();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }
		}

		private void OnReset()
		{
			OnPropertyChanged("Count");
			OnPropertyChanged("Item[]");
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
		public void Sort<TKey>(Func<T, TKey> keySelector)
		{
			Comparer<TKey> comparer = Comparer<TKey>.Default;

			for (int i = this.Count - 1; i >= 0; i--)
			{
				for (int j = 1; j <= i; j++)
				{
					
					T o1 = this[j - 1];
					T o2 = this[j];
					if (comparer.Compare(keySelector(o1), keySelector(o2)) > 0)
					{
						this.Remove(o1);
						this.Insert(j, o1);
					}
				}
			}
		}
		private void OnPropertyChanged(string propertyName)
		{
			OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
		}
	}
	public interface IGroupData<T>
	{
		Collection<T> Children
		{
			get;
			set;
		}
		int ChildrenCount
		{
			get;
			set;
		}
		bool IsExpanded
		{
			get;
			set;
		}
	}

	public class ASObjectGroup : ASObject, IGroupData<ASObject>, INotifyPropertyChanged
	{
		private GroupCollection<ASObject> container;

		public ASObjectGroup(GroupCollection<ASObject> container, bool IsExpanded = false)
		{
			this.container = container;
			this._isExpanded = IsExpanded;

			this["$is_group"] = true;
		}
		public override Object this[string key]
		{
			get
			{
				if (key == "$is_expanded")
					return IsExpanded;
				return base[key];
			}
			set
			{
				base[key] = value;
				if (key == "$is_expanded" && value is bool)
				{
					this.IsExpanded = (bool)value;
					OnPropertyChanged("[$is_expanded]");
				}
			}
		}
		private Collection<ASObject> _children = null;
		public Collection<ASObject> Children
		{
			get { return _children; }
			set 
			{ 
				_children = value;
				if (_children != null)
					ChildrenCount = _children.Count;
			}
		}

		private int _childrenCount = 0;
		public int ChildrenCount
		{
			get { return _childrenCount; }
			set 
			{ 
				_childrenCount = value;
				this["$children_count"] = "(" + _childrenCount + ")";
			}
		}
		private bool _isExpanded = false;
		public bool IsExpanded
		{
			get { return _isExpanded; }
			set
			{
				if (value != _isExpanded)
				{
					_isExpanded = value;
					OnPropertyChanged("IsExpanded");

					if (container != null)
					{
						if (_isExpanded == true)
							container.ExpandGroup(this);
						else
							container.CloseGroup(this);
					}
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;
		private void OnPropertyChanged(string name)
		{
			if (PropertyChanged != null)
				PropertyChanged(this, new PropertyChangedEventArgs(name));
		}
	}
}
