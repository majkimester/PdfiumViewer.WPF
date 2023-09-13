﻿using PdfiumViewer.Enums;
using System;

namespace PdfiumViewer
{
    public partial class ScrollPanel
    {
        /// <summary>
        /// Zooms the PDF document in one step.
        /// </summary>
        public void ZoomIn()
        {
            SetZoom(Zoom * ZoomFactor);
        }

        /// <summary>
        /// Zooms the PDF document out one step.
        /// </summary>
        public void ZoomOut()
        {
            SetZoom(Zoom / ZoomFactor);
        }

        public void SetZoom(double zoom)
        {
            var newZoom = Math.Min(Math.Max(zoom, ZoomMin), ZoomMax);
            if (newZoom != Zoom)
            {
                Zoom = newZoom;
                ZoomMode = PdfViewerZoomMode.None;
                OnPagesDisplayModeChanged();
            }
        }

        public void SetZoomMode(PdfViewerZoomMode mode)
        {
            if (ZoomMode != mode)
            {
                ZoomMode = mode;
                OnPagesDisplayModeChanged();
            }
        }
    }
}
