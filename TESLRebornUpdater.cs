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
    private readonly string DownloadPage = "https://tesl-reborn.com/download";
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
            
            string zipUrl = GetLatestZipUrl();
            string fileName = Path.GetFileName(new Uri(zipUrl).LocalPath);
            Console.WriteLine($"    Found: {fileName}");
            LogMessage($"Latest version found: {fileName}");
            LogMessage($"Download URL: {zipUrl}");
            
            Console.WriteLine("\n[2/4] Downloading...");
            LogMessage("Phase 2: Downloading");
            
            string zipPath = DownloadFile(zipUrl, downloadDir);
            
            if (zipPath == null)
            {
                Console.WriteLine("    Already up to date!");
                LogMessage("Update: Already up to date, skipping download");
                WaitAndExit();
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
            
            Console.WriteLine("\n[4/4] Cleaning up...");
            LogMessage("Phase 4: Cleaning up old downloads");
            
            int cleanedCount = CleanupOldDownloads(zipPath, downloadDir);
            if (cleanedCount > 0)
            {
                Console.WriteLine($"    Removed {cleanedCount} old file(s)");
                LogMessage($"Cleaned up {cleanedCount} old download(s)");
            }
            else
            {
                Console.WriteLine("    No old files to remove");
                LogMessage("No old downloads to clean up");
            }
            
            Console.WriteLine("\n✅ Update completed successfully!");
            LogMessage("SUCCESS: Update completed successfully");
            LogMessage("=======================================");
            
            // Also write success to a separate marker file
            WriteSuccessMarker(gameDir);
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
        
        WaitAndExit();
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
    
    private void WriteSuccessMarker(string gameDir)
    {
        try
        {
            string markerPath = Path.Combine(gameDir, "TESLReborn_Update_Success.txt");
            string content = $"Last successful update: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
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
    
    private void WaitAndExit()
    {
        LogMessage($"Process completed at {DateTime.Now:HH:mm:ss}");
        
        if (File.Exists(_logFilePath))
        {
            FileInfo logFile = new FileInfo(_logFilePath);
            LogMessage($"Total log file size: {FormatFileSize(logFile.Length)}");
        }
    }
    
    private string GetLatestZipUrl()
    {
        LogMessage($"Fetching download page: {DownloadPage}");
        // Force use of TLS 1.2 (fixes SSL/TLS errors on older .NET)
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        using (WebClient client = new WebClient())
        {
            client.Headers.Add("User-Agent", "TESL-Reborn-Updater/1.0");
            client.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            
            string html = client.DownloadString(DownloadPage);
            LogMessage($"Download page fetched successfully ({html.Length} bytes)");
            
            List<string> zipLinks = new List<string>();
            var regex = new Regex(@"href=[""']([^""']+\.zip)[""']", RegexOptions.IgnoreCase);
            
            foreach (Match match in regex.Matches(html))
            {
                if (match.Groups[1].Success)
                {
                    string link = match.Groups[1].Value;
                    if (!link.StartsWith("http"))
                    {
                        link = new Uri(new Uri(DownloadPage), link).AbsoluteUri;
                    }
                    zipLinks.Add(link);
                }
            }
            
            LogMessage($"Found {zipLinks.Count} ZIP links on the page");
            
            if (zipLinks.Count == 0)
            {
                throw new Exception("No ZIP download links found on the page");
            }
            
            // Sort by version (assuming version numbers in URL)
            zipLinks.Sort((a, b) =>
            {
                var versionA = ExtractVersion(a);
                var versionB = ExtractVersion(b);
                return CompareVersions(versionB, versionA);
            });
            
            string latestUrl = zipLinks[0];
            LogMessage($"Selected latest version: {string.Join(".", ExtractVersion(latestUrl))}");
            
            return latestUrl;
        }
    }
    
private List<int> ExtractVersion(string url)
{
    List<int> version = new List<int>();
    
    // Find all sequences of digits separated by dots
    int startIndex = -1;
    int endIndex = -1;
    
    for (int i = 0; i < url.Length; i++)
    {
        if (char.IsDigit(url[i]))
        {
            if (startIndex == -1)
            {
                startIndex = i;
            }
            endIndex = i;
        }
        else if (url[i] == '.' && startIndex != -1 && i + 1 < url.Length && char.IsDigit(url[i + 1]))
        {
            // Continue through dots that are part of version
            endIndex = i;
        }
        else
        {
            if (startIndex != -1 && endIndex != -1)
            {
                // Check if this looks like a version number (has dots)
                string potentialVersion = url.Substring(startIndex, endIndex - startIndex + 1);
                if (potentialVersion.Contains("."))
                {
                    // Manual parsing without Split
                    string currentNumber = "";
                    for (int j = 0; j < potentialVersion.Length; j++)
                    {
                        if (char.IsDigit(potentialVersion[j]))
                        {
                            currentNumber += potentialVersion[j];
                        }
                        else if (potentialVersion[j] == '.')
                        {
                            if (currentNumber.Length > 0)
                            {
                                int num;
                                if (int.TryParse(currentNumber, out num))
                                {
                                    version.Add(num);
                                }
                                currentNumber = "";
                            }
                        }
                    }
                    
                    // Add the last number
                    if (currentNumber.Length > 0)
                    {
                        int num;
                        if (int.TryParse(currentNumber, out num))
                        {
                            version.Add(num);
                        }
                    }
                    
                    // Found a version, return it
                    if (version.Count > 0)
                    {
                        return version;
                    }
                }
                
                // Reset for next potential version
                startIndex = -1;
                endIndex = -1;
                version.Clear();
            }
        }
    }
    
    return version;
}
    
    private int CompareVersions(List<int> a, List<int> b)
    {
        int maxLength = Math.Max(a.Count, b.Count);
        for (int i = 0; i < maxLength; i++)
        {
            int numA = i < a.Count ? a[i] : 0;
            int numB = i < b.Count ? b[i] : 0;
            
            if (numA != numB)
                return numA.CompareTo(numB);
        }
        
        return 0;
    }
    
    private string DownloadFile(string url, string destDir)
    {
        Directory.CreateDirectory(destDir);
        string fileName = Path.GetFileName(new Uri(url).LocalPath);
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
                    client.Headers.Add("User-Agent", "TESL-Reborn-Updater/1.0");
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
            client.Headers.Add("User-Agent", "TESL-Reborn-Updater/1.0");
            
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
    
    private int CleanupOldDownloads(string currentZipPath, string downloadDir)
    {
        int deletedCount = 0;
        
        try
        {
            if (!Directory.Exists(downloadDir))
            {
                LogMessage($"Download directory does not exist: {downloadDir}");
                return 0;
            }
            
            var files = Directory.GetFiles(downloadDir, "*.zip");
            LogMessage($"Found {files.Length} ZIP files in download directory");
            
            foreach (var file in files)
            {
                if (!string.Equals(file, currentZipPath, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        FileInfo fileInfo = new FileInfo(file);
                        LogMessage($"Deleting old file: {Path.GetFileName(file)} ({FormatFileSize(fileInfo.Length)})");
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"WARNING: Could not delete {file}: {ex.Message}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            LogMessage($"ERROR during cleanup: {e.Message}");
        }
        
        return deletedCount;
    }
}
