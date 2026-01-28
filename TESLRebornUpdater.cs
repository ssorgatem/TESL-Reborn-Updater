using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO.Compression;

class Program
{
    static void Main(string[] args)
    {
        new TESLRebornUpdater().Run();
    }
}

public class TESLRebornUpdater
{
    private readonly string ApiUrl = "https://tesl-reborn.com/download";
    private readonly string UserAgent = "TESL-Reborn-Updater/2.0";
    private string _logFilePath;
    
    public void Run()
    {
        Console.Title = "TESL Reborn Updater";
        Console.WriteLine("=======================================");
        Console.WriteLine("     TESL Reborn Updater");
        Console.WriteLine("=======================================");
        
        try
        {
            // Setup logging
            string gameDir = Directory.GetCurrentDirectory();
            _logFilePath = Path.Combine(gameDir, "TESLReborn_Update.log");
            
            LogMessage("=======================================");
            LogMessage("TESL Reborn Updater Started");
            LogMessage($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            LogMessage($"Game Directory: {gameDir}");
            LogMessage("=======================================");
            
            string downloadDir = Path.Combine(gameDir, "BepInEx", "TESLRebornDownloads");
            
            Console.WriteLine("\n[1/4] Checking for updates...");
            LogMessage("Phase 1: Checking for updates");
            
            // Get update info from API
            var updateInfo = GetLatestUpdateInfo();
            
            if (updateInfo == null || string.IsNullOrEmpty(updateInfo.Item1) || string.IsNullOrEmpty(updateInfo.Item2))
            {
                throw new Exception("Failed to retrieve update information from API");
            }
            
            string version = updateInfo.Item1;
            string zipUrl = updateInfo.Item2;
            
            Console.WriteLine($"    Version {version} found");
            Console.WriteLine($"    File: {Path.GetFileName(zipUrl)}");
            
            LogMessage($"Latest version found: {version}");
            LogMessage($"Download URL: {zipUrl}");
            
            Console.WriteLine("\n[2/4] Downloading...");
            LogMessage("Phase 2: Downloading");
            
            string zipPath = DownloadFile(zipUrl, downloadDir, version);
            
            if (zipPath == null)
            {
                Console.WriteLine("    Already up to date!");
                LogMessage("Update: Already up to date, skipping download");
                Console.WriteLine("\n✅ Already up to date!");
                LogMessage("SUCCESS: Already up to date");
                LogMessage("=======================================");
                return;
            }
            
            Console.WriteLine($"    Saved to: {Path.GetFileName(zipPath)}");
            LogMessage($"Download saved to: {zipPath}");
            
            // Get file size
            FileInfo fileInfo = new FileInfo(zipPath);
            LogMessage($"Download size: {FormatFileSize(fileInfo.Length)}");
            
            Console.WriteLine("\n[3/4] Extracting to game directory...");
            LogMessage("Phase 3: Extracting files");
            
            int extractedCount = ExtractZip(zipPath, gameDir);
            Console.WriteLine($"    Extracted {extractedCount} files");
            LogMessage($"Extracted {extractedCount} files to: {gameDir}");
            
            Console.WriteLine("\n[4/4] Complete!");
            LogMessage("Phase 4: Update completed");
            
            Console.WriteLine($"\n✅ Update to version {version} completed successfully!");
            LogMessage($"SUCCESS: Update to version {version} completed successfully");
            LogMessage("=======================================");
            
            // Also write success to a separate marker file
            WriteSuccessMarker(gameDir, version);
        }
        catch (Exception e)
        {
            Console.WriteLine($"\n❌ Error: {e.Message}");
            LogMessage($"ERROR: {e.GetType().Name}: {e.Message}");
            LogMessage($"Stack Trace: {e.StackTrace}");
            LogMessage("=======================================");
            
            // Write error to a separate error log
            WriteErrorLog(e);
        }
    }
    
    // Simple JSON parser for extracting version and url
    private Tuple<string, string> GetLatestUpdateInfo()
    {
        LogMessage($"Fetching update info from API: {ApiUrl}");
        
        // Force use of TLS 1.2 (fixes SSL/TLS errors on older .NET)
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        
        using (WebClient client = new WebClient())
        {
            // Set headers for JSON API using global UserAgent
            client.Headers.Add("User-Agent", UserAgent);
            client.Headers.Add("Accept", "application/json");
            
            try
            {
                // Download the JSON response
                string jsonResponse = client.DownloadString(ApiUrl);
                LogMessage($"API response received ({jsonResponse.Length} bytes)");
                
                // Simple JSON parsing - extract version and url
                string version = ExtractJsonValue(jsonResponse, "version");
                string url = ExtractJsonValue(jsonResponse, "url");
                
                if (string.IsNullOrEmpty(version))
                {
                    LogMessage("ERROR: Version field missing in API response");
                    throw new Exception("Version information missing in API response");
                }
                
                if (string.IsNullOrEmpty(url))
                {
                    LogMessage("ERROR: URL field missing in API response");
                    throw new Exception("Download URL missing in API response");
                }
                
                LogMessage($"API parsed successfully - Version: {version}, URL: {url}");
                
                return new Tuple<string, string>(version, url);
            }
            catch (WebException webEx)
            {
                LogMessage($"WebException when calling API: {webEx.Message}");
                
                if (webEx.Response is HttpWebResponse httpResponse)
                {
                    LogMessage($"HTTP Status Code: {httpResponse.StatusCode} ({httpResponse.StatusDescription})");
                    
                    // Try to read the error response
                    try
                    {
                        using (StreamReader reader = new StreamReader(webEx.Response.GetResponseStream()))
                        {
                            string errorResponse = reader.ReadToEnd();
                            LogMessage($"Error response body: {errorResponse}");
                        }
                    }
                    catch { }
                }
                
                throw new Exception($"Failed to connect to update server: {webEx.Message}");
            }
            catch (Exception ex)
            {
                LogMessage($"Exception when parsing API response: {ex.Message}");
                throw new Exception($"Failed to process update information: {ex.Message}");
            }
        }
    }
    
    // Simple method to extract values from JSON
    private string ExtractJsonValue(string json, string key)
    {
        try
        {
            // Find the key in the JSON
            string searchPattern = $"\"{key}\"\\s*:\\s*\"";
            Match match = Regex.Match(json, searchPattern + "([^\"]+)\"");
            
            if (match.Success)
            {
                string value = match.Groups[1].Value;
                // Unescape JSON characters (especially \/)
                value = UnescapeJsonString(value);
                return value;
            }
            
            // Also try without quotes (for numeric or boolean values)
            searchPattern = $"\"{key}\"\\s*:\\s*";
            match = Regex.Match(json, searchPattern + "([^,}\\s\"]+)");
            
            if (match.Success)
            {
                string value = match.Groups[1].Value.Trim();
                return value;
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }
    
    // Unescape JSON string (handles \/, \", \\, \n, \r, \t, etc.)
    private string UnescapeJsonString(string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString))
            return jsonString;
            
        // Replace escaped forward slashes first
        string result = jsonString.Replace("\\/", "/");
        
        // Handle other common JSON escapes
        result = result.Replace("\\\"", "\"")
                      .Replace("\\\\", "\\")
                      .Replace("\\n", "\n")
                      .Replace("\\r", "\r")
                      .Replace("\\t", "\t")
                      .Replace("\\b", "\b")
                      .Replace("\\f", "\f");
        
        // Handle Unicode escapes (simple version - \uXXXX)
        // This regex matches \u followed by 4 hex digits
        result = Regex.Replace(result, @"\\u([0-9a-fA-F]{4})", match =>
        {
            try
            {
                int unicodeValue = Convert.ToInt32(match.Groups[1].Value, 16);
                return char.ConvertFromUtf32(unicodeValue);
            }
            catch
            {
                return match.Value; // Return original if conversion fails
            }
        });
        
        return result;
    }
    
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double len = bytes;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }
    
    private void LogMessage(string message)
    {
        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logEntry = $"[{timestamp}] {message}";
            
            // Write to log file
            File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            
            // Also write to console in debug mode
            #if DEBUG
            Console.WriteLine($"[LOG] {message}");
            #endif
        }
        catch (Exception ex)
        {
            // If we can't log to file, at least write to console
            Console.WriteLine($"[LOGGING ERROR] {ex.Message}");
        }
    }
    
    private void WriteSuccessMarker(string gameDir, string version)
    {
        try
        {
            string markerPath = Path.Combine(gameDir, "TESLReborn_Update_Success.txt");
            string content = $"Last successful update: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            content += $"Version: {version}\n";
            content += $"Log file: {_logFilePath}\n";
            File.WriteAllText(markerPath, content);
        }
        catch { }
    }
    
    private void WriteErrorLog(Exception e)
    {
        try
        {
            string errorLogPath = Path.Combine(Path.GetDirectoryName(_logFilePath), "TESLReborn_Update_Error.log");
            string content = $"Error occurred: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
            content += $"Type: {e.GetType().FullName}\n";
            content += $"Message: {e.Message}\n";
            content += $"Stack Trace:\n{e.StackTrace}\n";
            content += "=======================================\n";
            File.AppendAllText(errorLogPath, content);
        }
        catch { }
    }
    
    private string DownloadFile(string url, string destDir, string version)
    {
        Directory.CreateDirectory(destDir);
        
        // Extract filename from URL
        Uri uri = new Uri(url);
        string fileName = Path.GetFileName(uri.LocalPath);
        
        // Include version in filename if possible
        if (!string.IsNullOrEmpty(version) && !fileName.Contains(version))
        {
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            fileName = $"{baseName}_v{version}{extension}";
        }
        
        string localPath = Path.Combine(destDir, fileName);
        
        LogMessage($"Download destination: {localPath}");
        
        // Check if already downloaded
        if (File.Exists(localPath))
        {
            LogMessage($"File already exists: {fileName}");
            
            // Verify file size
            try
            {
                long localSize = new FileInfo(localPath).Length;
                LogMessage($"Local file size: {FormatFileSize(localSize)}");
                
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("User-Agent", UserAgent);
                    client.OpenRead(url);
                    long remoteSize = Convert.ToInt64(client.ResponseHeaders["Content-Length"]);
                    LogMessage($"Remote file size: {FormatFileSize(remoteSize)}");
                    
                    if (localSize == remoteSize)
                    {
                        LogMessage($"File verification passed: {fileName}");
                        return null; // Already downloaded with correct size
                    }
                    else
                    {
                        LogMessage($"File size mismatch, re-downloading: {fileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Could not verify file, re-downloading: {ex.Message}");
            }
        }
        
        LogMessage($"Starting download: {url}");
        Console.Write("    Progress: [");
        
        using (WebClient client = new WebClient())
        {
            client.Headers.Add("User-Agent", UserAgent);
            
            // Add progress reporting
            client.DownloadProgressChanged += (sender, e) =>
            {
                Console.CursorLeft = 13;
                Console.Write($"{e.ProgressPercentage}%");
            };
            
            DateTime startTime = DateTime.Now;
            client.DownloadFile(url, localPath);
            TimeSpan downloadTime = DateTime.Now - startTime;
            
            Console.CursorLeft = 13;
            Console.Write("100%] Done!");
            
            // Get final file info
            FileInfo fileInfo = new FileInfo(localPath);
            double downloadSpeed = fileInfo.Length / downloadTime.TotalSeconds;
            
            LogMessage($"Download completed in {downloadTime.TotalSeconds:F1} seconds");
            LogMessage($"Average speed: {FormatFileSize((long)downloadSpeed)}/s");
        }
        
        Console.WriteLine(); // New line after progress bar
        
        return localPath;
    }
    
    private int ExtractZip(string zipPath, string extractDir)
    {
        Directory.CreateDirectory(extractDir);
        int extractedCount = 0;
        
        LogMessage($"Starting extraction from: {zipPath}");
        LogMessage($"Extracting to: {extractDir}");
        
        // Use System.IO.Compression.ZipFile class
        using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
        {
            LogMessage($"Archive contains {archive.Entries.Count} entries");
            
            foreach (var entry in archive.Entries)
            {
                try
                {
                    string destinationPath = Path.GetFullPath(Path.Combine(extractDir, entry.FullName));
                    
                    // Skip if it's a directory entry
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        // It's a directory - ensure it exists
                        string destinationDir = Path.GetDirectoryName(destinationPath);
                        if (!Directory.Exists(destinationDir))
                        {
                            Directory.CreateDirectory(destinationDir);
                            LogMessage($"Created directory: {entry.FullName}");
                        }
                        continue;
                    }
                    
                    // Ensure the directory exists
                    string destinationDir2 = Path.GetDirectoryName(destinationPath);
                    if (!Directory.Exists(destinationDir2))
                        Directory.CreateDirectory(destinationDir2);
                    
                    // Check if file already exists
                    bool overwritten = File.Exists(destinationPath);
                    
                    // Extract the file
                    entry.ExtractToFile(destinationPath, true);
                    extractedCount++;
                    
                    if (overwritten)
                    {
                        LogMessage($"Overwrote: {entry.FullName}");
                    }
                    else
                    {
                        LogMessage($"Extracted: {entry.FullName}");
                    }
                    
                    // Update progress every 10 files
                    if (extractedCount % 10 == 0)
                    {
                        Console.Write(".");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"WARNING: Failed to extract {entry.FullName}: {ex.Message}");
                }
            }
        }
        
        Console.WriteLine(); // New line after progress dots
        LogMessage($"Total files extracted: {extractedCount}");
        
        return extractedCount;
    }
}
