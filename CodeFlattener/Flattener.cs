using System.Text;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using Markdig;
using YamlDotNet.Serialization;

namespace CodeFlattener
{
    public partial class Flattener
    {
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

        private static bool MatchesPattern(string input, string pattern)
        {
            // Convert glob pattern to regex
            string regex = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".")
                + "$";

            return Regex.IsMatch(input, regex, RegexOptions.IgnoreCase);
        }

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

        [GeneratedRegex(@"\s+")]
        private static partial Regex ExtraSpaces();

        [GeneratedRegex(@"(\(|\[|{) ")]
        private static partial Regex SpacesAfterSyntax();

        [GeneratedRegex(@" (\)|\]|}|,|;)")]
        private static partial Regex ClosingCodeSpaces();
    }

    public class FileMetadata
    {
        public string RelativePath { get; set; }
        public string AbsolutePath { get; set; }
        public DateTime LastModified { get; set; }
        public long SizeInBytes { get; set; }
        public string FileExtension { get; set; }

        // Git metadata
        public string GitCommitId { get; set; }
        public string GitLastAuthor { get; set; }
        public DateTime? GitLastModified { get; set; }
        public string GitBranch { get; set; }
        public string GitRemoteUrl { get; set; }
        public List<string> GitContributors { get; set; } = new();

        // Related files
        public List<string> RelatedFiles { get; set; } = new();
    }
}