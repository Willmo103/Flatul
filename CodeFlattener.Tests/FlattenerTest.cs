using Xunit;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using System.Linq;
using YamlDotNet.Serialization;

namespace CodeFlattener.Tests
{
    /// <summary>
    /// Test class for the Flattener functionality
    /// </summary>
    public class FlattenerTests : IDisposable
    {
        private readonly string _testRootPath;
        private readonly string _outputPath;
        private readonly Repository _testRepo;

        public FlattenerTests()
        {
            // Initialize the FileHelper
            FileHelper.Initialize();

            // Setup test environment
            _testRootPath = Path.Combine(Path.GetTempPath(), "FlattenerTests_" + Guid.NewGuid());
            _outputPath = Path.Combine(_testRootPath, "output.md");
            Directory.CreateDirectory(_testRootPath);

            // Initialize test Git repository
            Repository.Init(_testRootPath);
            _testRepo = new Repository(_testRootPath);
        }

        public void Dispose()
        {
            _testRepo?.Dispose();
            try
            {
                if (Directory.Exists(_testRootPath))
                    Directory.Delete(_testRootPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        /// <summary>
        /// Creates test files with specified content in the test directory
        /// </summary>
        private async Task CreateTestFilesAsync()
        {
            // Create test files with different extensions
            await File.WriteAllTextAsync(
                Path.Combine(_testRootPath, "test1.cs"),
                "public class Test1 { void Method() { } }"
            );

            await File.WriteAllTextAsync(
                Path.Combine(_testRootPath, "test2.cs"),
                "public class Test2 { void Method() { Test1 test; } }"
            );

            // Create a subdirectory with a file
            var subDir = Path.Combine(_testRootPath, "subdir");
            Directory.CreateDirectory(subDir);
            await File.WriteAllTextAsync(
                Path.Combine(subDir, "test3.cs"),
                "Some text content"
            );

            // Commit files to Git repository
            Commands.Stage(_testRepo, "*");
            var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
            _testRepo.Commit("Initial commit", signature, signature);
        }

        [Fact]
        public async Task FlattenCodebaseAsync_WithValidInput_CreatesOutputFile()
        {
            // Arrange
            await CreateTestFilesAsync();
            var acceptedTypes = new[] { "*.cs" };
            var ignoredPaths = Array.Empty<string>();
            Console.WriteLine($"Test: FlattenCodebaseAsync_WithValidInput_CreatesOutputFile()");

            // Act
            await Flattener.FlattenCodebaseAsync(
                _testRootPath,
                _outputPath,
                acceptedTypes,
                ignoredPaths,
                false
            );

            // Assert
            Assert.True(File.Exists(_outputPath));
            string content = await File.ReadAllTextAsync(_outputPath);

            Console.WriteLine($"Output file content: {content}");
            Console.WriteLine($"{content.Contains("test1.cs")}, {content.Contains("test2.cs")}, {content.Contains("test3.txt")}");

            Assert.Contains("test1.cs", content);
            Assert.Contains("test2.cs", content);
            Assert.DoesNotContain("test3.txt", content); // Should be filtered out
        }

        [Fact]
        public async Task FlattenCodebaseAsync_WithCompression_CompressesContent()
        {
            // Arrange
            await CreateTestFilesAsync();
            var acceptedTypes = new[] { "*.cs" };
            var ignoredPaths = Array.Empty<string>();

            // Act
            await Flattener.FlattenCodebaseAsync(
                _testRootPath,
                _outputPath,
                acceptedTypes,
                ignoredPaths,
                true
            );

            // Assert
            string content = await File.ReadAllTextAsync(_outputPath);
            Assert.DoesNotContain("  ", content); // Should not contain multiple spaces
            Assert.Contains("public class Test1{", content); // Spaces should be removed
        }

        [Fact]
        public async Task FlattenCodebaseAsync_WithIgnoredPaths_ExcludesIgnoredFiles()
        {
            // Arrange
            await CreateTestFilesAsync();
            var acceptedTypes = new[] { "*.*" };
            var ignoredPaths = new[] { "subdir" };

            // Act
            await Flattener.FlattenCodebaseAsync(
                _testRootPath,
                _outputPath,
                acceptedTypes,
                ignoredPaths,
                false
            );

            // Assert
            string content = await File.ReadAllTextAsync(_outputPath);
            Assert.Contains("test1.cs", content);
            Assert.Contains("test2.cs", content);
            Assert.DoesNotContain("test3.txt", content); // Should be ignored
        }

        [Fact]
        public async Task FlattenCodebaseAsync_StoresGitMetadata()
        {
            // Arrange
            await CreateTestFilesAsync();
            var acceptedTypes = new[] { "*.cs" };
            var ignoredPaths = Array.Empty<string>();

            // Act
            await Flattener.FlattenCodebaseAsync(
                _testRootPath,
                _outputPath,
                acceptedTypes,
                ignoredPaths,
                false
            );

            // Assert
            string content = await File.ReadAllTextAsync(_outputPath);
            Assert.Contains("GitLastAuthor: Test User", content);
            Assert.Contains("GitCommitId:", content);
        }

        [Theory]
        [InlineData("nonexistent")]
        public async Task FlattenCodebaseAsync_WithInvalidDirectory_ThrowsException(string invalidPath)
        {
            // Arrange
            var acceptedTypes = new[] { "*.cs" };
            var ignoredPaths = Array.Empty<string>();

            // Act & Assert
            await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
                Flattener.FlattenCodebaseAsync(
                    invalidPath,
                    _outputPath,
                    acceptedTypes,
                    ignoredPaths,
                    false
                )
            );
        }

        [Fact]
        public async Task FlattenCodebaseAsync_FindsRelatedFiles()
        {
            // Arrange
            await CreateTestFilesAsync();
            var acceptedTypes = new[] { "*.cs" };
            var ignoredPaths = Array.Empty<string>();

            // Act
            await Flattener.FlattenCodebaseAsync(
                _testRootPath,
                _outputPath,
                acceptedTypes,
                ignoredPaths,
                false
            );

            // Assert
            string content = await File.ReadAllTextAsync(_outputPath);
            Assert.Contains("## Related Files", content);
            // test2.cs should be related to test1.cs because it references it
            Assert.Contains("[[test1.cs]]", content);
        }

        [Fact]
        public async Task FlattenCodebaseAsync_GeneratesValidMarkdown()
        {
            // Arrange
            await CreateTestFilesAsync();
            var acceptedTypes = new[] { "*.cs" };
            var ignoredPaths = Array.Empty<string>();

            // Act
            await Flattener.FlattenCodebaseAsync(
                _testRootPath,
                _outputPath,
                acceptedTypes,
                ignoredPaths,
                false
            );

            // Assert
            string content = await File.ReadAllTextAsync(_outputPath);
            Assert.Contains("---", content); // YAML frontmatter delimiter
            Assert.Contains("```cs", content); // Code block start
            Assert.Contains("```", content); // Code block end
        }
    }
}