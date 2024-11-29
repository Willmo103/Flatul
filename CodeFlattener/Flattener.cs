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
