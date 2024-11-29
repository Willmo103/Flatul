# 
# AssemblyInfo.cs
```csharp
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Flattener.Tests")]
```

# CodeFlattener.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
	  <Version>2.2.0.0</Version>
  </PropertyGroup>
	<ItemGroup>
		<PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
		<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
		<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
	</ItemGroup>

	<ItemGroup>
		<None Update="appsettings.json">
			<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
		</None>
	</ItemGroup>

</Project>

```

# FileHelper.cs
```csharp
using System.Collections.Generic;
using System.IO;

namespace CodeFlattener
{
    public static class FileHelper
    {
        private static Dictionary<string, string> _fileExtensionToLanguageMap;

        public static void Initialize(Dictionary<string, string> fileExtensionToLanguageMap)
        {
            _fileExtensionToLanguageMap = fileExtensionToLanguageMap;
        }

        public static string GetLanguageIdentifier(string filePath)
        {
            if (_fileExtensionToLanguageMap == null)
            {
                throw new InvalidOperationException("FileHelper is not initialized. Call Initialize() before using this method.");
            }

            string extension = Path.GetExtension(filePath).ToLower();
            return _fileExtensionToLanguageMap.TryGetValue(extension, out string languageIdentifier) ? languageIdentifier : "";
        }
    }
}

```

# Flattener.cs
```csharp
using System.Text;
using System.Text.RegularExpressions;

namespace CodeFlattener
{
    public partial class Flattener
    {
        public static void FlattenCodebase(
            string rootFolder,
            string outputFile,
            string[] acceptedFileTypes,
            string[] ignoredPaths,
            bool compress)
        {
            if (!Directory.Exists(rootFolder))
            {
                throw new DirectoryNotFoundException($"Directory not found: {rootFolder}");
            }

            StringBuilder markdownContent = new();

            var files = GetFilteredFiles(rootFolder, acceptedFileTypes, ignoredPaths);

            foreach (string filePath in files)
            {
                AppendFileContent(markdownContent, rootFolder, filePath, compress);
            }

            File.WriteAllText(outputFile, markdownContent.ToString());
        }

        private static IEnumerable<string> GetFilteredFiles(string rootFolder, string[] acceptedFileTypes, string[] ignoredPaths)
        {
            return Directory.EnumerateFiles(rootFolder, "*.*", SearchOption.AllDirectories)
                .Where(file =>
                {
                    string relativePath = Path.GetRelativePath(rootFolder, file);
                    Console.WriteLine($"Checking {relativePath}");
                    return !IsPathIgnored(relativePath, ignoredPaths) && acceptedFileTypes.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);
                });
        }

        private static bool IsPathIgnored(string path, string[] ignoredPaths)
        {
            return ignoredPaths.Any(ignoredPath =>
                path.Split(Path.DirectorySeparatorChar).Any(segment =>
                    segment.Equals(ignoredPath, StringComparison.OrdinalIgnoreCase)));
        }

        private static void AppendFileContent(
            StringBuilder markdownContent,
            string rootFolder,
            string filePath,
            bool compress)
        {
            string relativePath = Path.GetRelativePath(rootFolder, filePath);
            markdownContent.AppendLine($"# {relativePath.Replace('\\', '/')}");

            string languageIdentifier = FileHelper.GetLanguageIdentifier(filePath);
            markdownContent.AppendLine($"```{languageIdentifier}");

            string content = File.ReadAllText(filePath);
            if (compress)
            {
                content = CompressContent(content);
            }

            markdownContent.AppendLine(content);
            markdownContent.AppendLine("```");
            markdownContent.AppendLine();
        }

        private static string CompressContent(string content)
        {
            // Remove all whitespace except for single spaces between words
            content = ExtraSpaces().Replace(content, " ");
            // remove all whitespaces except for single spaces between words, again
            content = ExtraSpaces().Replace(content, " ");
            // Remove spaces after certain characters
            content = SpacesAfterSyntax().Replace(content, "$1");
            // Remove spaces before certain characters
            content = ClosingCodeSpaces().Replace(content, "$1");
            return content.Trim();
        }

        [GeneratedRegex(@"\s+")] // This matches one or more whitespace characters (spaces, tabs, newlines). It will be replaced with a single space.
        private static partial Regex ExtraSpaces();
        [GeneratedRegex(@"(\(|\[|{) ")]
        private static partial Regex SpacesAfterSyntax();
        [GeneratedRegex(@" (\)|\]|}|,|;)")]
        private static partial Regex ClosingCodeSpaces();
    }
}

```

# Program.cs
```csharp
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace CodeFlattener
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var config = BuildConfiguration();
                RunCodeFlattener(args, config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unhandled error occurred: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        public static void RunCodeFlattener(string[] args, IConfiguration config)
        {
            try
            {
                if (args.Length < 2 || args.Length > 3)
                {
                    Console.WriteLine("Usage: CodeFlattener <rootFolder> <outputFile> [-c|-Compress]");
                    return;
                }

                string rootFolder = args[0];
                string outputFile = args[1];
                bool compress = args.Length == 3 && (args[2] == "-c" || args[2] == "-Compress");

                Console.WriteLine($"Root folder: {rootFolder}, Output file: {outputFile}, Compress: {compress}");

                var allowedFiles = config.GetSection("AllowedFiles").GetChildren().ToDictionary(x => x.Key, x => x.Value);
                var ignoredPaths = config.GetSection("Ignored").GetChildren().ToDictionary(x => x.Key, x => x.Value);

                Console.WriteLine($"Accepted file types: {string.Join(", ", allowedFiles.Keys)}");
                Console.WriteLine($"Ignored paths: {string.Join(", ", ignoredPaths.Keys)}");

                if (allowedFiles.Count == 0 || ignoredPaths.Count == 0)
                {
                    Console.WriteLine("Error: Configuration sections are missing or empty.");
                    return;
                }

                // Initialize the FileHelper with the allowed file types dictionary
                FileHelper.Initialize(allowedFiles);

                ValidateAndFlattenCodebase(rootFolder, outputFile, allowedFiles, ignoredPaths, compress);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unhandled error occurred: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
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

        private static void ValidateAndFlattenCodebase(string rootFolder, string outputFile, Dictionary<string, string> acceptedFileTypes, Dictionary<string, string> ignoredPaths, bool compress)
        {
            Console.WriteLine("Validating and flattening codebase...");
            try
            {
                string absoluteRootFolder = Path.GetFullPath(rootFolder);
                Console.WriteLine($"Root folder: {absoluteRootFolder}\nValidating Location...");

                ValidateDirectoryExists(absoluteRootFolder);

                string absoluteOutputFile = Path.IsPathRooted(outputFile) ? outputFile : Path.Combine(Directory.GetCurrentDirectory(), outputFile);
                Console.WriteLine($"Output file: {absoluteOutputFile}");

                Flattener flattener = new();
                Flattener.FlattenCodebase(absoluteRootFolder, absoluteOutputFile, acceptedFileTypes.Keys.ToArray(), ignoredPaths.Keys.ToArray(), compress);

                Console.WriteLine($"Output written to: {absoluteOutputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message} -- {ex.StackTrace}");
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
}

```

