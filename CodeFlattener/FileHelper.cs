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
