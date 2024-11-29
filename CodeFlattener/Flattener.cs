using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using Markdig;
using YamlDotNet.Serialization;

namespace CodeFlattener
{
    /// <summary>
    /// Provides functionality to flatten a codebase into a single file while preserving metadata
    /// and relationships between files. The flattener processes source files, collects metadata,
    /// and generates a markdown-formatted output with YAML frontmatter.
    /// </summary>
    public partial class Flattener
    {
        /// <summary>
        /// Flattens the codebase by collecting metadata, processing files, and building content.
        /// </summary>
        /// <param name="rootFolder">The root folder of the codebase to process.</param>
        /// <param name="outputFile">The path where the flattened output file will be created.</param>
        /// <param name="acceptedFileTypes">Array of file extensions or patterns to include. Empty array means accept all.</param>
        /// <param name="ignoredPaths">Array of path patterns to exclude from processing.</param>
        /// <param name="compress">When true, compresses the content by removing unnecessary whitespace.</param>
        /// <returns>A task representing the asynchronous flattening operation.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown when the root folder doesn't exist.</exception>
        public static async Task FlattenCodebaseAsync(
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

            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .UseEmphasisExtras()
                .UseTaskLists()
                .UseAutoLinks()
                .Build();

            var files = GetFilteredFiles(rootFolder, acceptedFileTypes, ignoredPaths).ToList();
            var allMetadata = new Dictionary<string, FileMetadata>();
            var contentBuilder = new StringBuilder();

            // First pass: collect metadata
            foreach (string filePath in files)
            {
                var metadata = await CollectFileMetadataAsync(rootFolder, filePath);
                allMetadata[filePath] = metadata;
            }

            // Second pass: process files and build content
            foreach (string filePath in files)
            {
                var metadata = allMetadata[filePath];
                FindRelatedFiles(metadata, allMetadata);

                // Add YAML metadata section
                contentBuilder.AppendLine("---");
                var serializer = new SerializerBuilder()
                    .WithQuotingNecessaryStrings()
                    .Build();
                contentBuilder.AppendLine(serializer.Serialize(metadata));
                contentBuilder.AppendLine("---");

                // Add file content with proper heading
                string relativePath = metadata.RelativePath.Replace('/', '.');
                contentBuilder.AppendLine($"# {relativePath}");

                string languageIdentifier = FileHelper.GetLanguageIdentifier(filePath);
                contentBuilder.AppendLine($"```{languageIdentifier}");

                string content = await File.ReadAllTextAsync(filePath);
                if (compress)
                {
                    content = CompressContent(content);
                }
                contentBuilder.AppendLine(content);
                contentBuilder.AppendLine("```");

                // Add backlinks section
                if (metadata.RelatedFiles.Any())
                {
                    contentBuilder.AppendLine("\n## Related Files");
                    foreach (var related in metadata.RelatedFiles)
                    {
                        contentBuilder.AppendLine($"- [[{related.Replace('/', '.')}]]");
                    }
                }

                contentBuilder.AppendLine("\n---\n");
            }

            // Process the entire content through Markdig
            string finalContent = Markdown.ToPlainText(contentBuilder.ToString());
            await File.WriteAllTextAsync(outputFile, finalContent);
        }

        /// <summary>
        /// Filters and returns a collection of file paths based on accepted file types and ignored paths.
        /// </summary>
        /// <param name="rootFolder">The root folder to start the file enumeration.</param>
        /// <param name="acceptedFileTypes">Array of file extensions or patterns to include.</param>
        /// <param name="ignoredPaths">Array of path patterns to exclude.</param>
        /// <returns>An enumerable collection of filtered file paths.</returns>
        private static IEnumerable<string> GetFilteredFiles(
            string rootFolder,
            string[] acceptedFileTypes,
            string[] ignoredPaths)
        {
            return Directory.EnumerateFiles(rootFolder, "*.*", SearchOption.AllDirectories)
                .Where(file =>
                {
                    string relativePath = Path.GetRelativePath(rootFolder, file);
                    Console.WriteLine($"Checking {relativePath}");

                    // Check if path matches any filter patterns
                    bool isIncluded = acceptedFileTypes.Length == 0 ||
                        acceptedFileTypes.Any(pattern => MatchesPattern(file, pattern));

                    bool isExcluded = ignoredPaths.Any(pattern =>
                        relativePath.Split(Path.DirectorySeparatorChar)
                            .Any(segment => MatchesPattern(segment, pattern)));

                    return isIncluded && !isExcluded;
                });
        }

        /// <summary>
        /// Matches an input string against a glob pattern.
        /// </summary>
        /// <param name="input">The input string to check.</param>
        /// <param name="pattern">The glob pattern to match against.</param>
        /// <returns>True if the input matches the pattern, false otherwise.</returns>
        private static bool MatchesPattern(string input, string pattern)
        {
            // Convert glob pattern to regex
            string regex = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".")
                + "$";

            return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Collects metadata for a specific file, including Git information if available.
        /// </summary>
        /// <param name="rootFolder">The root folder of the codebase.</param>
        /// <param name="filePath">The path to the file to collect metadata for.</param>
        /// <returns>A FileMetadata object containing the collected information.</returns>
        private static async Task<FileMetadata> CollectFileMetadataAsync(string rootFolder, string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var metadata = new FileMetadata
            {
                RelativePath = Path.GetRelativePath(rootFolder, filePath).Replace('\\', '/'),
                AbsolutePath = Path.GetFullPath(filePath),
                LastModified = fileInfo.LastWriteTime,
                SizeInBytes = fileInfo.Length,
                FileExtension = Path.GetExtension(filePath)
            };

            try
            {
                // Try to collect Git metadata if available
                var gitDir = Path.Combine(rootFolder, ".git");
                if (Directory.Exists(gitDir))
                {
                    using var repo = new Repository(rootFolder);
                    var commits = repo.Commits.QueryBy(metadata.RelativePath).ToList();
                    if (commits.Any())
                    {
                        var lastCommit = commits.First();
                        metadata.GitCommitId = lastCommit.Commit.Id.Sha;
                        metadata.GitLastAuthor = lastCommit.Commit.Author.Name;
                        metadata.GitLastModified = lastCommit.Commit.Author.When.DateTime;

                        // Get additional Git metadata
                        metadata.GitBranch = repo.Head.FriendlyName;
                        metadata.GitRemoteUrl = repo.Network.Remotes.FirstOrDefault()?.Url ?? string.Empty;

                        var blame = repo.Blame(metadata.RelativePath);
                        if (blame != null)
                        {
                            metadata.GitContributors = blame
                                .Select(hunk => hunk.InitialCommit.Author.Name)
                                .Distinct()
                                .ToList();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to collect Git metadata for {filePath}: {ex.Message}");
                // Continue without Git metadata
            }

            return metadata;
        }

        /// <summary>
        /// Identifies and populates related files for a given file based on directory location,
        /// file extension, and content references.
        /// </summary>
        /// <param name="file">The FileMetadata object to find related files for.</param>
        /// <param name="allFiles">Dictionary of all files in the codebase.</param>
        private static void FindRelatedFiles(FileMetadata file, Dictionary<string, FileMetadata> allFiles)
        {
            var directory = Path.GetDirectoryName(file.RelativePath);

            // Find files in the same directory
            var sameDirectoryFiles = allFiles.Values
                .Where(f => Path.GetDirectoryName(f.RelativePath) == directory &&
                           f.RelativePath != file.RelativePath)
                .Select(f => f.RelativePath);

            // Find files with the same extension
            var sameExtensionFiles = allFiles.Values
                .Where(f => f.FileExtension == file.FileExtension &&
                           f.RelativePath != file.RelativePath)
                .Select(f => f.RelativePath);

            // Find files referenced in the content
            var referencedFiles = allFiles.Values
                .Where(f => File.ReadAllText(file.AbsolutePath)
                    .Contains(Path.GetFileNameWithoutExtension(f.RelativePath)) &&
                    f.RelativePath != file.RelativePath)
                .Select(f => f.RelativePath);

            file.RelatedFiles = sameDirectoryFiles
                .Concat(sameExtensionFiles)
                .Concat(referencedFiles)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Compresses the content by removing unnecessary whitespace while preserving code functionality.
        /// </summary>
        /// <param name="content">The content to compress.</param>
        /// <returns>The compressed content string.</returns>
        private static string CompressContent(string content)
        {
            // Remove all whitespace except for single spaces between words
            content = ExtraSpaces().Replace(content, " ");
            // Remove all whitespaces except for single spaces between words, again
            content = ExtraSpaces().Replace(content, " ");
            // Remove spaces after certain characters
            content = SpacesAfterSyntax().Replace(content, "$1");
            // Remove spaces before certain characters
            content = ClosingCodeSpaces().Replace(content, "$1");
            return content.Trim();
        }

        /// <summary>
        /// Regular expression to match multiple whitespace characters.
        /// </summary>
        [GeneratedRegex(@"\s+")]
        private static partial Regex ExtraSpaces();

        /// <summary>
        /// Regular expression to match spaces after opening brackets and parentheses.
        /// </summary>
        [GeneratedRegex(@"(\(|\[|{) ")]
        private static partial Regex SpacesAfterSyntax();

        /// <summary>
        /// Regular expression to match spaces before closing brackets, parentheses, and punctuation.
        /// </summary>
        [GeneratedRegex(@" (\)|\]|}|,|;)")]
        private static partial Regex ClosingCodeSpaces();
    }

    /// <summary>
    /// Represents metadata for a file in the codebase, including both file system and Git information.
    /// </summary>
    public class FileMetadata
    {
        /// <summary>
        /// Gets or sets the file path relative to the root folder.
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// Gets or sets the absolute file path on the system.
        /// </summary>
        public string AbsolutePath { get; set; }

        /// <summary>
        /// Gets or sets the last modification timestamp of the file.
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long SizeInBytes { get; set; }

        /// <summary>
        /// Gets or sets the file extension including the dot.
        /// </summary>
        public string FileExtension { get; set; }

        /// <summary>
        /// Gets or sets the Git commit ID (SHA) of the last commit that modified the file.
        /// </summary>
        public string GitCommitId { get; set; }

        /// <summary>
        /// Gets or sets the name of the last Git author who modified the file.
        /// </summary>
        public string GitLastAuthor { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the last Git commit that modified the file.
        /// </summary>
        public DateTime? GitLastModified { get; set; }

        /// <summary>
        /// Gets or sets the current Git branch name.
        /// </summary>
        public string GitBranch { get; set; }

        /// <summary>
        /// Gets or sets the Git remote repository URL.
        /// </summary>
        public string GitRemoteUrl { get; set; }

        /// <summary>
        /// Gets or sets the list of Git contributors who have modified the file.
        /// </summary>
        public List<string> GitContributors { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of related file paths based on various relationships.
        /// </summary>
        public List<string> RelatedFiles { get; set; } = new();
    }
}