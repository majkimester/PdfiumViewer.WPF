﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Image = System.Windows.Controls.Image;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace PdfiumViewer
{
    public class PdfRenderer : ScrollViewer, IPdfDocument, INotifyPropertyChanged
    {
        public PdfRenderer()
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            Effect = new DropShadowEffect()
            {
                BlurRadius = 10,
                Direction = 270,
                RenderingBias = RenderingBias.Performance,
                ShadowDepth = 0
            };
            Frames = new List<Image>();
            Panel = new StackPanel()
            {
                
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Content = Panel;

            ZoomMode = PdfViewerZoomMode.FitHeight;
            PagesDisplayMode = PdfViewerPagesDisplayMode.SinglePageMode;
            Dpi = 96;
            ScrollWidth = 50;
            FrameSpace = new Thickness(5);
        }

        protected Process CurrentProcess { get; } = Process.GetCurrentProcess();
        protected PdfDocument Document { get; set; }
        protected StackPanel Panel { get; set; }
        protected Thickness FrameSpace { get; set; }
        protected Image Frame1 => Frames?.FirstOrDefault();
        protected Image Frame2 => Frames?.Count > 1 ? Frames[1] : null;
        protected List<Image> Frames { get; set; }
        protected int ScrollWidth { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public int PageNo { get; set; }
        public int Dpi { get; set; }
        public PdfViewerZoomMode ZoomMode { get; set; }
        public PdfViewerPagesDisplayMode PagesDisplayMode { get; set; }
        public bool IsDocumentLoaded => Document != null;


        /// <summary>
        /// Note: called by `PropertyChanged.Fody` when PageNo changed
        /// </summary>
        protected void OnPageNoChanged()
        {
            GotoPage(PageNo);
        }
        /// <summary>
        /// Note: called by `PropertyChanged.Fody` when PagesDisplayMode changed
        /// </summary>
        protected void OnPagesDisplayModeChanged()
        {
            Panel.Children.Clear();
            Frames.Clear();

            if (PagesDisplayMode == PdfViewerPagesDisplayMode.SinglePageMode)
            {
                Frames.Add(new Image() { Margin = FrameSpace });

                Panel.Orientation = Orientation.Horizontal;
            }
            else if (PagesDisplayMode == PdfViewerPagesDisplayMode.BookMode)
            {
                Frames.Add(new Image() { Margin = FrameSpace }); // frame1
                Frames.Add(new Image() { Margin = FrameSpace }); // frame2

                Panel.Orientation = Orientation.Horizontal;
            }
            else if (IsDocumentLoaded == false)
            {
                // frames created at scrolling
                Frames.AddRange(new Image[Document.PageCount]);

                Panel.Orientation = Orientation.Vertical;
            }

            foreach (var frame in Frames)
            {
                if (frame != null)
                    Panel.Children.Add(frame);
            }

            GotoPage(PageNo);
        }
        /// <summary>
        /// Note: called by `PropertyChanged.Fody` when ZoomMode changed
        /// </summary>
        protected void OnZoomModeChanged()
        {
            GotoPage(PageNo);
        }
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            GotoPage(PageNo);
        }
        protected BitmapSource RenderPageToMemory(int page, double width, double height)
        {
            var image = Document.Render(page, (int)width, (int)height, Dpi, Dpi, false);
            var bs = image.ToBitmapSource();
            CurrentProcess?.Refresh();
            GC.Collect();
            return bs;
        }
        public void GotoPage(int page)
        {
            var containerWidth = ActualWidth; // ViewportWidth
            var containerHeight = ActualHeight; // ViewportHeight

            if (IsDocumentLoaded)
            {
                var currentPageSize = Document.PageSizes[page];
                var whRatio = currentPageSize.Width / currentPageSize.Height;

                var height = containerHeight;
                var width = whRatio * height;

                if (ZoomMode == PdfViewerZoomMode.FitWidth)
                {
                    width = containerWidth - ScrollWidth;
                    if (PagesDisplayMode == PdfViewerPagesDisplayMode.BookMode)
                        width /= 2;
                    height = (int)(1 / whRatio * width);
                }

                Dispatcher.Invoke(() =>
                {

                    Frame1.Source = RenderPageToMemory(page, width, height);
                    if (PagesDisplayMode == PdfViewerPagesDisplayMode.BookMode &&
                        page + 1 < Document.PageCount)
                        Frame2.Source = RenderPageToMemory(page + 1, width, height);
                });
            }
        }
        public void OpenPdf(string path)
        {
            Document = PdfDocument.Load(path);
            GotoPage(PageNo = 0);
        }
        public void OpenPdf(string path, string password)
        {
            Document = PdfDocument.Load(path, password);
            GotoPage(PageNo = 0);
        }
        public void OpenPdf(IWin32Window owner, string path)
        {
            Document = PdfDocument.Load(owner, path);
            GotoPage(PageNo = 0);
        }
        public void OpenPdf(IWin32Window owner, Stream stream)
        {
            Document = PdfDocument.Load(owner, stream);
            GotoPage(PageNo = 0);
        }
        public void OpenPdf(IWin32Window owner, Stream stream, string password)
        {
            Document = PdfDocument.Load(owner, stream, password);
            GotoPage(PageNo = 0);
        }
        public void OpenPdf(Stream stream)
        {
            Document = PdfDocument.Load(stream);
            GotoPage(PageNo = 0);
        }
        public void OpenPdf(Stream stream, string password)
        {
            Document = PdfDocument.Load(stream, password);
            GotoPage(PageNo = 0);
        }



        #region IPdfDocument implementation

        public int PageCount => Document?.PageCount ?? 0;
        public PdfBookmarkCollection Bookmarks => Document?.Bookmarks;
        public IList<SizeF> PageSizes => Document?.PageSizes;

        public void Render(int page, Graphics graphics, float dpiX, float dpiY, Rectangle bounds, bool forPrinting)
        {
            Document.Render(page, graphics, dpiX, dpiY, bounds, forPrinting);
        }

        public void Render(int page, Graphics graphics, float dpiX, float dpiY, Rectangle bounds, PdfRenderFlags flags)
        {
            Document.Render(page, graphics, dpiX, dpiY, bounds, flags);
        }

        public System.Drawing.Image Render(int page, float dpiX, float dpiY, bool forPrinting)
        {
            return Document.Render(page, dpiX, dpiY, forPrinting);
        }

        public System.Drawing.Image Render(int page, float dpiX, float dpiY, PdfRenderFlags flags)
        {
            return Document.Render(page, dpiX, dpiY, flags);
        }

        public System.Drawing.Image Render(int page, int width, int height, float dpiX, float dpiY, bool forPrinting)
        {
            return Document.Render(page, width, height, dpiX, dpiY, forPrinting);
        }

        public System.Drawing.Image Render(int page, int width, int height, float dpiX, float dpiY, PdfRenderFlags flags)
        {
            return Document.Render(page, width, height, dpiX, dpiY, flags);
        }

        public System.Drawing.Image Render(int page, int width, int height, float dpiX, float dpiY, PdfRotation rotate, PdfRenderFlags flags)
        {
            return Document.Render(page, width, height, dpiX, dpiY, rotate, flags);
        }

        public void Save(string path)
        {
            Document.Save(path);
        }

        public void Save(Stream stream)
        {
            Document.Save(stream);
        }

        public PdfMatches Search(string text, bool matchCase, bool wholeWord)
        {
            return Document.Search(text, matchCase, wholeWord);
        }

        public PdfMatches Search(string text, bool matchCase, bool wholeWord, int page)
        {
            return Document.Search(text, matchCase, wholeWord, page);
        }

        public PdfMatches Search(string text, bool matchCase, bool wholeWord, int startPage, int endPage)
        {
            return Document.Search(text, matchCase, wholeWord, startPage, endPage);
        }

        public PrintDocument CreatePrintDocument()
        {
            return Document.CreatePrintDocument();
        }

        public PrintDocument CreatePrintDocument(PdfPrintMode printMode)
        {
            return Document.CreatePrintDocument(printMode);
        }

        public PrintDocument CreatePrintDocument(PdfPrintSettings settings)
        {
            return Document.CreatePrintDocument(settings);
        }

        public PdfPageLinks GetPageLinks(int page, Size size)
        {
            return Document.GetPageLinks(page, size);
        }

        public void DeletePage(int page)
        {
            Document.DeletePage(page);
        }

        public void RotatePage(int page, PdfRotation rotation)
        {
            Document.RotatePage(page, rotation);
        }

        public PdfInformation GetInformation()
        {
            return Document.GetInformation();
        }

        public string GetPdfText(int page)
        {
            return Document.GetPdfText(page);
        }

        public string GetPdfText(PdfTextSpan textSpan)
        {
            return Document.GetPdfText(textSpan);
        }

        public IList<PdfRectangle> GetTextBounds(PdfTextSpan textSpan)
        {
            return Document.GetTextBounds(textSpan);
        }

        public PointF PointToPdf(int page, Point point)
        {
            return Document.PointToPdf(page, point);
        }

        public Point PointFromPdf(int page, PointF point)
        {
            return Document.PointFromPdf(page, point);
        }

        public RectangleF RectangleToPdf(int page, Rectangle rect)
        {
            return Document.RectangleToPdf(page, rect);
        }

        public Rectangle RectangleFromPdf(int page, RectangleF rect)
        {
            return Document.RectangleFromPdf(page, rect);
        }

        #endregion

        public void Dispose()
        {
            Document?.Dispose();
        }
    }
}
