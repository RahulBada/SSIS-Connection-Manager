using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.Dts.Pipeline;
using Microsoft.SqlServer.Dts.Runtime.Wrapper;
using Microsoft.SqlServer.Dts.Pipeline.Wrapper;
using Microsoft.SqlServer.Dts.Runtime;
using System.Text.RegularExpressions;
using System.Text;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
    [Serializable]
    public enum RowType
    {
		Unknown = 0,
        Header = 1,
        Body = 2,
        Trailer = 3
    }

    [CLSCompliant(false)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Disposed via ReleaseConnections.")]
    [DtsPipelineComponent(
        ComponentType = ComponentType.SourceAdapter,
        DisplayName = "CSV File Source",
        Description = "File source for CSV files with headers and trailers",
        IconResource = "ExtendHealth.SqlServer.IntegrationServices.Extensions.CsvFileSource.ico")]
    public class CsvFileSource : CsvFilePipelineComponentBase
    {
        private const string DateTimeOffsetFormatString = "yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'fffffff' 'zzz";

        private const int DefaultCodePage = 1252;
        private List<string> _columnValues = new List<string>();
        private bool _hasSetCount;
        private int _columnCount;

        private List<RowError> RowErrors = new List<RowError>();

        public CsvFileSource()
            : base("CsvConnectionManagerInput", CsvFilePipelines.Output)
        {
        }

        public override void ProvideComponentProperties()
        {
            base.ProvideComponentProperties();
            // add column count variable reference holder
            var columnCountProperty = ComponentMetaData.CustomPropertyCollection.New();
            columnCountProperty.Name = "ColumnCountVariable";
            columnCountProperty.Value = null;
            columnCountProperty.TypeConverter = typeof(IntegerVariableListConverter).AssemblyQualifiedName;
            // add column count equality restriction
            var isUniformColumnCountEnforcedProperty = ComponentMetaData.CustomPropertyCollection.New();
            isUniformColumnCountEnforcedProperty.Name = "IsUniformColumnCountEnforced";
            isUniformColumnCountEnforcedProperty.Value = false;
            isUniformColumnCountEnforcedProperty.Description =
                "Indicates whether to enforce column count uniformity across rows.";
        }

        public override void PreExecute()
        {
            //base.PreExecute();
            //Debugger.Launch();

            GetOutputIds();
            _columnCount = 0;
            _hasSetCount = false;
            _columnValues.Clear();
            RowErrors.Clear();
        }

        public override void AcquireConnections(object transaction)
        {
            AcquireConnections(transaction, FileMode.Open, FileAccess.Read);
        }

        public override DTSValidationStatus Validate()
        {
            GetOutputIds();
            DTSValidationStatus baseStatus = base.Validate();
            if (baseStatus != DTSValidationStatus.VS_ISVALID)
            {
                return baseStatus;
            }

            // TODO: Maybe wrap everything with a try / catch

            var componentMetaData = this.ComponentMetaData;

            if (componentMetaData.InputCollection.Count != 0)
            {
                this.FireErrorWithArgs(HResults.DTS_E_INCORRECTEXACTNUMBEROFINPUTS, 0);
                return DTSValidationStatus.VS_ISCORRUPT;
            }
            if (componentMetaData.OutputCollection.Count != 2)
            {
                this.FireErrorWithArgs(HResults.DTS_E_INCORRECTEXACTNUMBEROFOUTPUTS, 2);
                return DTSValidationStatus.VS_ISCORRUPT;
            }

            var outputCollection = componentMetaData.OutputCollection.OfType<IDTSOutput100>();
            var output = outputCollection.FirstOrDefault(n => n.ID == _defaultOutputId);

            var externalMetadataColumns = output.ExternalMetadataColumnCollection;
            // TODO: Check output.ErrorRowDisposition and figure out what we support and what error to fire when we don't the selected option.
            // TODO: Check output.TruncationRowDisposition and figure out what we support and what error to fire when we don't the selected option.
            if (externalMetadataColumns.Count <= 0)
            {
                return DTSValidationStatus.VS_NEEDSNEWMETADATA;
            }

            var outputColumns = output.OutputColumnCollection;
            var outputColumnsCount = outputColumns.Count;
            if (outputColumnsCount == 0)
            {
                this.FireErrorWithArgs(HResults.DTS_E_CANNOTHAVEZEROOUTPUTCOLUMNS, output.IdentificationString);
                return DTSValidationStatus.VS_ISBROKEN;
            }

            // TODO: Once we have some custom properties, validate them here.

            if (this._isConnected && componentMetaData.ValidateExternalMetadata)
            {
                var externalMetadataValidationStatus = this.ValidateWithExternalMetadata(outputColumns, externalMetadataColumns);
                if (externalMetadataValidationStatus != DTSValidationStatus.VS_ISVALID)
                {
                    return externalMetadataValidationStatus;
                }
            }

            return DTSValidationStatus.VS_ISVALID;
        }

        private DTSValidationStatus ValidateWithExternalMetadata(IDTSOutputColumnCollection100 outputColumns, IDTSExternalMetadataColumnCollection100 externalMetadataColumns)
        {
            // TODO: This.
            return DTSValidationStatus.VS_ISVALID;
        }

        // TODO: Maybe need to override SetOutputColumnDataTypeProperties.

        // TODO: Evaluate whether we need to use this method or any of its contents somewhere
        private void PopulateColumnData(IDTSOutput100 output, CsvColumn column)
        {
            IDTSOutputColumn100 col = output.OutputColumnCollection.New();
            IDTSExternalMetadataColumn100 exCol = output.ExternalMetadataColumnCollection.New();
            col.Name = column.Name;
            col.ErrorRowDisposition = DTSRowDisposition.RD_FailComponent;
            exCol.Name = column.Name;
            switch (column.IntegrationServicesType)
            {
                case DataType.DT_DECIMAL:
                    column.MaxLength = 0;
                    if (column.Scale <= 0)
                        column.Scale = 1;
                    column.Precision = 0;
                    column.CodePage = 0;
                    break;
                case DataType.DT_CY:
                    column.MaxLength = 0;
                    column.Scale = 0;
                    column.Precision = 0;
                    column.CodePage = 0;
                    break;
                case DataType.DT_NUMERIC:
                    column.MaxLength = 0;
                    if (column.Scale <= 0)
                        column.Scale = 1;
                    if (column.Precision < 1)
                        column.Precision = 1;
                    column.CodePage = 0;
                    break;
                case DataType.DT_BYTES:
                    if (column.MaxLength <= 0)
                        column.MaxLength = 100;
                    column.Scale = 0;
                    column.Precision = 0;
                    column.CodePage = 0;
                    break;
                case DataType.DT_STR:
                    if (column.MaxLength <= 0)
                        column.MaxLength = 100;
                    if (column.MaxLength >= 8000)
                        column.MaxLength = 7999;
                    column.Scale = 0;
                    column.Precision = 0;
                    if (column.CodePage == 0)
                        column.CodePage = 1252;
                    break;
                case DataType.DT_WSTR:
                    if (column.MaxLength <= 0)
                        column.MaxLength = 100;
                    if (column.MaxLength >= 4000)
                        column.MaxLength = 3999;
                    column.Scale = 0;
                    column.Precision = 0;
                    column.CodePage = 0;
                    break;
				case DataType.DT_NTEXT:
					if (column.MaxLength <= 0)
						column.MaxLength = 100;
					if (column.MaxLength >= (1 << 30) - 1)
						column.MaxLength = (1 << 30) - 2;
					column.Scale = 0;
					column.Precision = 0;
					column.CodePage = 0;
					break;
            }
            col.SetDataTypeProperties(column.IntegrationServicesType, column.MaxLength, column.Precision, column.Scale, column.CodePage.Value);
            col.ErrorRowDisposition = DTSRowDisposition.RD_NotUsed;
            exCol.DataType = column.IntegrationServicesType;
            exCol.Length = column.MaxLength;
            exCol.Precision = column.Precision;
            exCol.Scale = column.Scale;
            exCol.CodePage = column.CodePage.Value;
            col.ExternalMetadataColumnID = exCol.ID;
        }

        /// <summary>
        /// Redirects the error row based on RowErrors and ErrorRowDisposition.
        /// </summary>
        /// <param name="defaultBuffer">The default buffer.</param>
        /// <param name="errorBuffer">The error buffer.</param>
		public void HandleBodyRowErrors(PipelineBuffer defaultBuffer, PipelineBuffer errorBuffer)
		{
			var failRow =
				RowErrors.FirstOrDefault(
					n =>
					n.Column.ErrorRowDisposition == DTSRowDisposition.RD_NotUsed ||
					n.Column.ErrorRowDisposition == DTSRowDisposition.RD_FailComponent);
			var redirectRow =
				RowErrors.FirstOrDefault(n => n.Column.ErrorRowDisposition == DTSRowDisposition.RD_RedirectRow);
			if (failRow != null)
			{
				Debug.Fail("Execution shouldn't be able to reach this point. We think.");
				throw this.FatalException(failRow);
			}
			else if (redirectRow != null && errorBuffer != null)
			{
				//asynchronous errors
				errorBuffer.AddRow();
				for (int x = 0; x < _columnValues.Count; x++)
				{
					errorBuffer[x] = _columnValues[x];
				}
				var columnIndex = _manager.BodyColumns.IndexOf(redirectRow.Column);
				
				errorBuffer.SetErrorInfo(_errorOutputId, redirectRow.HResult, columnIndex);
				//get error messages

				var message = string.Join(" ", RowErrors.Select(x => x.Message));

				//sanitize
				message = message.Replace("\r\n", string.Empty);
				// count + 2 (for the built in ErrorCode and ErrorColumn) gives us the last position in the buffer
				errorBuffer[_manager.BodyColumns.Count() + 2] = message;
				defaultBuffer.RemoveRow();
			}
			RowErrors.Clear();
		}

        /// <summary>
        /// Raises the row error.
        /// </summary>
        /// <param name="rowError">The row error.</param>
        public void RaiseRowError(RowError rowError)
        {
            //get ErrorRowDisposition
            var rowDisposition = rowError.Column.ErrorRowDisposition;
            if (rowDisposition == DTSRowDisposition.RD_NotUsed ||
                rowDisposition == DTSRowDisposition.RD_FailComponent)
            {
				throw this.FatalException(rowError);
            }
            else if (rowDisposition == DTSRowDisposition.RD_RedirectRow)
            {
                RowErrors.Add(rowError);
            }
        }

        private static readonly DataType[] StringLikeDataTypes = new[] { DataType.DT_WSTR, DataType.DT_BYTES, DataType.DT_STR, DataType.DT_TEXT, DataType.DT_NTEXT };

        public override void PrimeOutput(int outputs, int[] outputIDs, PipelineBuffer[] buffers)
        {
            var outputIds = outputIDs.ToList();
            PipelineBuffer defaultBuffer = null;
            if (outputIds.Contains(_defaultOutputId))
            {
                defaultBuffer = buffers[outputIds.IndexOf(_defaultOutputId)];
            }
            PipelineBuffer errorBuffer = null;
            if (outputIds.Contains(_errorOutputId))
            {
                errorBuffer = buffers[outputIds.IndexOf(_errorOutputId)];
            }

            //Trace("Starting output");
            StreamReader reader = new StreamReader(_fileStream);
            char[] charBuffer = new char[_manager.BufferSize];
            bool inQualifiedBlock = false;
            bool firstPass = true;
            bool isEndOfFile = false;
            int currentColumn = 0;
            //RowType rowType = _manager.HasHeader ? RowType.Header : RowType.Body;
			RowType rowType = RowType.Unknown;
            IDTSVariables100 variables = null;
            StringBuilder builder = new StringBuilder();
            int length;
            int offset = 0;
            int escapeCount = 0;
            int rowCount = 0;
            string previousChar = null;
            string currentChar = null;


            var escapeCharacter = _manager.EscapeCharacter.ToString(CultureInfo.InvariantCulture);
            var delimiterCharacter = _manager.Delimiter.ToString(CultureInfo.InvariantCulture);

            //process the file in buffered chunks
            while ((length = reader.ReadBlock(charBuffer, 0, charBuffer.Length)) > 0)
            {
                for (int i = offset; (i < length && (currentChar = charBuffer[i].ToString(CultureInfo.InvariantCulture)) != "\0") || 
                        (isEndOfFile && previousChar == delimiterCharacter); i++)
                {
                    //Check for end of file as end of line
                    isEndOfFile = i >= length - 1 && reader.Peek() == -1;

                    if (i < length || (isEndOfFile && previousChar == delimiterCharacter))
                    {
                        if (isEndOfFile && previousChar == delimiterCharacter && i >= length)
                        {
                            currentChar = "\r";
                        }
                        else
                        {
                            currentChar = charBuffer[i].ToString(CultureInfo.InvariantCulture);
                        }
                        //Check for end of quoted block
                        if (inQualifiedBlock)
                        {
                            if (escapeCount % 2 == 0 && previousChar == escapeCharacter)
                            {
                                if (currentChar == delimiterCharacter || currentChar == "\r" || currentChar == "\n")
                                {
                                    inQualifiedBlock = false;
                                    builder.Remove(builder.Length - 1, 1);
                                }
                                else if (currentChar != escapeCharacter)
                                {
                                    var message = string.Format(CultureInfo.InvariantCulture, "Row {0} in file \"{1}\" has an unqualifed escape character.",
                                                                         rowCount + 1, _manager.ConnectionString);
                                    // Not sure why this was just throwing and not raising an error. I'm changing it to be consistent with everything else... -KMO, 2013-03-20
                                    ////bool ignore;
                                    ////this.ComponentMetaData.FireError(-1, null, message, null, 0, out ignore);
                                    //throw new FormatException(message);
                                    throw this.FatalException(new FormatException(message));
                                }
                            }
                            else if (isEndOfFile && currentChar != escapeCharacter)
                            {
                                var message = string.Format(CultureInfo.InvariantCulture, "Row {0} in file \"{1}\" has an unqualifed escape character.",
                                                                          rowCount + 1, _manager.ConnectionString);
                                // Not sure why this was just throwing and not raising an error. I'm changing it to be consistent with everything else... -KMO, 2013-03-20
                                ////bool ignore;
                                ////this.ComponentMetaData.FireError(-1, null, message, null, 0, out ignore);
                                //throw new FormatException(message);
                                throw this.FatalException(new FormatException(message));
                            }
                        }

                        //Account for last row not having a return
                        if (isEndOfFile && i < length)
                        {
                            if (currentChar != escapeCharacter && currentChar != delimiterCharacter && currentChar != "\r" && currentChar != "\n")
                            {
                                builder.Append(currentChar);
                            }
                            if (currentChar != delimiterCharacter)
                            {
                                previousChar = currentChar;
                                currentChar = "\r";
                            }
                        }

                        //Finished column or file
                        if (((currentChar == delimiterCharacter || currentChar == "\r" || currentChar == "\n" || currentChar == "\0")
                            && !inQualifiedBlock) || isEndOfFile)
                        {
                            //Trace("Finished column results: " + builder.ToString());
                            //Move past the \n character, on next iteration
                            if (i < length - 1)
                            {
								if (currentChar == "\r" && charBuffer[i + 1] == '\n')
								{
									i++;
								}
                            }
                            

                            //Get RowType if on first character of new line
                            if (currentColumn == 0 && rowType == RowType.Unknown)
                            {
                                if (builder.Length == 0 && (currentChar == "\r" || currentChar == "\n"))
                                {
                                    continue;
                                }

                                // Copy the value out, and trim it, so that we can determine if this is the row type or the first data column.
                                var dataType = TrimString(builder.ToString(), TrimOptions.TrimBoth);

                                if (i < length)
                                {
                                    offset = 0;
                                    //this.ComponentMetaData.FireWarning(0, this.ComponentMetaData.Name, "Offset reset", null, 0);
                                }
                                else
                                {
                                    //if the index has been moved forward a set number of spaces, carry the offset change to the next buffer chunk
                                    offset = i - length;
                                    //this.ComponentMetaData.FireWarning(0, this.ComponentMetaData.Name, "Offset: " + offset, null, 0);
                                }

                                if (_manager.HasHeader && _manager.HeaderNotifier.HasValue &&
                                    dataType == _manager.HeaderNotifier.Value.ToString(CultureInfo.InvariantCulture) && firstPass)
                                {
                                    rowType = RowType.Header;
                                    builder.Clear();
									firstPass = false;
                                    continue;
                                }
                                else if (_manager.HasTrailer && _manager.TrailerNotifier.HasValue
                                    && dataType == _manager.TrailerNotifier.Value.ToString(CultureInfo.InvariantCulture))
                                {
                                    rowType = RowType.Trailer;
                                    builder.Clear();
									firstPass = false;
                                    continue;
                                }
                                else if (_manager.BodyNotifier.HasValue && dataType == _manager.BodyNotifier.Value.ToString(CultureInfo.InvariantCulture))
                                {
                                    rowType = RowType.Body;
                                    defaultBuffer.AddRow();
                                    builder.Clear();
									firstPass = false;
                                    continue;
                                }
                                //Default to Body row if no identifier found
                                else
                                {
                                    rowType = RowType.Body;
                                    defaultBuffer.AddRow();
									firstPass = false;
                                }
                            }

                            CsvColumn column = null;
                            try
                            {
                                //Grab the current column based on the row type
                                switch (rowType)
                                {
                                    case RowType.Header:
                                        if (_manager.HeaderColumns.Count > 0)
                                            column = _manager.HeaderColumns[currentColumn];
                                        break;
                                    case RowType.Body:
                                        column = _manager.BodyColumns[currentColumn];
                                        break;
                                    case RowType.Trailer:
                                        if (_manager.TrailerColumns.Count > 0)
                                            column = _manager.TrailerColumns[currentColumn];
                                        break;
									case RowType.Unknown:
									default:
										Debug.Fail("Expected to have RowType set by this point.");
										break;
                                }
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                throw this.FatalException(
                                    string.Format(CultureInfo.InvariantCulture, "Row {0} in file \"{1}\" has too many columns.", rowCount + 1, _manager.ConnectionString),
                                    HResults.DTS_E_INCORRECTCOLUMNCOUNT);
                            }

                            //Validate the column results
							int rowErrorsCountBeforeParsing = RowErrors.Count;
                            object obj = null;
                            if (column != null)
                            {
                                var unescapedValue =
                                    builder.ToString().Replace(escapeCharacter + escapeCharacter,
                                                               escapeCharacter);
                                var trimmedValue = TrimString(unescapedValue, column.TrimValueOption);
                                if (string.IsNullOrEmpty((trimmedValue)))
                                {
                                    if (StringLikeDataTypes.Any(n => n == column.IntegrationServicesType) &&
                                        column.EmptyStringHandler == EmptyStringOrNull.EmptyString)
                                    {
										// Call Parse anyway so that other column constraints can still be enforced.
                                        obj = Parse(string.Empty, column, rowCount + 1);
                                    }
                                    else if (column.IsRequired)
                                    {
                                        RaiseRowError(new RowError(rowCount + 1, column, ErrorType.RequiredFieldWasEmpty,
                                            string.Format(CultureInfo.InvariantCulture, "Line {1}: Required field \"{0}\" is empty.", column.Name, (rowCount + 1))));
                                    }
                                }
                                else
                                {

                                    //this.ComponentMetaData.FireWarning(0, null, string.Format("Parcing  cell: {0}, {1}, {2}", rowCount, column.Name, trimmedValue), null, 0);
                                    obj = Parse(trimmedValue, column, rowCount + 1);
                                }
								// This is only used for the error output.
                                _columnValues.Add(trimmedValue);
                                builder.Clear();
                            }
                            if (rowType == RowType.Header || rowType == RowType.Trailer)
                            {
                                if (column != null)
                                {
                                    //Trace("Putting " + obj.ToString() + " into variable " + (column as HeaderTrailerColumn).VariableName);
                                    //Add a value for the variable defined
                                    IDTSVariable100 variable = null;
                                    var headerTrailerColumn = column as HeaderTrailerColumn;
                                    var variableName = headerTrailerColumn.VariableName;
                                    this.VariableDispenser.LockForRead(variableName);
                                    this.VariableDispenser.GetVariables(out variables);
                                    variable = variables[variableName];
                                    if (variable != null)
                                    {
                                        if (column.ManagedType == typeof(DateTimeOffset))
                                        {
                                            var variableTypeCode =
                                                DtsConvert.TypeCodeFromVarType(checked((ushort)variable.DataType));
                                            if (variableTypeCode == TypeCode.String)
                                            {
                                                obj = ((DateTimeOffset)obj).ToString(DateTimeOffsetFormatString,
                                                                                      System.Globalization.
                                                                                          DateTimeFormatInfo.
                                                                                          InvariantInfo);
                                            }
                                        }
                                        variable.Value = obj;
                                    }
                                    variables.Unlock();
                                }
                            }
                            else
                            {
								if (RowErrors.Count == rowErrorsCountBeforeParsing)
								{
									// There were no errors, so we can put the value for this column into the dataflow.

									//Trace("Putting into dataflow: " + obj.ToString());
									//this.ComponentMetaData.FireWarning(0, null, string.Format("Filling  cell: {0}, {1}, {2}", rowCount, column.Name, obj), null, 0);
									//try
									//{
									PutIntoDataFlow(obj, column.IntegrationServicesType, currentColumn, defaultBuffer);
									//}
									//catch (DoesNotFitBufferException ex)
									//{
									//    throw new FormatException(
									//        string.Format("The value [{0}] on row [{4}] does not fit within the limits (min {1}/ max {2}) allowed by the column [{3}].",
									//        obj, column.MinLength, column.MaxLength, column.Name, rowCount + 1), ex);
									//}
								}
                            }
                            if (currentChar == "\r" || currentChar == "\n")
                            {
                                //End of the row, reset the column count
                                //Ignore empty lines.
                                if (previousChar != "\r" && previousChar != "\n")
                                {
                                    if (rowType == RowType.Body)
                                    {
                                        var isUniformColumnCountEnforcedProperty =
                                            this.ComponentMetaData.CustomPropertyCollection["IsUniformColumnCountEnforced"];
                                        if (_columnCount == 0)
                                        {
                                            _columnCount = currentColumn + 1;

                                            //set columnCount if a variable exists
                                            var columnCountProperty =
                                                this.ComponentMetaData.CustomPropertyCollection["ColumnCountVariable"];
                                            if (columnCountProperty != null &&
                                                !string.IsNullOrEmpty((string)columnCountProperty.Value) &&
                                                !_hasSetCount)
                                            {
                                                var columnCountVariableName = (string)columnCountProperty.Value;
                                                this.VariableDispenser.LockForRead(columnCountVariableName);

                                                IDTSVariables100 customVariables = null;
                                                VariableDispenser.GetVariables(out customVariables);
                                                if (customVariables.Locked)
                                                {
                                                    var count = customVariables[0];
                                                    count.Value = _columnCount;
                                                    customVariables.Unlock();

                                                }
                                                else
                                                {
                                                    VariableDispenser.Reset();
                                                }
                                                _hasSetCount = true;
                                            }
                                        }
                                        else if (isUniformColumnCountEnforcedProperty != null &&
                                            (bool)isUniformColumnCountEnforcedProperty.Value &&
                                            _columnCount != currentColumn + 1)
                                        {
											throw this.FatalException(
												string.Format(CultureInfo.InvariantCulture, "Row {0} in file \"{1}\" has an inconsistent number of columns compared to prior rows.", rowCount + 1, _manager.ConnectionString),
												HResults.DTS_E_INCORRECTCOLUMNCOUNT);
                                        }
                                    }

									if (this.RowErrors.Count > 0)
									{
										if (rowType != RowType.Body)
										{
											throw this.FatalException(this.RowErrors[0]);
										}
										else
										{
											this.HandleBodyRowErrors(defaultBuffer, errorBuffer);
										}
									}
                                    //reset columns for next row
                                    _columnValues.Clear();
                                    currentColumn = 0;
                                    rowType = RowType.Unknown;
                                    rowCount++;
                                }
                            }
                            else
                            {
                                //Still in the same row, next column
                                currentColumn++;
                            }

                            escapeCount = 0;

                            //This may not be necessary
                            if (currentChar == "\0")
                            {
                                break;
                            }
                        }
                        //If there is an escape character by itself, we are either beginning or ending a qualified block
                        else if (!inQualifiedBlock && currentChar == escapeCharacter)
                        {
                            if (builder.Length != 0)
                            {
								var message = string.Format(CultureInfo.InvariantCulture, "Row {0} in file \"{1}\" has an unqualifed escape character.",
									 rowCount + 1, _manager.ConnectionString);
								// Not sure why this was just throwing and not raising an error. I'm changing it to be consistent with everything else... -KMO, 2013-03-29
								////bool ignore;
								////this.ComponentMetaData.FireError(-1, null, message, null, 0, out ignore);
								//throw new FormatException(message);
								throw this.FatalException(new FormatException(message));
                            }
                            inQualifiedBlock = true;
                            escapeCount++;
                            continue;
                        }
                        //If there is a delimiter in a qualified block or any other character, append it
                        else if (currentChar != delimiterCharacter ||
                                 (currentChar == delimiterCharacter && inQualifiedBlock))
                        {
                            builder.Append(currentChar);
                        }

                        if (currentChar == escapeCharacter)
                        {
                            escapeCount++;
                        }
                    }
                    //track previous character
                    previousChar = currentChar;
                }
            }
            //end of file reached
            defaultBuffer.SetEndOfRowset();
            if (errorBuffer != null)
            {
                errorBuffer.SetEndOfRowset();
            }
        }

        private void PutIntoDataFlow(object value, DataType type, int columnIndex, PipelineBuffer buffer)
        {
            //blob types do not work with SetNull method
            if (value == null && (type != DataType.DT_TEXT || type != DataType.DT_IMAGE || type != DataType.DT_NTEXT))
            {
				// TODO: Now that we support DT_NTEXT, what do we do if we have null for an NTEXT column? Does just not outputting anything for that column produce the same result? -KMO, 2016-11-01

                //allows for nulls of any type
                buffer.SetNull(columnIndex);
            }
            else
            {
                switch (type)
                {
                    case DataType.DT_WSTR:
					case DataType.DT_NTEXT:
                        buffer.SetString(columnIndex, value.ToString());
                        break;
                    case DataType.DT_BYTES:
                        buffer.SetBytes(columnIndex, (byte[])value);
                        break;
                    case DataType.DT_FILETIME:
                    case DataType.DT_DBTIMESTAMP:
                    case DataType.DT_DBTIMESTAMP2:
                        buffer.SetDateTime(columnIndex, (DateTime)value);
                        break;
                    case DataType.DT_DBTIMESTAMPOFFSET:
                        buffer.SetDateTimeOffset(columnIndex, (DateTimeOffset)value);
                        break;
                    case DataType.DT_DBTIME:
                    case DataType.DT_DBTIME2:
                        buffer.SetTime(columnIndex, (TimeSpan)value);
                        break;
                    case DataType.DT_DBDATE:
                    case DataType.DT_DATE:
                        buffer.SetDateTime(columnIndex, ((DateTime)value).Date);
                        break;
                    case DataType.DT_NUMERIC:
                        buffer.SetDecimal(columnIndex, (decimal)value);
                        break;
                    case DataType.DT_GUID:
                        buffer.SetGuid(columnIndex, (Guid)value);
                        break;
                    case DataType.DT_I1:
                        buffer.SetSByte(columnIndex, (sbyte)value);
                        break;
                    case DataType.DT_I2:
                        buffer.SetInt16(columnIndex, (short)value);
                        break;
                    case DataType.DT_I4:
                        buffer.SetInt32(columnIndex, (int)value);
                        break;
                    case DataType.DT_I8:
                        buffer.SetInt64(columnIndex, (long)value);
                        break;
                    case DataType.DT_BOOL:
                        buffer.SetBoolean(columnIndex, (bool)value);
                        break;
                    case DataType.DT_R4:
                        buffer.SetSingle(columnIndex, (float)value);
                        break;
                    case DataType.DT_R8:
                        buffer.SetDouble(columnIndex, (double)value);
                        break;
                    case DataType.DT_UI1:
                        buffer.SetByte(columnIndex, (byte)value);
                        break;
                    case DataType.DT_UI2:
                        buffer.SetUInt16(columnIndex, (ushort)value);
                        break;
                    case DataType.DT_UI4:
                        buffer.SetUInt32(columnIndex, (uint)value);
                        break;
                    case DataType.DT_UI8:
                        buffer.SetUInt64(columnIndex, (ulong)value);
                        break;
                }
            }
        }

        private object Parse(string input, CsvColumn column, int rowNumber)
        {
            Type type = column.ManagedType;
            string format = column.Format;
            string regex = column.Regex;
            int minLength = column.MinLength;
            int maxLength = column.MaxLength;
            int? minValue = column.MinValue;
            int? maxValue = column.MaxValue;

            //Trace("Parsing: " + input + " to type: " + type.Name);
            try
            {
                if (type == typeof(string))
                {
                    if (!string.IsNullOrEmpty(regex))
                    {
                        if (!Regex.IsMatch(input, regex))
                        {
                            RaiseRowError(new RowError(rowNumber, column, ErrorType.ViolatedRegex,
                                string.Format(CultureInfo.InvariantCulture, "Line {1}: Value for field \"{0}\" does not match the regular expression.", column.Name, rowNumber)));
                        }
                    }
                    if (minLength <= maxLength && maxLength > 0)
                    {
                        if (input.Length < minLength)
						{
							RaiseRowError(new RowError(rowNumber, column, ErrorType.LessThanMinLength,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Length of value for field \"{0}\" is less than minimum length ({1}).", column.Name, minLength, rowNumber),
								HResults.DTS_E_INVALIDSTRINGLENGTH));
						}
						if (input.Length > maxLength)
                        {
                            RaiseRowError(new RowError(rowNumber, column, ErrorType.GreaterThanMaxLength,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Length of value for field \"{0}\" is greater than maximum length ({1}).", column.Name, maxLength, rowNumber),
								HResults.DTS_E_INVALIDSTRINGLENGTH));
                        }
                    }
                    return input;
                }
                else if (type == typeof(byte[]))
                {
					// TODO: Add length validation.
					// TODO: This doesn't make any sense...
                    if (_manager.FileEncoding != null)
                    {
                        return _manager.FileEncoding.GetBytes(input);
                    }
                    else
                    {
                        return Encoding.Default.GetBytes(input);
                    }
                }
                else if (type == typeof(DateTime))
                {
                    if (!String.IsNullOrEmpty(format))
                    {
                        return DateTime.ParseExact(input, format, CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        return DateTime.Parse(input, CultureInfo.InvariantCulture);
                    }
                }
                else if (type == typeof(DateTimeOffset))
                {
                    if (!String.IsNullOrEmpty(format))
                    {
                        return DateTimeOffset.ParseExact(input, format, CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        return DateTimeOffset.Parse(input, CultureInfo.InvariantCulture);
                    }
                }
                else if (type == typeof(TimeSpan))
                {
                    return TimeSpan.Parse(input);
                }
                else if (type == typeof(decimal))
                {
                    decimal val = decimal.Parse(input, CultureInfo.InvariantCulture);
                    if (minValue.HasValue)
                    {
                        if (val < minValue.GetValueOrDefault())
                        {
                            RaiseRowError(new RowError(rowNumber, column, ErrorType.LessThanMinValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is less than minimum value ({1}).", column.Name, minValue, rowNumber)));
                        }
                    }
                    if (maxValue.HasValue)
                    {
                        if (val > maxValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.GreaterThanMaxValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is greater than maximum value ({1}).", column.Name, maxValue, rowNumber)));
                        }
                    }
                    return val;
                }
                else if (type == typeof(Guid))
                {
                    return new Guid(input);
                }
                else if (type == typeof(sbyte))
                {
                    sbyte val = sbyte.Parse(input, CultureInfo.InvariantCulture);
                    if (minValue.HasValue)
                    {
                        if (val < minValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.LessThanMinValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is less than minimum value ({1}).", column.Name, minValue, rowNumber)));
                        }
                    }
                    if (maxValue.HasValue)
                    {
                        if (val > maxValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.GreaterThanMaxValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is greater than maximum value ({1}).", column.Name, maxValue, rowNumber)));
                        }
                    }
                    return val;
                }
                else if (type == typeof(short))
                {
                    short val = short.Parse(input, CultureInfo.InvariantCulture);
                    if (minValue.HasValue)
                    {
                        if (val < minValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.LessThanMinValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is less than minimum value ({1}).", column.Name, minValue, rowNumber)));
                        }
                    }
                    if (maxValue.HasValue)
                    {
                        if (val > maxValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.GreaterThanMaxValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is greater than maximum value ({1}).", column.Name, maxValue, rowNumber)));
                        }
                    }
                    return val;
                }
                else if (type == typeof(int))
                {
                    int val = int.Parse(input, CultureInfo.InvariantCulture);
                    if (minValue.HasValue)
                    {
                        if (val < minValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.LessThanMinValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is less than minimum value ({1}).", column.Name, minValue, rowNumber)));
                        }
                    }
                    if (maxValue.HasValue)
                    {
                        if (val > maxValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.GreaterThanMaxValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is greater than maximum value ({1}).", column.Name, maxValue, rowNumber)));
                        }
                    }
                    return val;
                }
                else if (type == typeof(long))
                {
                    long val = long.Parse(input, CultureInfo.InvariantCulture);
                    if (minValue.HasValue)
                    {
                        if (val < minValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.LessThanMinValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is less than minimum value ({1}).", column.Name, minValue, rowNumber)));
                        }
                    }
                    if (maxValue.HasValue)
                    {
                        if (val > maxValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.GreaterThanMaxValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is greater than maximum value ({1}).", column.Name, maxValue, rowNumber)));
                        }
                    }
                    return val;
                }
                else if (type == typeof(bool))
                {
                    return bool.Parse(input);
                }
                else if (type == typeof(float))
                {
                    float val = float.Parse(input, CultureInfo.InvariantCulture);
                    if (minValue.HasValue)
                    {
                        if (val < minValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.LessThanMinValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is less than minimum value ({1}).", column.Name, minValue, rowNumber)));
                        }
                    }
                    if (maxValue.HasValue)
                    {
                        if (val > maxValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.GreaterThanMaxValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is greater than maximum value ({1}).", column.Name, maxValue, rowNumber)));
                        }
                    }
                    return val;
                }
                else if (type == typeof(double))
                {
                    double val = double.Parse(input, CultureInfo.InvariantCulture);
                    if (minValue.HasValue)
                    {
                        if (val < minValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.LessThanMinValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is less than minimum value ({1}).", column.Name, minValue, rowNumber)));
                        }
                    }
                    if (maxValue.HasValue)
                    {
                        if (val > maxValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.GreaterThanMaxValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is greater than maximum value ({1}).", column.Name, maxValue, rowNumber)));
                        }
                    }
                    return val;
                }
                else if (type == typeof(byte))
                {
                    byte val = byte.Parse(input, CultureInfo.InvariantCulture);
                    if (minValue.HasValue)
                    {
                        if (val < minValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.LessThanMinValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is less than minimum value ({1}).", column.Name, minValue, rowNumber)));
                        }
                    }
                    if (maxValue.HasValue)
                    {
                        if (val > maxValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.GreaterThanMaxValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is greater than maximum value ({1}).", column.Name, maxValue, rowNumber)));
                        }
                    }
                    return val;
                }
                else if (type == typeof(ushort))
                {
                    ushort val = ushort.Parse(input, CultureInfo.InvariantCulture);
                    if (minValue.HasValue)
                    {
                        if (val < minValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.LessThanMinValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is less than minimum value ({1}).", column.Name, minValue, rowNumber)));
                        }
                    }
                    if (maxValue.HasValue)
                    {
                        if (val > maxValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.GreaterThanMaxValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is greater than maximum value ({1}).", column.Name, maxValue, rowNumber)));
                        }
                    }
                    return val;
                }
                else if (type == typeof(uint))
                {
                    uint val = uint.Parse(input, CultureInfo.InvariantCulture);
                    if (minValue.HasValue)
                    {
                        if (val < minValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.LessThanMinValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is less than minimum value ({1}).", column.Name, minValue, rowNumber)));
                        }
                    }
                    if (maxValue.HasValue)
                    {
                        if (val > maxValue.GetValueOrDefault())
                        {
							RaiseRowError(new RowError(rowNumber, column, ErrorType.GreaterThanMaxValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is greater than maximum value ({1}).", column.Name, maxValue, rowNumber)));
                        }
                    }
                    return val;
                }
				else if (type == typeof(ulong))
				{
					ulong val = ulong.Parse(input, CultureInfo.InvariantCulture);
					if (minValue.HasValue)
					{
						if (val < (ulong)minValue.GetValueOrDefault())
						{
							RaiseRowError(new RowError(rowNumber, column, ErrorType.LessThanMinValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is less than minimum value ({1}).", column.Name, minValue, rowNumber)));
						}
					}
					if (maxValue.HasValue)
					{
						if (val > (ulong)maxValue.GetValueOrDefault())
						{
							RaiseRowError(new RowError(rowNumber, column, ErrorType.GreaterThanMaxValue,
                                string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" is greater than maximum value ({1}).", column.Name, maxValue, rowNumber)));
						}
					}
					return val;
				}
				else
				{
					this.FatalException("An unrecognized or unsupported data type was specified for a column.");
				}
            }
            catch (FormatException ex)
            {
                RaiseRowError(new RowError(rowNumber, column, ErrorType.CouldNotParse,
                    string.Format(CultureInfo.InvariantCulture, "Line {2}: Value for field \"{0}\" could not be parsed: {1}.", column.Name, ex.Message, rowNumber),
					ex));
            }
            return null;
        }
    }
}