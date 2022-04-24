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

using PdfSharpCore.Pdf.Annotations;

namespace PdfSharpCore.Pdf.AcroForms
{
    /// <summary>
    /// Represents the check box field.
    /// </summary>
    public sealed class PdfCheckBoxField : PdfButtonField
    {
        /// <summary>
        /// Initializes a new instance of PdfCheckBoxField.
        /// </summary>
        internal PdfCheckBoxField(PdfDocument document)
            : base(document)
        {
            _document = document;
        }

        internal PdfCheckBoxField(PdfDictionary dict)
            : base(dict)
        { }

        /// <summary>
        /// Indicates whether the field is checked.
        /// </summary>
        public bool Checked
        {
            get
            {
                var value = Elements.GetString(PdfAcroField.Keys.V);
                var widget = Annotations.Elements.Count > 0 ? Annotations.Elements[0] : null;
                if (widget != null)
                {
                    if (string.IsNullOrEmpty(value))
                        value = widget.Elements.GetString(PdfAnnotation.Keys.AS);
                    var appearances = widget.Elements.GetDictionary(PdfAnnotation.Keys.AP);
                    if (appearances != null)
                    {
                        var normalState = appearances.Elements.GetDictionary("/N");
                        if (normalState != null)
                            return value.Length != 0 && value != "/Off" && normalState.Elements.ContainsKey(value);
                    }
                }
                return value.Length != 0 && value != "/Off";
            }
            set
            {
                var name = value ? GetNonOffValue() : "/Off";
                Elements.SetName(PdfAcroField.Keys.V, name);
                Elements.SetName(PdfAnnotation.Keys.AS, name);
            }
        }

        void RenderAppearance()
        {
            for (var i = 0; i < Annotations.Elements.Count; i++)
            {
                var widget = Annotations.Elements[i];
                if (widget == null)
                    continue;

                widget.Elements.SetName(PdfAnnotation.Keys.AS, Checked ? GetNonOffValue() : "/Off");
            }
        }

        internal override void Flatten()
        {
            base.Flatten();

            if (Checked)
            {
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
                                var appSel = apps.Elements.GetDictionary(Checked ? GetNonOffValue() : "/Off");
                                if (appSel != null)
                                {
                                    RenderContentStream(widget.Page, appSel.Stream, widget.Rectangle);
                                }
                            }
                        }
                    }
                }
            }
        }

        internal override void PrepareForSave()
        {
            base.PrepareForSave();
            RenderAppearance();
        }

        /// <summary>
        /// Predefined keys of this dictionary. 
        /// The description comes from PDF 1.4 Reference.
        /// </summary>
        public new class Keys : PdfButtonField.Keys
        {
            /// <summary>
            /// (Optional; inheritable; PDF 1.4) A text string to be used in place of the V entry for the
            /// value of the field.
            /// </summary>
            [KeyInfo(KeyType.TextString | KeyType.Optional)]
            public const string Opt = "/Opt";

            /// <summary>
            /// Gets the KeysMeta for these keys.
            /// </summary>
            internal static DictionaryMeta Meta
            {
                get { return _meta ?? (_meta = CreateMeta(typeof(Keys))); }
            }
            static DictionaryMeta _meta;
        }

        /// <summary>
        /// Gets the KeysMeta of this dictionary type.
        /// </summary>
        internal override DictionaryMeta Meta
        {
            get { return Keys.Meta; }
        }
    }
}
