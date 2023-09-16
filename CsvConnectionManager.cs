using System;
using System.Runtime.Serialization;
using Microsoft.SqlServer.Dts.Runtime;
using System.IO;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Text;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.Threading;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
	[CLSCompliant(false)]
	[Serializable]
	public sealed class CsvConnectionManager : INotifyPropertyChanged
	{
		private const char DefaultEscapeCharacter = '"';
		private const char DefaultDelimiter = ',';
	    private const int DefaultBufferSize = 8192;
		public CsvConnectionManager()
		{
			this._bodyColumns = new CsvColumnCollection<CsvColumn>();
			this._headerColumns = new CsvColumnCollection<HeaderTrailerColumn>();
			this._trailerColumns = new CsvColumnCollection<HeaderTrailerColumn>();
			this._escapeCharacter = DefaultEscapeCharacter;
			this._delimiter = DefaultDelimiter;
			this._fileEncoding = Encoding.UTF8;
		}

        private static readonly PropertyChangedEventArgs BodyColumnNamesAsHeaderChangedEventArgs = new PropertyChangedEventArgs("BodyColumnNamesAsHeader");
        [OptionalField]
	    private bool _useBodyColumnNamesAsHeader;
	    [Category("Header & Trailer")]
	    [DisplayName("Body column names as header")]
        
	    public bool BodyColumnNamesAsHeader
	    {
	        get
	        {
	            return this._useBodyColumnNamesAsHeader;
	        }
            set
            {
                if(this._useBodyColumnNamesAsHeader != value)
                {
                    this._useBodyColumnNamesAsHeader = value;
                    if(value && this._hasHeader)
                    {
                        this.HasHeader = !value;    
                    }
                    
                    this.OnPropertyChanged(BodyColumnNamesAsHeaderChangedEventArgs);
                }
            }
	    }

		private static readonly PropertyChangedEventArgs HasHeaderPropertyChangedEventArgs = new PropertyChangedEventArgs("HasHeader");
		private bool _hasHeader;
		[Category("Header & Trailer")]
		[DisplayName("Has header")]
		public bool HasHeader
		{
			get
			{
				return this._hasHeader;
			}
			set
			{
				if (this._hasHeader != value)
				{
					this._hasHeader = value;
                    if(value && this._useBodyColumnNamesAsHeader)
                    {
                        this._useBodyColumnNamesAsHeader = !value;
                    }
					this.OnPropertyChanged(HasHeaderPropertyChangedEventArgs);
				}
			}
		}

		private static readonly PropertyChangedEventArgs HasTrailerPropertyChangedEventArgs = new PropertyChangedEventArgs("HasTrailer");
		private bool _hasTrailer;
		[Category("Header & Trailer")]
		[DisplayName("Has trailer")]
		public bool  HasTrailer
		{
			get
			{
				return this._hasTrailer;
			}
			set
			{
				if (this._hasTrailer != value)
				{
					this._hasTrailer = value;
					this.OnPropertyChanged(HasTrailerPropertyChangedEventArgs);
				}
			}
		}

		private static readonly PropertyChangedEventArgs HeaderNotifierPropertyChangedEventArgs = new PropertyChangedEventArgs("HeaderNotifier");
		private char? _headerNotifier;
		[Category("Header & Trailer")]
		[DisplayName("Header notification character")]
		public char? HeaderNotifier
		{
			get
			{
				return this._headerNotifier;
			}
			set
			{
				if (this._headerNotifier != value)
				{
					this._headerNotifier = value;
					this.OnPropertyChanged(HeaderNotifierPropertyChangedEventArgs);
				}
			}
		}

		private static readonly PropertyChangedEventArgs BodyNotifierPropertyChangedEventArgs = new PropertyChangedEventArgs("BodyNotifier");
		private char? _bodyNotifier;
		[Category("Header & Trailer")]
		[DisplayName("Body notification character")]
		public char? BodyNotifier
		{
			get
			{
				return this._bodyNotifier;
			}
			set
			{
				if (this._bodyNotifier != value)
				{
					this._bodyNotifier = value;
					this.OnPropertyChanged(BodyNotifierPropertyChangedEventArgs);
				}
			}
		}

		private static readonly PropertyChangedEventArgs TrailerNotifierPropertyChangedEventArgs = new PropertyChangedEventArgs("TrailerNotifier");
		private char? _trailerNotifier;
		[Category("Header & Trailer")]
		[DisplayName("Trailer notification character")]
		public char? TrailerNotifier
		{
			get
			{
				return this._trailerNotifier;
			}
			set
			{
				if (this._trailerNotifier != value)
				{
					this._trailerNotifier = value;
					this.OnPropertyChanged(TrailerNotifierPropertyChangedEventArgs);
				}
			}
		}

		private static readonly PropertyChangedEventArgs FileEncodingPropertyChangedEventArgs = new PropertyChangedEventArgs("FileEncoding");
		private Encoding _fileEncoding;
		[Category("File")]
		[DisplayName("File encoding")]
		[TypeConverter(typeof(ExpandableObjectConverter))]
		public Encoding FileEncoding
		{
			get
			{
				return this._fileEncoding;
			}
			set
			{
				if (this._fileEncoding != value)
				{
					this._fileEncoding = value;
					this.OnPropertyChanged(FileEncodingPropertyChangedEventArgs);
				}
			}
		}

		private static readonly PropertyChangedEventArgs DelimiterPropertyChangedEventArgs = new PropertyChangedEventArgs("Delimiter");
		private char _delimiter;
		[Category("File")]
		[DefaultValue(DefaultDelimiter)]
		public char Delimiter
		{
			get
			{
				return this._delimiter;
			}
			set
			{
				if (this._delimiter != value)
				{
					this._delimiter = value;
					this.OnPropertyChanged(DelimiterPropertyChangedEventArgs);
				}
			}
		}

		private static readonly PropertyChangedEventArgs EscapeCharacterPropertyChangedEventArgs = new PropertyChangedEventArgs("EscapeCharacter");
		private char _escapeCharacter;
		[Category("File")]
		[DisplayName("Escape character")]
		[DefaultValue(DefaultEscapeCharacter)]
		public char EscapeCharacter
		{
			get
			{
				return this._escapeCharacter;
			}
			set
			{
				if (this._escapeCharacter != value)
				{
					this._escapeCharacter = value;
					this.OnPropertyChanged(EscapeCharacterPropertyChangedEventArgs);
				}
			}
		}

		private static readonly PropertyChangedEventArgs BodyColumnsPropertyChangedEventArgs = new PropertyChangedEventArgs("BodyColumns");
		private readonly CsvColumnCollection<CsvColumn> _bodyColumns;
		[Category("Columns")]
		[DisplayName("Body columns")]
		public CsvColumnCollection<CsvColumn> BodyColumns
		{
			get
			{
				return this._bodyColumns;
			}
		}

		private static readonly PropertyChangedEventArgs HeaderColumnsPropertyChangedEventArgs = new PropertyChangedEventArgs("HeaderColumns");
		private readonly CsvColumnCollection<HeaderTrailerColumn> _headerColumns;
		[Category("Columns")]
		[DisplayName("Header columns")]
		public CsvColumnCollection<HeaderTrailerColumn> HeaderColumns
		{
			get
			{
				return this._headerColumns;
			}
		}

		private static readonly PropertyChangedEventArgs TrailerColumnsPropertyChangedEventArgs = new PropertyChangedEventArgs("TrailerColumns");
		private readonly CsvColumnCollection<HeaderTrailerColumn> _trailerColumns;
		[Category("Columns")]
		[DisplayName("Trailer columns")]
		public CsvColumnCollection<HeaderTrailerColumn> TrailerColumns
		{
			get
			{
				return this._trailerColumns;
			}
		}

		private static readonly PropertyChangedEventArgs ConnectionStringPropertyChangedEventArgs = new PropertyChangedEventArgs("ConnectionString");
		private string _connectionString;
		[Category("General")]
		[DisplayName("File path")]
		[Editor("System.Windows.Forms.Design.FileNameEditor, System.Design, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
		public string ConnectionString
		{
			get
			{
				return _connectionString;
			}
			set
			{
				if (_connectionString != value)
				{
					_connectionString = value;
					this.OnPropertyChanged(ConnectionStringPropertyChangedEventArgs);
				}
			}
		}

        #region BufferSize property
        [OptionalField]
        private int? _bufferSize;
        [Category("General")]
        [Description("The buffer size used when reading from a file.")]
        [DefaultValue(DefaultBufferSize)]
        public int BufferSize
        {
            get
            {
                return this._bufferSize ?? DefaultBufferSize;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException("value", value, "BufferSize cannot be negative.");
                }
                this._bufferSize = value;
            }
        }
        #endregion // BufferSize property

		private void HandleBodyColumnsPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			this.OnPropertyChanged(BodyColumnsPropertyChangedEventArgs);
		}
		private void HandleHeaderColumnsPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			this.OnPropertyChanged(HeaderColumnsPropertyChangedEventArgs);
		}
		private void HandleTrailerColumnsPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			this.OnPropertyChanged(TrailerColumnsPropertyChangedEventArgs);
		}

		[NonSerialized]
		private PropertyChangedEventHandler _propertyChanged;
		public event PropertyChangedEventHandler PropertyChanged
		{
			add
			{
				if ((object)value != null)
				{
					lock (this)
					{
						var currentHandler = this._propertyChanged;
						this._propertyChanged = (PropertyChangedEventHandler)Delegate.Combine(currentHandler, value);

						if ((object)currentHandler == null)
						{
							this._bodyColumns.PropertyChanged += this.HandleBodyColumnsPropertyChanged;
							this._headerColumns.PropertyChanged += this.HandleHeaderColumnsPropertyChanged;
							this._trailerColumns.PropertyChanged += this.HandleTrailerColumnsPropertyChanged;
						}
					}
				}
			}
			remove
			{
				if ((object)value != null)
				{
					lock (this)
					{
						var newHandler = this._propertyChanged = (PropertyChangedEventHandler)Delegate.Remove(this._propertyChanged, value);

						if ((object)newHandler == null)
						{
							this._bodyColumns.PropertyChanged -= this.HandleBodyColumnsPropertyChanged;
							this._headerColumns.PropertyChanged -= this.HandleHeaderColumnsPropertyChanged;
							this._trailerColumns.PropertyChanged -= this.HandleTrailerColumnsPropertyChanged;
						}
					}
				}
			}
		}

		private void OnPropertyChanged(PropertyChangedEventArgs e)
		{
			var propertyChanged = this._propertyChanged;
			if ((object)propertyChanged != null)
			{
				propertyChanged(this, e);
			}
		}
	}
}
