using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.SqlServer.Dts.Pipeline;
using Microsoft.SqlServer.Dts.Pipeline.Wrapper;
using Microsoft.SqlServer.Dts.Runtime;
using Microsoft.SqlServer.Dts.Runtime.Wrapper;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
    [Flags]
    [Serializable]
    public enum CsvFilePipelines
    {
        Input = 2,
        Output = 4
    }

    public abstract class CsvFilePipelineComponentBase : PipelineComponent
    {
        protected const int DTS_PIPELINE_CTR_ROWSWRITTEN = 0x67;
		[CLSCompliant(false)]
        protected readonly string _runtimeConnectionManagerName;
		[CLSCompliant(false)]
        protected readonly CsvFilePipelines _inputOutput;
		[CLSCompliant(false)]
        protected CsvConnectionManager _manager;
		[CLSCompliant(false)]
        protected FileStream _fileStream;
		[CLSCompliant(false)]
        protected bool _isConnected;
		[CLSCompliant(false)]
        protected Dictionary<string, int> _columnMappings;

		[CLSCompliant(false)]
        protected int _defaultOutputId;
		[CLSCompliant(false)]
        protected int _errorOutputId;

        protected CsvFilePipelineComponentBase(string runtimeConnectionManagerName, CsvFilePipelines inputOutput)
            : base()
        {
            _runtimeConnectionManagerName = runtimeConnectionManagerName;
            _inputOutput = inputOutput;
        }

        #region Diagnostic/Error methods
        protected void Trace(string message)
        {
            byte[] psaDataBytes = null;
            base.ComponentMetaData.PostLogMessage("Diagnostic", null, message, DateTime.Now, DateTime.Now, 0, ref psaDataBytes);
        }

        internal static readonly Func<Exception, int> GetHResultForException = (Func<Exception, int>)Delegate.CreateDelegate(
            typeof(Func<Exception, int>),
            typeof(Exception).GetProperty(
                "HResult",
                // Starting in .NET 4.5, HResult is public. In previous versions it was protected. Since we still need to able to compile and
                // run against .NET 4.0, we will still access it through reflection, but we will include the BindingFlags for Public and NonPublic
                // so that it works against either version.
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.ExactBinding | BindingFlags.DeclaredOnly,
                null,
                typeof(int),
                Type.EmptyTypes,
                null).GetGetMethod(true),
            true);

        protected void FireErrorWithArgs(int hResult, params object[] args)
        {
            bool ignore;
            this.ErrorSupport.FireErrorWithArgs(hResult, out ignore, args);
        }

		protected Exception FatalException(RowError rowError)
		{
			bool ignore;
			this.ComponentMetaData.FireError(rowError.HResult, null, rowError.Message, null, -1, out ignore);
			return new CsvFilePipelineComponentException("Row " + rowError.RowNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) + ": " + rowError.Message,
				rowError.Exception, rowError.HResult);
		}

        protected Exception FatalException(Exception exception)
        {
            bool ignore;
            this.ComponentMetaData.FireError(GetHResultForException(exception), null, exception.Message, null, -1, out ignore);
            return exception;
        }

        protected Exception FatalException(string message, int hResult)
        {
            bool ignore;
            this.ComponentMetaData.FireError(hResult, null, message, null, -1, out ignore);
            return new CsvFilePipelineComponentException(message, hResult);
        }

        protected Exception FatalException(string message)
        {
            var exception = new CsvFilePipelineComponentException(message);
            bool ignore;
            this.ComponentMetaData.FireError(exception.HResult, null, message, null, -1, out ignore);
            return exception;
        }

        protected Exception FatalException(int hResult)
        {
            return this.FatalExceptionWithArgs(hResult, null);
        }

        protected Exception FatalExceptionWithArgs(int hResult, params object[] args)
        {
            Exception exception;

            string message;
            // GetFormattedMessageEx doesn't handle the paramList being null very well, hence the creation of the empty array.
            int result = Microsoft.SqlServer.Dts.ManagedMsg.ErrorSupport.GetFormattedMessageEx(hResult, out message, args ?? new object[0]);
            if (result >= 0 && !string.IsNullOrEmpty(message))
            {
                exception = new CsvFilePipelineComponentException(message, hResult);
            }
            else
            {
                exception = System.Runtime.InteropServices.Marshal.GetExceptionForHR(hResult);
                message = exception.ToString();
            }
            bool ignore;
            this.ComponentMetaData.FireError(hResult, null, message, null, -1, out ignore);
            return exception;
        }
        #endregion //Diagnostic/Error methods

        #region Insert/Delete Input/Output methods
		[CLSCompliant(false)]
        public override IDTSInput100 InsertInput(DTSInsertPlacement insertPlacement, int inputID)
        {
            throw FatalException(HResults.DTS_E_CANTADDINPUT);
        }

        public override void DeleteInput(int inputID)
        {
            throw FatalException(HResults.DTS_E_CANTDELETEINPUT);
        }

		[CLSCompliant(false)]
        public override IDTSOutput100 InsertOutput(DTSInsertPlacement insertPlacement, int outputID)
        {
            throw FatalException(HResults.DTS_E_CANTADDOUTPUT);
        }

        public override void DeleteOutput(int outputID)
        {
            throw FatalException(HResults.DTS_E_CANTDELETEOUTPUT);
        }
        #endregion // Insert/Delete Input/Output methods

        /// <summary>
        /// Called after <see cref="M:Microsoft.SqlServer.Dts.Pipeline.PipelineComponent.PrepareForExecute"/>, and before <see cref="M:Microsoft.SqlServer.Dts.Pipeline.PipelineComponent.PrimeOutput(System.Int32,System.Int32[],Microsoft.SqlServer.Dts.Pipeline.PipelineBuffer[])"/> and <see cref="M:Microsoft.SqlServer.Dts.Pipeline.PipelineComponent.ProcessInput(System.Int32,Microsoft.SqlServer.Dts.Pipeline.PipelineBuffer)"/>.
        /// </summary>
        public override void PreExecute()
        {
            base.PreExecute();
            //maps column order based on lineageIds
            this._columnMappings = new Dictionary<string, int>();
            if (_inputOutput == CsvFilePipelines.Input)
            {
                var inputCount = ComponentMetaData.InputCollection.Count;
                if (inputCount > 0)
                {
                    IDTSInput100 input = this.ComponentMetaData.InputCollection[0];

                    foreach (IDTSInputColumn100 col in input.InputColumnCollection)
                    {
                        var lineageId = BufferManager.FindColumnByLineageID(input.Buffer, col.LineageID);
                        this._columnMappings.Add(col.Name, lineageId);
                    }
                }
            }
        }

        /// <summary>
        /// Called when a component is first added to the data flow task, to initialize the <see cref="P:Microsoft.SqlServer.Dts.Pipeline.PipelineComponent.ComponentMetaData"/> of the component.
        /// </summary>
        public override void ProvideComponentProperties()
        {
            //Trace("Providing component properties");
            base.ProvideComponentProperties();
            this.RemoveAllInputsOutputsAndCustomProperties();

            try
            {
                var connection = this.ComponentMetaData.RuntimeConnectionCollection[_runtimeConnectionManagerName];
            }
            catch
            {
                IDTSRuntimeConnection100 runtimeConnection = this.ComponentMetaData.RuntimeConnectionCollection.New();
                runtimeConnection.Name = _runtimeConnectionManagerName;
            }

            var componentMetaData = this.ComponentMetaData;
            componentMetaData.UsesDispositions = false; // We don't have an error output.
            componentMetaData.ValidateExternalMetadata = true; // For now, we are treating the body columns as "external", so default this to true so that they will be validated.

            if (_inputOutput == CsvFilePipelines.Input)
            {
                IDTSInput100 input = this.ComponentMetaData.InputCollection.New();
                input.Name = "Input";
                input.HasSideEffects = true; // If we don't indicate that we have side effects on the input, we could be optimized out since we have no output.
                // Setting IsUsed to true displays the external metadata columns inside the Advanced Editor for the Destination
                // and allows mapping between input and output columns of differing names.
                //input.ExternalMetadataColumnCollection.IsUsed = true; // We populate the external metadata column collection.
            }
            if (_inputOutput == CsvFilePipelines.Output)
            {
                IDTSOutput100 output = this.ComponentMetaData.OutputCollection.New();
                output.Name = "Output";
                output.ErrorRowDisposition = DTSRowDisposition.RD_FailComponent;
                output.HasSideEffects = true; // If we don't indicate that we have side effects on the output, we could be optimized out since we have no input.
                // Exposes the ExternalMetadataColumns in the advanced editor.
                //output.ExternalMetadataColumnCollection.IsUsed = true; // We populate the external metadata column collection.

                //indicate that we have an error output
                ComponentMetaData.UsesDispositions = true;
                var errorOutput = ComponentMetaData.OutputCollection.New();
                errorOutput.IsErrorOut = true;
                errorOutput.Name = "ErrorOutput";
                //add ErrorMessage column
                var messageColumn = errorOutput.OutputColumnCollection.New();
                messageColumn.Name = "ErrorMessage";
                messageColumn.SetDataTypeProperties(DataType.DT_WSTR, 4000, 0, 0, 0);
				// Add ErrorXml column.
				//var errorXmlColumn = errorOutput.OutputColumnCollection.New();
				//errorXmlColumn.Name = "ErrorXml";
				//errorXmlColumn.SetDataTypeProperties(DataType.DT_WSTR, 4000, 0, 0, 0);
            }
        }

        /// <summary>
        /// Establishes a connection to a connection manager.
        /// </summary>
        /// <param name="transaction">The transaction the connection is participating in.</param>
        public override void AcquireConnections(object transaction)
        {
            this.ComponentMetaData.FireWarning(0, null, "Standard Connection", null, 0);
            AcquireConnections(transaction, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        }

        /// <summary>
        /// Establishes a connection to a connection manager.
        /// </summary>
        /// <param name="transaction">The transaction the connection is participating in.</param>
        /// <param name="fileMode">The file mode the connection uses.</param>
        /// <param name="fileAccess">The file access the connection uses.</param>
        protected void AcquireConnections(object transaction, FileMode fileMode, FileAccess fileAccess)
        {
            if (this._isConnected)
            {
                throw this.FatalException(HResults.DTS_E_ALREADYCONNECTED);
            }
            var runtimeConnection = this.ComponentMetaData.RuntimeConnectionCollection[_runtimeConnectionManagerName];
            if (runtimeConnection != null)
            {
                var dtsConnectionManager = runtimeConnection.ConnectionManager;
                if (dtsConnectionManager != null)
                {
                    ConnectionManager cManager = DtsConvert.GetWrapper(dtsConnectionManager);
                    CsvConnectionManagerAdapter adapter = cManager.InnerObject as CsvConnectionManagerAdapter;
                    if (adapter != null)
                    {
                        //force evaluation of expressions on ConnectionManager with each new connection attempt
                        // probably only necessary for ConnectionString property
                        var evaluator = new ExpressionEvaluator();
                        foreach (var prop in cManager.Properties)
                        {
                            string expression = null;
                            object expressionResult = null;
                            if (prop.Get)
                            {
                                expression = cManager.GetExpression(prop.Name);
                                if (!string.IsNullOrEmpty(expression) && prop.Set)
                                {
                                    evaluator.Expression = expression;
                                    evaluator.Evaluate(this.VariableDispenser, out expressionResult, false);
                                    prop.SetValue(cManager, expressionResult);
                                }
                            }
                        }

                        this._manager = adapter.Manager;
                        this._fileStream = adapter.AcquireConnection(transaction, fileMode, fileAccess);
                        this._isConnected = true;
                    }
                }
            }
        }

        /// <summary>
        /// Frees the connections established during <see cref="M:Microsoft.SqlServer.Dts.Pipeline.PipelineComponent.AcquireConnections(System.Object)"/>. Called at design time and run time.
        /// </summary>
        public override void ReleaseConnections()
        {
            if (this._fileStream != null)
            {
                var connectionManager = DtsConvert.GetWrapper(this.ComponentMetaData.RuntimeConnectionCollection[_runtimeConnectionManagerName].ConnectionManager);
                connectionManager.ReleaseConnection(this._fileStream);
                this._fileStream = null;
                this._isConnected = false;
            }

            base.ReleaseConnections();
        }

        /// <summary>
        /// Called when an <see cref="T:Microsoft.SqlServer.Dts.Pipeline.Wrapper.IDTSInput100"/> object is connected to the component through the <see cref="T:Microsoft.SqlServer.Dts.Pipeline.Wrapper.IDTSPath100"/> interface.
        /// </summary>
        /// <param name="inputID">Contains the ID of the <see cref="T:Microsoft.SqlServer.Dts.Pipeline.Wrapper.IDTSInput100"/> object that is attached.</param>
        public override void OnInputPathAttached(int inputID)
        {
            base.OnInputPathAttached(inputID);
            var input = this.ComponentMetaData.InputCollection.FindObjectByID(inputID);
            var inputColumns = input.InputColumnCollection;
            var externalMetadataColumns = input.ExternalMetadataColumnCollection;
            externalMetadataColumns.RemoveAll();
            // generate and map to external metadata columns -- avoids inputColumn.ExternalMetadataColumnId == 0
            GenerateAndMapExternalMetadataColumns(externalMetadataColumns, null, inputColumns);
        }

        /// <summary>
        /// Repairs any errors identified during validation that cause the component to return <see cref="F:Microsoft.SqlServer.Dts.Pipeline.Wrapper.DTSValidationStatus.VS_NEEDSNEWMETADATA"/> at design time.
        /// </summary>
        public override void ReinitializeMetaData()
        {
            base.ReinitializeMetaData();
            if (!this._isConnected)
            {
                throw FatalException(HResults.DTS_E_CONNECTIONREQUIREDFORMETADATA);
            }
            if (_inputOutput == CsvFilePipelines.Input)
            {
                var input = this.ComponentMetaData.InputCollection.Cast<IDTSInput100>().FirstOrDefault(n => n.IsAttached);
                if (input != null)
                {
                    var vInput = input.GetVirtualInput();
                    var inputColumns = input.InputColumnCollection;
                    var inputExternalMetadataColumns = input.ExternalMetadataColumnCollection;
                    inputExternalMetadataColumns.RemoveAll();
                    inputColumns.RemoveAll();
                    // regenerate input columns off of the upstream output columns
                    // -- avoids inputColumn.ExternalMetadataColumnId == 0
                    foreach (IDTSVirtualInputColumn100 vColumn in vInput.VirtualInputColumnCollection)
                    {
                        var column = inputColumns.New();
                        column.Name = vColumn.Name;
                        column.LineageID = vColumn.LineageID;
                        column.Description = vColumn.Description;
                    }
                    // map input columns to external metadata columns
                    GenerateAndMapExternalMetadataColumns(inputExternalMetadataColumns, null, inputColumns);
                }
            }
            if (_inputOutput == CsvFilePipelines.Output)
            {
                var outputCollection = ComponentMetaData.OutputCollection.OfType<IDTSOutput100>();
                //default output
                var output = outputCollection.FirstOrDefault(n => n.ID == _defaultOutputId);
                var outputColumns = output.OutputColumnCollection;
                var outputExternalMetadataColumns = output.ExternalMetadataColumnCollection;
                var outputColumnCount = output.OutputColumnCollection.Count;
                outputExternalMetadataColumns.RemoveAll();
                outputColumns.RemoveAll();
                GenerateAndMapExternalMetadataColumns(outputExternalMetadataColumns, outputColumns, null);
                //error output
                var errorOutput = outputCollection.FirstOrDefault(n => n.ID == _errorOutputId);
                if (errorOutput != null)
                {
                    var errorOutputColumns = errorOutput.OutputColumnCollection;
                    var errorOutputExternaMetadataColumns = errorOutput.ExternalMetadataColumnCollection;
                    for (int i = 0; i < outputColumnCount; i++)
                    {
                        var externalMetadataColumnId = errorOutputColumns[0].ExternalMetadataColumnID;
                        errorOutputExternaMetadataColumns.RemoveObjectByID(externalMetadataColumnId);
                        errorOutputColumns.RemoveObjectByIndex(0);
                    }
                    GenerateAndMapExternalMetadataColumns(
                        errorOutputExternaMetadataColumns, errorOutputColumns, null, true);
                }
            }
        }

        /// <summary>
        /// Generates the external metadata columns and maps them to the input or output columns.
        /// </summary>
        /// <param name="externalMetadataColumns">The external metadata columns.</param>
        /// <param name="outputColumns">The output columns.</param>
        /// <param name="inputColumns">The input columns.</param>
		[CLSCompliant(false)]
        protected void GenerateAndMapExternalMetadataColumns(
            IDTSExternalMetadataColumnCollection100 externalMetadataColumns,
            IDTSOutputColumnCollection100 outputColumns,
            IDTSInputColumnCollection100 inputColumns)
        {
            GenerateAndMapExternalMetadataColumns(externalMetadataColumns, outputColumns, inputColumns, false);
        }

        /// <summary>
        /// Generates the external metadata columns and maps them to the input or output columns.
        /// </summary>
        /// <param name="externalMetadataColumns">The external metadata columns.</param>
        /// <param name="outputColumns">The output columns.</param>
        /// <param name="inputColumns">The input columns.</param>
        /// <param name="isErrorOutput">if set to <c>true</c> [is error output].</param>
		[CLSCompliant(false)]
        protected void GenerateAndMapExternalMetadataColumns(
            IDTSExternalMetadataColumnCollection100 externalMetadataColumns,
            IDTSOutputColumnCollection100 outputColumns,
            IDTSInputColumnCollection100 inputColumns,
            bool isErrorOutput)
        {
            Debug.Assert(this._manager != null);

            var bodyColumns = this._manager.BodyColumns;
            int bodyColumnCount = bodyColumns.Count;
            for (int i = 0; i < bodyColumnCount; i++)
            {
                var bodyColumn = bodyColumns[i];
                var externalMetadataColumn = externalMetadataColumns.NewAt(i);
                externalMetadataColumn.Name = bodyColumn.Name;
                if (!isErrorOutput)
                {
                    externalMetadataColumn.CodePage = bodyColumn.CodePage.GetValueOrDefault();
                    externalMetadataColumn.Length = bodyColumn.MaxLength;
                    externalMetadataColumn.Precision = bodyColumn.Precision;
                    externalMetadataColumn.Scale = bodyColumn.Scale;
                    externalMetadataColumn.DataType = bodyColumn.IntegrationServicesType;
                }
                else
                {
                    externalMetadataColumn.CodePage = 0;
                    //externalMetadataColumn.Length = 500;
					externalMetadataColumn.Length = 4000;
                    externalMetadataColumn.Precision = 0;
                    externalMetadataColumn.Scale = 0;
                    externalMetadataColumn.DataType = DataType.DT_WSTR;
                }

                if (outputColumns != null)
                {
                    // generate output column, and map it to the external metadata column
                    var outputColumn = outputColumns.NewAt(i);
                    outputColumn.Name = bodyColumn.Name;
                    // NOTE: May need extra handling for DT_NUMERIC with precision == 0 and scale == 0.
                    if (!isErrorOutput)
                    {
                        outputColumn.SetDataTypeProperties(bodyColumn.IntegrationServicesType, bodyColumn.MaxLength,
                                                           bodyColumn.Precision, bodyColumn.Scale,
                                                           bodyColumn.CodePage.GetValueOrDefault());
                    }
                    else
                    {
                        // TODO: what size should this be?
                        //outputColumn.SetDataTypeProperties(DataType.DT_WSTR, 500, 0, 0, 0);
						outputColumn.SetDataTypeProperties(DataType.DT_WSTR, 4000, 0, 0, 0);
                    }
                    outputColumn.ExternalMetadataColumnID = externalMetadataColumn.ID;
                    outputColumn.ErrorRowDisposition = bodyColumn.ErrorRowDisposition;
                }
                if (inputColumns != null)
                {
                    // find input column by name, and map it to the external metadata column -- 
                    var inputColumn = inputColumns.Cast<IDTSInputColumn100>().FirstOrDefault(n => n.Name == bodyColumn.Name);
                    if (inputColumn != null)
                    {
                        inputColumn.ExternalMetadataColumnID = externalMetadataColumn.ID;
                    }
                }
            }
        }



        /// <summary>
        /// Gets the output ids.
        /// </summary>
        public void GetOutputIds()
        {
            var errorOutputIndex = -1;
            this.GetErrorOutputInfo(ref _errorOutputId, ref errorOutputIndex);
            _defaultOutputId =
                ComponentMetaData.OutputCollection.OfType<IDTSOutput100>()
                    .FirstOrDefault(n => n.ID != _errorOutputId)
                    .ID;
        }

        private static readonly char[] TrimCharacters = new[] { ' ', '\n', '\r', '\t' };

        /// <summary>
        /// Trims the string based on the <see cref="TrimOptions"/> specified.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="option">The option.</param>
        /// <returns></returns>
        public static string TrimString(string value, TrimOptions option)
        {
            string result = null;
            if (value != null)
            {
                switch (option)
                {
                    case TrimOptions.TrimLeft:
                        result = value.TrimStart(TrimCharacters);
                        break;
                    case TrimOptions.TrimRight:
                        result = value.TrimEnd(TrimCharacters);
                        break;
                    case TrimOptions.TrimBoth:
                        result = value.Trim(TrimCharacters);
                        break;
                    case TrimOptions.NormalizeSpaces:
                        var builder = new StringBuilder();
                        var flag = false;
                        foreach (var c in value.Trim(TrimCharacters))
                        {
                            if (!char.IsWhiteSpace(c))
                            {
                                flag = true;
                                builder.Append(c);
                            }
                            else if (flag)
                            {
                                flag = false;
                                builder.Append(' ');
                            }
                        }
                        result = builder.ToString();
                        break;
                    case TrimOptions.None:
                        result = value;
                        break;
                }
            }
            return result;
        }
    }
}
