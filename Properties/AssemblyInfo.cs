using System;
using System.Diagnostics;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Permissions;

[assembly: AssemblyTitle(ThisAssembly.FileName)]
[assembly: AssemblyDefaultAlias(ThisAssembly.FileName)]
[assembly: AssemblyDescription(ThisAssembly.FileName)]
[assembly: AssemblyCompany(ThisAssembly.Company)]
[assembly: AssemblyProduct(ThisAssembly.Product)]
[assembly: AssemblyCopyright(ThisAssembly.Copyright)]
[assembly: AssemblyVersion(ThisAssembly.Version)]
[assembly: AssemblyFileVersion(ThisAssembly.InformationalVersion)]
[assembly: AssemblyInformationalVersion(ThisAssembly.InformationalVersion)]
[assembly: SatelliteContractVersion(ThisAssembly.Version)]

[assembly: ComVisible(false)]
[assembly: CLSCompliant(true)]
[assembly: NeutralResourcesLanguage("en-US")]
[assembly: Guid("D4F5C63B-0160-42CE-9A66-A0FC897ABBE7")]
#pragma warning disable 0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, Execution = true)]
#pragma warning restore 0618

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else //!DEBUG
[assembly: AssemblyConfiguration("Release")]
#endif //DEBUG

[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints
#if DEBUG
	| DebuggableAttribute.DebuggingModes.DisableOptimizations
#endif //DEBUG
)]

[assembly: CompilationRelaxations((CompilationRelaxations)
	((int)CompilationRelaxations.NoStringInterning /*0x8*/ |
	/*CompilationRelaxations.RelaxedArrayExceptions*/ 0x200 |
	/*CompilationRelaxations.RelaxedInvalidCastException*/ 0x80 |
	/*CompilationRelaxations.RelaxedNullReferenceException*/ 0x20 |
	/*CompilationRelaxations.RelaxedOverflowExceptions*/ 0x800))]

internal static class ThisAssembly
{
	public const string Product = "Extend Health SQL Server Integration Services Extensions";
	public const string Company = "Extend Health, Inc.";
	public const string Copyright = "Copyright © Extend Health, Inc. All rights reserved.";
	public const string FileName = "ExtendHealth.SqlServer.IntegrationServices.Extensions.dll";
	public const string Version = "0.9.9.9";
	public const string InformationalVersion = "0.9.9.9";
}