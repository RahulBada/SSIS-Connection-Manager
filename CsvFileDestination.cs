using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Dts.Pipeline;
using Microsoft.SqlServer.Dts.Pipeline.Wrapper;
using Microsoft.SqlServer.Dts.Runtime;
using System.IO;
using Microsoft.SqlServer.Dts.Runtime.Wrapper;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Globalization;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
    [CLSCompliant(false)]
    [DtsPipelineComponent(
        ComponentType = ComponentType.DestinationAdapter,
        DisplayName = "Csv File Destination",
        Description = "Destination to write to CSV files with headers and trailers",
        IconResource = "ExtendHealth.SqlServer.IntegrationServices.Extensions.CsvFileDestination.ico")]
    public class CsvFileDestination : CsvFilePipelineComponentBase
    {
        private StreamWriter _writer;
        private bool _hasWrittenHeader;

        public CsvFileDestination()
            : base("CsvConnectionManagerOutput", CsvFilePipelines.Input)
        {
        }

        public override void AcquireConnections(object transaction)
        {
            AcquireConnections(transaction, FileMode.Create, FileAccess.Write);
        }

        public override DTSValidationStatus Validate()
        {
            DTSValidationStatus baseStatus = base.Validate();
            if (baseStatus != DTSValidationStatus.VS_ISVALID)
            {
                return baseStatus;
            }

            try
            {
                var componentMetaData = this.ComponentMetaData;

                if (componentMetaData.InputCollection.Count != 1)
                {
                    this.FireErrorWithArgs(HResults.DTS_E_INCORRECTEXACTNUMBEROFINPUTS, 1);
                    return DTSValidationStatus.VS_ISCORRUPT;
                }
                if (componentMetaData.OutputCollection.Count != 0)
                {
                    this.FireErrorWithArgs(HResults.DTS_E_INCORRECTEXACTNUMBEROFOUTPUTS, 0);
                    return DTSValidationStatus.VS_ISCORRUPT;
                }

                var input = componentMetaData.InputCollection[0];

                var externalMetadataColumns = input.ExternalMetadataColumnCollection;
                // TODO: Check input.ErrorRowDisposition and figure out what we support and what error to fire when we don't the selected option.
                // TODO: Check input.TruncationRowDisposition and figure out what we support and what error to fire when we don't the selected option.
                if (externalMetadataColumns.Count <= 0)
                {
                    return DTSValidationStatus.VS_NEEDSNEWMETADATA;
                }

                var inputColumns = input.InputColumnCollection;
                var inputColumnsCount = inputColumns.Count;
                if (inputColumnsCount == 0)
                {
                    this.FireErrorWithArgs(HResults.DTS_E_CANNOTHAVEZEROINPUTCOLUMNS, input.IdentificationString);
                    return DTSValidationStatus.VS_ISBROKEN;
                }
                //for (int i = 0; i < inputColumnsCount; i++)
                //{
                //    var inputColumn = inputColumns[i];
                //    TODO: Check inputColumn.ErrorRowDisposition and figure out what we support and what error to fire when we don't the selected option.
                //    TODO: Check inputColumn.TruncationRowDisposition and figure out what we support and what error to fire when we don't the selected option.
                //}

                // TODO: Once we have some custom properties, validate them here.

                if (this._isConnected && componentMetaData.ValidateExternalMetadata)
                {
                    var externalMetadataValidationStatus = this.ValidateWithExternalMetadata(inputColumns, externalMetadataColumns);
                    if (externalMetadataValidationStatus != DTSValidationStatus.VS_ISVALID)
                    {
                        return externalMetadataValidationStatus;
                    }
                }

                for (int i = 0; i < inputColumnsCount; i++)
                {
                    var inputColumn = inputColumns[i];
                    var externalMetadataColumnId = inputColumn.ExternalMetadataColumnID;
                    IDTSExternalMetadataColumn100 externalMetadataColumn;
                    // TODO: How do we deal with all of the externalMetadataColumnIds that are set to 0?
                    if (externalMetadataColumnId != 0)
                    {
                        try
                        {
                            externalMetadataColumn = externalMetadataColumns.FindObjectByID(externalMetadataColumnId);
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            this.FireErrorWithArgs(HResults.DTS_E_COLUMNMAPPEDTONONEXISTENTEXTERNALMETADATACOLUMN, inputColumn.IdentificationString);
                            return DTSValidationStatus.VS_ISBROKEN;
                        }
                        this.ValidateDataTypes(inputColumn, externalMetadataColumn);
                    }
                }

                return DTSValidationStatus.VS_ISVALID;
            }
            catch
            {
                return DTSValidationStatus.VS_ISCORRUPT;
            }
        }

        private DTSValidationStatus ValidateWithExternalMetadata(IDTSInputColumnCollection100 inputColumns, IDTSExternalMetadataColumnCollection100 externalMetadataColumns)
        {
            var inputColumnsCount = inputColumns.Count;
            var externalMetadataColumnsCount = externalMetadataColumns.Count;
            var bodyColumns = this._manager.BodyColumns;
            var bodyColumnsCount = bodyColumns.Count;

            Dictionary<string, CsvColumn> bodyColumnsByName = new Dictionary<string, CsvColumn>(bodyColumnsCount);
            foreach (var bodyColumn in bodyColumns)
            {
                bodyColumnsByName.Add(bodyColumn.Name, bodyColumn);
            }

            HashSet<int> externalMetadataColumnIdsFromInputColumns = new HashSet<int>();
            for (int i = 0; i < inputColumnsCount; i++)
            {
                bool wasAdded = externalMetadataColumnIdsFromInputColumns.Add(inputColumns[i].ExternalMetadataColumnID);
                // If it couldn't be added then we have more than one inputColumn mapped to the same externalMetadataColumn.
                if (!wasAdded)
                {
                    this.FireErrorWithArgs(HResults.DTS_E_COLUMNMAPPEDTOALREADYMAPPEDEXTERNALMETADATACOLUMN, inputColumns[i].IdentificationString, inputColumns[i].ExternalMetadataColumnID);
                    return DTSValidationStatus.VS_NEEDSNEWMETADATA;
                }
            }

            bool[] hasFoundEnternalMetadataColumnForBodyColumn = new bool[bodyColumnsCount];

            for (int i = 0; i < externalMetadataColumnsCount; i++)
            {
                var externalMetadataColumn = externalMetadataColumns[i];
                CsvColumn bodyColumn;
                if (!bodyColumnsByName.TryGetValue(externalMetadataColumn.Name, out bodyColumn))
                {
                    if (externalMetadataColumnIdsFromInputColumns.Contains(externalMetadataColumn.ID))
                    {
                        this.FireErrorWithArgs(HResults.DTS_E_ADODESTEXTERNALCOLNOTEXIST, string.Format(CultureInfo.CurrentCulture, "<<< No body column for external metadata column \"{0}\", but input column exists. >>>", externalMetadataColumn.IdentificationString));
                        return DTSValidationStatus.VS_NEEDSNEWMETADATA;
                    }
                    else
                    {
                        this.ErrorSupport.FireWarningWithArgs(HResults.DTS_W_ADODESTEXTERNALCOLNOTEXIST, string.Format(CultureInfo.CurrentCulture, "<<< No body column for external metadata column \"{0}\", and no input column exists. >>>", externalMetadataColumn.IdentificationString));
                    }
                }
                else
                {
                    hasFoundEnternalMetadataColumnForBodyColumn[i] = true;
                    string columnChanges = string.Empty;
                    if (externalMetadataColumn.CodePage != bodyColumn.CodePage.GetValueOrDefault())
                    {
                        columnChanges += string.Format(CultureInfo.CurrentCulture, "new code page: {0} (old value {1}) ", bodyColumn.CodePage, externalMetadataColumn.CodePage);
                    }
                    if (externalMetadataColumn.Length != bodyColumn.MaxLength)
                    {
                        columnChanges += string.Format(CultureInfo.CurrentCulture, "new length: {0} (old value {1}) ", bodyColumn.MaxLength, externalMetadataColumn.Length);
                    }
                    if (externalMetadataColumn.Precision != bodyColumn.Precision)
                    {
                        columnChanges += string.Format(CultureInfo.CurrentCulture, "new precision: {0} (old value {1}) ", bodyColumn.Precision, externalMetadataColumn.Precision);
                    }
                    if (externalMetadataColumn.Scale != bodyColumn.Scale)
                    {
                        columnChanges += string.Format(CultureInfo.CurrentCulture, "new scale: {0} (old value {1}) ", bodyColumn.Scale, externalMetadataColumn.Scale);
                    }
                    if (externalMetadataColumn.DataType != bodyColumn.IntegrationServicesType)
                    {
                        columnChanges += string.Format(CultureInfo.CurrentCulture, "new data type: {0} (old value {1}) ", bodyColumn.IntegrationServicesType, externalMetadataColumn.DataType);
                    }

                    if (!string.IsNullOrEmpty(columnChanges))
                    {
                        if (externalMetadataColumnIdsFromInputColumns.Contains(externalMetadataColumn.ID))
                        {
                            this.FireErrorWithArgs(HResults.DTS_W_ADODESTEXTERNALCOLNOTMATCHSCHEMACOL, externalMetadataColumn.IdentificationString, columnChanges);
                            return DTSValidationStatus.VS_NEEDSNEWMETADATA;
                        }
                        else
                        {
                            this.ErrorSupport.FireWarningWithArgs(HResults.DTS_W_ADODESTEXTERNALCOLNOTMATCHSCHEMACOL, externalMetadataColumn.IdentificationString, columnChanges);
                        }
                    }
                }
            }

            for (int i = 0; i < hasFoundEnternalMetadataColumnForBodyColumn.Length; i++)
            {
                if (!hasFoundEnternalMetadataColumnForBodyColumn[i])
                {
                    this.ErrorSupport.FireWarningWithArgs(HResults.DTS_W_ADODESTNEWEXTCOL, bodyColumns[i].Name);
                }
            }

            return DTSValidationStatus.VS_ISVALID;
        }

        private void ValidateDataTypes(IDTSInputColumn100 inputColumn, IDTSExternalMetadataColumn100 externalMetadataColumn)
        {
            bool isLong = false;
            DataType convertedInputDataType = PipelineComponent.ConvertBufferDataTypeToFitManaged(inputColumn.DataType, ref isLong);
            DataType externalMetadataDataType = externalMetadataColumn.DataType;

            if (convertedInputDataType == externalMetadataDataType)
            {
                if ((convertedInputDataType == DataType.DT_WSTR) || (convertedInputDataType == DataType.DT_BYTES) || (convertedInputDataType == DataType.DT_NTEXT))
                {
                    if (inputColumn.Length > externalMetadataColumn.Length)
                    {
                        this.ErrorSupport.FireWarningWithArgs(HResults.DTS_W_POTENTIALTRUNCATIONFROMDATAINSERTION, inputColumn.Name, inputColumn.Length, externalMetadataColumn.Name, externalMetadataColumn.Length);
                    }
                }
            }

            if (!PipelineComponent.IsCompatibleNumericTypes(convertedInputDataType, externalMetadataDataType))
            {
                this.ErrorSupport.FireWarningWithArgs(HResults.DTS_W_ADODESTPOTENTIALDATALOSS, inputColumn.Name, convertedInputDataType.ToString(), externalMetadataColumn.Name, externalMetadataDataType.ToString());
            }
        }

        /// <summary>
        /// Called after <see cref="M:Microsoft.SqlServer.Dts.Pipeline.PipelineComponent.PrepareForExecute"/>, and before <see cref="M:Microsoft.SqlServer.Dts.Pipeline.PipelineComponent.PrimeOutput(System.Int32,System.Int32[],Microsoft.SqlServer.Dts.Pipeline.PipelineBuffer[])"/> and <see cref="M:Microsoft.SqlServer.Dts.Pipeline.PipelineComponent.ProcessInput(System.Int32,Microsoft.SqlServer.Dts.Pipeline.PipelineBuffer)"/>.
        /// Writers file header row.
        /// </summary>
        public override void PreExecute()
        {
            base.PreExecute();

            if (_writer != null)
            {
                // TODO: should it flush the output here, or just close?
                _writer.Flush();
                _writer.Close();
                _writer = null;
            }
            _hasWrittenHeader = false;
            if (this._fileStream.CanWrite)
            {
                //get writer
                _writer = new StreamWriter(_fileStream);
            }
        }

        /// <summary>
        /// Called at run time when a <see cref="T:Microsoft.SqlServer.Dts.Pipeline.PipelineBuffer"/> from an upstream component is available to the component to let the component process the incoming rows.
        /// Writes the body rows of the file.
        /// </summary>
        /// <param name="inputID">The ID of the input of the component.</param>
        /// <param name="buffer">The <see cref="T:Microsoft.SqlServer.Dts.Pipeline.PipelineBuffer"/> object.</param>
        public override void ProcessInput(int inputID, PipelineBuffer buffer)
        {
            if (this._fileStream.CanWrite)
            {
                if ((_manager.HasHeader || _manager.BodyColumnNamesAsHeader) && !_hasWrittenHeader)
                {
                    CsvColumnCollection<HeaderTrailerColumn> headerColumns;
                    
                    if(_manager.BodyColumnNamesAsHeader)
                    {
                        var columns = _manager.BodyColumns;
                        headerColumns = new CsvColumnCollection<HeaderTrailerColumn>();

                        if(!_manager.HeaderNotifier.HasValue && _manager.BodyNotifier.HasValue)
                        {
                            _manager.HeaderNotifier = 'H';
                        }

                        foreach (var bodyColumn in columns)
                        {
                            headerColumns.Add(new HeaderTrailerColumn()
                                                  {
                                                      TrimValueOption = TrimOptions.TrimBoth,
                                                      CodePage = 0,
                                                      MaxLength = bodyColumn.Name.Trim().Length,
                                                      IntegrationServicesType = Microsoft.SqlServer.Dts.Runtime.Wrapper.DataType.DT_WSTR,
                                                      ManagedType = typeof(string),
                                                      IsRequired = true,
                                                      Name = bodyColumn.Name,
                                                      EscapeSpaces = true
                                                  });
                        }
                    }
                    else
                    {
                        headerColumns = _manager.HeaderColumns;
                    }

                    var headerColumnsCount = headerColumns.Count;

                    //Write HeaderNotifier
                    if (_manager.HeaderNotifier.HasValue)
                    {
                        _writer.Write(_manager.HeaderNotifier.Value.ToString(CultureInfo.InvariantCulture));
                        WriteDelimiters(_writer, -1, headerColumnsCount);
                    }
                    

                    //Get all the header variables and write them out
                    IDTSVariables100 variables = null;
                    for (int i = 0; i < headerColumnsCount; i++)
                    {
                        //Trace("variable " + _manager.HeaderColumns[i].VariableName);
                        var headerColumn = headerColumns[i];
                        var variableName = headerColumn.VariableName;
                        object variable = null;

                        if(_manager.BodyColumnNamesAsHeader)
                        {
                            variable = headerColumn.Name;
                        }
                        else
                        {
                            this.VariableDispenser.LockOneForRead(variableName, ref variables);
                            variable = variables[this.VariableDispenser.GetQualifiedName(variableName)].Value;
                        }

                        var headerColumnValue = Parse(variable, headerColumn);

                        if (string.IsNullOrEmpty(headerColumnValue) && headerColumn.IsRequired)
                        {
                            throw this.FatalException("Value was not provided for required header field \"" + headerColumn.Name + "\".");
                        }
                        _writer.Write(headerColumnValue);
                        WriteDelimiters(_writer, i, headerColumnsCount);
                    }
                    _hasWrittenHeader = true;
                }


                var escapeCharacter = _manager.EscapeCharacter.ToString(CultureInfo.InvariantCulture);
                var delimiterCharacter = _manager.Delimiter.ToString(CultureInfo.InvariantCulture);

                var bodyColumns = _manager.BodyColumns;
                var bodyColumnsCount = bodyColumns.Count;
                //Trace("Number of rows to write: " + buffer.RowCount);
                while (buffer.NextRow())
                {
                    //Write BodyNotifier, if it exists
                    var bodyNotifier = _manager.BodyNotifier;
                    if (bodyNotifier.HasValue)
                    {
                        _writer.Write(bodyNotifier.Value.ToString(CultureInfo.InvariantCulture));
                        WriteDelimiters(_writer, -1, bodyColumnsCount);
                    }
                    //Write BodyColumn values
                    for (int i = 0; i < bodyColumnsCount; i++)
                    {
                        //Get the column and parse the data into the correct type
                        CsvColumn column = bodyColumns[i];
                        var index = this._columnMappings[column.Name];

                        string stringToWrite = null;
                        var value = GetValueFromBuffer(index, column, buffer);
                        stringToWrite = Parse(value, column);

                        //Escape the data if it has the delimiter character, new line character, or leading or trailing spaces
                        StringBuilder escapedString = new StringBuilder();
                        if (!string.IsNullOrEmpty(stringToWrite) &&
                            (stringToWrite.Contains(delimiterCharacter) || stringToWrite.Contains('\n') || stringToWrite.Contains('\r') || (column.EscapeSpaces && (stringToWrite.StartsWith(" ") || stringToWrite.EndsWith(" "))) || stringToWrite.Contains(escapeCharacter) || column.EscapeSpaces))
                        {
                            if (stringToWrite.Contains(escapeCharacter))
                            {
                                stringToWrite = stringToWrite.Replace(escapeCharacter, escapeCharacter + escapeCharacter);
                            }
                            escapedString.Append(escapeCharacter);
                            escapedString.Append(stringToWrite);
                            escapedString.Append(escapeCharacter);
                        }
                        else
                        {
                            var trimmedValue = TrimString(stringToWrite, column.TrimValueOption);
                            escapedString.Append(trimmedValue);
                        }

                        //Write the escaped string out
                        //Trace("Writing " + escapedString + " out to file");
                        _writer.Write(string.Format(CultureInfo.InvariantCulture, "{0}", escapedString));
                        WriteDelimiters(_writer, i, bodyColumnsCount);
                    }
                    // notify ssis that a row has been written to the file
                    this.ComponentMetaData.IncrementPipelinePerfCounter(DTS_PIPELINE_CTR_ROWSWRITTEN, 1);
                }
            }
        }

        /// <summary>
        /// Called at the end of component execution, but before <see cref="M:Microsoft.SqlServer.Dts.Pipeline.PipelineComponent.Cleanup"/>.
        /// Writes file trailer row and closes the stream.
        /// </summary>
        public override void PostExecute()
        {
            base.PostExecute();
            if (_manager.HasTrailer)
            {
                var trailerColumns = _manager.TrailerColumns;
                var trailerColumnsCount = trailerColumns.Count;

                //Write out TrailerNotifier character
                _writer.Write(_manager.TrailerNotifier.Value.ToString(CultureInfo.InvariantCulture));
                WriteDelimiters(_writer, -1, trailerColumnsCount);

                //Get the trailer variables and write them out
                IDTSVariables100 variables;
                this.VariableDispenser.GetVariables(out variables);
                for (int i = 0; i < trailerColumnsCount; i++)
                {
                    var trailerColumn = trailerColumns[i];
                    var variableName = trailerColumn.VariableName;
                    this.VariableDispenser.LockOneForRead(variableName, ref variables);
                    var trailerColumnValue =
                        Parse(variables[this.VariableDispenser.GetQualifiedName(variableName)].Value,
                              trailerColumn);
                    if (string.IsNullOrEmpty(trailerColumnValue) && trailerColumn.IsRequired)
                    {
						throw this.FatalException("Value was not provided for required trailer field \"" + trailerColumn.Name + "\".");
                    }
                    _writer.Write(trailerColumnValue);
                    WriteDelimiters(_writer, i, trailerColumnsCount);
                }
            }

            _writer.Flush();
            _writer.Close();
            _writer = null;
        }

        private void WriteDelimiters(StreamWriter writer, int position, int columnCount)
        {
            if (position < columnCount - 1)
            {
                writer.Write(_manager.Delimiter.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                writer.WriteLine();
            }
        }

        private object GetValueFromBuffer(int index, CsvColumn column, PipelineBuffer buffer)
        {
            object value = null;
            var managedType = column.ManagedType;
            
            if (!buffer.IsNull(index))
            {
                if (managedType == typeof(string))
                {
                    var s = buffer.GetString(index);
                    if (!String.IsNullOrEmpty(column.Regex))
                    {
                        if (!Regex.IsMatch(s, column.Regex))
                        {
                            bool cancel;
                            this.ErrorSupport.FireError(HResults.DTS_E_DATACONVERSIONFAILED, out cancel);
                            throw new PipelineComponentHResultException(HResults.DTS_E_DATACONVERSIONFAILED);
                        }
                    }
                    if (string.IsNullOrEmpty(s) && column.EmptyStringHandler == EmptyStringOrNull.EmptyString)
                    {
                        value = string.Empty;
                    }
                    else
                    {
                        value = s;
                    }
                }
                else if (managedType == typeof(byte[]))
                {
                    var s = _manager.FileEncoding.GetString(buffer.GetBytes(index));
                    if (string.IsNullOrEmpty(s) && column.EmptyStringHandler == EmptyStringOrNull.EmptyString)
                    {
                        value = string.Empty;
                    }
                    else
                    {
                        value = s;
                    }
                }
                else if (managedType == typeof(DateTime))
                {
                    // PiplineBuffer.GetDateTime method throws an error when the IIS type is DT_DBDATE (DATE type in SQL)
                    // The appropriate method to use is GetDate.
                    if(column.IntegrationServicesType == DataType.DT_DBDATE)
                    {
                        value = buffer.GetDate(index);
                    }
                    else
                    {
                        value = buffer.GetDateTime(index);
                    }
                    
                }
                else if (managedType == typeof(DateTimeOffset))
                {
                    value = buffer.GetDateTimeOffset(index);
                }
                else if (managedType == typeof(TimeSpan))
                {
                    value = buffer.GetTime(index).ToString();
                }
                else if (managedType == typeof(decimal))
                {
                    value = buffer.GetDecimal(index);
                }
                else if (managedType == typeof(Guid))
                {
                    value = buffer.GetGuid(index);
                }
                else if (managedType == typeof(sbyte))
                {
                    value = buffer.GetSByte(index);
                }
                else if (managedType == typeof(short))
                {
                    value = buffer.GetInt16(index);
                }
                else if (managedType == typeof(int))
                {
                    value = buffer.GetInt32(index);
                }
                else if (managedType == typeof(long))
                {
                    value = buffer.GetInt64(index);
                }
                else if (managedType == typeof(bool))
                {
                    value = buffer.GetBoolean(index).ToString();
                }
                else if (managedType == typeof(Single))
                {
                    value = buffer.GetSingle(index);
                }
                else if (managedType == typeof(double))
                {
                    value = buffer.GetDouble(index);
                }
                else if (managedType == typeof(byte))
                {
                    value = buffer.GetByte(index);
                }
                else if (managedType == typeof(ushort))
                {
                    value = buffer.GetUInt16(index);
                }
                else if (managedType == typeof(uint))
                {
                    value = buffer.GetUInt32(index);
                }
                else if (managedType == typeof(ulong))
                {
                    value = buffer.GetUInt64(index);
                }
            }
            if (value == null && column.IsRequired)
            {
				throw this.FatalException("Value was not provided for required field \"" + column.Name + "\".");
            }

            return value;
        }

        private string Parse(object value, CsvColumn column)
        {
            string parsedValue = null;
            if (value != null)
            {
                string format = null;
                bool toUpper = false;
                var managedType = column.ManagedType;
                //get format
                if (string.IsNullOrEmpty(column.Format))
                {
                    if (managedType == typeof(Guid))
                    {
                        format = "B";
                        toUpper = true;
                    }
                }
                else
                {
                    format = column.Format;
                }

                //force invariant parsing
                var formattableType = value as IFormattable;
                if (formattableType != null)
                {
                    parsedValue = formattableType.ToString(format, CultureInfo.InvariantCulture);
                }
                else
                {
                    parsedValue = string.Format(CultureInfo.InvariantCulture, "{0}", value);
                }

                if (toUpper)
                {
                    parsedValue = parsedValue.ToUpper(CultureInfo.InvariantCulture);
                }
            }
            return parsedValue;
        }
    }
}
