using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Formatting;

namespace LM.ImageComments.EditorComponent
{
    [Export(typeof(ILineTransformSourceProvider))]
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
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal class MyLineTransformSourceProvider : ILineTransformSourceProvider
    {
        ILineTransformSource ILineTransformSourceProvider.Create(IWpfTextView view)
        {
            
            ImageAdornmentManager manager = view.Properties.GetOrCreateSingletonProperty<ImageAdornmentManager>("ImageAdornmentManager", () => new ImageAdornmentManager(view));
            return new MyLineTransformSource(manager);
        }
    }
}