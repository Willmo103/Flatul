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
