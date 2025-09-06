using System;
using System.IO;
using System.Linq;
using System.Collections.Generic; // Added for List

namespace Draggy.Services
{
    public class CacheService
    {
        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "Draggy", 
            "Cache");

        // Limiti per la cache
        private const long MaxCacheSizeMB = 100; // 100MB massimo
        private const long MaxCacheSizeBytes = MaxCacheSizeMB * 1024 * 1024;
        private const int MaxCacheFiles = 50; // Massimo 50 file in cache

        static CacheService()
        {
            EnsureCacheDirectoryExists();
            CleanupCacheIfNeeded();
        }

        public static string GetCacheDirectory()
        {
            EnsureCacheDirectoryExists();
            return CacheDirectory;
        }

        public static string CopyToCache(string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException($"File sorgente non trovato: {sourceFilePath}");

            EnsureCacheDirectoryExists();

            var fileName = Path.GetFileName(sourceFilePath);
            var destinationPath = Path.Combine(CacheDirectory, fileName);

            // Se esiste già un file con lo stesso nome, aggiungi un suffisso numerico
            if (File.Exists(destinationPath))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                int counter = 1;

                do
                {
                    fileName = $"{nameWithoutExt} ({counter}){extension}";
                    destinationPath = Path.Combine(CacheDirectory, fileName);
                    counter++;
                } while (File.Exists(destinationPath));
            }

            try
            {
                // Controlla se la cache è piena prima di copiare
                var fileInfo = new FileInfo(sourceFilePath);
                if (fileInfo.Length > MaxCacheSizeBytes)
                {
                    throw new InvalidOperationException($"File troppo grande per la cache: {fileInfo.Length / (1024 * 1024)}MB");
                }

                // Pulisci la cache se necessario prima di aggiungere il nuovo file
                CleanupCacheIfNeeded(fileInfo.Length);

                File.Copy(sourceFilePath, destinationPath, overwrite: false);
                return destinationPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Errore nella copia del file in cache: {ex.Message}", ex);
            }
        }

        public static void ClearCache()
        {
            if (Directory.Exists(CacheDirectory))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(CacheDirectory))
                    {
                        File.Delete(file);
                    }
                    System.Diagnostics.Debug.WriteLine("Cache completamente pulita");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Errore nella pulizia della cache: {ex.Message}");
                }
            }
        }

        public static void DeleteFromCache(string filePath)
        {
            if (File.Exists(filePath) && filePath.StartsWith(CacheDirectory))
            {
                try
                {
                    File.Delete(filePath);
                    System.Diagnostics.Debug.WriteLine($"File rimosso dalla cache: {Path.GetFileName(filePath)}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Errore nell'eliminazione del file dalla cache: {ex.Message}");
                }
            }
        }

        private static void CleanupCacheIfNeeded(long newFileSize = 0)
        {
            if (!Directory.Exists(CacheDirectory))
                return;

            try
            {
                var files = Directory.GetFiles(CacheDirectory)
                    .Select(f => new FileInfo(f))
                    .OrderBy(f => f.LastAccessTime) // Rimuovi prima i file più vecchi
                    .ToList();

                long totalSize = files.Sum(f => f.Length) + newFileSize;
                int fileCount = files.Count + (newFileSize > 0 ? 1 : 0);

                // Rimuovi file se superiamo i limiti
                var filesToRemove = new List<FileInfo>();

                // Controlla limite numero file
                if (fileCount > MaxCacheFiles)
                {
                    int filesToDelete = fileCount - MaxCacheFiles;
                    filesToRemove.AddRange(files.Take(filesToDelete));
                }

                // Controlla limite dimensione
                if (totalSize > MaxCacheSizeBytes)
                {
                    long sizeToFree = totalSize - MaxCacheSizeBytes;
                    long freedSize = 0;

                    foreach (var file in files.Where(f => !filesToRemove.Contains(f)))
                    {
                        if (freedSize >= sizeToFree)
                            break;

                        filesToRemove.Add(file);
                        freedSize += file.Length;
                    }
                }

                // Rimuovi i file selezionati
                foreach (var file in filesToRemove)
                {
                    try
                    {
                        File.Delete(file.FullName);
                        System.Diagnostics.Debug.WriteLine($"File rimosso dalla cache per limiti: {file.Name}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Errore nella rimozione del file {file.Name}: {ex.Message}");
                    }
                }

                if (filesToRemove.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Pulizia cache completata: rimossi {filesToRemove.Count} file");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore nella pulizia automatica della cache: {ex.Message}");
            }
        }

        public static long GetCacheSize()
        {
            if (!Directory.Exists(CacheDirectory))
                return 0;

            try
            {
                return Directory.GetFiles(CacheDirectory)
                    .Sum(f => new FileInfo(f).Length);
            }
            catch
            {
                return 0;
            }
        }

        public static int GetCacheFileCount()
        {
            if (!Directory.Exists(CacheDirectory))
                return 0;

            try
            {
                return Directory.GetFiles(CacheDirectory).Length;
            }
            catch
            {
                return 0;
            }
        }

        private static void EnsureCacheDirectoryExists()
        {
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
        }
    }
}
