using System;
using System.Collections.Concurrent;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic; // Added missing import

namespace Draggy.Services
{
    public static class ThumbnailCache
    {
        private static readonly ConcurrentDictionary<string, WeakReference<BitmapSource>> _thumbnailCache = new();
        private static readonly object _cacheLock = new object();
        
        // Limiti per il cache dei thumbnail
        private const int MaxCachedThumbnails = 100;
        private static int _currentCacheSize = 0;

        public static BitmapSource? GetThumbnail(string filePath, int size)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            // Crea una chiave unica per il file e la dimensione
            var cacheKey = $"{filePath}_{size}";
            
            // Prova a recuperare dal cache
            if (_thumbnailCache.TryGetValue(cacheKey, out var weakRef))
            {
                if (weakRef.TryGetTarget(out var cachedThumbnail))
                {
                    System.Diagnostics.Debug.WriteLine($"Thumbnail recuperato dal cache: {Path.GetFileName(filePath)}");
                    return cachedThumbnail;
                }
                else
                {
                    // Il riferimento è stato garbage collected, rimuovilo
                    _thumbnailCache.TryRemove(cacheKey, out _);
                    _currentCacheSize--;
                }
            }

            // Genera il nuovo thumbnail
            var thumbnail = ShellThumbnail.GetThumbnail(filePath, size);
            if (thumbnail != null)
            {
                // Aggiungi al cache se c'è spazio
                if (_currentCacheSize < MaxCachedThumbnails)
                {
                    _thumbnailCache.TryAdd(cacheKey, new WeakReference<BitmapSource>(thumbnail));
                    _currentCacheSize++;
                    System.Diagnostics.Debug.WriteLine($"Thumbnail aggiunto al cache: {Path.GetFileName(filePath)} (totale: {_currentCacheSize})");
                }
                else
                {
                    // Cache pieno, pulisci i riferimenti deboli
                    CleanupWeakReferences();
                    
                    // Prova di nuovo ad aggiungere
                    if (_currentCacheSize < MaxCachedThumbnails)
                    {
                        _thumbnailCache.TryAdd(cacheKey, new WeakReference<BitmapSource>(thumbnail));
                        _currentCacheSize++;
                    }
                }
            }

            return thumbnail;
        }

        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                _thumbnailCache.Clear();
                _currentCacheSize = 0;
                System.Diagnostics.Debug.WriteLine("Cache thumbnail pulita");
            }
        }

        public static void RemoveFromCache(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            // Rimuovi tutte le dimensioni per questo file
            var keysToRemove = new List<string>();
            foreach (var key in _thumbnailCache.Keys)
            {
                if (key.StartsWith(filePath + "_"))
                {
                    keysToRemove.Add(key);
                }
            }

            foreach (var key in keysToRemove)
            {
                if (_thumbnailCache.TryRemove(key, out _))
                {
                    _currentCacheSize--;
                }
            }

            if (keysToRemove.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Rimossi {keysToRemove.Count} thumbnail dal cache per: {Path.GetFileName(filePath)}");
            }
        }

        private static void CleanupWeakReferences()
        {
            var keysToRemove = new List<string>();
            
            foreach (var kvp in _thumbnailCache)
            {
                if (!kvp.Value.TryGetTarget(out _))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                if (_thumbnailCache.TryRemove(key, out _))
                {
                    _currentCacheSize--;
                }
            }

            if (keysToRemove.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Puliti {keysToRemove.Count} riferimenti deboli dal cache thumbnail");
            }
        }

        public static int GetCacheSize()
        {
            return _currentCacheSize;
        }

        public static long GetEstimatedMemoryUsage()
        {
            // Stima approssimativa: ogni thumbnail 48x48 a 32bit = ~9KB
            return _currentCacheSize * 9 * 1024;
        }
    }
}
