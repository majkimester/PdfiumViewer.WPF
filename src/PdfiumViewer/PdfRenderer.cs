using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using PdfiumViewer.Core;
using PdfiumViewer.Drawing;
using PdfiumViewer.Enums;
using Size = System.Drawing.Size;
using SizeF = System.Drawing.SizeF;
using PointF = System.Drawing.PointF;
using System.Windows.Documents;

namespace PdfiumViewer
{
    public partial class PdfRenderer : ScrollPanel
    {
        public PdfRenderer()
        {
            IsTabStop = true;
            Markers = new PdfMarkerCollection();
        }

        /// <summary>
        /// Gets a collection with all markers.
        /// </summary>
        public PdfMarkerCollection Markers { get; }
        private Dictionary<int, List<IPdfMarker>> _markersByPage;

        public void OpenPdf(string path, bool isRightToLeft = false)
        {
            UnLoad();
            IsRightToLeft = isRightToLeft;
            Document = PdfDocument.Load(path);
            OnPagesDisplayModeChanged();
            GotoPage(0, true);
        }

        public void OpenPdf(string path, string password, bool isRightToLeft = false)
        {
            UnLoad();
            IsRightToLeft = isRightToLeft;
            Document = PdfDocument.Load(path, password);
            OnPagesDisplayModeChanged();
            GotoPage(0, true);
        }

        public void OpenPdf(Stream stream, bool isRightToLeft = false, int pageNumber = 0)
        {
            UnLoad();
            IsRightToLeft = isRightToLeft;
            Document = PdfDocument.Load(stream);
            OnPagesDisplayModeChanged();
            GotoPage(pageNumber, true);
        }

        public void OpenPdf(Stream stream, string password, bool isRightToLeft = false)
        {
            UnLoad();
            IsRightToLeft = isRightToLeft;
            Document = PdfDocument.Load(stream, password);
            OnPagesDisplayModeChanged();
            GotoPage(0, true);
        }

        public void UnLoad()
        {
            PageNo = 0;
            Document?.Dispose();
            Document = null;
            Frames = null;
            Markers.Clear();
            _markersByPage = null;
            Panel.Children.Clear();
            Rotate = PdfRotation.Rotate0;
            ZoomFactor = DefaultZoomFactor;
            Zoom = DefaultZoom;
            GC.Collect();
        }

        public void ClockwiseRotate()
        {
            // _____
            //      |
            //      |
            //      v
            // Clockwise

            switch (Rotate)
            {
                case PdfRotation.Rotate0:
                    RotatePage(PageNo, PdfRotation.Rotate90);
                    break;
                case PdfRotation.Rotate90:
                    RotatePage(PageNo, PdfRotation.Rotate180);
                    break;
                case PdfRotation.Rotate180:
                    RotatePage(PageNo, PdfRotation.Rotate270);
                    break;
                case PdfRotation.Rotate270:
                    RotatePage(PageNo, PdfRotation.Rotate0);
                    break;
            }
        }

        public void Counterclockwise()
        {
            //      ^
            //      |
            //      |
            // _____|
            // Counterclockwise

            switch (Rotate)
            {
                case PdfRotation.Rotate0:
                    RotatePage(PageNo, PdfRotation.Rotate270);
                    break;
                case PdfRotation.Rotate90:
                    RotatePage(PageNo, PdfRotation.Rotate0);
                    break;
                case PdfRotation.Rotate180:
                    RotatePage(PageNo, PdfRotation.Rotate90);
                    break;
                case PdfRotation.Rotate270:
                    RotatePage(PageNo, PdfRotation.Rotate180);
                    break;
            }
        }

        /// <summary>
        /// Scroll the PDF bounds into view.
        /// </summary>
        /// <param name="bounds">The PDF bounds to scroll into view.</param>
        public void ScrollIntoView(PdfRectangle bounds)
        {
            Rect? bound = BoundsFromPdf(bounds);
            if (bound != null)
            {
                ScrollIntoView(bounds.Page, bound.Value);
            }
        }

        /// <summary>
        /// Scroll the client rectangle into view.
        /// </summary>
        /// <param name="page">Page number</param>
        /// <param name="rectangle">The client rectangle to scroll into view.</param>
        public void ScrollIntoView(int page, Rect rectangle)
        {
            var clientArea = GetScrollClientArea();
            if (ScrollableWidth > 0)
            {
                double horizontalOffset = rectangle.X - clientArea.Width / 2;
                if (PagesDisplayMode == PdfViewerPagesDisplayMode.BookMode)
                {
                    if ((!IsRightToLeft && page == PageNoLast) || (IsRightToLeft && page == PageNo))
                    {
                        horizontalOffset += Frame1.Width + FrameSpace.Left + FrameSpace.Right;
                    }
                }
                ScrollToHorizontalOffset(horizontalOffset);
            }

            if (ScrollableHeight > 0 && ZoomMode != PdfViewerZoomMode.FitHeight)
            {
                double verticalOffset = GetPageVerticalOffset(page);
                verticalOffset += rectangle.Y - clientArea.Height / 2;
                ScrollToVerticalOffset(verticalOffset);
            }
        }

        /// <summary>
        /// Converts PDF bounds to client bounds.
        /// </summary>
        /// <param name="bounds">The PDF bounds to convert.</param>
        /// <returns>The bounds of the PDF bounds in client coordinates.</returns>
        public Rect? BoundsFromPdf(PdfRectangle bounds)
        {
            return BoundsFromPdf(bounds, true);
        }

        private Rect? BoundsFromPdf(PdfRectangle bounds, bool translateOffset)
        {
            PdfImage frame;
            if (PagesDisplayMode == PdfViewerPagesDisplayMode.ContinuousMode)
            {
                frame = Frames[bounds.Page];
            }
            else
            {
                frame = Frame1;
                if (frame?.PageNo != bounds.Page)
                {
                    frame = Frame2;
                }
            }
            if (frame == null) return null;

            var pageBoundsSize = new Size((int)frame.Width, (int)frame.Height);

            var pageSize = Document.PageSizes[bounds.Page];

            var translated = Document.RectangleFromPdf(
                bounds.Page,
                bounds.Bounds
            );

            var topLeft = TranslatePointFromPdf(pageBoundsSize, pageSize, new PointF(translated.Left, translated.Top));
            var bottomRight = TranslatePointFromPdf(pageBoundsSize, pageSize, new PointF(translated.Right, translated.Bottom));

            return new Rect(
                Math.Min(topLeft.X, bottomRight.X),
                Math.Min(topLeft.Y, bottomRight.Y),
                Math.Abs(bottomRight.X - topLeft.X),
                Math.Abs(bottomRight.Y - topLeft.Y)
            );
        }

        private PointF TranslatePointToPdf(Size size, SizeF pageSize, Point point)
        {
            switch (Rotate)
            {
                case PdfRotation.Rotate90:
                    point = new Point(size.Height - point.Y, point.X);
                    size = new Size(size.Height, size.Width);
                    break;
                case PdfRotation.Rotate180:
                    point = new Point(size.Width - point.X, size.Height - point.Y);
                    break;
                case PdfRotation.Rotate270:
                    point = new Point(point.Y, size.Width - point.X);
                    size = new Size(size.Height, size.Width);
                    break;
            }

            return new PointF(
                ((float)point.X / size.Width) * pageSize.Width,
                ((float)point.Y / size.Height) * pageSize.Height
            );
        }

        private Point TranslatePointFromPdf(Size size, SizeF pageSize, PointF point)
        {
            switch (Rotate)
            {
                case PdfRotation.Rotate90:
                    point = new PointF(pageSize.Height - point.Y, point.X);
                    pageSize = new SizeF(pageSize.Height, pageSize.Width);
                    break;
                case PdfRotation.Rotate180:
                    point = new PointF(pageSize.Width - point.X, pageSize.Height - point.Y);
                    break;
                case PdfRotation.Rotate270:
                    point = new PointF(point.Y, pageSize.Width - point.X);
                    pageSize = new SizeF(pageSize.Height, pageSize.Width);
                    break;
            }

            return new Point(
                (int)((point.X / pageSize.Width) * size.Width),
                (int)((point.Y / pageSize.Height) * size.Height)
            );
        }

        private Size GetScrollOffset()
        {
            var bounds = GetScrollClientArea();
            // int maxWidth = (int)(_maxWidth * _scaleFactor) + ShadeBorder.Size.Horizontal + PageMargin.Horizontal;
            // int leftOffset = (HScroll ? DisplayRectangle.X : (bounds.Width - maxWidth) / 2) + maxWidth / 2;
            // int topOffset = VScroll ? DisplayRectangle.Y : 0;
            //
            // return new Size(leftOffset, topOffset);
            return new Size((int)bounds.Width, (int)bounds.Height);
        }

        private Rect GetScrollClientArea()
        {
            return new Rect(0, 0, (int)ViewportWidth, (int)ViewportHeight);
        }

        public void EnsureMarkers()
        {
            if (_markersByPage != null)
                return;

            _markersByPage = new Dictionary<int, List<IPdfMarker>>();

            foreach (var marker in Markers)
            {
                if (marker.Page < 0 || marker.Page >= PageCount)
                    continue;

                List<IPdfMarker> pageMarkers;
                _markersByPage.TryGetValue(marker.Page, out pageMarkers);
                if (pageMarkers == null)
                {
                    pageMarkers = new List<IPdfMarker>();
                    _markersByPage.Add(marker.Page, pageMarkers);
                }
                pageMarkers.Add(marker);
            }
        }

        public void DrawMarkers(DrawingContext graphics, int page)
        {
            List<IPdfMarker> pageMarkers = null;
            _markersByPage?.TryGetValue(page, out pageMarkers);
            if (pageMarkers == null) return;
            foreach (var marker in pageMarkers)
            {
                marker.Draw(this, graphics);
            }
        }

        public void UpdateAdorner()
        {
            AdornerLayer layer = AdornerLayer.GetAdornerLayer(this);
            layer?.Update();
        }

        public void RedrawMarkers()
        {
            _markersByPage = null;
            UpdateAdorner();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                base.Dispose(true);
                if (!Dispatcher.HasShutdownStarted)
                {
                    Dispatcher.Invoke(UnLoad);
                }
                GC.SuppressFinalize(this);
                GC.Collect();
            }
        }

        ~PdfRenderer() => Dispose(true);
    }
}
