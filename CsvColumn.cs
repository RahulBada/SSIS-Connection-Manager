using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading;
using Microsoft.SqlServer.Dts.Runtime.Wrapper;
using Microsoft.SqlServer.Dts.Pipeline.Wrapper;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
    [Serializable]
    public enum EmptyStringOrNull
    {
        Null,
        EmptyString
    }

    [Serializable]
    public class CsvColumn : INotifyPropertyChanged
    {
        private const int DefaultCodePage = 1252;

        public CsvColumn()
        {
            this.ManagedType = typeof(string);
            this.IntegrationServicesType = DataType.DT_WSTR;
            this.SetDefaultsForDataType();
        }

		[CLSCompliant(false)]
        public string _managedTypeName;

        private static readonly PropertyChangedEventArgs ManagedTypePropertyChangedEventArgs = new PropertyChangedEventArgs("ManagedType");
        [NonSerialized]
        private Type _managedType;
        [TypeConverter(typeof(CsvColumnDataTypeTypeConverter))]
        [Category("Data")]
        [DisplayName("Managed type")]
        [DefaultValue(typeof(string))]
        public Type ManagedType
        {
            get
            {
                if ((this._managedType == null || this._managedType == typeof(string)) && !String.IsNullOrEmpty(this._managedTypeName))
                {
                    this._managedType = Type.GetType(this._managedTypeName);
                }
                return this._managedType;
            }
            set
            {
                if (this._managedType != value)
                {
                    this._managedType = value;
                    this._managedTypeName = this._managedType.AssemblyQualifiedName;
                    this.OnPropertyChanged(ManagedTypePropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs IntegrationServicesTypePropertyChangedEventArgs = new PropertyChangedEventArgs("IntegrationServicesType");
        private Microsoft.SqlServer.Dts.Runtime.Wrapper.DataType _integrationServicesType;
		[CLSCompliant(false)]
        [TypeConverter(typeof(CsvColumnIntegrationServicesTypeTypeConverter))]
        [Category("Data")]
        [DisplayName("Integration Services type")]
        [DefaultValue(Microsoft.SqlServer.Dts.Runtime.Wrapper.DataType.DT_WSTR)]
        public Microsoft.SqlServer.Dts.Runtime.Wrapper.DataType IntegrationServicesType
        {
            get
            {
                return this._integrationServicesType;
            }
            set
            {
                if (this._integrationServicesType != value)
                {
                    this._integrationServicesType = value;
                    this.OnPropertyChanged(IntegrationServicesTypePropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs TrimValueOptionPropertyChangedEventArgs = new PropertyChangedEventArgs("TrimValueOption");
        private TrimOptions _trimValueOption;
        [Category("Data")]
        [Description("Indicates how the system should clean up the data.")]
        [DisplayName("Trim the value")]
        public TrimOptions TrimValueOption
        {
            get
            {
                return _trimValueOption;
            }
            set
            {
                if (_trimValueOption != value)
                {
                    _trimValueOption = value;
                    this.OnPropertyChanged(TrimValueOptionPropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs EmptyStringHandlerPropertyChangedEventArgs = new PropertyChangedEventArgs("EmptyStringHandler");
        private EmptyStringOrNull _emptyStringHandler;
        [Category("Data")]
        [Description("Indicates how to handle an empty string value on a string type.")]
        [DisplayName("Handle empty string as")]
        public EmptyStringOrNull EmptyStringHandler
        {
            get
            {
                return _emptyStringHandler;
            }
            set
            {
                if (_emptyStringHandler != value)
                {
                    _emptyStringHandler = value;
                    this.OnPropertyChanged(EmptyStringHandlerPropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs IsRequirePropertyChangedEventArgs = new PropertyChangedEventArgs("IsRequired");
        [System.Runtime.Serialization.OptionalField]
        private bool _isRequired;
        [Category("Data")]
        [Description("Indicates whether the field is required to contain a value.")]
        [DisplayName("Required field")]
        [DefaultValue(false)]
        public bool IsRequired
        {
            get
            {
                return _isRequired;
            }
            set
            {
                if (_isRequired != value)
                {
                    _isRequired = value;
                    this.OnPropertyChanged(IsRequirePropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs ErrorRowDispositionPropertyChangedEventArgs = new PropertyChangedEventArgs("ErrorRowDisposition");
        [System.Runtime.Serialization.OptionalField] 
        private DTSRowDisposition _errorRowDisposition;
		[CLSCompliant(false)]
        [Category("Data")]
        [Description("Indicates how an error with this column should be handled.")]
        [DisplayName("Error handling")]
        [DefaultValue(DTSRowDisposition.RD_FailComponent)]
        public DTSRowDisposition ErrorRowDisposition
        {
            get
            {
                return _errorRowDisposition;
            }
            set
            {
                if (_errorRowDisposition != value)
                {
                    _errorRowDisposition = value;
                    this.OnPropertyChanged(ErrorRowDispositionPropertyChangedEventArgs);
                }
            }
        }


        private static readonly PropertyChangedEventArgs NamePropertyChangedEventArgs = new PropertyChangedEventArgs("Name");
        private string _name;
        [Category("General")]
        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    this.OnPropertyChanged(NamePropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs FormatPropertyChangedEventArgs = new PropertyChangedEventArgs("Format");
        private string _format;
        [Category("Data")]
        public string Format
        {
            get
            {
                return _format;
            }
            set
            {
                if (_format != value)
                {
                    _format = value;
                    this.OnPropertyChanged(FormatPropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs RegexPropertyChangedEventArgs = new PropertyChangedEventArgs("Regex");
        private string _regex;
        [Category("Data")]
        public string Regex
        {
            get
            {
                return _regex;
            }
            set
            {
                if (_regex != value)
                {
                    _regex = value;
                    this.OnPropertyChanged(RegexPropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs MinValuePropertyChangedEventArgs = new PropertyChangedEventArgs("MinValue");
        private int? _minValue;
        [Category("Data")]
        [DisplayName("Minimum value")]
        public int? MinValue
        {
            get
            {
                return _minValue; ;
            }
            set
            {
                if (_minValue != value)
                {
                    _minValue = value;
                    this.OnPropertyChanged(MinValuePropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs MaxValuePropertyChangedEventArgs = new PropertyChangedEventArgs("MaxValue");
        private int? _maxValue;
        [Category("Data")]
        [DisplayName("Maximum value")]
        public int? MaxValue
        {
            get
            {
                return _maxValue;
            }
            set
            {
                if (_maxValue != value)
                {
                    _maxValue = value;
                    this.OnPropertyChanged(MaxValuePropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs MinLengthPropertyChangedEventArgs = new PropertyChangedEventArgs("MinLength");
        private int _minLength;
        [Category("Data")]
        [DisplayName("Minimum length")]
        public int MinLength
        {
            get
            {
                return _minLength;
            }
            set
            {
				if ((IntegrationServicesType == DataType.DT_WSTR || IntegrationServicesType == DataType.DT_BYTES || IntegrationServicesType == DataType.DT_NTEXT) && (ManagedType == typeof(string) || ManagedType == typeof(byte[])))
                {
                    if (value < 0)
                    {
                        throw new ArgumentException("Min length cannot be less than 0");
                    }
                    if (value > MaxLength)
                    {
                        throw new ArgumentException("Min length cannot be greater than max length");
                    }
                }
                if (_minLength != value)
                {
                    _minLength = value;
                    this.OnPropertyChanged(MinLengthPropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs MaxLengthPropertyChangedEventArgs = new PropertyChangedEventArgs("MaxLength");
        private int _maxLength;
        [Category("Data")]
        [DisplayName("Maximum length")]
        public int MaxLength
        {
            get
            {
                return _maxLength;
            }
            set
            {
				if ((IntegrationServicesType == DataType.DT_WSTR || IntegrationServicesType == DataType.DT_BYTES || IntegrationServicesType == DataType.DT_NTEXT) && (ManagedType == typeof(string) || ManagedType == typeof(byte[])))
                {
                    if (value < 1)
                    {
                        throw new ArgumentException("Max length must greater than 0");
                    }
                    if (value < MinLength)
                    {
                        throw new ArgumentException("Max length must be greater than or equal to min length");
                    }
                }
                if (_maxLength != value)
                {
                    _maxLength = value;
                    this.OnPropertyChanged(MaxLengthPropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs PrecisionPropertyChangedEventArgs = new PropertyChangedEventArgs("Precision");
        private int _precision;
        [Category("Data")]
        public int Precision
        {
            get
            {
                return _precision;
            }
            set
            {
                if (_precision != value)
                {
                    _precision = value;
                    this.OnPropertyChanged(PrecisionPropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs ScalePropertyChangedEventArgs = new PropertyChangedEventArgs("Scale");
        private int _scale;
        [Category("Data")]
        public int Scale
        {
            get
            {
                return _scale;
            }
            set
            {
                if (_scale != value)
                {
                    _scale = value;
                    this.OnPropertyChanged(ScalePropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs CodePagePropertyChangedEventArgs = new PropertyChangedEventArgs("CodePage");
        private int? _codePage;
        [Category("Data")]
        [DisplayName("Code page")]
        public int? CodePage
        {
            get
            {
                return this._codePage ?? DefaultCodePage;
            }
            set
            {
                if (_codePage != value)
                {
                    _codePage = value;
                    this.OnPropertyChanged(CodePagePropertyChangedEventArgs);
                }
            }
        }

        private static readonly PropertyChangedEventArgs EscapeSpacesPropertyChangedEventArgs = new PropertyChangedEventArgs("EscapeSpaces");
        private bool _escapeSpaces;
        [Category("General")]
        [DisplayName("Escape leading and trailing spaces")]
        public bool EscapeSpaces
        {
            get
            {
                return _escapeSpaces;
            }
            set
            {
                if (_escapeSpaces != value)
                {
                    _escapeSpaces = value;
                    this.OnPropertyChanged(EscapeSpacesPropertyChangedEventArgs);
                }
            }
        }

        [NonSerialized]
        private PropertyChangedEventHandler _propertyChanged;
        public event PropertyChangedEventHandler PropertyChanged
        {
            add
            {
                if ((object)value != null)
                {
                    PropertyChangedEventHandler handler;
                    while ((object)Interlocked.CompareExchange<PropertyChangedEventHandler>(ref this._propertyChanged, (PropertyChangedEventHandler)Delegate.Combine(handler = this._propertyChanged, value), handler) != (object)handler) ;
                }
            }
            remove
            {
                if ((object)value != null)
                {
                    PropertyChangedEventHandler handler;
                    while ((object)Interlocked.CompareExchange<PropertyChangedEventHandler>(ref this._propertyChanged, (PropertyChangedEventHandler)Delegate.Remove(handler = this._propertyChanged, value), handler) != (object)handler) ;
                }
            }
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            var propertyChanged = this._propertyChanged;
            if ((object)propertyChanged != null)
            {
                propertyChanged(this, e);
                if (e.PropertyName == "IntegrationServicesType")
                {
                    SetDefaultsForDataType();
                }
            }
        }

        private void SetDefaultsForDataType()
        {
            switch (IntegrationServicesType)
            {
                case DataType.DT_DECIMAL:
                    MaxLength = 0;
                    if (Scale <= 0)
                        Scale = 1;
                    Precision = 0;
                    CodePage = 0;
                    break;
                case DataType.DT_CY:
                    MaxLength = 0;
                    Scale = 0;
                    Precision = 0;
                    CodePage = 0;
                    break;
                case DataType.DT_NUMERIC:
                    MaxLength = 0;
                    if (Scale <= 0)
                        Scale = 1;
                    if (Precision < 1)
                        Precision = 1;
                    CodePage = 0;
                    break;
                case DataType.DT_BYTES:
                    if (MaxLength <= 0)
                        MaxLength = 100;
                    Scale = 0;
                    Precision = 0;
                    CodePage = 0;
                    break;
                case DataType.DT_STR:
                    if (MaxLength <= 0)
                        MaxLength = 100;
                    if (MaxLength >= 8000)
                        MaxLength = 7999;
                    Scale = 0;
                    Precision = 0;
                    CodePage = 1252;
                    break;
                case DataType.DT_WSTR:
                    if (MaxLength <= 0)
                        MaxLength = 100;
                    if (MaxLength >= 4000)
                        MaxLength = 3999;
                    Scale = 0;
                    Precision = 0;
                    CodePage = 0;
                    break;
				case DataType.DT_NTEXT:
					if (MaxLength <= 0)
						MaxLength = 100;
					if (MaxLength >= (1 << 30) - 1)
						MaxLength = (1 << 30) - 2;
					Scale = 0;
					Precision = 0;
					CodePage = 0;
					break;
                default:
                    MaxLength = 0;
                    Scale = 0;
                    Precision = 0;
                    CodePage = 0;
                    break;
            }
        }
    }
}
