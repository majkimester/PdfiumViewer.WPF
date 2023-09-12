﻿using Microsoft.Win32;
using PdfiumViewer.Core;
using PdfiumViewer.Demo.Annotations;
using PdfiumViewer.Drawing;
using PdfiumViewer.Enums;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace PdfiumViewer.Demo
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            this.OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            var version = GetType().Assembly.GetName().Version.ToString(3);
            Title = $"WPF PDFium Viewer Demo v{version}";
            CurrentProcess = Process.GetCurrentProcess();
            Cts = new CancellationTokenSource();
            DataContext = this;
            Renderer.PropertyChanged += delegate
            {
                OnPropertyChanged(nameof(Page));
                OnPropertyChanged(nameof(ZoomPercent));
            };

            MemoryChecker = new System.Windows.Threading.DispatcherTimer();
            MemoryChecker.Tick += OnMemoryChecker;
            MemoryChecker.Interval = new TimeSpan(0, 0, 1);
            MemoryChecker.Start();

            SearchManager = new PdfSearchManager(Renderer);
            MatchCaseCheckBox.IsChecked = SearchManager.MatchCase;
            WholeWordOnlyCheckBox.IsChecked = SearchManager.MatchWholeWord;
            HighlightAllMatchesCheckBox.IsChecked = SearchManager.HighlightAllMatches;
        }


        private Process CurrentProcess { get; }
        private CancellationTokenSource Cts { get; }
        private System.Windows.Threading.DispatcherTimer MemoryChecker { get; }
        private PdfSearchManager SearchManager { get; }

        public string InfoText { get => _infoText; protected set => SetProperty(ref _infoText, value); }
        private string _infoText;

        public string SearchTerm { get; set; }
        public PdfBookmarkCollection Bookmarks { get; set; }
        public bool ShowBookmarks { get; set; }
        public PdfBookmark SelectedBookIndex { get; set; }
        public double ZoomPercent
        {
            get => Renderer.Zoom * 100;
            set => Renderer.SetZoom(value / 100);
        }

        public bool IsSearchOpen { get => _isSearchOpen; set => SetProperty(ref _isSearchOpen, value); }
        private bool _isSearchOpen;

        public int SearchMatchItemNo { get; set; }
        public int SearchMatchesCount { get; set; }
        public int Page
        {
            get => Renderer.PageNo + 1;
            set => Renderer.GotoPage(Math.Min(Math.Max(value - 1, 0), Renderer.PageCount - 1));
        }
        public FlowDirection IsRtl
        {
            get => Renderer.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            set => Renderer.IsRightToLeft = value == FlowDirection.RightToLeft ? true : false;
        }


        private void OnMemoryChecker(object sender, EventArgs e)
        {
            CurrentProcess.Refresh();
            InfoText = $"Memory: {CurrentProcess.PrivateMemorySize64 / 1024 / 1024} MB";
        }


        private async void RenderToMemory(object sender, RoutedEventArgs e)
        {
            try
            {
                var pageStep = Renderer.PagesDisplayMode == PdfViewerPagesDisplayMode.BookMode ? 2 : 1;
                Dispatcher.Invoke(() => Renderer.GotoPage(0));
                while (Renderer.PageNo < Renderer.PageCount - pageStep)
                {
                    Dispatcher.Invoke(() => Renderer.NextPage());
                    await Task.Delay(1);
                }
            }
            catch (Exception ex)
            {
                Cts.Cancel();
                Debug.Fail(ex.Message);
            }
        }

        private void OpenPdf(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
                Title = "Open PDF File"
            };

            if (dialog.ShowDialog() == true)
            {
                _thumbnailFilename = dialog.FileName;
                Renderer.OpenPdf(new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            MemoryChecker?.Stop();
            Renderer?.Dispose();
            ThumbnailRenderer?.Dispose();
            _thumbnailFilename = null;
        }

        private void OnPrevPageClick(object sender, RoutedEventArgs e)
        {
            Renderer.PreviousPage();
        }

        private void OnNextPageClick(object sender, RoutedEventArgs e)
        {
            Renderer.NextPage();
        }

        private void OnFitWidth(object sender, RoutedEventArgs e)
        {
            Renderer.SetZoomMode(PdfViewerZoomMode.FitWidth);
        }

        private void OnFitHeight(object sender, RoutedEventArgs e)
        {
            Renderer.SetZoomMode(PdfViewerZoomMode.FitHeight);
        }

        private void OnZoomInClick(object sender, RoutedEventArgs e)
        {
            Renderer.ZoomIn();
        }

        private void OnZoomOutClick(object sender, RoutedEventArgs e)
        {
            Renderer.ZoomOut();
        }

        private void OnRotateLeftClick(object sender, RoutedEventArgs e)
        {
            Renderer.Counterclockwise();
        }

        private void OnRotateRightClick(object sender, RoutedEventArgs e)
        {
            Renderer.ClockwiseRotate();
        }

        private void OnInfo(object sender, RoutedEventArgs e)
        {
            var info = Renderer.GetInformation();
            if (info != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Author: {info.Author}");
                sb.AppendLine($"Creator: {info.Creator}");
                sb.AppendLine($"Keywords: {info.Keywords}");
                sb.AppendLine($"Producer: {info.Producer}");
                sb.AppendLine($"Subject: {info.Subject}");
                sb.AppendLine($"Title: {info.Title}");
                sb.AppendLine($"Create Date: {info.CreationDate}");
                sb.AppendLine($"Modified Date: {info.ModificationDate}");

                MessageBox.Show(sb.ToString(), "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OnGetText(object sender, RoutedEventArgs e)
        {
            var txtViewer = new TextViewer();
            var page = Renderer.PageNo;
            txtViewer.Body = Renderer.GetPdfText(page);
            txtViewer.Caption = $"Page {page + 1} contains {txtViewer.Body?.Length} character(s):";
            txtViewer.ShowDialog();
        }

        private void OnDisplayBookmarks(object sender, RoutedEventArgs e)
        {
            Bookmarks = Renderer.Bookmarks;
            if (Bookmarks?.Count > 0)
                ShowBookmarks = !ShowBookmarks;
        }

        private void OnContinuousModeClick(object sender, RoutedEventArgs e)
        {
            Renderer.PagesDisplayMode = PdfViewerPagesDisplayMode.ContinuousMode;
        }

        private void OnBookModeClick(object sender, RoutedEventArgs e)
        {
            Renderer.PagesDisplayMode = PdfViewerPagesDisplayMode.BookMode;
        }

        private void OnSinglePageModeClick(object sender, RoutedEventArgs e)
        {
            Renderer.PagesDisplayMode = PdfViewerPagesDisplayMode.SinglePageMode;
        }

        private void OnTransparent(object sender, RoutedEventArgs e)
        {
            if ((Renderer.Flags & PdfRenderFlags.Transparent) != 0)
            {
                Renderer.Flags &= ~PdfRenderFlags.Transparent;
            }
            else
            {
                Renderer.Flags |= PdfRenderFlags.Transparent;
            }
        }

        private void OpenCloseSearch(object sender, RoutedEventArgs e)
        {
            IsSearchOpen = !IsSearchOpen;
        }

        private void OnSearchTermKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Search();
            }
        }

        private void SaveAsImages(object sender, RoutedEventArgs e)
        {
            // Create a "Save As" dialog for selecting a directory (HACK)
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Select a Directory",
                Filter = "Directory|*.this.directory",
                FileName = "select"
            };
            // instead of default "Save As"
            // Prevents displaying files
            // Filename will then be "select.this.directory"
            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FileName;
                // Remove fake filename from resulting path
                path = path.Replace("\\select.this.directory", "");
                path = path.Replace(".this.directory", "");
                // If user has changed the filename, create the new directory
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                // Our final value is in path
                SaveAsImages(path);
            }
        }

        private void SaveAsImages(string path)
        {
            try
            {
                for (var i = 0; i < Renderer.PageCount; i++)
                {
                    var size = Renderer.Document.PageSizes[i];
                    var image = Renderer.Document.Render(i, (int)size.Width * 5, (int)size.Height * 5, 300, 300, false);
                    image.Save(Path.Combine(path, $"img{i}.png"));
                }
            }
            catch (Exception ex)
            {
                Cts.Cancel();
                Debug.Fail(ex.Message);
                MessageBox.Show(this, ex.Message, "Error!");
            }
        }

        private void Search()
        {
            SearchMatchItemNo = 0;
            SearchManager.MatchCase = MatchCaseCheckBox.IsChecked.GetValueOrDefault();
            SearchManager.MatchWholeWord = WholeWordOnlyCheckBox.IsChecked.GetValueOrDefault();
            SearchManager.HighlightAllMatches = HighlightAllMatchesCheckBox.IsChecked.GetValueOrDefault();
            SearchMatchesTextBlock.Visibility = Visibility.Visible;

            if (!SearchManager.Search(SearchTerm))
            {
                MessageBox.Show(this, "No matches found.");
            }
            else
            {
                SearchMatchesCount = SearchManager.MatchesCount;
                // DisplayTextSpan(SearchMatches.Items[SearchMatchItemNo++].TextSpan);
            }

            if (!SearchManager.FindNext(true))
                MessageBox.Show(this, "Find reached the starting point of the search.");
        }

        private void DisplayTextSpan(PdfTextSpan span)
        {
            Page = span.Page + 1;
            Renderer.ScrollToVerticalOffset(span.Offset);
        }

        private void OnNextFoundClick(object sender, RoutedEventArgs e)
        {
            if (SearchMatchesCount > SearchMatchItemNo)
            {
                SearchMatchItemNo++;
                //DisplayTextSpan(SearchMatches.Items[SearchMatchItemNo - 1].TextSpan);
                SearchManager.FindNext(true);
            }
        }

        private void OnPrevFoundClick(object sender, RoutedEventArgs e)
        {
            if (SearchMatchItemNo > 1)
            {
                SearchMatchItemNo--;
                // DisplayTextSpan(SearchMatches.Items[SearchMatchItemNo - 1].TextSpan);
                SearchManager.FindNext(false);
            }
        }

        private void ToRtlClick(object sender, RoutedEventArgs e)
        {
            Renderer.IsRightToLeft = true;
            OnPropertyChanged(nameof(IsRtl));
        }

        private void ToLtrClick(object sender, RoutedEventArgs e)
        {
            Renderer.IsRightToLeft = false;
            OnPropertyChanged(nameof(IsRtl));
        }

        private async void OnClosePdf(object sender, RoutedEventArgs e)
        {
            try
            {
                InfoBar.Foreground = System.Windows.Media.Brushes.Red;
                Renderer.UnLoad();
                await Task.Delay(5000);
                InfoBar.Foreground = System.Windows.Media.Brushes.Black;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
        private void EnableHandTools(object sender, RoutedEventArgs e)
        {
            var toggle = (ToggleButton)sender;
            Renderer.EnableKinetic = toggle.IsChecked == true;
        }

        /// <summary>
        /// Call when SelectedBookIndex changed.
        /// </summary>
        private void OnSelectedBookIndexChanged()
        {
            if (SelectedBookIndex != null)
                Renderer.GotoPage(SelectedBookIndex.PageIndex);
        }

        #region Thumbnail view

        private string _thumbnailFilename = null;
        private double _thumbnailViewVerticalOffset;

        public bool IsThumbnailOpen { get => _isThumbnailOpen; set => SetProperty(ref _isThumbnailOpen, value); }
        private bool _isThumbnailOpen;

        private void OpenThumbnail(object sender, RoutedEventArgs e)
        {
            IsThumbnailOpen = !IsThumbnailOpen;
            if (IsThumbnailOpen && !ThumbnailRenderer.IsDocumentLoaded)
            {
                ThumbnailRenderer.OpenPdf(new FileStream(_thumbnailFilename, FileMode.Open, FileAccess.Read, FileShare.Read));

                ThumbnailRenderer.PagesDisplayMode = PdfViewerPagesDisplayMode.ContinuousMode;
                ThumbnailRenderer.SetZoom(0.16);
                _thumbnailViewVerticalOffset = 0;
            }
        }
   
        private void ThumbnailRenderer_MouseClick(object sender, EventArgs e)
        {
            var i = Mouse.GetPosition(Application.Current.MainWindow);
            int index = (int)(((int)_thumbnailViewVerticalOffset + i.Y - 50) / (ThumbnailRenderer.CurrentPageSize.Height + 10));
            Renderer.GotoPage(index);
        }

        private void ThumbnailRenderer_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
        {
            _thumbnailViewVerticalOffset = +e.VerticalOffset;
            if (_thumbnailViewVerticalOffset < 0)
                _thumbnailViewVerticalOffset = 0;
        }

        #endregion

        #region Print 

        private void OnPrint(object sender, RoutedEventArgs e)
        {
            var pdfDocument = Renderer.Document;
            if (pdfDocument == null) return;

            using (var printDialog = new System.Windows.Forms.PrintDialog())
            using (var printDocument = pdfDocument.CreatePrintDocument())
            {
                printDialog.AllowSomePages = true;
                printDialog.Document = printDocument;
                printDialog.UseEXDialog = true;
                printDialog.Document.PrinterSettings.FromPage = 1;
                printDialog.Document.PrinterSettings.ToPage = pdfDocument.PageCount;
                printDialog.Document.PrinterSettings.PrinterName = printDialog.PrinterSettings.PrinterName;

                if (printDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    try
                    {
                        if (printDialog.Document.PrinterSettings.FromPage <= pdfDocument.PageCount)
                        {
                            printDialog.Document.Print();
                        }
                    }
                    catch
                    {
                        // Ignore exceptions; the printer dialog should take care of this.
                    }
                }
            }
        }

        #endregion
    }
}
