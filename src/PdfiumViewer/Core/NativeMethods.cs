using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PdfiumViewer.Core
{
    internal static partial class NativeMethods
    {
        private const string LIBPDFIUM = "pdfium.dll";

        public const int GmAdvanced = 2;
        public const uint MwtLeftMultiply = 2;

        static NativeMethods()
        {
            // First try the custom resolving mechanism.

            string fileName = PdfiumResolver.GetPdfiumFileName();
            if (fileName != null && File.Exists(fileName) && LoadLibrary(fileName) != IntPtr.Zero)
                return;

            // Load the platform dependent Pdfium.dll if it exists.

            if (!TryLoadNativeLibrary(AppDomain.CurrentDomain.RelativeSearchPath))
                TryLoadNativeLibrary(Path.GetDirectoryName(typeof(NativeMethods).Assembly.Location));
        }

        private static bool TryLoadNativeLibrary(string basePath)
        {
            if (string.IsNullOrEmpty(basePath))
                return false;

            string archFolder = GetArchitectureFolder();

            string fullPath = Path.Combine(basePath, archFolder, LIBPDFIUM);

            if (!File.Exists(fullPath))
                return false;

            IntPtr handle = LoadLibrary(fullPath);
            return handle != IntPtr.Zero;
        }

        private static string GetArchitectureFolder()
        {
            if (!Environment.Is64BitProcess)
                return "x86";
            else
                return IsArm64() ? "arm64" : "x64";
        }

        private static bool IsArm64()
        {
            SYSTEM_INFO info;
            GetNativeSystemInfo(out info);
            const ushort PROCESSOR_ARCHITECTURE_ARM64 = 12;
            return info.wProcessorArchitecture == PROCESSOR_ARCHITECTURE_ARM64;
        }

        [DllImport("kernel32")]
        private static extern void GetNativeSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_INFO
        {
            public ushort wProcessorArchitecture;
            public ushort wReserved;
            public uint dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public ushort wProcessorLevel;
            public ushort wProcessorRevision;
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPTStr)] string lpFileName);

        [DllImport("gdi32.dll")]
        public static extern int SetGraphicsMode(IntPtr hdc, int iMode);

        [StructLayout(LayoutKind.Sequential)]
        public struct XFORM
        {
            public float eM11;
            public float eM12;
            public float eM21;
            public float eM22;
            public float eDx;
            public float eDy;
        }

        [DllImport("gdi32.dll")]
        public static extern bool ModifyWorldTransform(IntPtr hdc, [In] ref XFORM lpXform, uint iMode);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("gdi32.dll")]
        public static extern bool SetViewportOrgEx(IntPtr hdc, int X, int Y, out POINT lpPoint);
    }
}
