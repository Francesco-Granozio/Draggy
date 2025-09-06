using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Draggy.Services
{
    public static class ShellThumbnail
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string pszPath, IntPtr pbc, [In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object ppv);

        static readonly Guid IID_IShellItem = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
        static Guid IID_IShellItemImageFactory = new Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b");

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
        private interface IShellItemImageFactory
        {
            void GetImage([In] SIZE size, uint flags, out IntPtr phbm);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE { public int cx, cy; }

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr hObject);

        [Flags]
        private enum SIIGBF : uint
        {
            THUMBNAILONLY = 0x00000001,
            ICONONLY = 0x00000004,
        }

        public static BitmapSource? GetThumbnail(string path, int size)
        {
            try
            {
                SHCreateItemFromParsingName(path, IntPtr.Zero, ref IID_IShellItemImageFactory, out var shellItemObj);
                if (shellItemObj is not IShellItemImageFactory factory)
                    return null;

                factory.GetImage(new SIZE { cx = size, cy = size }, 0, out IntPtr hBitmap);

                if (hBitmap == IntPtr.Zero)
                    return null;

                try
                {
                    var src = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(size, size));
                    src.Freeze(); // Per performance
                    return src;
                }
                finally
                {
                    DeleteObject(hBitmap);
                    Marshal.ReleaseComObject(factory);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore nel generare thumbnail per {path}: {ex.Message}");
                return null;
            }
        }

        public static BitmapSource? GetFileIcon(string path, int size)
        {
            try
            {
                SHCreateItemFromParsingName(path, IntPtr.Zero, ref IID_IShellItemImageFactory, out var shellItemObj);
                if (shellItemObj is not IShellItemImageFactory factory)
                    return null;

                // Richiedi esplicitamente l'icona del sistema, non la miniatura del contenuto
                factory.GetImage(new SIZE { cx = size, cy = size }, (uint)SIIGBF.ICONONLY, out IntPtr hBitmap);

                if (hBitmap == IntPtr.Zero)
                    return null;

                try
                {
                    var src = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(size, size));
                    src.Freeze();
                    return src;
                }
                finally
                {
                    DeleteObject(hBitmap);
                    Marshal.ReleaseComObject(factory);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore nel generare icona per {path}: {ex.Message}");
                return null;
            }
        }
    }
}
