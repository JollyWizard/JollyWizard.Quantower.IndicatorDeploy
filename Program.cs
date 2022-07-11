using JollyWizard.Quantower.CLI.IndicatorDeploy.Helpers;
using Helpers = JollyWizard.Quantower.CLI.IndicatorDeploy.Helpers;
using Setup = JollyWizard.Quantower.Setup;

static void sectionBreak(String? message = null) 
{
    Console.WriteLine("--------------------------------------------\n");
    if (message is not null) Console.WriteLine(message);

    Console.WriteLine("Press [Enter] to continue, or [Close Window] or [Ctrl+C] to exit.");
    Console.ReadKey();
    Console.WriteLine("\n--------------------------------------------\n");
};

/*
 * If we don't have the value stored we are going to try to add it via auto-detect.
 */
if (Setup.EnvironmentConfig.Values.QuantowerRoot is null)
{
    Console.WriteLine("$(QuantowerRoot) value is not detected. ");
    Console.WriteLine("Ensure Quantower is running to auto-detect.");
    Console.WriteLine("Value will be saved for future uses.");
    sectionBreak();

    bool success = Setup.EnvironmentConfig.Setup.RootPath();

    if (!success)
    {
        sectionBreak("QuantowerRoot Detection failed. Deployment Aborted.");
        Environment.Exit(-1);
    }
    else
    {
        Console.WriteLine($"\tSuccess\n\tDetected Path := {Setup.EnvironmentConfig.Values.QuantowerRoot}");
    }
    sectionBreak();
}

try
{
    String? CustomIndicatorsPath = Setup.Utils.DetectCustomIndicatorsPath() ?? Setup.QuantowerPaths.ConvertRootToCustomIndicators(Setup.EnvironmentConfig.Values.QuantowerRoot);

    if (CustomIndicatorsPath == null) throw new Exception("User Indicator Path detection failed.");
    if (!Directory.Exists(CustomIndicatorsPath)) throw new Exception(" User Indicator path does not exist: \n\t" +  CustomIndicatorsPath);

    Console.WriteLine($"Custom Indicator Install Directory: {CustomIndicatorsPath}\n");

    sectionBreak("The indicator directory will now open.");

    if (!JollyWizard.SystemHelpers.FolderBrowser.ExploreIfExists(CustomIndicatorsPath))
    {
        throw new Exception("Failed to browse target folder.");
    }
}
catch (Exception e)
{
    Console.Error.WriteLine("{0} | {1}", "EXCEPTION ERROR: ", e.Message);
    sectionBreak("Deployment Aborted");
    Environment.Exit(-2);

}


/*
 *  Copy the files with diagnostics.
 */
try
{
    List<String> configFiles = Helpers.Utils.DetectQidConfigFiles();

    Console.WriteLine($"The following Deployment Files have been loaded:\n");
    foreach (String configFilePath in configFiles)
    {
        Console.WriteLine($"\t{configFilePath}");
    }
    Console.WriteLine();

    Console.WriteLine("Loading Plans.");
    List<CopyPlan> plans = configFiles.SelectMany(configFilePath => Helpers.Utils.ProcessConfigFile(configFilePath)).ToList();

    Console.WriteLine($"Plan Count: {plans.Count} \n");

    sectionBreak("Plan Diagnostics To Follow:");
    foreach (CopyPlan plan in plans)
    {
        Console.WriteLine($"Plan Definition:\n\t{plan}\n");

        Console.WriteLine("Sources Filter Matches:\n");
        foreach (String path in plan.SourcesAbsolutePaths ?? new())
        {
            Console.WriteLine($"\t\t{path}");
        }

        Console.WriteLine("\n");
        Console.WriteLine("\nRelative Paths:\n");
        foreach (String path in plan.SourcesRelativePaths ?? new())
        {
            Console.WriteLine($"\tSource\t{path}");
                
            Console.WriteLine($"\tTarget\t{plan.OutputRelativePath(path)}");
            Console.WriteLine();
        }

        Console.WriteLine("\nCopy Orders:\n");
        foreach (CopyOrder co in plan.CreateCopyOrders())
        {
            Console.WriteLine($"\tSource\t{co.SourcePath}");
            if (!co.IsSourceAvailable())
                Console.WriteLine("\t*Does Not Exist*");

            Console.WriteLine($"\tTarget\t{co.TargetPath}");
            if (co.IsTargetConflict())
                Console.WriteLine("\t\t*Conflict*");

            Console.WriteLine();
        }

        sectionBreak("Proceed with copy operations?");

        foreach (CopyOrder co in plan.CreateCopyOrders())
        {
            Console.WriteLine($"\tSource\t{co.SourcePath}");
            if (!co.IsSourceAvailable())
                Console.WriteLine("\t*Does Not Exist*");

            Console.WriteLine($"\tTarget\t{co.TargetPath}");
            if (co.IsTargetConflict())
                Console.WriteLine("\t\t*Conflict*");

            bool result = co.Execute(Force:true);
            String resultMessage = result ? "Success" : "Fail" ;
            Console.WriteLine($"\tCOPY RESULT: {resultMessage}");

            Console.WriteLine();
        }

    }
}
catch (Exception e) { Console.Error.WriteLine("{0}", e.Message); }

Console.WriteLine("PROGRAM COMPLETE");
Console.ReadLine();