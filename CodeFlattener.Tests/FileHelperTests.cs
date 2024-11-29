using Xunit;
using System.Text;

namespace CodeFlattener.Tests
{
    public class FileHelperTests : IDisposable
    {
        private readonly string _testDirectory;

        public FileHelperTests()
        {
            // Initialize FileHelper with default mappings
            FileHelper.Initialize();

            // Create a temporary test directory
            _testDirectory = Path.Combine(Path.GetTempPath(), "FileHelperTests_" + Guid.NewGuid());
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [Theory]
        [InlineData("test.cs", "csharp")]
        [InlineData("test.py", "python")]
        [InlineData("test.js", "javascript")]
        [InlineData("test.unknown", "plaintext")]
        [InlineData("Dockerfile", "dockerfile")]
        [InlineData("Makefile", "makefile")]
        public void GetLanguageIdentifier_ReturnsCorrectLanguage(string fileName, string expectedLanguage)
        {
            Console.WriteLine($"Test: GetLanguageIdentifier_ReturnsCorrectLanguage({fileName}, {expectedLanguage})");
            // Arrange
            var filePath = Path.Combine(_testDirectory, fileName);
            // Act
            var result = FileHelper.GetLanguageIdentifier(filePath);
            Console.WriteLine($"Result for GetLanguageIdentifier: {result}, Expected: {expectedLanguage}");
            // Assert
            Assert.Equal(expectedLanguage, result);
        }

        [Fact]
        public void GetLanguageIdentifier_WithCustomMapping_UsesCustomMapping()
        {
            Console.WriteLine("Test: GetLanguageIdentifier_WithCustomMapping_UsesCustomMapping()");
            // Arrange
            var customMapping = new Dictionary<string, string>
            {
                { ".custom", "customlang" }
            };
            FileHelper.Initialize(customMapping);

            // Act
            var result = FileHelper.GetLanguageIdentifier("test.custom");

            Console.WriteLine($"Result for GetLanguageIdentifier: {result}, Expected: customlang");
            // Assert
            Assert.Equal("customlang", result);
        }

        [Fact]
        public async Task IsTextFile_WithTextFile_ReturnsTrue()
        {
            Console.WriteLine("Test: IsTextFile_WithTextFile_ReturnsTrue()");
            // Arrange
            var filePath = Path.Combine(_testDirectory, "test.txt");
            await File.WriteAllTextAsync(filePath, "This is a text file\nWith multiple lines\n");

            // Act
            var result = FileHelper.IsTextFile(filePath);

            Console.WriteLine($"Result for IsTextFile: {result}, Expected: True");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsTextFile_WithBinaryFile_ReturnsFalse()
        {
            Console.WriteLine("Test: IsTextFile_WithBinaryFile_ReturnsFalse()");
            // Arrange
            var filePath = Path.Combine(_testDirectory, "test.bin");
            await File.WriteAllBytesAsync(filePath, new byte[] { 0x7F, 0x45, 0x4C, 0x46, 0x00, 0x01, 0x02 }); // ELF signature

            // Act
            var result = FileHelper.IsTextFile(filePath);

            Console.WriteLine($"Result for IsTextFile: {result}, Expected: False");
            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("path/to/file.txt", "path.to.file.txt")]
        [InlineData("path\\to\\file.txt", "path.to.file.txt")]
        [InlineData("file.txt", "file.txt")]
        public void NormalizePath_ReplacesSlashesWithDots(string input, string expected)
        {
            Console.WriteLine($"Test: NormalizePath_ReplacesSlashesWithDots({input}, {expected})");
            // Act
            var result = FileHelper.NormalizePath(input);

            Console.WriteLine($"Result for NormalizePath: {result}, Expected: {expected}");
            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("*.cs", "test.cs", true)]
        [InlineData("*.cs", "test.js", false)]
        [InlineData("test*", "test123.cs", true)]
        [InlineData("test?", "test1.cs", true)]
        [InlineData("test?", "test12.py", false)]
        public void MatchesFilter_WithVariousPatterns_ReturnsExpectedResult(string filter, string path, bool expected)
        {
            Console.WriteLine($"Test: MatchesFilter_WithVariousPatterns_ReturnsExpectedResult({filter}, {path}, {expected})");
            // Act
            var result = FileHelper.MatchesFilter(path, filter);

            Console.WriteLine($"Result for MatchesFilter: {result}, Expected: {expected}");
            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetRelativePath_ReturnsCorrectPath()
        {
            Console.WriteLine("Test: GetRelativePath_ReturnsCorrectPath()");
            // Arrange
            var basePath = Path.Combine(_testDirectory, "base");
            var fullPath = Path.Combine(basePath, "subfolder", "file.txt");

            // Act
            var result = FileHelper.GetRelativePath(basePath, fullPath);

            Console.WriteLine($"Result for GetRelativePath: {result}, Expected: subfolder/file.txt");

            // Assert
            Assert.Equal("subfolder/file.txt", result);
        }

        [Fact]
        public void IsGitRepository_WithGitFolder_ReturnsTrue()
        {
            Console.WriteLine("Test: IsGitRepository_WithGitFolder_ReturnsTrue()");
            // Arrange
            Directory.CreateDirectory(Path.Combine(_testDirectory, ".git"));

            // Act
            var result = FileHelper.IsGitRepository(_testDirectory);

            Console.WriteLine($"Result for IsGitRepository: {result}, Expected: True");
            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsGitRepository_WithoutGitFolder_ReturnsFalse()
        {
            Console.WriteLine("Test: IsGitRepository_WithoutGitFolder_ReturnsFalse()");
            // Act
            var result = FileHelper.IsGitRepository(_testDirectory);

            Console.WriteLine($"Result for IsGitRepository: {result}, Expected: False");
            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Initialize_CalledTwice_DoesNotReinitialize()
        {
            Console.WriteLine("Test: Initialize_CalledTwice_DoesNotReinitialize()");
            // Arrange
            FileHelper.Initialize();
            var firstResult = FileHelper.GetLanguageIdentifier("test.cs");

            Console.WriteLine($"Result for GetLanguageIdentifier (first call): {firstResult}");

            // Act - Initialize with empty mapping
            FileHelper.Initialize(new Dictionary<string, string>());
            var secondResult = FileHelper.GetLanguageIdentifier("test.cs");

            Console.WriteLine($"Result for GetLanguageIdentifier (second call): {secondResult}");

            // Assert
            Assert.Equal(firstResult, secondResult);
        }

        [Fact]
        public async Task IsTextFile_WithEmptyFile_ReturnsTrue()
        {
            Console.WriteLine("Test: IsTextFile_WithEmptyFile_ReturnsTrue()");

            // Arrange
            var filePath = Path.Combine(_testDirectory, "empty.txt");
            await File.WriteAllTextAsync(filePath, "");

            // Act
            var result = FileHelper.IsTextFile(filePath);

            Console.WriteLine($"Result for IsTextFile: {result}, Expected: True");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsTextFile_WithHighNonPrintableRatio_ReturnsFalse()
        {
            Console.WriteLine("Test: IsTextFile_WithHighNonPrintableRatio_ReturnsFalse()");
            // Arrange
            var filePath = Path.Combine(_testDirectory, "nonprintable.bin");
            var content = new byte[1000];
            for (int i = 0; i < content.Length; i++)
            {
                content[i] = (byte)(i % 31); // Lots of non-printable characters
            }
            await File.WriteAllBytesAsync(filePath, content);

            Console.WriteLine($"Content length: {content.Length}");
            
            // Act
            var result = FileHelper.IsTextFile(filePath);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData(new byte[] { 0x7F, 0x45, 0x4C, 0x46 })] // ELF
        [InlineData(new byte[] { 0x4D, 0x5A })] // PE/DOS
        [InlineData(new byte[] { 0x50, 0x4B, 0x03, 0x04 })] // ZIP
        [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 })] // PDF
        [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 })] // PNG
        public async Task IsTextFile_WithVariousBinarySignatures_ReturnsFalse(byte[] signature)
        {
            Console.WriteLine("Test: IsTextFile_WithVariousBinarySignatures_ReturnsFalse()");
            // Arrange
            var filePath = Path.Combine(_testDirectory, "binary.bin");
            await File.WriteAllBytesAsync(filePath, signature.Concat(new byte[] { 0x00, 0x01, 0x02 }).ToArray());

            // Act
            var result = FileHelper.IsTextFile(filePath);

            Console.WriteLine($"Result for IsTextFile: {result}, Expected: False");

            // Assert
            Assert.False(result);
        }
    }
}