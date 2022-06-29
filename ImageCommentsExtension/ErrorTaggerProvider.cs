namespace LM.ImageComments.EditorComponent
{
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Utilities;
    using Microsoft.VisualStudio.Text.Tagging;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text;
    using System.Diagnostics;

    //https://docs.microsoft.com/hu-hu/previous-versions/visualstudio/visual-studio-2015/extensibility/language-service-and-editor-extension-points?view=vs-2015&redirectedfrom=MSDN
    [Export(typeof(IViewTaggerProvider))]
    [
        //ContentType("Any"),
        ContentType("CSharp"),
        ContentType("C/C++"),
        ContentType("Basic"),
        ContentType("code++.F#"),
        ContentType("F#"),
        ContentType("JScript"),
        ContentType("Python"),
        ContentType("XML"),
        ContentType("SQL Server Tools")
    ]

    [TagType(typeof(ErrorTag))]
    internal class ErrorTaggerProvider : IViewTaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView == null)
            {
                return null;
            }

            if (textView.TextBuffer != buffer)
            {
                return null;
            }

            Trace.Assert(textView is IWpfTextView);
            ImageAdornmentManager imageAdornmentManager = textView.Properties.GetOrCreateSingletonProperty<ImageAdornmentManager>("ImageAdornmentManager", () => new ImageAdornmentManager((IWpfTextView)textView));
            return imageAdornmentManager as ITagger<T>;
        }
    }
}
