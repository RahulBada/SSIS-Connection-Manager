using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
	[Serializable]
	/// <summary>
	/// Standard representation of each error. Matches the name written to the error information XML output.
	/// </summary>
	public enum ErrorType
	{
		RequiredFieldWasEmpty = 1,
		ViolatedRegex = 2,
		LessThanMinLength = 3,
		GreaterThanMaxLength = 4,
		CouldNotParse = 5,
		LessThanMinValue = 6,
		GreaterThanMaxValue = 7
	}

    /// <summary>
    /// Contains the information related to an error on a row.
    /// </summary>
    public class RowError
    {
        public int HResult { get; private set; }
        //public object[] Parameters { get; set; }
        //public RowType RowType { get; set; }
        //public int ColumnIndex { get; set; }
        public CsvColumn Column { get; private set; }
        public string Message { get; private set; }
		public ErrorType ErrorType { get; private set; }
		public int RowNumber { get; private set; }
		public Exception Exception { get; private set; }

		public RowError(int rowNumber, CsvColumn column, ErrorType errorType, string message)
			: this (rowNumber, column, errorType, message, -2146233088 /*.NET default Exception HRESULT*/)
		{
		}

        public RowError(int rowNumber, CsvColumn column, ErrorType errorType, string message, int hResult)
        {
			this.RowNumber = rowNumber;
			this.Column = column;
			this.ErrorType = errorType;
			this.Message = message;
			this.HResult = hResult;
        }

		public RowError(int rowNumber, CsvColumn column, ErrorType errorType, string message, Exception exception)
		{
			this.RowNumber = rowNumber;
			this.Column = column;
			this.ErrorType = errorType;
			this.Message = message;
			this.HResult = CsvFilePipelineComponentBase.GetHResultForException(exception);
			this.Exception = exception;
		}

		//public RowError(CsvColumn column, CsvFilePipelineComponentException exception)
		//{
		//    this.Column = column;
		//    this.Message = exception.Message;
		//    this.HResult = exception.HResult;
		//}
    }
}
