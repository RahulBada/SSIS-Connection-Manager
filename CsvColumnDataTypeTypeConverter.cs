using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
	public class CsvColumnDataTypeTypeConverter : TypeConverter
	{
		public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
		{
			//All managed types that can be converted to SSIS data types
			List<Type> types = new Type[]{typeof(string), typeof(byte[]), typeof(DateTime), 
				typeof(DateTimeOffset), typeof(TimeSpan), typeof(decimal), typeof(Guid), 
				typeof(sbyte), typeof(short), typeof(int), typeof(long), typeof(bool), typeof(Single), 
				typeof(double), typeof(byte), typeof(ushort), typeof(uint), typeof(ulong) }.ToList();
			return new StandardValuesCollection(types);
		}

		public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
		{
			return true;
		}

		public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
		{
			return true;
		}

		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			return (destinationType == typeof(string)) || base.CanConvertTo(context, destinationType);
		}

		public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
		{
			if (value == null)
				return null;
			if (destinationType == typeof(string))
			{
				return ((Type)value).AssemblyQualifiedName;
			}
			return base.ConvertTo(context, culture, value, destinationType);
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			return (sourceType == typeof(string)) || base.CanConvertFrom(context, sourceType);
		}
		public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
		{
			if (value == null)
			{
				return null;
			}
			var typeOfValue = value.GetType();
			switch (Type.GetTypeCode(typeOfValue))
			{
				case TypeCode.String:
					return Type.GetType((string)value, true, false);
			}
			return base.ConvertFrom(context, culture, value);
		}
	}
}
