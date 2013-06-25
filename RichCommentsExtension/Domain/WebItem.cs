﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Text.Editor;
using System.Diagnostics;
using System.Security;

namespace LM.RichComments.Domain
{
    class WebItem : ContentControl, IRichCommentItem
    {
        public WebItem() : base()
        {
            _parameters = new Parameters(0, 0, ""); 
            _webBrowser = new WebBrowser();
            this.Content = _webBrowser;
            //...
            //throw new NotImplementedException();
        }

        private WebBrowser _webBrowser;

        private Parameters _parameters;

        public void AddToAdornmentLayer(Microsoft.VisualStudio.Text.Editor.IAdornmentLayer adornmentLayer, double lineTextLeft, double lineTextBottom, Microsoft.VisualStudio.Text.SnapshotSpan lineExtent)
        {
            // TODO: This code will probably be shared for all richcommentitem types... put in abstract class.
            Canvas.SetLeft(this, lineTextLeft);
            Canvas.SetTop(this, lineTextBottom);
            adornmentLayer.AddAdornment(AdornmentPositioningBehavior.TextRelative, lineExtent, null, this, null);
        }

        public void RemoveFromAdornmentLayer(Microsoft.VisualStudio.Text.Editor.IAdornmentLayer adornmentLayer)
        {
            adornmentLayer.RemoveAdornment(this);
        }

        public void Deactivate()
        {
            throw new NotImplementedException();
        }

        public string MakeFriendlyErrorMessage(Exception exception)
        {
            throw new NotImplementedException();
        }

        public void Update(IRichCommentItemParameters parameters, out Exception itemUpdateException)
        {
            itemUpdateException = null;
            _parameters = parameters as Parameters;
            Debug.Assert(_parameters != null);

            try
            {
                _webBrowser.Source = _parameters.Url;
                _webBrowser.Width = _parameters.Width;
                _webBrowser.Height = _parameters.Height;
            }
            catch (SecurityException ex)
            {
                itemUpdateException = ex;
            }
            catch (InvalidOperationException ex)
            {
                itemUpdateException = ex;  
            }
        }


        public double Height
        {
            get { return _parameters.Height; }
        }

        public class Parameters : IRichCommentItemParameters
        {
            public Parameters(double width, double height, string uriString)
            {
                this.Width = width;
                this.Height = height;
                try
                {
                    this.Url = new Uri(uriString);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("Specified URL is invalid.", "uriString", ex);
                }
            }
            public double Height { get; private set; }
            public double Width { get; private set; }
            public Uri Url { get; private set; }
            
            public Type RichCommentItemType
            {
                get { return typeof(WebItem); }
            }
        }
    }
}