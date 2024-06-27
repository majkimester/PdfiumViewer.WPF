﻿using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PdfiumViewer.Core
{
    internal static partial class NativeMethods
    {
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

        private static bool TryLoadNativeLibrary(string path)
        {
            if (path == null)
                return false;

            path = Path.Combine(path, IntPtr.Size == 4 ? "x86" : "x64");
            path = Path.Combine(path, "Pdfium.dll");

            return File.Exists(path) && LoadLibrary(path) != IntPtr.Zero;
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
