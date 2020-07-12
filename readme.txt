----------------------------------------
Thank you for installing dotnetreport.
----------------------------------------

The nuget package adds client side code to your project, which calls a remote service at https://www.dotnetreport.com. 

Installing this package adds app setting keys to your web.config, and you have to replace the dummy values with your Account Api tokens. 
You must create an online account get the Api tokens.  

You can run your project and navigate to:
	http://localhost/report for the Report Builder
	http://localhost/report/dashboard for the Reports Dashboard
	http://localhost/setup for the Admin Setup 

To start the Scheduler Job, add the following line to your Startup.cs:

	JobScheduler.Start();

For .net Core, dotnet Report uses npm packages for all the client side libraries, and uses gulp to place them in the wwwroot/js/lib folder. 

----------------------------------------------
-----------------IMPORTANT--------------------
----------------------------------------------

With .net core nuget package, there are some additional steps to get dotnet Report running locally in your project. 

1. Need to get files locally in your project.

First, add GeneratePathProperty="true" dotNetReport.core package reference:

  <ItemGroup>
    <PackageReference Include="dotNetReport.core" Version="x.x.x" GeneratePathProperty="true" />
  </ItemGroup>

Next, add the following to your project to copy front end files included in your project directly rather than as a reference.

  <PropertyGroup>
    <ContentFilesPath>$(PkgdotNetReport_core)\contentFiles\any\any\</ContentFilesPath>
  </PropertyGroup>
  <Target Name="CopyDotNetReportContent" BeforeTargets="PreBuildEvent">
    <Copy SourceFiles="$(ContentFilesPath)gulpfile.dotnetreport.js" DestinationFiles="$(ProjectDir)\gulpfile.dotnetreport.js" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)package.dotnetreport.json" DestinationFiles="$(ProjectDir)\package.dotnetreport.json" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)wwwroot/img/report-logo.png" DestinationFiles="$(ProjectDir)\wwwroot/img/report-logo.png" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)wwwroot/js/dotnetreport.js" DestinationFiles="$(ProjectDir)\wwwroot/js/dotnetreport.js" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)wwwroot/js/dotnetreport-helper.js" DestinationFiles="$(ProjectDir)\wwwroot/js/dotnetreport-helper.js" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)wwwroot/css/dotnetreport.css" DestinationFiles="$(ProjectDir)\wwwroot/css/dotnetreport.css" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)Jobs/DotNetReportJob.cs" DestinationFiles="$(ProjectDir)Jobs/DotNetReportJob.cs" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)Models/DotNetReportModel.cs" DestinationFiles="$(ProjectDir)Models/DotNetReportModel.cs" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)Controllers/ReportApiController.cs" DestinationFiles="$(ProjectDir)Controllers/ReportApiController.cs" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)Controllers/ReportController.cs" DestinationFiles="$(ProjectDir)Controllers/ReportController.cs" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)Controllers/SetupController.cs" DestinationFiles="$(ProjectDir)Controllers/SetupController.cs" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)Views/Report/Dashboard.cshtml" DestinationFiles="$(ProjectDir)Views/Report/Dashboard.cshtml" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)Views/Report/Index.cshtml" DestinationFiles="$(ProjectDir)Views/Report/Index.cshtml" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)Views/Report/Report.cshtml" DestinationFiles="$(ProjectDir)Views/Report/Report.cshtml" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)Views/Setup/Index.cshtml" DestinationFiles="$(ProjectDir)Views/Setup/Index.cshtml" SkipUnchangedFiles="true"></Copy>
    <Copy SourceFiles="$(ContentFilesPath)Views/Shared/_Layout.Report.cshtml" DestinationFiles="$(ProjectDir)Views/Shared/_Layout.Report.cshtml" SkipUnchangedFiles="true"></Copy>
  </Target>

2. Client side packages need to be added to your package.json file. 

The list of libraries dotnet Report uses from npm is included package.dotnetreport.json. Please manually merge the contents in to your project's actual package.json.

Then run gulpfile.dotnetreport.js. 

3. Add Static Config to your Startup.cs

Your Starup.cs should look like this:

 public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            StaticConfig = configuration; //<--- Add this line manually
        }

        public IConfiguration Configuration { get; }
        public static IConfiguration StaticConfig { get; private set; }  //<--- Add this line manually


If your project is not using controller with Views, you would also need to add Controller with Views and set NewtonSoft Json setting in your Startup.cs

		services.AddControllersWithViews()
                .AddNewtonsoftJson(options => options.UseMemberCasing());

You should be able to build and run the project after the above changes. 

For more details and documentation, you can visit https://www.dotnetreport.com. 

