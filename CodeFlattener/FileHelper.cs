using System.Text.RegularExpressions;

namespace CodeFlattener
{
    /// <summary>
    /// Provides helper methods for working with files.
    /// </summary>

    /// <summary>
    /// Provides helper methods for working with files.
    /// </summary>
    public static class FileHelper
    {
        private static Dictionary<string, string> _fileExtensionToLanguageMap;
        private static Dictionary<string, string> _defaultLanguageMap = new()
        {
            { ".cs", "csharp" },
            { ".py", "python" },
            { ".js", "javascript" },
            { ".ts", "typescript" },
            { ".java", "java" },
            { ".cpp", "cpp" },
            { ".c", "c" },
            { ".go", "go" },
            { ".rb", "ruby" },
            { ".php", "php" },
            { ".rs", "rust" },
            { ".swift", "swift" },
            { ".kt", "kotlin" },
            { ".scala", "scala" },
            { ".r", "r" },
            { ".md", "markdown" },
            { ".html", "html" },
            { ".xml", "xml" },
            { ".json", "json" },
            { ".yaml", "yaml" },
            { ".yml", "yaml" },
            { ".sh", "bash" },
            { ".bash", "bash" },
            { ".sql", "sql" },
            { ".css", "css" },
            { ".scss", "scss" },
            { ".less", "less" },
            { ".vue", "vue" },
            { ".jsx", "jsx" },
            { ".tsx", "tsx" },
            { ".dart", "dart" },
            { ".lua", "lua" },
            { ".pl", "perl" },
            { ".m", "matlab" },
            { ".f90", "fortran" },
            { ".f95", "fortran" },
            { ".f", "fortran" },
            { ".jl", "julia" },
            { ".ex", "elixir" },
            { ".exs", "elixir" },
            { ".erl", "erlang" },
            { ".hrl", "erlang" },
            { ".hs", "haskell" },
            { ".lhs", "haskell" },
            { ".ps1", "powershell" },
            { ".psm1", "powershell" },
            { ".psd1", "powershell" },
            { ".proto", "protobuf" },
            { ".gradle", "groovy" },
            { ".tf", "terraform" },
            { ".hcl", "hcl" },
            { ".dockerfile", "dockerfile" },
            { ".toml", "toml" },
            { ".ini", "ini" },
            { ".conf", "configuration" },
            { ".bat", "batch" },
            { ".cmd", "batch" },
            { ".tex", "latex" },
            { ".rst", "restructuredtext" },
            { ".org", "org" },
            { ".mk", "makefile" },
            { ".ada", "ada" },
            { ".adb", "ada" },
            { ".ads", "ada" }
        };
        private static bool _isInitialized;

        /// <summary>
        /// Initializes the FileHelper with default language mappings.
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;
            _fileExtensionToLanguageMap = new Dictionary<string, string>(_defaultLanguageMap, StringComparer.OrdinalIgnoreCase);
            _isInitialized = true;
        }


        /// <summary>
        /// Initializes the file extension to language mapping.
        /// </summary>
        /// <param name="fileExtensionToLanguageMap">The dictionary containing the file extension to language mapping.</param>
        public static void Initialize(Dictionary<string, string> fileExtensionToLanguageMap)
        {
            _fileExtensionToLanguageMap = new Dictionary<string, string>(
                fileExtensionToLanguageMap,
                StringComparer.OrdinalIgnoreCase
            );
        }

        /// <summary>
        /// Gets the language identifier for a given file path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns>The language identifier.</returns>
        /// <exception cref="InvalidOperationException">Thrown when FileHelper is not initialized. Call Initialize() before using this method.</exception>
        public static string GetLanguageIdentifier(string filePath)
        {
            if (_fileExtensionToLanguageMap == null)
            {
                throw new InvalidOperationException("FileHelper is not initialized. Call Initialize() before using this method.");
            }

            string extension = Path.GetExtension(filePath).ToLower();

            // First try user-provided mapping
            if (_fileExtensionToLanguageMap.TryGetValue(extension, out string languageIdentifier))
            {
                return languageIdentifier;
            }

            // Then try default mapping
            if (_defaultLanguageMap.TryGetValue(extension, out string defaultLanguage))
            {
                return defaultLanguage;
            }

            // Handle special case for Dockerfile (which might not have an extension)
            if (Path.GetFileName(filePath).Equals("Dockerfile", StringComparison.OrdinalIgnoreCase))
            {
                return "dockerfile";
            }

            // Handle special case for Makefile
            if (Path.GetFileName(filePath).Equals("Makefile", StringComparison.OrdinalIgnoreCase))
            {
                return "makefile";
            }

            // Default to plaintext for unknown extensions
            return "plaintext";
        }

        /// <summary>
        /// Checks if a file is a text file.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns>True if the file is a text file; otherwise, false.</returns>
        public static bool IsTextFile(string filePath)
        {
            try
            {
                // Read the first 8KB of the file
                const int maxBytesToRead = 8 * 1024;
                byte[] buffer;

                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Length == 0) return true; // Empty files are considered text files

                    buffer = new byte[Math.Min(fs.Length, maxBytesToRead)];
                    fs.Read(buffer, 0, buffer.Length);
                }

                // Check for common binary file signatures
                if (IsBinaryFileSignature(buffer)) return false;

                // Check for null bytes and high ratio of non-printable characters
                int nullCount = 0;
                int nonPrintableCount = 0;

                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i] == 0) nullCount++;
                    else if (buffer[i] < 7 || (buffer[i] > 14 && buffer[i] < 32)) nonPrintableCount++;

                    // If we find too many null bytes or non-printable characters, consider it binary
                    if (nullCount > buffer.Length * 0.01 || nonPrintableCount > buffer.Length * 0.3)
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Error checking if file is text: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a file has a binary file signature.
        /// </summary>
        /// <param name="buffer">The buffer containing the file content.</param>
        /// <returns>True if the file has a binary file signature; otherwise, false.</returns>
        private static bool IsBinaryFileSignature(byte[] buffer)
        {
            // Check for common binary file signatures
            byte[][] signatures = {
                    new byte[] { 0x7F, 0x45, 0x4C, 0x46 }, // ELF
                    new byte[] { 0x4D, 0x5A }, // PE/DOS
                    new byte[] { 0x50, 0x4B, 0x03, 0x04 }, // ZIP
                    new byte[] { 0x25, 0x50, 0x44, 0x46 }, // PDF
                    new byte[] { 0x89, 0x50, 0x4E, 0x47 }, // PNG
                    new byte[] { 0xFF, 0xD8, 0xFF }, // JPEG
                    new byte[] { 0x47, 0x49, 0x46, 0x38 }, // GIF
                };

            foreach (var signature in signatures)
            {
                if (buffer.Length >= signature.Length &&
                    buffer.Take(signature.Length).SequenceEqual(signature))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Normalizes a file path by replacing slashes with dots.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The normalized path.</returns>
        public static string NormalizePath(string path)
        {
            // Replace both forward and back slashes with dots
            return path.Replace('/', '.').Replace('\\', '.');
        }

        /// <summary>
        /// Gets the relative path from a base path to a full path.
        /// </summary>
        /// <param name="basePath">The base path.</param>
        /// <param name="fullPath">The full path.</param>
        /// <returns>The relative path.</returns>
        public static string GetRelativePath(string basePath, string fullPath)
        {
            return Path.GetRelativePath(basePath, fullPath).Replace('\\', '/');
        }

        /// <summary>
        /// Checks if a directory is a Git repository.
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <returns>True if the directory is a Git repository; otherwise, false.</returns>
        public static bool IsGitRepository(string path)
        {
            return Directory.Exists(Path.Combine(path, ".git"));
        }

        /// <summary>
        /// Checks if a file path matches a filter.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="filter">The filter to match against.</param>
        /// <returns>True if the file path matches the filter; otherwise, false.</returns>
        public static bool MatchesFilter(string path, string filter)
        {
            if (filter.StartsWith("*."))
            {
                // Handle file extension filter
                return path.EndsWith(filter.Substring(1), StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                // Handle directory/file name filter
                var pattern = filter
                    .Replace(".", "\\.")
                    .Replace("*", ".*")
                    .Replace("?", ".");
                return Regex.IsMatch(path, pattern, RegexOptions.IgnoreCase);
            }
        }
    }
}