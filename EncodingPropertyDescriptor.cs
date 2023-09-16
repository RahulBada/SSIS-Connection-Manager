using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
	public class EncodingPropertyDescriptor : PropertyDescriptor
	{
		private Encoding _encoding;
		private Type _componentType;

		public EncodingPropertyDescriptor(Encoding encoding, Type componentType) : base("Encoding", null)
		{
			this._encoding = encoding;
			this._componentType = componentType;
		}

		public override bool CanResetValue(object component)
		{
			return true;
		}

		public override Type ComponentType
		{
			get { return _componentType; }
		}

		public override bool IsReadOnly
		{
			get { return false; }
		}

		public override Type PropertyType
		{
			get { return typeof(Encoding); }
		}

		public override bool ShouldSerializeValue(object component)
		{
			return true;
		}

		public override string Description
		{
			get
			{
				return "Source file encoding";
			}
		}

		public override string Name
		{
			get
			{
				return "Source file encoding";
			}
		}

		public override string DisplayName
		{
			get
			{
				return "Source file encoding";
			}
		}

		public override object GetValue(object component)
		{
			throw new NotImplementedException();
		}

		public override void ResetValue(object component)
		{
			throw new NotImplementedException();
		}

		public override void SetValue(object component, object value)
		{
			throw new NotImplementedException();
		}
	}
}
