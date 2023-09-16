using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
    [Serializable]
    public class CsvFilePipelineComponentException : Exception
    {
        protected CsvFilePipelineComponentException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public CsvFilePipelineComponentException(int hResult)
            : base()
        {
            
        }

        public CsvFilePipelineComponentException(string message, int hResult)
            : base(message)
        {
            this.HResult = hResult;
        }

        public CsvFilePipelineComponentException(string message, Exception innerException, int hResult)
            : base(message, innerException)
        {
            this.HResult = hResult;
        }

        public CsvFilePipelineComponentException(string message)
            : base(message)
        {
        }

        public CsvFilePipelineComponentException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public new int HResult
        {
            get
            {
                return base.HResult;
            }
            protected set
            {
                base.HResult = value;
            }
        }

		//protected static string GetFormattedMessage(int hResult, params object[] args)
		//{
		//    string message;
		//    int result = Microsoft.SqlServer.Dts.ManagedMsg.ErrorSupport.GetFormattedMessageEx(hResult, out message, args);
		//    return (result >= 0 && string.IsNullOrEmpty(message) ? null : message);
		//}
    }
}
