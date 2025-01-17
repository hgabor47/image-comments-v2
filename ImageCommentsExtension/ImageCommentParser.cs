﻿using System.Globalization;

namespace LM.ImageComments.EditorComponent
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;
    using System.Xml.Linq;
    using System.Diagnostics;
    /// <sound src="c:/a/hangmp3.mp3" start="167.3" volume="0.2" />
    // TODO [?]: Could make this a non-static class and use instances, but ensure a new instance is created when content type of a view is changed.
    internal static class ImageCommentParser
    {
        private static readonly Regex CsharpImageCommentRegex;
        private static readonly Regex CsharpIndentRegex;
        private static readonly Regex VbImageCommentRegex;
        private static readonly Regex VbIndentRegex;
        private static readonly Regex PythonImageCommentRegex;
        private static readonly Regex PythonIndentRegex;
        private static readonly Regex XmlImageTagRegex;
        private static readonly Regex SQLIndentRegex;
        private static readonly Regex SQLImageCommentRegex;
        private static readonly Regex XMLIndentRegex;
        private static readonly Regex XMLImageCommentRegex;

        // Initialize regex objects
        static ImageCommentParser()
        {
            // alternate Regex "image" for use with docfx
            const string xmlImageTagPattern = @"<img.*>|<image.*>|<sound.*>|<snd.*>";

            // C/C++/C#
            const string cSharpIndent = @"//\s+";
            CsharpIndentRegex = new Regex(cSharpIndent, RegexOptions.Compiled);
            const string cSharpCommentPattern = @"//.*";
            CsharpImageCommentRegex = new Regex(cSharpCommentPattern + xmlImageTagPattern, RegexOptions.Compiled);

            // VB
            const string vbIndent = @"'\s+";
            VbIndentRegex = new Regex(vbIndent, RegexOptions.Compiled);
            const string vbCommentPattern = @"'.*";
            VbImageCommentRegex = new Regex(vbCommentPattern + xmlImageTagPattern, RegexOptions.Compiled);

            //Python
            const string pythonIndent = @"#\s+";
            PythonIndentRegex = new Regex(pythonIndent, RegexOptions.Compiled);
            const string pythonCommentPattern = @"#.*";
            PythonImageCommentRegex = new Regex(pythonCommentPattern + xmlImageTagPattern, RegexOptions.Compiled);

            //Sql
            const string SQLIndent = @"--\s+";
            SQLIndentRegex = new Regex(SQLIndent, RegexOptions.Compiled);
            const string SQLCommentPattern = @"--.*";
            SQLImageCommentRegex = new Regex(SQLCommentPattern + xmlImageTagPattern, RegexOptions.Compiled);

            //XML
            const string XMLIndent = @"<!--\s+";
            XMLIndentRegex = new Regex(XMLIndent, RegexOptions.Compiled);
            const string XMLCommentPattern = @"<!--.*";
            XMLImageCommentRegex = new Regex(XMLCommentPattern + xmlImageTagPattern, RegexOptions.Compiled);



            XmlImageTagRegex = new Regex(xmlImageTagPattern, RegexOptions.Compiled);
        }

        /// <summary>
        /// Tries to match Regex on input text
        /// </summary>
        /// <img src="c:/a/logo.png" />
        /// <returns>Position in line at start of matched image comment. -1 if not matched</returns>
        public static int Match(string contentTypeName, string lineText, out string matchedText)
        {
            //System.IO.File.AppendAllText("C:\\tmp\\imagecomment.log", contentTypeName);
            //Debug.WriteLine("Unsupported content type: " + contentTypeName);

            Match commentMatch;
            Match indentMatch;
            switch(contentTypeName)
            {
                case "C/C++":
                case "CSharp":
                case "JScript":
                case "code++.F#":
                case "F#":
                    commentMatch = CsharpImageCommentRegex.Match(lineText);
                    indentMatch = CsharpIndentRegex.Match(lineText);
                    break;
                case "SQL Server Tools":
                    commentMatch = SQLImageCommentRegex.Match(lineText);
                    indentMatch = SQLIndentRegex.Match(lineText);
                    break;
                case "XML":
                    commentMatch = XMLImageCommentRegex.Match(lineText);
                    indentMatch = XMLIndentRegex.Match(lineText);
                    break;
                case "Basic":
                    commentMatch = VbImageCommentRegex.Match(lineText);
                    indentMatch = VbIndentRegex.Match(lineText);
                    break;
                case "Python":
                    commentMatch = PythonImageCommentRegex.Match(lineText);
                    indentMatch = PythonIndentRegex.Match(lineText);
                    break;
                //TODO: Add support for more languages
                default:
                    //Debug.WriteLine("Unsupported content type: " + contentTypeName);
                    matchedText = "";
                    return -1;
            }

            matchedText = commentMatch.Value;
            if (matchedText == "")
                return -1;
            
            return indentMatch.Index + indentMatch.Length;
        }

        /// <summary>
        /// Looks for well formed image comment in line of text and tries to parse parameters
        /// </summary>
        /// <param name="matchedText">Input: Line of text in editor window</param>
        /// <param name="imageData">Output: The collected image data</param>
        /// <param name="parsingError">An error string describing the problem if parsing failed</param>
        /// <returns>Returns true if successful, otherwise false</returns>
        public static bool TryParse(string matchedText, out ImageAttributes imageData, out string parsingError)
        {
            imageData = new ImageAttributes();
            if (ImageAdornmentManager.test)  System.IO.File.AppendAllText("C:\\tmp\\imagecomment.log", "\nmatchedText:" + matchedText);
            // Try parse text
            if (matchedText != "")
            {
                string tagText = XmlImageTagRegex.Match(matchedText).Value;
                try
                {
                    XElement imgEl = XElement.Parse(tagText);
                    if (imgEl.Name == "img" || imgEl.Name == "image")
                    {
                        
                        XAttribute srcAttr = imgEl.Attribute("src");
                        // alternate Attribute name "url" used by docfx
                        if (srcAttr == null)
                        {
                            srcAttr = imgEl.Attribute("url");
                        }
                        if (srcAttr == null)
                        {
                            parsingError = "src (or url) attribute not specified.";
                            return false;
                        }
                        imageData.Url = srcAttr.Value;

                        //scale
                        XAttribute scaleAttr = imgEl.Attribute("scale");
                        if (scaleAttr != null)
                        {
                            double.TryParse(scaleAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out imageData.Scale);
                        }

                        //opacity
                        XAttribute opacityAttr = imgEl.Attribute("opacity");
                        if (opacityAttr != null)
                        {
                            double.TryParse(opacityAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out imageData.Opacity);
                        }

                        //background color
                        XAttribute bgColorAttr = imgEl.Attribute("bgcolor");
                        if (bgColorAttr != null)
                        {
                            var colorString = bgColorAttr.Value.Replace("#", "").Replace("0x", "");

                            //expand short hand color format
                            if (colorString.Length == 3)
                            {
                                colorString = String.Format("{0}{0}{1}{1}{2}{2}",
                                    colorString[0], colorString[1], colorString[2]);
                            }

                            if ((colorString.Length == 6 || colorString.Length == 8) &&
                                UInt32.TryParse(colorString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var color))
                            {
                                if (colorString.Length == 6)
                                {
                                    imageData.Background.A = 255;
                                    imageData.Background.B = (byte)color;
                                    imageData.Background.G = (byte)(color >> 8);
                                    imageData.Background.R = (byte)(color >> 16);
                                }
                                else
                                {
                                    imageData.Background.A = (byte)color;
                                    imageData.Background.B = (byte)(color >> 8);
                                    imageData.Background.G = (byte)(color >> 16);
                                    imageData.Background.R = (byte)(color >> 24);
                                }

                            }
                        }
                        else
                        {
                            imageData.Background.A = 0;
                        }

                        parsingError = null;
                        return true;
                    }
                    if (imgEl.Name == "snd" || imgEl.Name == "sound")
                    {
                        imageData.Name = "sound";
                        XAttribute srcAttr = imgEl.Attribute("src");
                        if (srcAttr == null)
                        {
                            srcAttr = imgEl.Attribute("url");
                        }
                        if (srcAttr == null)
                        {
                            parsingError = "src (or url) attribute not specified.";
                            return false;
                        }
                        imageData.Url = srcAttr.Value;
                        XAttribute volAttr = imgEl.Attribute("volume");
                        if (volAttr != null)
                        {
                            double.TryParse(volAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out imageData.Volume);
                        }
                        XAttribute startAttr = imgEl.Attribute("start");
                        if (volAttr != null)
                        {
                            double.TryParse(startAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out imageData.Start);
                        }
                        parsingError = null;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    parsingError = ex.Message;
                    return false;
                }
            }
            parsingError = @"<img... /> or <image... /> tag not in correct format.";
            return false;
        }
    }
}
