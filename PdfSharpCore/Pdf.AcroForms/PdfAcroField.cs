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
using System.Globalization;
using System.IO;
using System.Linq;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf.Advanced;
using PdfSharpCore.Pdf.Annotations;
using PdfSharpCore.Pdf.Content;
using PdfSharpCore.Pdf.Content.Objects;
using PdfSharpCore.Pdf.Internal;
using PdfSharpCore.Pdf.AcroForms.enums;
using PdfSharpCore.Pdf.IO;

namespace PdfSharpCore.Pdf.AcroForms
{
    /// <summary>
    /// Represents the base class for all interactive field dictionaries.
    /// </summary>
    [DebuggerDisplay("{FullyQualifiedName} ({GetType().Name})")]
    public abstract class PdfAcroField : PdfDictionary
    {
        /// <summary>
        /// Initializes a new instance of PdfAcroField.
        /// </summary>
        internal PdfAcroField(PdfDocument document)
            : base(document)
        {
            document._irefTable.Add(new PdfReference(this));
            Reference.Document = document;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfAcroField"/> class. Used for type transformation.
        /// </summary>
        protected PdfAcroField(PdfDictionary dict)
            : base(dict)
        {
            Elements.ChangeOwner(this);
            DetermineAppearance();
        }

        /// <summary>
        /// Gets the name of this field.
        /// </summary>
        public string Name
        {
            get
            {
                string name = Elements.GetString(Keys.T);
                return name;
            }
            set { Elements.SetString(Keys.T, value); }
        }

        /// <summary>
        /// Gets the alternative Name of the Field (/TU)
        /// </summary>
        public string AlternateName
        {
            get { return Elements.GetString(Keys.TU); }
            set { Elements.SetString(Keys.TU, value); }
        }

        /// <summary>
        /// Gets the mapping Name of the Field (/TM)
        /// </summary>
        public string MappingName
        {
            get { return Elements.GetString(Keys.TM); }
            set { Elements.SetString(Keys.TM, value); }
        }

        /// <summary>
        /// Gets the fully qualified name of this field, that is: "parent-name.field-name"
        /// <para>If the field has no parent, this is equal to <see cref="Name"/></para>
        /// </summary>
        public string FullyQualifiedName
        {
            get
            {
                var fqn = Name;
                var parent = Elements.GetObject(Keys.Parent) as PdfDictionary;
                while (parent != null)
                {
                    var parentName = parent.Elements.GetString(Keys.T);
                    if (!string.IsNullOrEmpty(parentName))
                        fqn = parentName + "." + fqn;
                    parent = parent.Elements.GetObject(Keys.Parent) as PdfDictionary;
                }
                return fqn;
            }
        }

        /// <summary>
        /// Gets the Parent of this field or null, if the field has no parent
        /// </summary>
        public PdfAcroField Parent
        {
            get
            {
                var parentRef = Elements.GetReference(Keys.Parent);
                if (parentRef != null)
                    return PdfAcroFieldCollection.CreateAcroField(parentRef.Value as PdfDictionary);
                return null;
            }
            internal set
            {
                if (value != null)
                    Elements.SetReference(Keys.Parent, value);
                else
                    Elements[Keys.Parent] = PdfNull.Value;
            }
        }

        /// <summary>
        /// Gets the field flags of this instance.
        /// </summary>
        public PdfAcroFieldFlags Flags
        {
            // TODO: This entry is inheritable, thus the implementation is incorrect...
            get { return (PdfAcroFieldFlags)Elements.GetInteger(Keys.Ff); }
        }

        internal PdfAcroFieldFlags SetFlags
        {
            get { return (PdfAcroFieldFlags)Elements.GetInteger(Keys.Ff); }
            set { Elements.SetInteger(Keys.Ff, (int)value); }
        }

        /// <summary>
        /// Gets or sets the font used to draw the text of the field.
        /// </summary>
        public XFont Font
        {
            get { return font; }
            set { font = value; }
        }
        XFont font;

        /// <summary>
        /// Gets the font name that was obtained by analyzing the Fields' content-stream.<br></br>
        /// This is the name of the font as present in the <b>DR/Font</b> dictionary
        /// </summary>
        internal string ContentFontName { get; private set; }

        /// <summary>
        /// Gets the base font name that was obtained by analyzing the Fields' content-stream.<br></br>
        /// This is the name of the <b>BaseFont</b>, <see cref="ContentFontName"/> refers to
        /// <br></br>Example:
        /// <code>
        /// 53 0 obj   % PdfSharpCore.Pdf.AcroForms.PdfAcroForm
        /// &lt;&lt;
        ///   /DA(/Helv 0 Tf 0 g )      &lt;-- Default Appearance
        ///   /DR
        ///   &lt;&lt;
        ///     /Encoding
        ///     &lt;&lt;
        ///       /PDFDocEncoding 6 0 R
        ///     &gt;&gt;
        ///     /Font
        ///     &lt;&lt;
        ///       /Arial 57 0 R
        ///       /HeBo 4 0 R		&lt;-- ContentFontName, text-operators in Content-Streams use this name
        ///       /Helv 5 0 R
        ///       /ZaDb 63 0 R
        ///     &gt;&gt;
        ///   &gt;&gt;
        ///   /Fields[...]
        /// &gt;&gt;
        /// endobj
        ///
        /// 4 0 obj   % PdfSharpCore.Pdf.PdfDictionary
        /// &lt;&lt;
        ///   /BaseFont /Helvetica-Bold	    &lt;-- BaseContentFontName, refers to one of the 14 Standard Fonts
        ///   /Encoding 6 0 R
        ///   /Name /HeBo
        ///   /Subtype /Type1
        ///   /Type /Font
        /// &gt;&gt;
        /// endobj
        /// </code>
        /// </summary>
        internal string BaseContentFontName { get; private set; }

        /// <summary>
        /// Gets the font size that was obtained by analyzing the Fields' content-stream.
        /// </summary>
        public double DeterminedFontSize { get; private set; }

        /// <summary>
        /// Gets or sets the foreground color of the field.
        /// </summary>
        public XColor ForeColor
        {
            get { return foreColor; }
            set { foreColor = value; }
        }
        XColor foreColor = XColors.Black;

        /// <summary>
        /// Gets or sets the value of the field.
        /// </summary>
        public virtual PdfItem Value
        {
            get { return Elements.ContainsKey(Keys.V) ? Elements.GetValue(Keys.V) : new PdfString(""); }
            set
            {
                if (ReadOnly)
                    throw new InvalidOperationException("The field is read only.");
                if (value is PdfString || value is PdfName)
                    Elements[Keys.V] = value;
                else
                    throw new NotImplementedException("Values other than string cannot be set.");
            }
        }

        /// <summary>
        /// Gets or sets the default value of the field.
        /// </summary>
        public virtual PdfItem DefaultValue
        {
            get { return Elements.ContainsKey(Keys.DV) ? Elements.GetValue(Keys.DV) : new PdfString(""); }
            set { Elements[Keys.DV] = value; }
        }

        /// <summary>
        /// Gets or sets the alignment for the text of this field.
        /// </summary>
        public virtual TextAlignment TextAlign
        {
            get
            {
                var alignment = TextAlignment.Left; // default
                if (Elements.ContainsKey(Keys.Q))
                    alignment = (TextAlignment)Elements.GetInteger(Keys.Q);
                else if (_document.AcroForm.Elements.ContainsKey(Keys.Q))
                    alignment = (TextAlignment)_document.AcroForm.Elements.GetInteger(Keys.Q);
                return alignment;
            }
            set { Elements[Keys.Q] = new PdfInteger((int)value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the field is read only.
        /// </summary>
        public bool ReadOnly
        {
            get { return (Flags & PdfAcroFieldFlags.ReadOnly) != 0; }
            set
            {
                if (value)
                    SetFlags |= PdfAcroFieldFlags.ReadOnly;
                else
                    SetFlags &= ~PdfAcroFieldFlags.ReadOnly;
            }
        }

        /// <summary>
        /// Gets the field with the specified name.
        /// </summary>
        public PdfAcroField this[string name]
        {
            get { return GetValue(name); }
        }

        /// <summary>
        /// Gets a child field by name.
        /// </summary>
        protected virtual PdfAcroField GetValue(string name)
        {
            if (String.IsNullOrEmpty(name))
                return this;
            if (HasKids)
                return Fields.GetValue(name);
            return null;
        }

        /// <summary>
        /// Indicates whether the field has child elements (child-fields or annotations).<br></br>
        /// To determine, whether this field has child-fields, use <see cref="HasChildFields"/>
        /// </summary>
        public bool HasKids
        {
            get
            {
                var kidsArray = Elements.GetArray(Keys.Kids);
                return kidsArray != null && kidsArray.Elements.Count > 0;
            }
        }

        /// <summary>
        /// Indicates whether the field has child fields.
        /// </summary>
        public bool HasChildFields
        {
            get
            {
                var kidsArray = Elements.GetArray(Keys.Kids);
                if (kidsArray != null)
                {
                    for (var i = 0; i < kidsArray.Elements.Count; i++)
                    {
                        var kid = kidsArray.Elements.GetObject(i) as PdfDictionary;
                        // TODO: Don't know how to clearly distinguish a Field from an Annotation in the Kids-Array.
                        // 12.7.1:
                        //    As a convenience, when a field has only a single associated widget annotation, the
                        //    contents of the field dictionary and the annotation dictionary(12.5.2, �Annotation Dictionaries�)
                        //    may be merged into a single dictionary containing entries that pertain to both a field and an annotation.
                        // For now, we consider a Kid to be a Field, if the "Subtype" Value is missing (which is required for Annotations),
                        // or it has an /T or /FT entry (which is required for terminal fields) -> Chapter 12.7.3.1 in PdfReference
                        if (kid != null
                            && (!kid.Elements.ContainsKey(PdfAnnotation.Keys.Subtype)
                                || kid.Elements.ContainsKey(Keys.FT)
                                || kid.Elements.ContainsKey(Keys.T)
                               )
                           )
                           return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Gets the names of all descendants of this field.
        /// </summary>
        public string[] GetDescendantNames()
        {
            List<string> names = new List<string>();
            if (HasKids)
            {
                PdfAcroFieldCollection fields = Fields;
                fields.GetDescendantNames(ref names, null);
            }
            List<string> temp = new List<string>();
            foreach (string name in names)
                temp.Add(name);
            return temp.ToArray();
        }

        /// <summary>
        /// Gets the names of all appearance dictionaries of this AcroField.
        /// </summary>
        public string[] GetAppearanceNames()
        {
            Dictionary<string, object> names = new Dictionary<string, object>();
            PdfDictionary dict = Elements["/AP"] as PdfDictionary;
            if (dict != null)
            {
                AppDict(dict, names);

                if (HasKids)
                {
                    PdfItem[] kids = Fields.Elements.Items;
                    foreach (PdfItem pdfItem in kids)
                    {
                        if (pdfItem is PdfReference)
                        {
                            PdfDictionary xxx = ((PdfReference)pdfItem).Value as PdfDictionary;
                            if (xxx != null)
                                AppDict(xxx, names);
                        }
                    }
                }
            }
            string[] array = new string[names.Count];
            names.Keys.CopyTo(array, 0);
            return array;
        }

        static void AppDict(PdfDictionary dict, Dictionary<string, object> names)
        {
            PdfDictionary sub;
            if ((sub = dict.Elements["/D"] as PdfDictionary) != null)
                AppDict2(sub, names);
            if ((sub = dict.Elements["/N"] as PdfDictionary) != null)
                AppDict2(sub, names);
        }

        static void AppDict2(PdfDictionary dict, Dictionary<string, object> names)
        {
            foreach (string key in dict.Elements.Keys)
            {
                if (!names.ContainsKey(key))
                    names.Add(key, null);
            }
        }

        internal virtual void GetDescendantNames(ref List<string> names, string partialName)
        {
            if (HasKids)
            {
                PdfAcroFieldCollection fields = Fields;
                string t = Elements.GetString(Keys.T);
                Debug.Assert(t != "");
                if (t.Length > 0)
                {
                    if (!string.IsNullOrEmpty(partialName))
                        partialName += "." + t;
                    else
                        partialName = t;
                    fields.GetDescendantNames(ref names, partialName);
                }
            }
            else
            {
                string t = Elements.GetString(Keys.T);
                Debug.Assert(t != "");
                if (t.Length > 0)
                {
                    if (!string.IsNullOrEmpty(partialName))
                        names.Add(partialName + "." + t);
                    else
                        names.Add(t);
                }
            }
        }


        /// <summary>
        /// Gets the collection of fields within this field.
        /// </summary>
        public PdfAcroFieldCollection Fields
        {
            get
            {
                if (_fields == null)
                {
                    // owner may be a widget annotation, we have to make sure, the owner is correct, otherwise an exception occurs (/Kids is not a valid key for Annotations)
                    if (Elements.Owner != this)
                        Elements.ChangeOwner(this);
                    object o = Elements.GetValue(Keys.Kids, VCF.CreateIndirect);
                    _fields = (PdfAcroFieldCollection)o;
                }
                // TODO: It would be nice if the FieldCollection contains only "real" fields.
                // Currently, the items are a mix of fields and annotations...
                return _fields;
            }
        }
        PdfAcroFieldCollection _fields;

        /// <summary>
        /// Gets the annotations-array for this field.
        /// The elements of this array are of type <see cref="PdfWidgetAnnotation"/>.
        /// </summary>
        public PdfAnnotationArray Annotations
        {
            get
            {
                if (_annotations == null)
                {
                    _annotations = new PdfAnnotationArray();
                    var childs = Elements.GetArray(Keys.Kids);
                    if (childs != null && childs.Elements.Count > 0)
                    {
                        for (var i = 0; i < childs.Elements.Count; i++)
                        {
                            var obj = childs.Elements.GetDictionary(i);
                            if (obj is PdfWidgetAnnotation)
                                _annotations.Elements.Add((PdfWidgetAnnotation)obj);
                            else if (obj != null && string.Compare(obj.Elements.GetString(PdfAnnotation.Keys.Subtype), "/Widget", StringComparison.OrdinalIgnoreCase) == 0
                                // if it is a merged field, do not count as Annotation, but as a child-field
                                     && !obj.Elements.ContainsKey(Keys.FT) && !obj.Elements.ContainsKey(Keys.T))
                            {
                                _annotations.Elements.Add(new PdfWidgetAnnotation(obj));
                                // must reset the value in the reference after type-transformation so a reference to this field points to the field, not the widget
                                obj.Reference.Value = obj;
                            }
                        }
                    }
                    // if the dictionaries are merged (no childs), use current field as Widget
                    if (Elements.GetString(PdfAnnotation.Keys.Subtype) == "/Widget")
                    {
                        _annotations.Elements.Add(new PdfWidgetAnnotation(this));
                        // must reset the value in the reference after type-transformation
                        Reference.Value = this;
                    }
                }
                return _annotations;
            }
        }
        PdfAnnotationArray _annotations;

        /// <summary>
        /// Adds a new Annotation to this field.
        /// </summary>
        /// <param name="configure">A method that is used to configure the Annotation</param>
        /// <returns>The created and configured Annotation</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public PdfWidgetAnnotation AddAnnotation(Action<PdfWidgetAnnotation> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            var annotation = new PdfWidgetAnnotation(_document)
            {
                ParentField = this
            };
            configure(annotation);
            if (!Elements.ContainsKey(Keys.Kids))
                Elements.GetValue(Keys.Kids, VCF.CreateIndirect);
            var childs = Elements.GetArray(Keys.Kids);
            childs.Elements.Add(annotation.Reference);
            return annotation;
        }

        /// <summary>
        /// Adds the specified <see cref="PdfAcroField"/> to the list of child-fields of this field
        /// </summary>
        /// <param name="childField"></param>
        public void AddChild(PdfAcroField childField)
        {
            Fields.Elements.Add(childField);
            childField.Parent = this;
        }

        /// <summary>
        /// Tries to determine the Appearance of the Field by checking elements of its dictionary
        /// </summary>
        protected internal virtual void DetermineAppearance()
        {
            try
            {
                var da = Elements.GetString(Keys.DA);     // 12.7.3.3
                if (string.IsNullOrEmpty(da) && this is PdfTextField)
                {
                    // if Field does not contain appearance dictionary, check AcroForm
                    da = _document.AcroForm.Elements.GetString(Keys.DA);
                    if (da == null)
                    {
                        // no appearance found, use some default
                        ContentFontName = "/Helv";
                        BaseContentFontName = "/Helvetica";
                        Font = new XFont("Arial", DetermineFontSize());
                        return;
                    }
                }
                if (!string.IsNullOrEmpty(da))
                    DetermineFontFromContent(PdfEncoders.RawEncoding.GetBytes(da));
            }
            catch
            {
                Font = new XFont("Arial", 10);
            }
        }

        /// <summary>
        /// Attempts to determine the font, font-size and fore-color of this AcroField
        /// </summary>
        /// <param name="contentBytes"></param>
        protected void DetermineFontFromContent(byte[] contentBytes)
        {
            string fontName = null;
            double fontSize = 0.0;
            var content = ContentReader.ReadContent(contentBytes);
            for (var i = 0; i < content.Count; i++)
            {
                var op = content[i] as COperator;
                if (op != null)
                {
                    switch (op.OpCode.OpCodeName)
                    {
                        case OpCodeName.Tf:
                            fontName = op.Operands[0].ToString();
                            fontSize = double.Parse(op.Operands[1].ToString(), CultureInfo.InvariantCulture);
                            break;
                        case OpCodeName.g:          // gray value (0.0 = black, 1.0 = white)
                            if (op.Operands.Count > 0)
                                ForeColor = XColor.FromGrayScale(double.Parse(op.Operands[0].ToString(), CultureInfo.InvariantCulture));
                            break;
                        case OpCodeName.rg:         // rgb color (Chapter 8.6.8)
                            if (op.Operands.Count > 2)
                            {
                                var r = double.Parse(op.Operands[0].ToString(), CultureInfo.InvariantCulture);
                                var g = double.Parse(op.Operands[1].ToString(), CultureInfo.InvariantCulture);
                                var b = double.Parse(op.Operands[2].ToString(), CultureInfo.InvariantCulture);
                                ForeColor = XColor.FromArgb((int)(r * 255.0), (int)(g * 255.0), (int)(b * 255.0));
                            }
                            break;
                    }
                }
            }
            if (!string.IsNullOrEmpty(fontName))
            {
                var fontIsPresentInDocument = false;
                var presentFontEncoding = PdfFontEncoding.WinAnsi;
                ContentFontName = fontName;    // e.g. "/Helv"
                var possibleResources = new[]
                {
                    _document.AcroForm.Elements.GetDictionary(PdfAcroForm.Keys.DR),
                    Elements.GetDictionary(PdfAcroForm.Keys.DR)
                };
                foreach (var resources in possibleResources)
                {
                    if (resources != null && resources.Elements.ContainsKey("/Font"))
                    {
                        var fontList = resources.Elements.GetDictionary("/Font");
                        var fontRef = fontList.Elements.GetReference(fontName);
                        if (fontRef != null)
                        {
                            fontIsPresentInDocument = true;
                            var fontDict = fontRef.Value as PdfDictionary;
                            if (fontDict != null && fontDict.Elements.GetName("/Encoding") != "/WinAnsiEncoding")
                                presentFontEncoding = PdfFontEncoding.Unicode;
                            if (fontDict != null && fontDict.Elements.ContainsKey("/BaseFont"))
                            {
                                var baseName = fontDict.Elements.GetString("/BaseFont");
                                if (!string.IsNullOrEmpty(baseName))
                                {
                                    fontName = baseName; // e.g. "/Helvetica"
                                    break;
                                }
                            }
                        }
                    }
                }
                BaseContentFontName = fontName;
                if (fontSize < 1.0)
                {
                    fontSize = DetermineFontSize();
                }
                DeterminedFontSize = fontSize;
                // When the field's font is one of the standard fonts, use WinAnsiEncoding, as that seems to work best with the tested documents
                var systemFontName = BaseContentFontName;
                XFontStyle fontStyle = XFontStyle.Regular;
                if (fontIsPresentInDocument || IsStandardFont(BaseContentFontName, out systemFontName, out fontStyle))
                {
                    font = new XFont
                    (
                        systemFontName,
                        Math.Max(1.0, fontSize),
                        fontStyle,
                        new XPdfFontOptions(presentFontEncoding)
                    )
                    {
                        FromDocument = true,
                        DocumentFontName = ContentFontName
                    };
                }
                else
                {
                    // ok, we bite the bullet and embed a new font (so at least the correct characters should show up, although with the possibly wrong font)
                    // TODO: Reasearch how to get the correct glyph indices for embedded fonts
                    font = new XFont(BaseContentFontName.TrimStart('/'), Math.Max(1.0, fontSize));     // Avoid Exception, if size is zero
                }
            }
        }

        private double DetermineFontSize()
        {
            var fontSize = 10.0;
            for (var a = 0; a < Annotations.Elements.Count; a++)
            {
                var widget = Annotations.Elements[a];
                if (widget != null && !widget.Rectangle.IsEmpty)
                {
                    var refValue = widget.Rotation == 0 || widget.Rotation == 180 || (widget.Flags & PdfAnnotationFlags.NoRotate) != 0 ? widget.Rectangle.Height : widget.Rectangle.Width;
                    if (!(this is PdfTextField) || !((PdfTextField)this).MultiLine)
                        fontSize = refValue * 0.8;
                    if (fontSize > 1.0)
                        break;
                }
            }
            return fontSize;
        }

        // 9.6.2.2 Standard Type 1 Fonts (Standard 14 Fonts)
        // TODO: check on non-Windows platforms
        private static readonly Dictionary<string, string> standardFonts = new Dictionary<string, string>
        {
            { "Times-Roman", "Times New Roman" },
            { "Times-Bold", "Times New Roman" },
            { "Times-Italic", "Times New Roman" },
            { "Times-BoldItalic", "Times New Roman" },
            { "Helvetica", "Arial" },
            { "Helvetica-Bold", "Arial" },
            { "Helvetica-Oblique", "Arial" },
            { "Helvetica-BoldOblique", "Arial" },
            { "Courier", "Courier New" },
            { "Courier-Bold", "Courier New" },
            { "Courier-Oblique", "Courier New" },
            { "Courier-BoldOblique", "Courier New" },
            { "ZapfDingbats", "ZapfDingbats" },
            { "Symbol", "Symbol" },
        };

        /// <summary>
        /// Gets a value indicating whether the specified font name is one of the predefined font-names for pdf documents
        /// </summary>
        /// <param name="fontName"></param>
        /// <param name="systemFontName"></param>
        /// <returns></returns>
        protected static bool IsStandardFont(string fontName, out string systemFontName, out XFontStyle fontStyle)
        {
            fontStyle = XFontStyle.Regular;
            foreach (var kv in standardFonts)
            {
                if (fontName.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    systemFontName = kv.Value;
                    if (kv.Key.Contains("Bold") || kv.Key.Contains("Oblique"))
                        fontStyle |= XFontStyle.Bold;
                    if (kv.Key.Contains("Italic"))
                        fontStyle |= XFontStyle.Italic;
                    return true;
                }
            }
            systemFontName = null;
            return false;
        }

        /// <summary>
        /// Adds the font of the current AcroField to the specified XForm object
        /// </summary>
        /// <param name="form"></param>
        protected void SetXFormFont(XForm form)
        {
            // Non-flattened AcroFields need to have the proper Font set in the XForm's Resource-Table
            if (Font.FromDocument 
                && !string.IsNullOrEmpty(Font.DocumentFontName)
                && _document.AcroForm.Resources != null)
            {
                // standard fonts
                if (!form.PdfForm.Resources.Fonts.Elements.ContainsKey(Font.DocumentFontName))
                {
                    // Retrieve font-resources from the document's AcroForm
                    if (_document.AcroForm.Resources.Fonts.Elements.ContainsKey(Font.DocumentFontName))
                    {
                        // copy font-entry to XForm
                        var formFont = _document.AcroForm.Resources.Fonts.Elements.GetDictionary(Font.DocumentFontName);
                        if (formFont != null)
                            form._pdfForm.Resources.Fonts.Elements.Add(Font.DocumentFontName, formFont.Reference);
                    }
                }
            }
            else
            {
                // non-standard fonts
                var docFont = _document.FontTable.GetFont(Font);
                form.PdfForm.Resources.AddFont(docFont);
            }
        }

        internal override void PrepareForSave()
        {
            base.PrepareForSave();
            // for newly created fields, add the font to the AcroForm's resources
            if (Font != null && !Font.FromDocument)
            {
                var formResources = _document.AcroForm.GetOrCreateResources();
                var pdfFont = _document.FontTable.GetFont(Font);
                formResources.AddFont(pdfFont);
            }
            if (HasKids)
            {
                for (var i = 0; i < Fields.Elements.Count; i++)
                {
                    var field = Fields[i];
                    field.PrepareForSave();
                }
            }
            // accessing the Fields-property may have created a new empty array, remove that
            if (Fields.Elements.Count == 0)
                Elements.Remove(Keys.Kids);
        }

        internal void RemoveAnnotation(PdfWidgetAnnotation widget)
        {
            var kids = Elements.GetArray(Keys.Kids);
            if (kids != null)
            {
                for (var i = 0; i < kids.Elements.Count; i++)
                {
                    var kid = kids.Elements.GetObject(i);
                    if (kid != null && kid.ObjectID == widget.ObjectID)
                    {
                        kids.Elements.RemoveAt(i);
                        return;
                    }
                }
            }
            Annotations.Elements.Remove(widget);
        }

        internal virtual void Flatten()
        {
            // Copy Font-Resources to the Page
            // This is neccessary, because Fonts used by AcroFields may be referenced only by the AcroForm, which is deleted after flattening
            for (var i = 0; i < Annotations.Elements.Count; i++)
            {
                var widget = Annotations.Elements[i];
                if ((widget.Flags & PdfAnnotationFlags.Hidden) != 0 || (widget.Flags & PdfAnnotationFlags.NoView) != 0)
                {
                    RemoveAnnotation(widget);
                    i--;
                    continue;
                }
                if (widget.Page != null)
                {
                    // there's still something missing on page import, sometimes the owner of a widget is still the document we imported from...
                    if (widget.Page.Owner != Owner)
                    {
                        widget.Page._document = Owner;
                        Debug.WriteLine(string.Format("Fixed owner of Field {0}", FullyQualifiedName));
                    }
                    var possibleResources = new[]
                    {
                        _document.AcroForm.Elements.GetDictionary(PdfAcroForm.Keys.DR),
                        Elements.GetDictionary(PdfAcroForm.Keys.DR)
                    };
                    foreach (var resources in possibleResources)
                    {
                        if (!string.IsNullOrEmpty(ContentFontName) && resources != null && resources.Elements.ContainsKey(PdfResources.Keys.Font))
                        {
                            var fontKey = ContentFontName;
                            var fontList = resources.Elements.GetDictionary(PdfResources.Keys.Font);
                            var fontRef = fontList.Elements.GetReference(fontKey);
                            if (fontRef != null)
                            {
                                if (!widget.Page.Resources.Elements.ContainsKey(PdfResources.Keys.Font))
                                {
                                    widget.Page.Resources.Elements.Add(PdfResources.Keys.Font, new PdfDictionary());
                                }
                                var fontDict = widget.Page.Resources.Elements.GetDictionary(PdfResources.Keys.Font);
                                if (fontDict != null && !fontDict.Elements.ContainsKey(fontKey))
                                    fontDict.Elements.Add(fontKey, fontRef);
                            }
                        }
                    }
                }
            }

            for (var i = 0; i < Annotations.Elements.Count; i++)
            {
                var widget = Annotations.Elements[i];
                var rect = widget.Rectangle;
                if (!rect.IsEmpty && (!widget.BackColor.IsEmpty || !widget.BorderColor.IsEmpty))
                {
                    using (var gfx = XGraphics.FromPdfPage(widget.Page))
                    {
                        gfx.TranslateTransform(rect.X1, widget.Page.Height.Point - rect.Y2);
                        if (widget.BackColor != XColor.Empty)
                            gfx.DrawRectangle(new XSolidBrush(widget.BackColor), rect.ToXRect() - rect.Location);
                        // Draw Border
                        if (!widget.BorderColor.IsEmpty)
                            gfx.DrawRectangle(new XPen(widget.BorderColor), rect.ToXRect() - rect.Location);
                    }
                }
                // Remove annotation
                if (widget.Parent != null)
                    widget.Parent.Remove(widget);
                if (widget.Page != null)
                    widget.Page.Annotations.Remove(widget);
            }

            if (HasChildFields)
            {
                for (var i = 0; i < Fields.Elements.Count; i++)
                {
                    var field = Fields[i];
                    field.Flatten();
                }
            }

            if (Reference != null)
                _document._irefTable.Remove(Reference);
        }

        /// <summary>
        /// Renders the contents of the supplied Stream to the Page at the position specified by the provided Rectangle
        /// </summary>
        /// <param name="page"></param>
        /// <param name="stream"></param>
        /// <param name="rect"></param>
        protected virtual void RenderContentStream(PdfPage page, PdfStream stream, PdfRectangle rect)
        {
            if (stream == null || rect.IsEmpty)
                return;
            var content = ContentReader.ReadContent(stream.UnfilteredValue);
            var matrix = new XMatrix();
            matrix.TranslateAppend(rect.X1, rect.Y1);
            var matElements = matrix.GetElements();
            var matrixOp = OpCodes.OperatorFromName("cm");
            foreach (var el in matElements)
                matrixOp.Operands.Add(new CReal { Value = el });
            content.Insert(0, matrixOp);

            // Save and restore Graphics state
            content.Insert(0, OpCodes.OperatorFromName("q"));
            content.Add(OpCodes.OperatorFromName("Q"));
            var appendedContent = page.Contents.AppendContent();
            using (var ms = new MemoryStream())
            {
                var cw = new ContentWriter(ms);
                foreach (var obj in content)
                    obj.WriteObject(cw);
                appendedContent.CreateStream(ms.ToArray());
            }
        }

        /// <summary>
        /// Holds the collection of WidgetAnnotations for a field
        /// </summary>
        public sealed class PdfAnnotationArray
        {
            private readonly List<PdfWidgetAnnotation> elements = new List<PdfWidgetAnnotation>();

            /// <summary>
            /// Gets the list of <see cref="PdfWidgetAnnotation"/> of the array
            /// </summary>
            public List<PdfWidgetAnnotation> Elements
            {
                get { return elements; }
            }
        }

        /// <summary>
        /// Holds a collection of interactive fields.
        /// </summary>
        public sealed class PdfAcroFieldCollection : PdfArray
        {
            PdfAcroFieldCollection(PdfArray array)
                : base(array)
            { }

            PdfAcroFieldCollection(PdfDocument document)
              : base(document)
            { }


            /// <summary>
            /// Gets the names of all fields in the collection.
            /// </summary>
            public string[] Names
            {
                get
                {
                    int count = Elements.Count;
                    string[] names = new string[count];
                    for (int idx = 0; idx < count; idx++)
                        names[idx] = ((PdfDictionary)((PdfReference)Elements[idx]).Value).Elements.GetString(Keys.T);
                    return names;
                }
            }

            /// <summary>
            /// Gets an array of all descendant names.
            /// </summary>
            public string[] DescendantNames
            {
                get
                {
                    List<string> names = new List<string>();
                    GetDescendantNames(ref names, null);
                    //List<string> temp = new List<string>();
                    //foreach (PdfName name in names)
                    //  temp.Add(name.ToString());
                    return names.ToArray();
                }
            }

            internal void GetDescendantNames(ref List<string> names, string partialName)
            {
                int count = Elements.Count;
                for (int idx = 0; idx < count; idx++)
                {
                    PdfAcroField field = this[idx];
                    Debug.Assert(field != null);
                    if (field != null)
                        field.GetDescendantNames(ref names, partialName);
                }
            }

            /// <summary>
            /// Gets a field from the collection. For your convenience an instance of a derived class like
            /// PdfTextField or PdfCheckBox is returned if PDFsharp can guess the actual type of the dictionary.
            /// If the actual type cannot be guessed by PDFsharp the function returns an instance
            /// of PdfGenericField.
            /// </summary>
            public PdfAcroField this[int index]
            {
                get
                {
                    PdfItem item = Elements[index];
                    Debug.Assert(item is PdfReference);
                    PdfDictionary dict = ((PdfReference)item).Value as PdfDictionary;
                    Debug.Assert(dict != null);
                    PdfAcroField field = dict as PdfAcroField;
                    if (field == null && dict != null)
                    {
                        // Do type transformation
                        field = CreateAcroField(dict);
                        //Elements[index] = field.XRef;
                    }
                    return field;
                }
            }

            /// <summary>
            /// Gets the field with the specified name.
            /// </summary>
            public PdfAcroField this[string name]
            {
                get { return GetValue(name); }
            }

            internal PdfAcroField GetValue(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return null;

                int dot = name.IndexOf('.');
                string prefix = dot == -1 ? name : name.Substring(0, dot);
                string suffix = dot == -1 ? "" : name.Substring(dot + 1);

                int count = Elements.Count;
                for (int idx = 0; idx < count; idx++)
                {
                    PdfAcroField field = this[idx];
                    if (field.Name == prefix)
                        return field.GetValue(suffix);
                }
                return null;
            }

            /// <summary>
            /// Create a derived type like PdfTextField or PdfCheckBox if possible.
            /// If the actual cannot be guessed by PDFsharp the function returns an instance
            /// of PdfGenericField.
            /// </summary>
            internal static PdfAcroField CreateAcroField(PdfDictionary dict)
            {
                if (dict == null)
                    return null;
                string ft = dict.Elements.GetName(Keys.FT);
                var flags = (PdfAcroFieldFlags)dict.Elements.GetInteger(Keys.Ff);
                switch (ft)
                {
                    case "/Btn":
                        if ((flags & PdfAcroFieldFlags.Pushbutton) != 0)
                            return new PdfPushButtonField(dict);
                        return CheckBoxMaybeRadioButton(dict, flags);

                    case "/Tx":
                        return new PdfTextField(dict);

                    case "/Ch":
                        if ((flags & PdfAcroFieldFlags.Combo) != 0)
                            return new PdfComboBoxField(dict);
                        return new PdfListBoxField(dict);

                    case "/Sig":
                        return new PdfSignatureField(dict);

                    default:
                        return new PdfGenericField(dict);
                }
            }
        }

        internal static PdfAcroField CheckBoxMaybeRadioButton(PdfDictionary dict, PdfAcroFieldFlags flags)
        {
            if ((flags & PdfAcroFieldFlags.Radio) != 0)             // explicitly given type
                return new PdfRadioButtonField(dict);
            if ((flags & PdfAcroFieldFlags.RadiosInUnison) != 0)    // this flag is only valid for radio buttons
                return new PdfRadioButtonField(dict);

            var radio = new PdfRadioButtonField(dict);
            var values = radio.Options;
            var distinctValues = values.Distinct().ToList();
            // if the Annotations of this field have all different names, we consider this as a RadioButton
            if (distinctValues.Count == values.Count && values.Count > 1)
                return radio;
            return new PdfCheckBoxField(dict);
        }

        /// <summary>
        /// Predefined keys of this dictionary. 
        /// The description comes from PDF 1.4 Reference.
        /// </summary>
        public class Keys : KeysBase
        {
            // ReSharper disable InconsistentNaming

            /// <summary>
            /// (Required for terminal fields; inheritable) The type of field that this dictionary
            /// describes:
            ///   Btn           Button
            ///   Tx            Text
            ///   Ch            Choice
            ///   Sig (PDF 1.3) Signature
            /// Note: This entry may be present in a nonterminal field (one whose descendants
            /// are themselves fields) in order to provide an inheritable FT value. However, a
            /// nonterminal field does not logically have a type of its own; it is merely a container
            /// for inheritable attributes that are intended for descendant terminal fields of
            /// any type.
            /// </summary>
            [KeyInfo(KeyType.Name | KeyType.Required)]
            public const string FT = "/FT";

            /// <summary>
            /// (Required if this field is the child of another in the field hierarchy; absent otherwise)
            /// The field that is the immediate parent of this one (the field, if any, whose Kids array
            /// includes this field). A field can have at most one parent; that is, it can be included
            /// in the Kids array of at most one other field.
            /// </summary>
            [KeyInfo(KeyType.Dictionary)]
            public const string Parent = "/Parent";

            /// <summary>
            /// (Optional) An array of indirect references to the immediate children of this field.
            /// </summary>
            [KeyInfo(KeyType.Array | KeyType.Optional, typeof(PdfAcroFieldCollection))]
            public const string Kids = "/Kids";

            /// <summary>
            /// (Optional) The partial field name.
            /// </summary>
            [KeyInfo(KeyType.TextString | KeyType.Optional)]
            public const string T = "/T";

            /// <summary>
            /// (Optional; PDF 1.3) An alternate field name, to be used in place of the actual
            /// field name wherever the field must be identified in the user interface (such as
            /// in error or status messages referring to the field). This text is also useful
            /// when extracting the document�s contents in support of accessibility to disabled
            /// users or for other purposes.
            /// </summary>
            [KeyInfo(KeyType.TextString | KeyType.Optional)]
            public const string TU = "/TU";

            /// <summary>
            /// (Optional; PDF 1.3) The mapping name to be used when exporting interactive form field 
            /// data from the document.
            /// </summary>
            [KeyInfo(KeyType.TextString | KeyType.Optional)]
            public const string TM = "/TM";

            /// <summary>
            /// (Optional; inheritable) A set of flags specifying various characteristics of the field.
            /// Default value: 0.
            /// </summary>
            [KeyInfo(KeyType.Integer | KeyType.Optional)]
            public const string Ff = "/Ff";

            /// <summary>
            /// (Optional; inheritable) The field�s value, whose format varies depending on
            /// the field type; see the descriptions of individual field types for further information.
            /// </summary>
            [KeyInfo(KeyType.Various | KeyType.Optional)]
            public const string V = "/V";

            /// <summary>
            /// (Optional; inheritable) The default value to which the field reverts when a
            /// reset-form action is executed. The format of this value is the same as that of V.
            /// </summary>
            [KeyInfo(KeyType.Various | KeyType.Optional)]
            public const string DV = "/DV";

            /// <summary>
            /// (Optional; PDF 1.2) An additional-actions dictionary defining the field�s behavior
            /// in response to various trigger events. This entry has exactly the same meaning as
            /// the AA entry in an annotation dictionary.
            /// </summary>
            [KeyInfo(KeyType.Dictionary | KeyType.Optional)]
            public const string AA = "/AA";

            // ----- Additional entries to all fields containing variable text --------------------------

            /// <summary>
            /// (Required; inheritable) A resource dictionary containing default resources
            /// (such as fonts, patterns, or color spaces) to be used by the appearance stream.
            /// At a minimum, this dictionary must contain a Font entry specifying the resource
            /// name and font dictionary of the default font for displaying the field�s text.
            /// </summary>
            [KeyInfo(KeyType.Dictionary | KeyType.Required)]
            public const string DR = "/DR";

            /// <summary>
            /// (Required; inheritable) The default appearance string, containing a sequence of
            /// valid page-content graphics or text state operators defining such properties as
            /// the field�s text size and color.
            /// </summary>
            [KeyInfo(KeyType.String | KeyType.Required)]
            public const string DA = "/DA";

            /// <summary>
            /// (Optional; inheritable) A code specifying the form of quadding (justification)
            /// to be used in displaying the text:
            ///   0 Left-justified
            ///   1 Centered
            ///   2 Right-justified
            /// Default value: 0 (left-justified).
            /// </summary>
            [KeyInfo(KeyType.Integer | KeyType.Optional)]
            public const string Q = "/Q";

            /// <summary>
            /// (Optional) The type of PDF object that this dictionary describes; if present,
            /// must be Sig for a signature dictionary.
            /// </summary>
            [KeyInfo(KeyType.Name | KeyType.Optional)]
            public const string Type = "/Type";

            /// <summary>
            /// 
            /// </summary>
            [KeyInfo(KeyType.Name | KeyType.Required)]
            public const string Subtype = "/Subtype";


            /// <summary>
            /// 
            /// </summary>
            [KeyInfo(KeyType.Rectangle | KeyType.Required)]
            public const string Rect = "/Rect";


            [KeyInfo(KeyType.Rectangle | KeyType.Required)]
            public const string P = "/P";

            // ReSharper restore InconsistentNaming
        }
    }
}
