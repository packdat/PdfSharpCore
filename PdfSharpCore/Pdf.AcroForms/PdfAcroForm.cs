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

using PdfSharpCore.Pdf.Advanced;
using System;

namespace PdfSharpCore.Pdf.AcroForms
{
    /// <summary>
    /// Represents a interactive form (or AcroForm), a collection of fields for 
    /// gathering information interactively from the user.
    /// </summary>
    public sealed class PdfAcroForm : PdfDictionary
    {
        /// <summary>
        /// Initializes a new instance of AcroForm.
        /// </summary>
        internal PdfAcroForm(PdfDocument document)
            : base(document)
        {
            _document = document;
        }

        internal PdfAcroForm(PdfDictionary dictionary)
            : base(dictionary)
        { }

        /// <summary>
        /// Gets the fields collection of this form.
        /// </summary>
        public PdfAcroField.PdfAcroFieldCollection Fields
        {
            get
            {
                if (_fields == null)
                {
                    object o = Elements.GetValue(Keys.Fields, VCF.CreateIndirect);
                    _fields = (PdfAcroField.PdfAcroFieldCollection)o;
                }
                return _fields;
            }
        }
        private PdfAcroField.PdfAcroFieldCollection _fields;

        internal PdfResources Resources
        {
            get
            {
                if (this.resources == null)
                    this.resources = (PdfResources)Elements.GetValue(PdfAcroForm.Keys.DR, VCF.None);
                return this.resources;
            }
        }
        PdfResources resources;

        /// <summary>
        /// Gets the <see cref="PdfResources"/> of this <see cref="PdfAcroForm"/> or creates a new one if none exist
        /// </summary>
        /// <returns>The <see cref="PdfResources"/> of this AcroForm</returns>
        internal PdfResources GetOrCreateResources()
        {
            var resources = Resources;
            if (resources == null)
                Elements.Add(Keys.DR, new PdfResources(_document));
            return Resources;
        }


        internal override void PrepareForSave()
        {
            // Need to create "Fields" Entry after importing fields from external documents
            if (_fields != null && _fields.Elements.Count > 0 && !Elements.ContainsKey(Keys.Fields))
            {
                Elements.Add(Keys.Fields, _fields);
            }
			// do not use the Fields-Property, as that may create new unwanted fields !
            var fieldsArray = Elements.GetArray(Keys.Fields);
            if (fieldsArray != null)
            {
                for (var i = 0; i < fieldsArray.Elements.Count; i++)
                {
                    var field = fieldsArray.Elements[i] as PdfReference;
                    if (field != null && field.Value != null)
                        field.Value.PrepareForSave();
                }
            }
            base.PrepareForSave();
        }

        /// <summary>
        /// Flattens the AcroForm by rendering Field-contents directly onto the page
        /// </summary>
        public void Flatten()
        {
            for (var i = 0; i < Fields.Elements.Count; i++)
            {
                var field = Fields[i];
                field.Flatten();
            }
            _document.Catalog.AcroForm = null;
        }

        /// <summary>
        /// Adds a new <see cref="PdfTextField"/> to the <see cref="PdfAcroForm"/>
        /// </summary>
        /// <param name="configure">
        /// A method that receives the new <see cref="PdfTextField"/> for further customization<br></br>
        /// It should return true to add the field to the Field-List of this AcroForm, otherwise false (e.g. when the field is a child of another field)
        /// </param>
        /// <returns>The created and configured <see cref="PdfTextField"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public PdfTextField AddTextField(Func<PdfTextField, bool> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));
            var field = new PdfTextField(_document);
            if (configure(field))
                Fields.Elements.Add(field);
            return field;
        }

        /// <summary>
        /// Adds a new <see cref="PdfCheckBoxField"/> to the <see cref="PdfAcroForm"/>
        /// </summary>
        /// <param name="configure">
        /// A method that receives the new <see cref="PdfCheckBoxField"/> for further customization<br></br>
        /// It should return true to add the field to the Field-List of this AcroForm, otherwise false (e.g. when the field is a child of another field)
        /// </param>
        /// <returns>The created and configured <see cref="PdfCheckBoxField"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public PdfCheckBoxField AddCheckBoxField(Func<PdfCheckBoxField, bool> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));
            var field = new PdfCheckBoxField(_document);
            if (configure(field))
                Fields.Elements.Add(field);
            return field;
        }

        /// <summary>
        /// Adds a new <see cref="PdfRadioButtonField"/> to the <see cref="PdfAcroForm"/>
        /// </summary>
        /// <param name="configure">
        /// A method that receives the new <see cref="PdfRadioButtonField"/> for further customization<br></br>
        /// It should return true to add the field to the Field-List of this AcroForm, otherwise false (e.g. when the field is a child of another field)
        /// </param>
        /// <returns>The created and configured <see cref="PdfRadioButtonField"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public PdfRadioButtonField AddRadioButtonField(Func<PdfRadioButtonField, bool> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));
            var field = new PdfRadioButtonField(_document);
            if (configure(field))
                Fields.Elements.Add(field);
            return field;
        }

        /// <summary>
        /// Adds a new <see cref="PdfComboBoxField"/> to the <see cref="PdfAcroForm"/>
        /// </summary>
        /// <param name="configure">
        /// A method that receives the new <see cref="PdfComboBoxField"/> for further customization<br></br>
        /// It should return true to add the field to the Field-List of this AcroForm, otherwise false (e.g. when the field is a child of another field)
        /// </param>
        /// <returns>The created and configured <see cref="PdfComboBoxField"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public PdfComboBoxField AddComboBoxField(Func<PdfComboBoxField, bool> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));
            var field = new PdfComboBoxField(_document);
            if (configure(field))
                Fields.Elements.Add(field);
            return field;
        }

        /// <summary>
        /// Adds a new <see cref="PdfListBoxField"/> to the <see cref="PdfAcroForm"/>
        /// </summary>
        /// <param name="configure">
        /// A method that receives the new <see cref="PdfListBoxField"/> for further customization<br></br>
        /// It should return true to add the field to the Field-List of this AcroForm, otherwise false (e.g. when the field is a child of another field)
        /// </param>
        /// <returns>The created and configured <see cref="PdfListBoxField"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public PdfListBoxField AddListBoxField(Func<PdfListBoxField, bool> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));
            var field = new PdfListBoxField(_document);
            if (configure(field))
                Fields.Elements.Add(field);
            return field;
        }

        /// <summary>
        /// Adds a new <see cref="PdfPushButtonField"/> to the <see cref="PdfAcroForm"/>
        /// </summary>
        /// <param name="configure">
        /// A method that receives the new <see cref="PdfPushButtonField"/> for further customization<br></br>
        /// It should return true to add the field to the Field-List of this AcroForm, otherwise false (e.g. when the field is a child of another field)
        /// </param>
        /// <returns>The created and configured <see cref="PdfPushButtonField"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public PdfPushButtonField AddPushButtonField(Func<PdfPushButtonField, bool> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));
            var field = new PdfPushButtonField(_document);
            if (configure(field))
                Fields.Elements.Add(field);
            return field;
        }

        /// <summary>
        /// Adds a new <see cref="PdfSignatureField"/> to the <see cref="PdfAcroForm"/>
        /// </summary>
        /// <param name="configure">
        /// A method that receives the new <see cref="PdfSignatureField"/> for further customization<br></br>
        /// It should return true to add the field to the Field-List of this AcroForm, otherwise false (e.g. when the field is a child of another field)
        /// </param>
        /// <returns>The created and configured <see cref="PdfSignatureField"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public PdfSignatureField AddSignatureField(Func<PdfSignatureField, bool> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));
            var field = new PdfSignatureField(_document);
            if (configure(field))
                Fields.Elements.Add(field);
            return field;
        }

        /// <summary>
        /// Adds a new <see cref="PdfGenericField"/> to the <see cref="PdfAcroForm"/><br></br>
        /// Typically used as a container for other fields
        /// </summary>
        /// <param name="configure">
        /// A method that receives the new <see cref="PdfGenericField"/> for further customization<br></br>
        /// It should return true to add the field to the Field-List of this AcroForm, otherwise false (e.g. when the field is a child of another field)
        /// </param>
        /// <returns>The created and configured <see cref="PdfGenericField"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public PdfGenericField AddGenericField(Func<PdfGenericField, bool> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));
            var field = new PdfGenericField(_document);
            if (configure(field))
                Fields.Elements.Add(field);
            return field;
        }


        /// <summary>
        /// Predefined keys of this dictionary. 
        /// The description comes from PDF 1.4 Reference.
        /// </summary>
        public sealed class Keys : KeysBase
        {
            // ReSharper disable InconsistentNaming

            /// <summary>
            /// (Required) An array of references to the documentís root fields (those with
            /// no ancestors in the field hierarchy).
            /// </summary>
            [KeyInfo(KeyType.Array | KeyType.Required, typeof(PdfAcroField.PdfAcroFieldCollection))]
            public const string Fields = "/Fields";

            /// <summary>
            /// (Optional) A flag specifying whether to construct appearance streams and
            /// appearance dictionaries for all widget annotations in the document.
            /// Default value: false.
            /// </summary>
            [KeyInfo(KeyType.Boolean | KeyType.Optional)]
            public const string NeedAppearances = "/NeedAppearances";

            /// <summary>
            /// (Optional; PDF 1.3) A set of flags specifying various document-level characteristics
            /// related to signature fields.
            /// Default value: 0.
            /// </summary>
            [KeyInfo("1.3", KeyType.Integer | KeyType.Optional)]
            public const string SigFlags = "/SigFlags";

            /// <summary>
            /// (Required if any fields in the document have additional-actions dictionaries
            /// containing a C entry; PDF 1.3) An array of indirect references to field dictionaries
            /// with calculation actions, defining the calculation order in which their values will 
            /// be recalculated when the value of any field changes.
            /// </summary>
            [KeyInfo(KeyType.Array)]
            public const string CO = "/CO";

            /// <summary>
            /// (Optional) A document-wide default value for the DR attribute of variable text fields.
            /// </summary>
            [KeyInfo(KeyType.Dictionary | KeyType.Optional, typeof(PdfResources))]
            public const string DR = "/DR";

            /// <summary>
            /// (Optional) A document-wide default value for the DA attribute of variable text fields.
            /// </summary>
            [KeyInfo(KeyType.String | KeyType.Optional)]
            public const string DA = "/DA";

            /// <summary>
            /// (Optional) A document-wide default value for the Q attribute of variable text fields.
            /// </summary>
            [KeyInfo(KeyType.Integer | KeyType.Optional)]
            public const string Q = "/Q";

            /// <summary>
            /// Gets the KeysMeta for these keys.
            /// </summary>
            internal static DictionaryMeta Meta
            {
                get
                {
                    if (s_meta == null)
                        s_meta = CreateMeta(typeof(Keys));
                    return s_meta;
                }
            }
            static DictionaryMeta s_meta;

            // ReSharper restore InconsistentNaming
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
