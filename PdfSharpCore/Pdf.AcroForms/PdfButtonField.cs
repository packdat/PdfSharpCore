#region PDFsharp - A .NET library for processing PDF
//
// Authors:
//   Stefan Lange (mailto:Stefan.Lange@pdfsharp.com)
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.pdfsharp.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PdfSharpCore.Pdf.Annotations;

namespace PdfSharpCore.Pdf.AcroForms
{
    /// <summary>
    /// Represents the base class for all button fields.
    /// </summary>
    public abstract class PdfButtonField : PdfAcroField
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PdfButtonField"/> class.
        /// </summary>
        protected PdfButtonField(PdfDocument document)
            : base(document)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfButtonField"/> class.
        /// </summary>
        protected PdfButtonField(PdfDictionary dict)
            : base(dict)
        { }

        /// <summary>
        /// Gets the name which represents the opposite of /Off.
        /// </summary>
        protected string GetNonOffValue()
        {
            // Try to get the information from the appearance dictionaray.
            // Just return the first key that is not /Off.
            // I'm not sure what is the right solution to get this value.
            if (Annotations.Elements.Count > 0)
            {
                var widget = Annotations.Elements[0];
                if (widget != null)
                {
                    var ap = widget.Elements.GetDictionary(PdfAnnotation.Keys.AP);
                    if (ap != null)
                    {
                        var n = ap.Elements.GetDictionary("/N");
                        if (n != null)
                        {
                            foreach (string name in n.Elements.Keys)
                                if (name != "/Off")
                                    return name;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the name which represents the opposite of /Off for the specified widget.
        /// </summary>
        /// <param name="widget"></param>
        /// <returns></returns>
        protected string GetNonOffValue(PdfWidgetAnnotation widget)
        {
            if (widget != null)
            {
                var ap = widget.Elements.GetDictionary(PdfAnnotation.Keys.AP);
                if (ap != null)
                {
                    var n = ap.Elements.GetDictionary("/N");
                    if (n != null)
                    {
                        return n.Elements.Keys.FirstOrDefault(name => name != "/Off");
                    }
                }
            }
            return null;
        }

        internal override void GetDescendantNames(ref List<string> names, string partialName)
        {
            string t = Elements.GetString(PdfAcroField.Keys.T);
            // HACK: ??? 
            if (t == "")
                t = "???";
            Debug.Assert(t != "");
            if (t.Length > 0)
            {
                if (!String.IsNullOrEmpty(partialName))
                    names.Add(partialName + "." + t);
                else
                    names.Add(t);
            }
        }

        /// <summary>
        /// Attempts to determine the visual appearance for this AcroField
        /// </summary>
        protected internal override void DetermineAppearance()
        {
            base.DetermineAppearance();
            if (!string.IsNullOrEmpty(ContentFontName) && DeterminedFontSize > 0.0)
                return;
            for (var i = 0; i < Annotations.Elements.Count; i++)
            {
                var widget = Annotations.Elements[i];
                if (widget.Page != null)
                {
                    var appearance = widget.Elements.GetDictionary(PdfAnnotation.Keys.AP);
                    if (appearance != null)
                    {
                        // /N -> Normal appearance, /R -> Rollover appearance, /D -> Down appearance
                        var apps = appearance.Elements.GetDictionary("/N");
                        if (apps != null)
                        {
                            var appSel = apps.Elements.GetDictionary(GetNonOffValue());
                            if (appSel != null)
                            {
                                try
                                {
                                    DetermineFontFromContent(appSel.Stream.UnfilteredValue);
                                }
                                catch
                                { }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Predefined keys of this dictionary. 
        /// The description comes from PDF 1.4 Reference.
        /// </summary>
        public new class Keys : PdfAcroField.Keys
        {
            // Pushbuttons have no additional entries.
        }
    }
}
