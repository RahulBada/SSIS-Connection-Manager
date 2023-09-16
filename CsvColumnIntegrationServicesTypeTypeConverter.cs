using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using Microsoft.SqlServer.Dts.Runtime.Wrapper;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
	public class CsvColumnIntegrationServicesTypeTypeConverter : TypeConverter
	{
		public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
		{
			//All SSIS data types that can be converted into managed types
			List<DataType> types = new List<DataType>()
			{
				DataType.DT_WSTR,
				DataType.DT_NTEXT,
				DataType.DT_BYTES,
				DataType.DT_DBTIMESTAMP,
				DataType.DT_DBTIMESTAMP2,
				DataType.DT_DBTIMESTAMPOFFSET,
				DataType.DT_DBDATE,
				DataType.DT_DBTIME,
				DataType.DT_DBTIME2,
				DataType.DT_DATE,
				DataType.DT_FILETIME,
				DataType.DT_NUMERIC,
				DataType.DT_GUID,
				DataType.DT_I1,
				DataType.DT_I2,
				DataType.DT_I4,
				DataType.DT_I8,
				DataType.DT_BOOL,
				DataType.DT_R4,
				DataType.DT_R8,
				DataType.DT_UI1,
				DataType.DT_UI2,
				DataType.DT_UI4,
				DataType.DT_UI8 
			};
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
				return ((DataType)value).ToString();
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
					return (DataType)Enum.Parse(typeof(DataType), (string)value);
			}
			return base.ConvertFrom(context, culture, value);
		}
	}
}
