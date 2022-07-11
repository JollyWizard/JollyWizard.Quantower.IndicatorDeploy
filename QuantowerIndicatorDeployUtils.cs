using System;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using System.Diagnostics;

namespace JollyWizard.Quantower.CLI.IndicatorDeploy.Helpers;

public class MAGIC_VALUES
{
    public const string CONFIG_FILE_NAME = "deploy.ini";

    public static string[] CONFIG_FILE_PATTERNS => new string[] {

        // Pattern for in executing directory.
        $"*{CONFIG_FILE_NAME}"

        /*@TODO Does this make first entry redundant?*/ 
    ,   $"**/*{CONFIG_FILE_NAME}"

    };
}

public class Utils
{

    /// <summary>
    /// Reads a config INI file and converts it into a plan.
    /// @TODO Support multiple plans.
    /// </summary>
    /// <param name="filepath"></param>
    /// <returns></returns>
    public static List<CopyPlan> ProcessConfigFile(String? filepath)
    {
        List<CopyPlan> copyPlans = new ();

        CopyPlan currentPlan = new();
        
        if (filepath == null || !File.Exists(filepath)) return copyPlans;

        foreach (String line in File.ReadLines(filepath))
        {
            // Ignore comments and blank lines.
            if (line.StartsWith("#") || line.Trim().Length == 0) continue;

            // "Key=Value" => ["Key", "Value"]
            String[] keyvalue = line.Split('=');

            //@TODO verify no edge cases missed.
            if (keyvalue.Length != 2) continue;
            string key = keyvalue[0];
            string value = keyvalue[1];

            /*
             * Process Each Key as a command, directing how to use value.
             */
            switch (key)
            {
                case "~":
                case "INPUT_ROOT":
                case "ROOT":
                    // Collect previous plan if has configuration.
                    if (currentPlan.IncludePatterns.Count > 0)
                        copyPlans.Add(currentPlan);
                    
                    currentPlan = new (); 
                    currentPlan.SourcesRoot = value;
                    break;

                case "+":
                case "INCLUDE":
                    currentPlan.IncludePatterns.Add(value);
                    break;

                case "-":
                case "EXCLUDE":
                    currentPlan.ExcludePatterns.Add(value);
                    break;

                case "?":
                case "SLUG":
                    currentPlan.OutputSlug = value; 
                    break;

                case "@":
                case "TARGET":
                case "OUTPUT":
                case "OUTPUT_ROOT":
                    currentPlan.OutputRoot = value;
                    break;
            }
        }

        // Don't forget the open target.
        copyPlans.Add(currentPlan);
        
        return copyPlans;
    }

    public static List<String> DetectQidConfigFiles()
    {
        // Where to search for the config.
        // @TODO make sure that relative path here leads to consistent absolute results in the CopyPlan
        String dirPath = ".\\";

        CopyPlan plan = CopyPlans.ConfigFilePlan();
        plan.SourcesRoot = dirPath;
        List<String>? ConfigPaths = plan?.SourcesAbsolutePaths;
        return ConfigPaths ?? new();
    }

    public static void Test()
    {
        List<CopyPlan> plans = new();

        foreach (String configPath in DetectQidConfigFiles())
        { 
            plans.AddRange(ProcessConfigFile(configPath));
        }

        Console.WriteLine(plans);
    }
}

public class CopyPlans
{
    public static CopyPlan ConfigFilePlan()
    {
        CopyPlan plan = new();
        plan.IncludePatterns.AddRange(MAGIC_VALUES.CONFIG_FILE_PATTERNS);
        return plan;
    }
}

public class CopyPlan
{
    #region Instance Properties
    public String? SourcesRoot { get; set; } = Directory.GetCurrentDirectory();

    public List<String> IncludePatterns = new();
    public List<String> ExcludePatterns = new();

    public String? OutputRoot { get; set; }

    public String? OutputSlug { get; set; }
    #endregion

    public String OutputRelativeBase => (this.OutputSlug is null ? "" : this.OutputSlug );

    public String? OutputRelativePath(String? relativePath)
    {    
        return Path.Combine(this.OutputRelativeBase, relativePath ?? "");
    }

    public static String? ResolveAbsoluePath(String? root, String? relative, bool ResolveEnvironmentVariables = true)
    {
        if (root is null || relative is null) return null;

        if (ResolveEnvironmentVariables)
        {
            root = Environment.ExpandEnvironmentVariables(root);
            relative = Environment.ExpandEnvironmentVariables(relative);
        }

        String CombinedPath = Path.Combine(root, relative ?? "");

        return Path.GetFullPath(CombinedPath);
    }

    public String? SourcesAbsolutePath(String? relativePath) => ResolveAbsoluePath(this.SourcesRoot, relativePath);

    public String? OutputAbsolutePath(String? relativePath) => ResolveAbsoluePath(this.OutputRoot, relativePath);

    public List<CopyOrder> CreateCopyOrders()
    {
        List<CopyOrder> copyOrders = new();

        foreach (String sourceRelativePath in this.SourcesRelativePaths ?? new())
        {
            CopyOrder co = new()
            {
                SourcePath = this.SourcesAbsolutePath(sourceRelativePath)
            ,   TargetPath = this.OutputAbsolutePath(this.OutputRelativePath(sourceRelativePath))
            };
            copyOrders.Add(co);
        }

        return copyOrders;
    }

    /// <summary>
    /// Gets the canonical path to `SourcesRoot`.
    /// </summary>
    public String? SourcesRootAbsolutePath => this.SourcesRoot == null ? null : Path.GetFullPath(this.SourcesRoot);

    /// <summary>
    /// Checks if `SourcesRootAbsoluePath` is a valid directory.
    /// </summary>
    public Boolean SourcesRootExists => this.SourcesRootAbsolutePath is not null && Directory.Exists(this.SourcesRoot);

    /// <summary>
    /// Version of the SourceRoot that can be fed into `Matcher.Execute(DirectoryInfoWrapper)`.
    /// </summary>
    public DirectoryInfoWrapper? SourcesRootMatchable => this.SourcesRoot is null ? null : new(new(this.SourcesRoot));

    /// <summary>
    /// Gets a `Matcher` that is configured with the inclusion and exclusion patterns.
    /// 
    /// Used to match the source files when applied to the root.
    /// </summary>
    public Matcher? SourcesMatcher 
    { 
        get 
        {
            if (this.SourcesRootMatchable is null) return null;

            Matcher m = new(); 
            m.AddIncludePatterns(this.IncludePatterns);
            m.AddExcludePatterns(this.ExcludePatterns);
            return m; 
        } 
    }

    public PatternMatchingResult? SourcesMatcherResult => this.SourcesMatcher?.Execute(this.SourcesRootMatchable);

    /// <summary>
    /// Takes a path and resolves it relative to `SourcesRoot`.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public String? GetPathRelativeToSourcesRoot(String? path)
    {
        if (
            !this.SourcesRootExists  // Ensures dir.exists, but doesn't reassure compiler.
        ||  this.SourcesRootAbsolutePath is null // Assure compiler for below.
        ||  path is null
        ) return null;
        return GetRelativePath(this.SourcesRootAbsolutePath, path);
    }

    private static String GetRelativePath(String? a, String? b)
    {
        Uri path1 = new Uri(a);
        Uri path2 = new Uri(b);
        Uri diff = path1.MakeRelativeUri(path2);
        return diff.OriginalString;
    }

    /// <summary>
    /// Get the absolute paths of all source matcher results.
    /// </summary>
    public List<String>? SourcesAbsolutePaths => this.SourcesMatcher?.GetResultsInFullPath(this.SourcesRoot).ToList();


    /// <summary>
    /// @TODO Convert the matcher results into relative paths.
    /// </summary>
    public List<String>? SourcesRelativePaths =>
            this.SourcesAbsolutePaths?.Select(absPath => this.GetPathRelativeToSourcesRoot(absPath)).OfType<String>().ToList();


    public override string ToString()
    {
        String[] r = new string[]
        {
            "~=" + this.SourcesRoot ?? ""
        ,   "+=" + String.Join(";", this.IncludePatterns)
        ,   "-=" + String.Join(";", this.ExcludePatterns)
        ,   this.OutputRoot is null ? "" : "@=" + this.OutputRoot
        ,   this.OutputSlug is null ? "" : "?=" + this.OutputSlug
        };
        return "[" + String.Join("|", r) + "] (" + this.SourcesRootAbsolutePath + ") {" + this.SourcesAbsolutePaths?.Count + "}";
    }
}

public class CopyOrder
{
    //@TODO. Figure out how to handle empty directories.

    public String? SourcePath;
    public String? TargetPath;

    public bool Execute(bool Force = true)
    {
        // Can trim this more for helpful exceptions.
        if (!this.IsSourceAvailable())
            return false;
        else if (this.TargetPath is null || this.SourcePath is null) 
            return false;
        else if (this.IsTargetConflict() && !Force) 
            return false;

        try
        {
            // Already filtered for Force, but just to be clear.
            if (this.IsTargetConflict() && Force)
                File.Delete(this.TargetPath);

            // Short circuit of input null; should generate exception anyway if the Path function fails..
            Directory.CreateDirectory(Path.GetDirectoryName(this.TargetPath) ?? "");

            File.Copy(this.SourcePath, this.TargetPath);
            return true;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"<Copy-Error Message=\"{e.Message}\" />");
            return false;
        }
    }

    public bool IsSourceAvailable()
    {
        if (this.SourcePath is null) return false;
        
        return File.Exists(this.SourcePath);
    }

    public bool IsTargetConflict()
    {
        if (this.TargetPath is null) return true;

        return File.Exists(this.TargetPath);
    }

    public override string ToString()
    {
        return $"<CopyOrder \"{this.SourcePath}\" > \"{this.TargetPath}\" />";
    }
}

