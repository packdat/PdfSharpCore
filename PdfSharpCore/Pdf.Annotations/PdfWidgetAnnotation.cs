#region PDFsharp - A .NET library for processing PDF
//
// Authors:
//   Stefan Lange
//
// Copyright (c) 2005-2016 empira Software GmbH, Cologne Area (Germany)
//
// http://www.PdfSharpCore.com
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

using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf.AcroForms;

namespace PdfSharpCore.Pdf.Annotations
{
    /// <summary>
    /// Represents a text annotation.
    /// </summary>
    public sealed class PdfWidgetAnnotation : PdfAnnotation
    {
        internal PdfWidgetAnnotation()
        {
            Initialize();
        }

        internal PdfWidgetAnnotation(PdfDocument document)
            : base(document)
        {
            Initialize();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfWidgetAnnotation"/> with the specified dictionary.
        /// </summary>
        /// <param name="dict"></param>
        public PdfWidgetAnnotation(PdfDictionary dict)
            : base(dict)
        {
            if (dict.Elements.GetString(PdfAnnotation.Keys.Subtype) != "/Widget")
                throw new PdfSharpException("PdfWidgetAnnotation not initialized with the /Widget Subtype");
            DetermineAppearance();
        }

        void Initialize()
        {
            Elements.SetName(Keys.Subtype, "/Widget");
        }

        /// <summary>
        /// Gets or sets the background color of the field.
        /// </summary>
        public XColor BackColor
        {
            get { return backColor; }
            //set { this.backColor = value; }
        }
        XColor backColor = XColor.Empty;

        /// <summary>
        /// Gets or sets the border color of the field.
        /// </summary>
        public XColor BorderColor
        {
            get { return borderColor; }
            //set { this.borderColor = value; }
        }
        XColor borderColor = XColor.Empty;

        /// <summary>
        /// Gets or sets the Rotation of this Widget in counter-clockwise direction.
        /// </summary>
        public int Rotation
        {
            get
            {
                var mk = Elements.GetDictionary(Keys.MK);
                if (mk != null && mk.Elements.ContainsKey("/R"))
                    return mk.Elements.GetInteger("/R");
                return 0;
            }
            set
            {
                if (!Elements.ContainsKey(Keys.MK))
                    Elements.Add(Keys.MK, new PdfDictionary());
                var mk = Elements.GetDictionary(Keys.MK);
                mk.Elements.SetInteger("/R", value);
            }
        }

        /// <summary>
        /// Get the parent-field of this Widget, if it is the child of a <see cref="PdfAcroField"/>.
        /// </summary>
        public PdfObject ParentField
        {
            get
            {
                return Elements.GetObject(Keys.Parent);
            }
        }

        private void DetermineAppearance()
        {
            var mk = Elements.GetDictionary(Keys.MK);     // 12.5.6.19
            if (mk != null)
            {
                var bc = mk.Elements.GetArray("/BC");
                if (bc == null || bc.Elements.Count == 0)
                    borderColor = XColor.Empty;
                else if (bc.Elements.Count == 1)
                    borderColor = XColor.FromGrayScale(bc.Elements.GetReal(0));
                else if (bc.Elements.Count == 3)
                    borderColor = XColor.FromArgb((int)(bc.Elements.GetReal(0) * 255.0), (int)(bc.Elements.GetReal(1) * 255.0), (int)(bc.Elements.GetReal(2) * 255.0));
                else if (bc.Elements.Count == 4)
                    borderColor = XColor.FromCmyk(bc.Elements.GetReal(0), bc.Elements.GetReal(1), bc.Elements.GetReal(2), bc.Elements.GetReal(3));

                var bg = mk.Elements.GetArray("/BG");
                if (bg == null || bg.Elements.Count == 0)
                    backColor = XColor.Empty;
                else if (bg.Elements.Count == 1)
                    backColor = XColor.FromGrayScale(bg.Elements.GetReal(0));
                else if (bg.Elements.Count == 3)
                    backColor = XColor.FromArgb((int)(bg.Elements.GetReal(0) * 255.0), (int)(bg.Elements.GetReal(1) * 255.0), (int)(bg.Elements.GetReal(2) * 255.0));
                else if (bg.Elements.Count == 4)
                    backColor = XColor.FromCmyk(bg.Elements.GetReal(0), bg.Elements.GetReal(1), bg.Elements.GetReal(2), bg.Elements.GetReal(3));
            }
        }

        /// <summary>
        /// Predefined keys of this dictionary.
        /// </summary>
        internal new class Keys : PdfAnnotation.Keys
        {
            /// <summary>
            /// (Optional) The annotation’s highlighting mode, the visual effect to be used when
            /// the mouse button is pressed or held down inside its active area:
            ///   N (None) No highlighting.
            ///   I (Invert) Invert the contents of the annotation rectangle.
            ///   O (Outline) Invert the annotation’s border.
            ///   P (Push) Display the annotation’s down appearance, if any. If no down appearance is defined,
            ///     offset the contents of the annotation rectangle to appear as if it were being pushed below
            ///     the surface of the page.
            ///   T (Toggle) Same as P (which is preferred).
            /// A highlighting mode other than P overrides any down appearance defined for the annotation. 
            /// Default value: I.
            /// </summary>
            [KeyInfo(KeyType.Name | KeyType.Optional)]
            public const string H = "/H";

            /// <summary>
            /// (Optional) An appearance characteristics dictionary to be used in constructing a dynamic 
            /// appearance stream specifying the annotation’s visual presentation on the page.
            /// The name MK for this entry is of historical significance only and has no direct meaning.
            /// </summary>
            [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
            public const string MK = "/MK";

            /// <summary>
            /// (Required if this widget annotation is one of multiple children in a field; absent otherwise)
            /// An indirect reference to the widget annotation’s parent field.
            /// A widget annotation may have at most one parent; that is, it can be included in the Kids array of at most one field
            /// </summary>
            [KeyInfo(KeyType.Optional)]
            public const string Parent = "/Parent";

            public static DictionaryMeta Meta
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
