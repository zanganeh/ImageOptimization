using Microsoft.Win32;

namespace Geta.ImageOptimization.Helpers
{
    public static class MimeTypeHelper
    {
        /// <summary>
        /// Credits: http://www.cyotek.com/blog/mime-types-and-file-extensions
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns></returns>

        public static string GetDefaultExtension(string mimeType)
        {
            string result;
            RegistryKey key;
            object value;

            key = Registry.ClassesRoot.OpenSubKey(@"MIME\Database\Content Type\" + mimeType, false);
            value = key != null ? key.GetValue("Extension", null) : null;
            result = value != null ? value.ToString() : string.Empty;

            return result;
        }
    }
}