using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using ComIDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;
using ComIStream = System.Runtime.InteropServices.ComTypes.IStream;
using ComFORMATETC = System.Runtime.InteropServices.ComTypes.FORMATETC;
using ComSTGMEDIUM = System.Runtime.InteropServices.ComTypes.STGMEDIUM;
using ComTYMED = System.Runtime.InteropServices.ComTypes.TYMED;
using ComDVASPECT = System.Runtime.InteropServices.ComTypes.DVASPECT;

namespace Draggy.Services
{
    internal static class DragVirtualFileHelper
    {
        private const string FileGroupDescriptorW = "FileGroupDescriptorW";
        private const string FileGroupDescriptor = "FileGroupDescriptor";
        private const string FileContents = "FileContents";
        private const string UriList = "text/uri-list";
        private const string UrlW = "UniformResourceLocatorW";
        private const string UrlA = "UniformResourceLocator";
        private const string DownloadUrl = "DownloadURL"; // Chromium
        private const string MozUrl = "text/x-moz-url";   // Firefox

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "RegisterClipboardFormatW")]
        private static extern ushort RegisterClipboardFormat(string lpszFormat);

        [DllImport("ole32.dll")]
        private static extern void ReleaseStgMedium(ref ComSTGMEDIUM pmedium);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern int GlobalSize(IntPtr hMem);

        // Use System.Runtime.InteropServices.ComTypes for FORMATETC/STGMEDIUM/DVASPECT/TYMED

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FILEDESCRIPTORW
        {
            public uint dwFlags;
            public Guid clsid;
            public System.Drawing.Size sizel;
            public System.Drawing.Point pointl;
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
        }

        public static bool ContainsVirtualFile(IDataObject data)
        {
            try
            {
                return data.GetDataPresent(FileGroupDescriptorW, false) || data.GetDataPresent(FileGroupDescriptor, false);
            }
            catch
            {
                return false;
            }
        }

        public static List<string> TrySaveVirtualFilesToCache(IDataObject data)
        {
            var savedFiles = new List<string>();

            try
            {
                // Try COM route first (more robust)
                var comSaved = TryExtractViaCom(data);
                if (comSaved.Count > 0)
                    return comSaved;

                // Prefer unicode descriptor
                var format = data.GetDataPresent(FileGroupDescriptorW, false) ? FileGroupDescriptorW : FileGroupDescriptor;
                if (!data.GetDataPresent(format, false))
                    return savedFiles;

                var descriptorStream = data.GetData(format, false) as Stream;
                if (descriptorStream == null)
                    return savedFiles;

                using (descriptorStream)
                using (var ms = new MemoryStream())
                {
                    descriptorStream.CopyTo(ms);
                    var buffer = ms.ToArray();
                    if (buffer.Length < 4)
                        return savedFiles;

                    int itemCount = BitConverter.ToInt32(buffer, 0);
                    int offset = 4; // after cItems
                    int descriptorSize = Marshal.SizeOf<FILEDESCRIPTORW>();

                    for (int i = 0; i < itemCount; i++)
                    {
                        if (offset + descriptorSize > buffer.Length)
                            break;

                        string fileName = ReadFileDescriptorName(buffer, offset);
                        offset += descriptorSize;

                        // Get file contents. For a single file, WPF often exposes a MemoryStream at "FileContents".
                        // For multiple files, some sources expose an array of streams; handle both cases best-effort.
                        if (!data.GetDataPresent(FileContents, false))
                            continue;

                        var contentsObj = data.GetData(FileContents, false);

                        if (contentsObj is Stream singleStream)
                        {
                            var savedPath = SaveStreamToCache(singleStream, fileName);
                            if (!string.IsNullOrEmpty(savedPath))
                                savedFiles.Add(savedPath);
                        }
                        else if (contentsObj is Stream[] streamArray)
                        {
                            if (i < streamArray.Length)
                            {
                                var savedPath = SaveStreamToCache(streamArray[i], fileName);
                                if (!string.IsNullOrEmpty(savedPath))
                                    savedFiles.Add(savedPath);
                            }
                        }
                        else if (contentsObj is object[] objArray)
                        {
                            // Some providers return object[] where each is a Stream
                            if (i < objArray.Length && objArray[i] is Stream s)
                            {
                                var savedPath = SaveStreamToCache(s, fileName);
                                if (!string.IsNullOrEmpty(savedPath))
                                    savedFiles.Add(savedPath);
                            }
                        }
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine($"Virtual file extraction failed: {comEx.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Virtual file extraction failed: {ex.Message}");
            }

            return savedFiles;
        }

        private static List<string> TryExtractViaCom(IDataObject data)
        {
            var saved = new List<string>();
            ComIDataObject? comData = null;
            try
            {
                comData = data as ComIDataObject;
            }
            catch
            {
                comData = null;
            }

            if (comData == null)
                return saved;

            ushort cfDescriptor = RegisterClipboardFormat(FileGroupDescriptorW);
            if (cfDescriptor == 0)
                cfDescriptor = RegisterClipboardFormat(FileGroupDescriptor);
            ushort cfContents = RegisterClipboardFormat(FileContents);

            if (cfDescriptor == 0 || cfContents == 0)
                return saved;

            // Request FILEGROUPDESCRIPTOR as HGLOBAL
            var fmt = new ComFORMATETC
            {
                cfFormat = (short)cfDescriptor,
                ptd = IntPtr.Zero,
                dwAspect = ComDVASPECT.DVASPECT_CONTENT,
                lindex = -1,
                tymed = ComTYMED.TYMED_HGLOBAL
            };

            ComSTGMEDIUM medium;
            try
            {
                comData.GetData(ref fmt, out medium);
            }
            catch (COMException)
            {
                return saved;
            }

            try
            {
                if (medium.tymed != ComTYMED.TYMED_HGLOBAL || medium.unionmember == IntPtr.Zero)
                    return saved;

                IntPtr hGlobal = medium.unionmember;
                IntPtr ptr = GlobalLock(hGlobal);
                if (ptr == IntPtr.Zero)
                    return saved;

                try
                {
                    int size = GlobalSize(hGlobal);
                    if (size < 4)
                        return saved;

                    int cItems = Marshal.ReadInt32(ptr);
                    int descriptorSize = Marshal.SizeOf<FILEDESCRIPTORW>();
                    IntPtr descPtr = IntPtr.Add(ptr, 4);

                    for (int i = 0; i < cItems; i++)
                    {
                        var fd = Marshal.PtrToStructure<FILEDESCRIPTORW>(descPtr);
                        descPtr = IntPtr.Add(descPtr, descriptorSize);

                        var fileName = string.IsNullOrWhiteSpace(fd.cFileName) ? $"attachment_{i + 1}" : fd.cFileName;
                        foreach (var c in Path.GetInvalidFileNameChars())
                            fileName = fileName.Replace(c, '_');

                        // Request FILECONTENTS for this index as IStream
                        var fmtContents = new ComFORMATETC
                        {
                            cfFormat = (short)cfContents,
                            ptd = IntPtr.Zero,
                            dwAspect = ComDVASPECT.DVASPECT_CONTENT,
                            lindex = i,
                            tymed = ComTYMED.TYMED_ISTREAM
                        };

                        ComSTGMEDIUM mediumContents;
                        bool gotStream = false;
                        try
                        {
                            comData.GetData(ref fmtContents, out mediumContents);
                            gotStream = true;
                        }
                        catch (COMException)
                        {
                            // Try HGLOBAL fallback
                            var fmtHGlobal = fmtContents;
                            fmtHGlobal.tymed = ComTYMED.TYMED_HGLOBAL;
                            try
                            {
                                comData.GetData(ref fmtHGlobal, out mediumContents);
                                if (mediumContents.tymed == ComTYMED.TYMED_HGLOBAL && mediumContents.unionmember != IntPtr.Zero)
                                {
                                    var savedPath = SaveHGlobalToCache(mediumContents.unionmember, fileName);
                                    if (!string.IsNullOrEmpty(savedPath))
                                        saved.Add(savedPath);
                                    ReleaseStgMedium(ref mediumContents);
                                    continue;
                                }
                                ReleaseStgMedium(ref mediumContents);
                                continue;
                            }
                            catch (COMException)
                            {
                                continue;
                            }
                        }

                        if (gotStream)
                        {
                            try
                            {
                                if (mediumContents.tymed != ComTYMED.TYMED_ISTREAM || mediumContents.unionmember == IntPtr.Zero)
                                    continue;

                                var comStreamObj = Marshal.GetObjectForIUnknown(mediumContents.unionmember) as ComIStream;
                                if (comStreamObj == null)
                                    continue;

                                var savedPath = SaveComStreamToCache(comStreamObj, fileName);
                                if (!string.IsNullOrEmpty(savedPath))
                                    saved.Add(savedPath);
                            }
                            finally
                            {
                                ReleaseStgMedium(ref mediumContents);
                            }
                        }
                    }
                }
                finally
                {
                    GlobalUnlock(hGlobal);
                }
            }
            finally
            {
                ReleaseStgMedium(ref medium);
            }

            return saved;
        }

        private static string SaveComStreamToCache(ComIStream comStream, string suggestedFileName)
        {
            try
            {
                var cacheDir = CacheService.GetCacheDirectory();
                var targetPath = Path.Combine(cacheDir, suggestedFileName);

                if (File.Exists(targetPath))
                {
                    var name = Path.GetFileNameWithoutExtension(suggestedFileName);
                    var ext = Path.GetExtension(suggestedFileName);
                    int i = 1;
                    do
                    {
                        targetPath = Path.Combine(cacheDir, $"{name} ({i}){ext}");
                        i++;
                    } while (File.Exists(targetPath));
                }

                using (var fs = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[64 * 1024];
                    while (true)
                    {
                        IntPtr bytesReadPtr = Marshal.AllocHGlobal(sizeof(int));
                        try
                        {
                            comStream.Read(buffer, buffer.Length, bytesReadPtr);
                            int read = Marshal.ReadInt32(bytesReadPtr);
                            if (read <= 0)
                                break;
                            fs.Write(buffer, 0, read);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(bytesReadPtr);
                        }
                    }
                }

                return targetPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed saving COM stream to cache: {ex.Message}");
                return string.Empty;
            }
        }

        private static string SaveHGlobalToCache(IntPtr hGlobal, string suggestedFileName)
        {
            try
            {
                IntPtr ptr = GlobalLock(hGlobal);
                if (ptr == IntPtr.Zero)
                    return string.Empty;

                try
                {
                    int size = GlobalSize(hGlobal);
                    if (size <= 0)
                        return string.Empty;

                    var cacheDir = CacheService.GetCacheDirectory();
                    var targetPath = Path.Combine(cacheDir, suggestedFileName);

                    if (File.Exists(targetPath))
                    {
                        var name = Path.GetFileNameWithoutExtension(suggestedFileName);
                        var ext = Path.GetExtension(suggestedFileName);
                        int i = 1;
                        do
                        {
                            targetPath = Path.Combine(cacheDir, $"{name} ({i}){ext}");
                            i++;
                        } while (File.Exists(targetPath));
                    }

                    unsafe
                    {
                        using (var fs = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[64 * 1024];
                            int remaining = size;
                            int offset = 0;
                            while (remaining > 0)
                            {
                                int toCopy = Math.Min(buffer.Length, remaining);
                                Marshal.Copy(IntPtr.Add(ptr, offset), buffer, 0, toCopy);
                                fs.Write(buffer, 0, toCopy);
                                remaining -= toCopy;
                                offset += toCopy;
                            }
                        }
                    }

                    return targetPath;
                }
                finally
                {
                    GlobalUnlock(hGlobal);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed saving HGLOBAL to cache: {ex.Message}");
                return string.Empty;
            }
        }

        public static bool ContainsDownloadableUrl(IDataObject data)
        {
            try
            {
                return data.GetDataPresent(UriList, false) ||
                       data.GetDataPresent(UrlW, false) ||
                       data.GetDataPresent(UrlA, false) ||
                       data.GetDataPresent(DownloadUrl, false) ||
                       data.GetDataPresent(MozUrl, false) ||
                       data.GetDataPresent(DataFormats.UnicodeText, false) ||
                       data.GetDataPresent(DataFormats.Text, false) ||
                       data.GetDataPresent(DataFormats.Html, false);
            }
            catch
            {
                return false;
            }
        }

        public static async System.Threading.Tasks.Task<List<string>> TrySaveFromUrlsToCacheAsync(IDataObject data)
        {
            var savedFiles = new List<string>();
            try
            {
                var urls = ExtractUrls(data);
                if (urls.Count == 0)
                    return savedFiles;

                using var http = new HttpClient(new HttpClientHandler
                {
                    UseCookies = true,
                    AllowAutoRedirect = true
                });

                foreach (var url in urls)
                {
                    try
                    {
                        using var response = await http.GetAsync(url);
                        if (!response.IsSuccessStatusCode)
                        {
                            System.Diagnostics.Debug.WriteLine($"Download failed ({(int)response.StatusCode}): {url}");
                            continue;
                        }

                        var fileName = TryGetFileNameFromResponse(response) ?? GetFileNameFromUrl(url) ?? "download";
                        await using var stream = await response.Content.ReadAsStreamAsync();
                        var saved = SaveStreamToCache(stream, fileName);
                        if (!string.IsNullOrEmpty(saved))
                            savedFiles.Add(saved);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"URL download failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"URL extraction/download failed: {ex.Message}");
            }

            return savedFiles;
        }

        private static List<string> ExtractUrls(IDataObject data)
        {
            var urls = new List<string>();

            try
            {
                // Chromium: DownloadURL format is "mimeType:url:fileName"
                if (data.GetDataPresent(DownloadUrl, false))
                {
                    var txt = GetStringFromData(data, DownloadUrl);
                    if (!string.IsNullOrWhiteSpace(txt))
                    {
                        foreach (var line in txt.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            var parts = line.Split(':');
                            if (parts.Length >= 2)
                            {
                                var url = line.Substring(line.IndexOf(':') + 1);
                                // After first ':' there may be another ':' before filename; try to get until before last ':'
                                var lastColon = url.LastIndexOf(':');
                                if (lastColon > 0)
                                {
                                    url = url.Substring(0, lastColon);
                                }
                                if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                                    urls.Add(url);
                            }
                        }
                    }
                }

                // Firefox: text/x-moz-url is lines of URL and title alternating
                if (urls.Count == 0 && data.GetDataPresent(MozUrl, false))
                {
                    var txt = GetStringFromData(data, MozUrl);
                    if (!string.IsNullOrWhiteSpace(txt))
                    {
                        var lines = txt.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < lines.Length; i += 2)
                        {
                            var url = lines[i].Trim();
                            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
                                urls.Add(url);
                        }
                    }
                }

                if (data.GetDataPresent(UriList, false))
                {
                    var txt = GetStringFromData(data, UriList);
                    if (!string.IsNullOrWhiteSpace(txt))
                    {
                        foreach (var line in txt.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (!line.StartsWith("#"))
                                urls.Add(line.Trim());
                        }
                    }
                }

                if (urls.Count == 0 && (data.GetDataPresent(UrlW, false) || data.GetDataPresent(UrlA, false)))
                {
                    var txt = GetStringFromData(data, data.GetDataPresent(UrlW, false) ? UrlW : UrlA);
                    if (!string.IsNullOrWhiteSpace(txt))
                        urls.Add(txt.Trim());
                }

                if (urls.Count == 0 && (data.GetDataPresent(DataFormats.UnicodeText, false) || data.GetDataPresent(DataFormats.Text, false)))
                {
                    var txt = GetStringFromData(data, data.GetDataPresent(DataFormats.UnicodeText, false) ? DataFormats.UnicodeText : DataFormats.Text);
                    if (Uri.IsWellFormedUriString(txt?.Trim(), UriKind.Absolute))
                        urls.Add(txt!.Trim());
                }

                if (urls.Count == 0 && data.GetDataPresent(DataFormats.Html, false))
                {
                    var html = GetStringFromData(data, DataFormats.Html);
                    var href = TryExtractHref(html);
                    if (!string.IsNullOrWhiteSpace(href))
                        urls.Add(href!);
                }
            }
            catch
            {
                // ignore
            }

            return urls;
        }

        private static string? GetStringFromData(IDataObject data, string format)
        {
            try
            {
                var obj = data.GetData(format, false);
                switch (obj)
                {
                    case string s:
                        return s;
                    case MemoryStream ms:
                        using (ms)
                        using (var reader = new StreamReader(ms))
                            return reader.ReadToEnd();
                    case Stream stream:
                        using (stream)
                        using (var reader = new StreamReader(stream))
                            return reader.ReadToEnd();
                    default:
                        return obj?.ToString();
                }
            }
            catch
            {
                return null;
            }
        }

        private static string? TryGetFileNameFromResponse(HttpResponseMessage response)
        {
            try
            {
                var cd = response.Content.Headers.ContentDisposition;
                var fileName = cd?.FileNameStar ?? cd?.FileName;
                if (string.IsNullOrWhiteSpace(fileName))
                    return null;
                return fileName.Trim('"');
            }
            catch
            {
                return null;
            }
        }

        private static string? GetFileNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var name = Path.GetFileName(uri.LocalPath);
                if (string.IsNullOrWhiteSpace(name))
                    return null;
                return name;
            }
            catch
            {
                return null;
            }
        }

        private static string? TryExtractHref(string? html)
        {
            if (string.IsNullOrEmpty(html))
                return null;

            try
            {
                // Very light-weight href extraction
                var marker = "href=\"";
                var idx = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    var start = idx + marker.Length;
                    var end = html.IndexOf('"', start);
                    if (end > start)
                        return html.Substring(start, end - start);
                }

                // Some HTML clipboard formats include a "SourceURL:" header line
                var sourceMarker = "SourceURL:";
                var lineIdx = html.IndexOf(sourceMarker, StringComparison.OrdinalIgnoreCase);
                if (lineIdx >= 0)
                {
                    var lineEnd = html.IndexOf("\r\n", lineIdx);
                    var value = html.Substring(lineIdx + sourceMarker.Length, (lineEnd > lineIdx ? lineEnd : html.Length) - (lineIdx + sourceMarker.Length));
                    var cleaned = value.Trim();
                    if (Uri.IsWellFormedUriString(cleaned, UriKind.Absolute))
                        return cleaned;
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }

        private static string ReadFileDescriptorName(byte[] buffer, int offset)
        {
            // Marshal FILEDESCRIPTORW from the buffer to get the file name
            string fileName = "attachment";
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                IntPtr basePtr = handle.AddrOfPinnedObject();
                IntPtr descPtr = IntPtr.Add(basePtr, offset);
                var fd = Marshal.PtrToStructure<FILEDESCRIPTORW>(descPtr);
                if (!string.IsNullOrWhiteSpace(fd.cFileName))
                {
                    fileName = fd.cFileName;
                }
            }
            finally
            {
                handle.Free();
            }

            // Sanitize filename
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }

            return fileName;
        }

        private static string SaveStreamToCache(Stream source, string suggestedFileName)
        {
            try
            {
                var cacheDir = CacheService.GetCacheDirectory();
                var targetPath = Path.Combine(cacheDir, suggestedFileName);

                // Ensure unique name
                if (File.Exists(targetPath))
                {
                    var name = Path.GetFileNameWithoutExtension(suggestedFileName);
                    var ext = Path.GetExtension(suggestedFileName);
                    int i = 1;
                    do
                    {
                        targetPath = Path.Combine(cacheDir, $"{name} ({i}){ext}");
                        i++;
                    } while (File.Exists(targetPath));
                }

                using (source)
                using (var fs = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    source.CopyTo(fs);
                }

                return targetPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed saving stream to cache: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
