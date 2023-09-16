using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
	[CLSCompliant(false)]
	public class HeaderTrailerColumn : CsvColumn
	{
		private static readonly PropertyChangedEventArgs VariableNamePropertyChangedEventArgs = new PropertyChangedEventArgs("VariableName");
		private string _variableName;
		[Category("Data")]
		[DisplayName("Variable name")]
		public string VariableName
		{
			get
			{
				return this._variableName;
			}
			set
			{
				if (this._variableName != value)
				{
					this._variableName = value;
					this.OnPropertyChanged(VariableNamePropertyChangedEventArgs);
				}
			}
		}
	}
}
