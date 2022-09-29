namespace LM.ImageComments.EditorComponent
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Formatting;
    using Microsoft.VisualStudio.Text.Tagging;

    /// <sound src="c:/a/hangmp3.mp3" start="167.3" volume="0.6" />

    /// <summary>
    /// Important class. Handles creation of image adornments on appropriate lines and associated error tags.
    /// </summary>
    public class ImageAdornmentManager : ITagger<ErrorTag>, IDisposable
    {
        private static System.Windows.Media.MediaPlayer soundPlayer;
        private static ImageAttributes soundPlayerData;
        public static bool test = false;

        private readonly IAdornmentLayer _layer;
        private readonly IWpfTextView _view;
        private readonly VariableExpander _variableExpander;
        private string _contentTypeName;
        private bool _initialised1;
        private bool _initialised2;
        private readonly List<ITagSpan<ErrorTag>> _errorTags;
        public ITextDocumentFactoryService TextDocumentFactory { get; set; }

        public static bool Enabled { get; set; }

        // Dictionary to map line number to image
        internal Dictionary<int, CommentImage> Images { get; set; }

        /// <summary>
        /// Initializes static members of the <see cref="ImageAdornmentManager"/> class
        /// </summary>
        static ImageAdornmentManager()
        {
            Enabled = true;
        }

        /// <summary>
        /// Enables or disables image comments. TODO: Make enable/disable mechanism better, e.g. specific to each editor instance and persistent
        /// </summary>
        public static void ToggleEnabled()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Enabled = !Enabled;
            UIMessage.Show($"Image comments enabled: {Enabled}. Scroll editor window(s) to update.");
        }

        public ImageAdornmentManager(IWpfTextView view)
        {
            _view = view;
            _layer = view.GetAdornmentLayer("ImageCommentLayer");
            Images = new Dictionary<int, CommentImage>();
            _view.LayoutChanged += LayoutChangedHandler;
            _contentTypeName = view.TextBuffer.ContentType.TypeName;
            _view.TextBuffer.ContentTypeChanged += contentTypeChangedHandler;

            _errorTags = new List<ITagSpan<ErrorTag>>();
            _variableExpander = new VariableExpander(_view);
        }

        private void contentTypeChangedHandler(object sender, ContentTypeChangedEventArgs e)
        {
            _contentTypeName = e.AfterContentType.TypeName;
            //System.IO.File.AppendAllText("C:\\tmp\\imagecomment.log", "\nType: "+_contentTypeName);
        }

        /// <summary>
        /// On layout change add the adornment to any reformatted lines
        /// </summary>
        private void LayoutChangedHandler(object sender, TextViewLayoutChangedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!Enabled)
                return;
            
            //System.IO.File.AppendAllText("C:\\tmp\\imagecomment.log", "\nLayoutChangedHandler" );            

            _errorTags.Clear();
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_view.TextSnapshot, new Span(0, _view.TextSnapshot.Length))));
            OnTagsChanged(new SnapshotSpan(_view.TextSnapshot, new Span(0, _view.TextSnapshot.Length)));

            foreach (var line in _view.TextViewLines) // TODO [?]: implement more sensible handling of removing error tags, then use e.NewOrReformattedLines
            {
                var lineNumber = line.Snapshot.GetLineFromPosition(line.Start.Position).LineNumber;
                //TODO [?]: Limit rate of calls to the below when user is editing a line
                try
                {
                    ITextDocument textDoc = null;
                    var success = TextDocumentFactory?.TryGetTextDocument(_view.TextBuffer, out textDoc);
                    CreateVisuals(line, lineNumber, success.HasValue && success.Value && textDoc!=null ? textDoc.FilePath : null);
                }
                catch (InvalidOperationException ex)
                {
                    ExceptionHandler.Notify(ex, true);
                }
            }

            // Sometimes, on loading a file in an editor view, the line transform gets triggered before the image adornments 
            // have been added, so the lines don't resize to the image height. So here's a workaround:
            // Changing the zoom level triggers the required update.
            // Need to do it twice - once to trigger the event, and again to change it back to the user's expected level.
            if (!_initialised1)
            {
                _view.ZoomLevel++;
                _initialised1 = true;
            }
            if (!_initialised2)
            {
                _view.ZoomLevel--;
                _initialised2 = true;
            }
        }

        /// <summary>
        /// Scans text line for matching image comment signature, then adds new or updates existing image adornment
        /// </summary>
        private void CreateVisuals(ITextViewLine line, int lineNumber, string absFilename)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var directory = absFilename!=null ? System.IO.Path.GetDirectoryName(absFilename) : null;
            var lineText = line.Extent.GetText().Split(new[] { "\r\n", "\r" }, StringSplitOptions.None)[0];
            var matchIndex = ImageCommentParser.Match(_contentTypeName, lineText, out var matchedText);
            if (matchIndex >= 0)
            {

                //lineText = line.Extent.GetText().Split(new string[] { "\r\n", "\r" }, StringSplitOptions.None)[0];
                // Get coordinates of text
                var start = line.Extent.Start.Position + matchIndex;
                var end = line.Start + (line.Extent.Length - 1);
                var span = new SnapshotSpan(_view.TextSnapshot, Span.FromBounds(start, end));
                

                ImageCommentParser.TryParse(matchedText, out var parsedImgData, out var parsingError);

                if (parsingError != null)
                {
                    if (Images.ContainsKey(lineNumber))
                    {
                        _layer.RemoveAdornment(Images[lineNumber]);
                        Images.Remove(lineNumber);
                    }

                    _errorTags.Add(new TagSpan<ErrorTag>(span,
                        new ErrorTag("XML parse error", $"Problem with comment format: {parsingError}")));

                    return;
                }

                string loadingMessage = null;
                if (parsedImgData.Name == "img" || parsedImgData.Name == "image")
                {
                    if (test) System.IO.File.AppendAllText("C:\\tmp\\imagecomment.log", "\nIMG:" + parsedImgData.Url);
                    // Check for and update existing image
                    CommentImage image = Images.ContainsKey(lineNumber) ? Images[lineNumber] : null;
                    if (image != null)
                    {
                        if (!image.Attributes.IsEqual(parsedImgData))
                        {
                            if (test) System.IO.File.AppendAllText("C:\\tmp\\imagecomment.log", "\nEXISTS:");
                            image.TrySet(directory, parsedImgData, out loadingMessage, () => CreateVisuals(line, lineNumber, absFilename));
                            if (test) System.IO.File.AppendAllText("C:\\tmp\\imagecomment.log", "\nEXISTSEND:");
                        }
                    }
                    else // No existing image, so create new one
                    {
                        if (test) System.IO.File.AppendAllText("C:\\tmp\\imagecomment.log", "\nADD:");
                        image = new CommentImage(_variableExpander);
                        image.TrySet(directory, parsedImgData, out loadingMessage, () => CreateVisuals(line, lineNumber, absFilename));
                        Images.Add(lineNumber, image);
                        if (test) System.IO.File.AppendAllText("C:\\tmp\\imagecomment.log", "\nADDEND:");
                    }

                    // Position image and add as adornment
                    if (loadingMessage == null && image.Source != null)
                    {

                        var geometry = _view.TextViewLines.GetMarkerGeometry(span);
                        if (geometry == null) // Exceptional case when image dimensions are massive (e.g. specifying very large scale factor)
                        {
                            throw new InvalidOperationException("Couldn't get source code line geometry. Is the loaded image massive?");
                        }
                        var textLeft = geometry.Bounds.Left;
                        var textBottom = line.TextBottom;
                        Canvas.SetLeft(image, textLeft);
                        Canvas.SetTop(image, textBottom);

                        // Add image to editor view
                        try
                        {
                            _layer.RemoveAdornment(image);
                            _layer.AddAdornment(AdornmentPositioningBehavior.TextRelative, line.Extent, null, image, null);
                        }
                        catch (Exception ex)
                        {
                            // No expected exceptions, so tell user something is wrong.
                            ExceptionHandler.Notify(ex, true);
                        }
                    }
                    else
                    {
                        if (Images.ContainsKey(lineNumber))
                        {
                            Images.Remove(lineNumber);
                        }

                        _errorTags.Add(new TagSpan<ErrorTag>(span, loadingMessage == null ?
                            new ErrorTag("No image set", "No image set") :
                            new ErrorTag("Trouble loading image", loadingMessage)));
                    }
                }
                if (parsedImgData.Name == "snd" || parsedImgData.Name == "sound")
                {
                    if (test) System.IO.File.AppendAllText("C:\\tmp\\imagecomment.log", "\nSND:" + parsedImgData.Url);
                    if (soundPlayerData == null)
                    {
                        try
                        {
                            if (parsedImgData.Start >= 0.0)
                            {
                                if (test) System.IO.File.AppendAllText("C:\\tmp\\imagecomment.log", "\nSNDFILE:" + parsedImgData.Url);
                                Play(parsedImgData.Url);
                                PlayPosition((float)parsedImgData.Start);
                                SetVolume((float)parsedImgData.Volume);
                                soundPlayerData = parsedImgData.clone();
                            }
                        }
                        catch (Exception e)
                        {
                            if (test) System.IO.File.AppendAllText("C:\\tmp\\imagecomment.log", "\nSNDERR:" + e.Message);
                            Stop();
                            soundPlayerData = null;
                        }
                    }
                    else
                    {
                        if (soundPlayerData.Url != parsedImgData.Url)
                        {
                            try
                            {
                                Stop();
                                soundPlayerData = null;
                                Play(parsedImgData.Url);
                                PlayPosition((float)parsedImgData.Start);
                                SetVolume((float)parsedImgData.Volume);
                                soundPlayerData = parsedImgData.clone();
                            }
                            catch (Exception e) { }
                        }

                        if (soundPlayerData.Volume != parsedImgData.Volume)
                        {
                            soundPlayerData.Volume = parsedImgData.Volume;
                            SetVolume((float)parsedImgData.Volume);                            
                        }
                        if (soundPlayerData.Start != parsedImgData.Start)
                        {
                            soundPlayerData.Start = parsedImgData.Start;
                            if (parsedImgData.Start < 0)
                            {
                                Stop();
                                soundPlayerData = null;
                            }
                            else { 
                                PlayPosition((float)parsedImgData.Start);
                            }
                        }
                    }
                }
            }
            else
            {
                if (Images.ContainsKey(lineNumber))
                {
                    Images.Remove(lineNumber);
                }
            }
        }

        private void UnsubscribeFromViewerEvents()
        {
            _view.LayoutChanged -= LayoutChangedHandler;
            _view.TextBuffer.ContentTypeChanged -= contentTypeChangedHandler;
        }

        #region ITagger<ErrorTag> Members

        public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            return _errorTags;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
        protected void OnTagsChanged(SnapshotSpan span)
        {
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// This is called by the TextView when closing. Events are unsubscribed here.
        /// </summary>
        /// <remarks>
        /// It's actually called twice - once by the IPropertyOwner instance, and again by the ITagger instance
        /// </remarks>
        public void Dispose()
        {
            UnsubscribeFromViewerEvents();
        }

        #endregion

        public void Play(string filename)
        {
            soundPlayer = new MediaPlayer();
            soundPlayer.Open(new Uri(filename));
            soundPlayer.Play();
        }
        public void SetVolume(float volume)
        {
            // MediaPlayer volume is a float value between 0 and 1.
            soundPlayer.Volume = volume;            
        }
        public void Stop()
        {
            if (soundPlayer != null)
            {
                soundPlayer.Stop();
            }
        }
        public void PlayPosition(float sec)
        {
            if (soundPlayer != null)
            {
                soundPlayer.Position=new TimeSpan( (long)Math.Round(10000000*sec) ) ;
            }
        }


    }
}
