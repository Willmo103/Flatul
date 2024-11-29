using LibGit2Sharp;
using Microsoft.Extensions.Configuration;

namespace CodeFlattener
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                var config = BuildConfiguration();
                await RunCodeFlattenerAsync(args, config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unhandled error occurred: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        public static async Task RunCodeFlattenerAsync(string[] args, IConfiguration config)
        {
            try
            {
                var options = ParseCommandLineArguments(args);
                if (options == null)
                {
                    PrintUsage();
                    return;
                }

                string rootFolder = options.InputPath;
                string tempFolder = null;

                if (!string.IsNullOrEmpty(options.RepositoryUrl))
                {
                    Console.WriteLine($"Cloning repository: {options.RepositoryUrl}");
                    tempFolder = await CloneRepositoryAsync(options.RepositoryUrl);
                    rootFolder = tempFolder;
                    Console.WriteLine($"Repository cloned to: {tempFolder}");
                }

                try
                {
                    var allowedFiles = config.GetSection("AllowedFiles").GetChildren().ToDictionary(x => x.Key, x => x.Value);
                    var ignoredPaths = config.GetSection("Ignored").GetChildren().ToDictionary(x => x.Key, x => x.Value);

                    Console.WriteLine($"Original allowed files: {string.Join(", ", allowedFiles.Keys)}");
                    Console.WriteLine($"Original ignored paths: {string.Join(", ", ignoredPaths.Keys)}");

                    if (options.Filters?.Any() == true)
                    {
                        Console.WriteLine("Applying custom filters...");
                        // Override existing filters with custom filters
                        allowedFiles.Clear();
                        ignoredPaths.Clear();
                        foreach (var filter in options.Filters)
                        {
                            if (filter.StartsWith("*."))
                            {
                                allowedFiles.Add(filter.Substring(1), DetermineLanguageFromExtension(filter));
                                Console.WriteLine($"Added file extension filter: {filter}");
                            }
                            else
                            {
                                ignoredPaths.Add(filter, filter);
                                Console.WriteLine($"Added path filter: {filter}");
                            }
                        }
                    }

                    if (allowedFiles.Count == 0 && ignoredPaths.Count == 0)
                    {
                        Console.WriteLine("Warning: No filters specified. All files will be processed.");
                    }

                    FileHelper.Initialize(allowedFiles);
                    await ValidateAndFlattenCodebaseAsync(rootFolder, options.OutputPath, allowedFiles, ignoredPaths, options.Compress);
                }
                finally
                {
                    if (tempFolder != null)
                    {
                        Console.WriteLine("Cleaning up temporary repository...");
                        Directory.Delete(tempFolder, true);
                        Console.WriteLine("Temporary repository cleaned up.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine(@"Usage: CodeFlattener -i <inputPath> -o <outputPath> [options]
Options:
  -i, --input          Input root folder path (required)
  -o, --output         Output file path (required)
  -c, --compress       Compress the output
  -r, --repository     Git repository URL to clone
  -f, --filter         Comma-separated list of filters (e.g., 'examples,*.md,README')

Examples:
  CodeFlattener -i ./src -o output.md -c
  CodeFlattener -i ./src -o output.md -f '*.cs,*.md,examples'
  CodeFlattener -r https://github.com/user/repo.git -o output.md -f '*.py'");
        }

        private static async Task<string> CloneRepositoryAsync(string repositoryUrl)
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempPath);

            try
            {
                Repository.Clone(repositoryUrl, tempPath);
                return tempPath;
            }
            catch (Exception ex)
            {
                Directory.Delete(tempPath, true);
                throw new Exception($"Failed to clone repository: {ex.Message}", ex);
            }
        }

        private static string DetermineLanguageFromExtension(string filter)
        {
            // Remove the * from the filter to get just the extension
            string extension = filter.StartsWith("*") ? filter.Substring(1) : filter;
            return FileHelper.GetLanguageIdentifier(extension);
        }

        private static CommandLineOptions? ParseCommandLineArguments(string[] args)
        {
            var options = new CommandLineOptions();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-i":
                    case "--input":
                        if (++i < args.Length) options.InputPath = args[i];
                        break;
                    case "-o":
                    case "--output":
                        if (++i < args.Length) options.OutputPath = args[i];
                        break;
                    case "-c":
                    case "--compress":
                        options.Compress = true;
                        break;
                    case "-r":
                    case "--repository":
                        if (++i < args.Length) options.RepositoryUrl = args[i];
                        break;
                    case "-f":
                    case "--filter":
                        if (++i < args.Length)
                        {
                            options.Filters = args[i].Split(',')
                                .Select(f => f.Trim())
                                .Where(f => !string.IsNullOrWhiteSpace(f))
                                .ToList();
                        }
                        break;
                }
            }

            // Validate required options
            if (string.IsNullOrEmpty(options.InputPath) && string.IsNullOrEmpty(options.RepositoryUrl))
            {
                Console.WriteLine("Error: Either input path (-i) or repository URL (-r) must be specified.");
                return null;
            }

            if (string.IsNullOrEmpty(options.OutputPath))
            {
                Console.WriteLine("Error: Output path (-o) is required.");
                return null;
            }

            return options;
        }

        private static IConfiguration BuildConfiguration()
        {
            string configPath = GetConfigPath();
            if (string.IsNullOrEmpty(configPath))
            {
                throw new FileNotFoundException("Unable to locate appsettings.json");
            }

            return new ConfigurationBuilder()
                .AddJsonFile(configPath, optional: false, reloadOnChange: true)
                .Build();
        }

        private static string GetConfigPath()
        {
            string[] possiblePaths =
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json"),
                Path.Combine(Environment.CurrentDirectory, "appsettings.json"),
                "appsettings.json"
            };

            return possiblePaths.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("Unable to locate appsettings.json");
        }

        private static async Task ValidateAndFlattenCodebaseAsync(
            string rootFolder,
            string outputFile,
            Dictionary<string, string> acceptedFileTypes,
            Dictionary<string, string> ignoredPaths,
            bool compress)
        {
            Console.WriteLine("Validating and flattening codebase...");
            try
            {
                string absoluteRootFolder = Path.GetFullPath(rootFolder);
                Console.WriteLine($"Root folder: {absoluteRootFolder}");
                Console.WriteLine("Validating Location...");

                ValidateDirectoryExists(absoluteRootFolder);

                string absoluteOutputFile = Path.IsPathRooted(outputFile)
                    ? outputFile
                    : Path.Combine(Directory.GetCurrentDirectory(), outputFile);
                Console.WriteLine($"Output file: {absoluteOutputFile}");

                await Flattener.FlattenCodebaseAsync(
                    absoluteRootFolder,
                    absoluteOutputFile,
                    acceptedFileTypes.Keys.ToArray(),
                    ignoredPaths.Keys.ToArray(),
                    compress);

                Console.WriteLine($"Output written to: {absoluteOutputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                throw;
            }
        }

        private static void ValidateDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            }
            Console.WriteLine($"Directory exists: {path}");
        }
    }

    public class CommandLineOptions
    {
        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public bool Compress { get; set; }
        public string RepositoryUrl { get; set; }
        public List<string> Filters { get; set; }
    }
}