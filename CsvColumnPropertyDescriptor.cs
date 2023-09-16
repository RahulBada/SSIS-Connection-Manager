using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
	[CLSCompliant(false)]
	public class CsvColumnPropertyDescriptor<T> : PropertyDescriptor where T : CsvColumn
	{
		private CsvColumnCollection<T> _collection = null;
		private int _index = -1;
		public CsvColumnPropertyDescriptor(CsvColumnCollection<T> collection, int index) : base("#" + index.ToString(), null)
		{
			this._collection = collection;
			this._index = index;
		}

		public override bool CanResetValue(object component)
		{
			return true;
		}

		public override Type ComponentType
		{
			get { return this._collection.GetType(); }
		}

		public override object GetValue(object component)
		{
			return this._collection[_index];
		}

		public override bool IsReadOnly
		{
			get { return false; }
		}

		public override Type PropertyType
		{
			get { return this._collection[_index].GetType(); }
		}

		public override void ResetValue(object component)
		{
		}

		public override void SetValue(object component, object value)
		{
			this._collection[_index] = (T)value;
		}

		public override bool ShouldSerializeValue(object component)
		{
			return false;
		}

		public override string Name
		{
			get
			{
				return this._collection[_index].Name;
			}
		}

		public override string DisplayName
		{
			get
			{
				return this._collection[_index].Name;
			}
		}

		public override string Description
		{
			get
			{
				return this._collection[_index].Name;
			}
		}
	}
}
