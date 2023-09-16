using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SqlServer.Dts.Runtime;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Runtime.Serialization;

namespace ExtendHealth.SqlServer.IntegrationServices.Extensions
{
	[CLSCompliant(false)]
	[Serializable]
	[DtsConnection(ConnectionType = "CSV",
		DisplayName = "CSV Connection Manager",
		Description = "Connection manager for csv's with headers and trailers",
		//LocalizationType = typeof(CsvConnectionManager),
        IconResource = "ExtendHealth.SqlServer.IntegrationServices.Extensions.CsvConnectionManager.ico",
		ConnectionContact = ThisAssembly.Company,
		UITypeName = "ExtendHealth.SqlServer.IntegrationServices.Extensions.UI.ShellInitializer, ExtendHealth.SqlServer.IntegrationServices.Extensions.UI, Version=0.1.0.0, Culture=neutral, PublicKeyToken=dc6160b5863b8350")]
	public class CsvConnectionManagerAdapter : ConnectionManagerBase, IDTSComponentPersist
	{
		public override DTSProtectionLevel ProtectionLevel { get; set; }

		[Category("Source File")]
		[DisplayName("File path")]
		public override string ConnectionString
		{
			get
			{
				return this.Manager.ConnectionString;
			}
			set
			{
				this.Manager.ConnectionString = value;
			}
		}

		[MergableProperty(true)]
		[TypeConverter(typeof(ExpandableObjectConverter))]
		public CsvConnectionManager Manager { get; set; }

		public CsvConnectionManagerAdapter()
			: base()
		{
			Manager = new CsvConnectionManager();
		}

		public override DTSExecResult Validate(IDTSInfoEvents infoEvents)
		{
			//if (!File.Exists(Manager.ConnectionString))
			//	infoEvents.FireError(Marshal.GetHRForException(new FileNotFoundException()), "", "The file was not found.", "", 0);
			return base.Validate(infoEvents);
		}

		public override object AcquireConnection(object txn)
		{
			return this.AcquireConnection(txn, FileMode.OpenOrCreate, FileAccess.ReadWrite);
		}
		public FileStream AcquireConnection(object txn, FileMode fileMode, FileAccess fileAccess)
		{
			// TODO: Use configured buffer size.
			var fileStream = new FileStream(this.Manager.ConnectionString, fileMode, fileAccess, FileShare.Read | FileShare.Delete, 8192, FileOptions.SequentialScan);
			if ((fileAccess & FileAccess.Read) != 0)
			{
				// Only try to get the encoding if the file was opened for read (or read/write) access.
				this.Manager.FileEncoding = GetFileEncoding(fileStream);
			}
			return fileStream;
		}

		public override void ReleaseConnection(object connection)
		{
			var fileStream = connection as FileStream;
			if (fileStream != null)
			{
				fileStream.Close();
			}
			base.ReleaseConnection(connection);
		}

		public void LoadFromXML(System.Xml.XmlElement node, IDTSInfoEvents infoEvents)
		{
			Type type = node.GetType().GetType();
			System.Reflection.Assembly mscorlibAssembly = System.Reflection.Assembly.Load("mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
			Type encoderBestFitType = mscorlibAssembly.GetType("System.Text.InternalEncoderBestFitFallback");
			Type decoderBestFitType = mscorlibAssembly.GetType("System.Text.InternalDecoderBestFitFallback");
			List<Type> knownTypes = new List<Type>()
			{
				type,
				encoderBestFitType,
				decoderBestFitType,
				typeof(DecoderReplacementFallback),
				typeof(EncoderReplacementFallback)
			};
			foreach (EncodingInfo info in Encoding.GetEncodings())
			{
				Encoding enc = info.GetEncoding();
				knownTypes.Add(enc.GetType());
			}
			DataContractSerializer dcs = new DataContractSerializer(typeof(CsvConnectionManager), knownTypes, 0x7FFF, false, true, null);
			Manager = dcs.ReadObject(node.CreateNavigator().ReadSubtree()) as CsvConnectionManager ?? new CsvConnectionManager();
		}

		public void SaveToXML(System.Xml.XmlDocument doc, IDTSInfoEvents infoEvents)
		{
			Type type = doc.GetType().GetType();
			System.Reflection.Assembly mscorlibAssembly = System.Reflection.Assembly.Load("mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
			Type encoderBestFitType = mscorlibAssembly.GetType("System.Text.InternalEncoderBestFitFallback");
			Type decoderBestFitType = mscorlibAssembly.GetType("System.Text.InternalDecoderBestFitFallback");
			List<Type> knownTypes = new List<Type>()
			{
				type,
				encoderBestFitType,
				decoderBestFitType,
				typeof(DecoderReplacementFallback),
				typeof(EncoderReplacementFallback)
			};
			foreach (EncodingInfo info in Encoding.GetEncodings())
			{
				Encoding enc = info.GetEncoding();
				knownTypes.Add(enc.GetType());
			}
			DataContractSerializer dcs = new DataContractSerializer(typeof(CsvConnectionManager), knownTypes, 0x7FFF, false, true, null);
			StringWriter sw = new StringWriter();
			XmlTextWriter writer = new XmlTextWriter(sw);
			dcs.WriteObject(writer, this.Manager);
			doc.LoadXml(sw.GetStringBuilder().ToString());
		}

		public static Encoding GetFileEncoding(Stream stream)
		{
				Encoding enc = Encoding.UTF8;
				byte[] buffer = new byte[5];

				stream.Read(buffer, 0, 5);

				//if (buffer[0] == 0xef && buffer[1] == 0xbb && buffer[2] == 0xbf)
				//    enc = Encoding.UTF8;
				/*else*/ if (buffer[0] == 0xfe && buffer[1] == 0xff)
					enc = Encoding.Unicode;
				else if (buffer[0] == 0 && buffer[1] == 0 && buffer[2] == 0xfe && buffer[3] == 0xff)
					enc = Encoding.UTF32;
				else if (buffer[0] == 0x2b && buffer[1] == 0x2f && buffer[2] == 0x76)
					enc = Encoding.UTF7;
				stream.Seek(0, SeekOrigin.Begin);
				return enc;
		}
	}
}
