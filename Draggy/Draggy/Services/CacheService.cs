using System;
using System.IO;

namespace Draggy.Services
{
    public class CacheService
    {
        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "Draggy", 
            "Cache");

        static CacheService()
        {
            EnsureCacheDirectoryExists();
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
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Errore nell'eliminazione del file dalla cache: {ex.Message}");
                }
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
