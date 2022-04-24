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
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfSharpCore.Pdf.AcroForms
{
    /// <summary>
    /// Represents the radio button field.
    /// </summary>
    public sealed class PdfRadioButtonField : PdfButtonField
    {
        /// <summary>
        /// Initializes a new instance of PdfRadioButtonField.
        /// </summary>
        internal PdfRadioButtonField(PdfDocument document)
            : base(document)
        {
            _document = document;
        }

        internal PdfRadioButtonField(PdfDictionary dict)
            : base(dict)
        {
            if (!Elements.ContainsKey(Keys.Opt))
            {
                var array = new PdfArray(_document);
                foreach (var val in Options)
                    array.Elements.Add(new PdfName(val));
                Elements.Add(Keys.Opt, array);
            }
        }

        /// <summary>
        /// Gets or sets the value of this field. This should be an item from the <see cref="Options"/> list.
        /// </summary>
        public new string Value
        {
            get { return base.Value.ToString(); }
            set
            {
                base.Value = new PdfString(value);
                var index = IndexInFieldValues(value);
                SelectedIndex = index;
            }
        }

        private IList<string> options;

        /// <summary>
        /// Gets the option-names of this RadioButton<br></br>
        /// Use one of these values when setting <see cref="Value"/>
        /// </summary>
        public ICollection<string> Options
        {
            get
            {
                if (options != null)
                    return options;

                var values = new List<string>();
                for (var i = 0; i < Annotations.Elements.Count; i++)
                {
                    var widget = Annotations.Elements[i];
                    if (widget == null)
                        continue;

                    values.Add(GetNonOffValue(widget));
                }
                options = values;
                return options;
            }
        }

        /// <summary>
        /// Gets the (optional) export-values for each entry in this radio button group.<br></br>
        /// If the field does not specify these, <b><see cref="Options"/></b> is returned.
        /// </summary>
        public ICollection<string> ExportValues
        {
            get
            {
                var opt = Elements.GetArray(Keys.Opt);
                if (opt != null)
                {
                    var list = new List<string>();
                    for (var i =0; i < opt.Elements.Count; i++)
                        list.Add(opt.Elements.GetString(i));
                    return list;
                }
                return Options;
            }
        }

        /// <summary>
        /// Gets or sets the index of the selected radio button in a radio button group.<br></br>
        /// This is an alternative to the <see cref="Value"/>
        /// </summary>
        public int SelectedIndex
        {
            get
            {
                var value = Elements.GetString(PdfAcroField.Keys.V);
                return IndexInFieldValues(value);
            }
            set
            {
                var values = Options;
                var count = values.Count;
                if (value < -1 || value >= count)
                    throw new ArgumentOutOfRangeException("value");
                var name = value == -1 ? "/Off" : values.ElementAt(value);
                Elements.SetName(PdfAcroField.Keys.V, name);
                // first, set all annotations to /Off
                for (var i = 0; i < Annotations.Elements.Count; i++)
                {
                    var widget = Annotations.Elements[i];
                    if (widget != null)
                        widget.Elements.SetName(PdfAnnotation.Keys.AS, "/Off");
                }
                if ((Flags & PdfAcroFieldFlags.RadiosInUnison) != 0)
                {
                    // Then set all Widgets with the same Appearance to the checked state
                    for (var i = 0; i < Annotations.Elements.Count; i++)
                    {
                        var widget = Annotations.Elements[i];
                        if (name == values.ElementAt(i) && widget != null)
                            widget.Elements.SetName(PdfAnnotation.Keys.AS, name);
                    }
                }
                else
                {
                    if (value >= 0 && value < Annotations.Elements.Count)
                    {
                        var widget = Annotations.Elements[value];
                        if (widget != null)
                        {
                            widget.Elements.SetName(PdfAnnotation.Keys.AS, name);
                        }
                    }
                }
            }
        }

        private int IndexInFieldValues(string value)
        {
            return Options.ToList().IndexOf(value);
        }

        internal override void Flatten()
        {
            base.Flatten();

            for (var i = 0; i < Annotations.Elements.Count; i++)
            {
                var widget = Annotations.Elements[i];
                if (widget.Page != null)
                {
                    var appearance = widget.Elements.GetDictionary(PdfAnnotation.Keys.AP);
                    var selectedAppearance = widget.Elements.GetName(PdfAnnotation.Keys.AS);
                    if (appearance != null && selectedAppearance != null)
                    {
                        // /N -> Normal appearance, /R -> Rollover appearance, /D -> Down appearance
                        var apps = appearance.Elements.GetDictionary("/N");
                        if (apps != null)
                        {
                            var appSel = apps.Elements.GetDictionary(selectedAppearance);
                            if (appSel != null)
                            {
                                RenderContentStream(widget.Page, appSel.Stream, widget.Rectangle);
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
        public new class Keys : PdfButtonField.Keys
        {
            /// <summary>
            /// (Optional; inheritable; PDF 1.4) An array of text strings to be used in
            /// place of the V entries for the values of the widget annotations representing
            /// the individual radio buttons. Each element in the array represents
            /// the export value of the corresponding widget annotation in the
            /// Kids array of the radio button field.
            /// </summary>
            [KeyInfo(KeyType.Array | KeyType.Optional)]
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
