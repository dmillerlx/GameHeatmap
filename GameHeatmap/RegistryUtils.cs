using System;
using Microsoft.Win32;

namespace GameHeatmap
{
    public static class RegistryUtils
    {
        // Base registry key for GameHeatmap
        private static readonly string baseRegistryKey = @"HKEY_CURRENT_USER\Software\GameHeatmap";

        // Set string value
        public static void SetString(string key, string value)
        {
            try
            {
                Registry.SetValue(baseRegistryKey, key, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting string value: {ex.Message}");
            }
        }

        // Get string value
        public static string GetString(string key, string defaultValue = "")
        {
            try
            {
                object value = Registry.GetValue(baseRegistryKey, key, defaultValue);
                return value?.ToString() ?? defaultValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting string value: {ex.Message}");
                return defaultValue;
            }
        }

        // Set integer value
        public static void SetInt(string key, int value)
        {
            try
            {
                Registry.SetValue(baseRegistryKey, key, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting int value: {ex.Message}");
            }
        }

        // Get integer value
        public static int GetInt(string key, int defaultValue = 0)
        {
            try
            {
                object value = Registry.GetValue(baseRegistryKey, key, defaultValue);
                return value is int ? (int)value : defaultValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting int value: {ex.Message}");
                return defaultValue;
            }
        }

        // Set boolean value
        public static void SetBool(string key, bool value)
        {
            try
            {
                Registry.SetValue(baseRegistryKey, key, value ? 1 : 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting bool value: {ex.Message}");
            }
        }

        // Get boolean value
        public static bool GetBool(string key, bool defaultValue = false)
        {
            try
            {
                object value = Registry.GetValue(baseRegistryKey, key, defaultValue ? 1 : 0);
                return value is int intValue && (intValue == 1) || value is bool boolValue && boolValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting bool value: {ex.Message}");
                return defaultValue;
            }
        }

        // Store list of file paths (pipe-separated)
        public static void SetFileList(List<string> files)
        {
            string fileList = string.Join("|", files);
            SetString("PGNFiles", fileList);
        }

        // Get list of file paths
        public static List<string> GetFileList()
        {
            string fileList = GetString("PGNFiles", "");
            if (string.IsNullOrEmpty(fileList))
                return new List<string>();
            
            return new List<string>(fileList.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
