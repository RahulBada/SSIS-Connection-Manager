This repository is used to build and install the custom SSIS connection manager used by the eligibility file ETLs. Note that the UI project in this solution is located in its own `SSIS-Extensions` repo.

Prerequisites for building the solution:
1. SQL Server Integration Services (the SQL Server 2019 version) installed on the machine that you're building from. This can be installed using the SQL Server 2019 installer -- check the Integration Services option in the installer (or install it with SQL Server 2019 Installation Center later). You don't have to install any other parts of SQL Server and you can use the free Dev version of SQL Server.
2. `SSIS-Extensions` repo for the UI project on your machine. This repo is referenced from the solution file that we're going to build.
3. *Data storage and processing* workload installed in Visual Studio. You can add this from the Visual Studio Installer.
4. SQL Server Integration Services Projects extension for Visual Studio.

To build the solution:
1. Open Visual Studio as an Administrator. This is necessary because the build process adds DLLs to the Global Assembly Cache (GAC) which requires Administrator privileges.
2. Open the `ExtendHealth.SqlServer.IntegrationServices.Extensions.sln` solution file.
3. Build the solution (Build menu -> Build Solution)
4. Check the output window to make sure the build succeeded for both projects. You should also see several "file copied" messages (which comes from copying the DLLs to the GAC).
5. Close Visual Studio and open it again. You do not have to run as Administrator any more.
