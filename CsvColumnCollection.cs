using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Threading;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
	[CLSCompliant(false)]
	[Serializable]
	public class CsvColumnCollection<T> : ObservableCollection<T>, ICustomTypeDescriptor, INotifyPropertyChanged where T : CsvColumn
	{
		[NonSerialized]
		private PropertyDescriptorCollection _pds;

		public CsvColumnCollection()
		{
		}

		#region ICustomTypeDescriptor Members

		public AttributeCollection GetAttributes()
		{
			if (this.Items.Count > 0)
			{
				if(GetProperties().Count > 0)
					return TypeDescriptor.GetAttributes(this);
			}
			return null;
		}

		public string GetClassName()
		{
			return TypeDescriptor.GetClassName(this);
		}

		public string GetComponentName()
		{
			return TypeDescriptor.GetComponentName(this);
		}

		public TypeConverter GetConverter()
		{
			return TypeDescriptor.GetConverter(this);
		}

		public EventDescriptor GetDefaultEvent()
		{
			return TypeDescriptor.GetDefaultEvent(this);
		}

		public PropertyDescriptor GetDefaultProperty()
		{
			return TypeDescriptor.GetDefaultProperty(this);
		}

		public object GetEditor(Type editorBaseType)
		{
			return TypeDescriptor.GetEditor(this, editorBaseType);
		}

		public EventDescriptorCollection GetEvents(Attribute[] attributes)
		{
			return TypeDescriptor.GetEvents(this, attributes);
		}

		public EventDescriptorCollection GetEvents()
		{
			return TypeDescriptor.GetEvents(this);
		}

		public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
		{
			return GetProperties();
		}

		public PropertyDescriptorCollection GetProperties()
		{
			_pds = new PropertyDescriptorCollection(null);
			for (int i = 0; i < Items.Count; i++)
			{
				_pds.Add(new CsvColumnPropertyDescriptor<T>(this, i));
			}
			return _pds;
		}

		public object GetPropertyOwner(PropertyDescriptor pd)
		{
			return this;
		}

		#endregion

		protected override void ClearItems()
		{
			base.ClearItems();
			this.OnPropertyChanged(CountPropertyChangedEventArgs);
			this.OnPropertyChanged(IndexerPropertyChangedEventArgs);
		}
		protected override void InsertItem(int index, T item)
		{
			base.InsertItem(index, item);
			this.OnPropertyChanged(CountPropertyChangedEventArgs);
			this.OnPropertyChanged(IndexerPropertyChangedEventArgs);
		}
		protected override void RemoveItem(int index)
		{
			base.RemoveItem(index);
			this.OnPropertyChanged(CountPropertyChangedEventArgs);
			this.OnPropertyChanged(IndexerPropertyChangedEventArgs);
		}
		protected override void SetItem(int index, T item)
		{
			base.SetItem(index, item);
			this.OnPropertyChanged(IndexerPropertyChangedEventArgs);
		}

		private static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new PropertyChangedEventArgs("Count");
		private static readonly PropertyChangedEventArgs IndexerPropertyChangedEventArgs = new PropertyChangedEventArgs("Item[]");

		[NonSerialized]
		private PropertyChangedEventHandler _propertyChanged;
		new public event PropertyChangedEventHandler PropertyChanged
		{
			add
			{
				if ((object)value != null)
				{
					PropertyChangedEventHandler handler;
					while ((object)Interlocked.CompareExchange<PropertyChangedEventHandler>(ref this._propertyChanged, (PropertyChangedEventHandler)Delegate.Combine(handler = this._propertyChanged, value), handler) != (object)handler);
				}
			}
			remove
			{
				if ((object)value != null)
				{
					PropertyChangedEventHandler handler;
					while ((object)Interlocked.CompareExchange<PropertyChangedEventHandler>(ref this._propertyChanged, (PropertyChangedEventHandler)Delegate.Remove(handler = this._propertyChanged, value), handler) != (object)handler);
				}
			}
		}

		new protected void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			var propertyChanged = this._propertyChanged;
			if ((object)propertyChanged != null)
			{
				propertyChanged(this, e);
			}
		}
	}
}
