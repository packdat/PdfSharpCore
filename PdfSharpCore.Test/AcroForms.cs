using FluentAssertions;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.AcroForms;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Test.Helpers;
using PdfSharpCore.Test.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace PdfSharpCore.Test
{
    public class AcroForms
    {
        private readonly ITestOutputHelper output;

        public AcroForms(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void CanFillExistingForm()
        {
            var files = new[] { "DocumentWithAcroForm.pdf", "DemoFormWithCombs.pdf" };
            foreach (var file in files)
            {
                var ms = new MemoryStream();
                using var fs = File.OpenRead(PathHelper.GetInstance().GetAssetPath(file));
                fs.CopyTo(ms);
                var inputDocument = Pdf.IO.PdfReader.Open(ms, PdfDocumentOpenMode.Modify);
                var fieldsInDocument = GetAllFields(inputDocument);

                // fill all fields
                FillFields(fieldsInDocument);

                var outFileName = Path.ChangeExtension(file, "filled.pdf");
                using var fsOut = File.Create(PathHelper.GetInstance().GetAssetPath(outFileName));
                inputDocument.Save(fsOut);
            }
        }

        [Fact]
        public void CanImportForm()
        {
            using var fs = File.OpenRead(PathHelper.GetInstance().GetAssetPath("DocumentWithAcroForm.pdf"));
            var inputDocument = Pdf.IO.PdfReader.Open(fs, PdfDocumentOpenMode.Import);

            // import into new document
            var copiedDocument = new PdfDocument();
            foreach (var page in inputDocument.Pages)
                copiedDocument.AddPage(page, AnnotationCopyingType.ShallowCopy);

            var fieldsInInputDocument = GetAllFields(inputDocument);
            var fieldsInCopiedDocument = GetAllFields(copiedDocument);
            fieldsInCopiedDocument.Count.Should().Be(fieldsInInputDocument.Count);

            // fill all fields
            FillFields(fieldsInCopiedDocument);

            using var fsOut = File.Create(PathHelper.GetInstance().GetAssetPath("FilledForm.pdf"));
            copiedDocument.Save(fsOut);
        }

        [Fact]
        public void CanImportMultipleForms()
        {
            var files = new[] { "DocumentWithAcroForm.pdf", "DemoFormWithCombs.pdf" };
            var copiedDocument = new PdfDocument();
            var importedFields = new List<PdfAcroField>();
            foreach (var file in files)
            {
                using var fs = File.OpenRead(PathHelper.GetInstance().GetAssetPath(file));
                var inputDocument = Pdf.IO.PdfReader.Open(fs, PdfDocumentOpenMode.Import);
                foreach (var page in inputDocument.Pages)
                    copiedDocument.AddPage(page, AnnotationCopyingType.ShallowCopy);
                importedFields.AddRange(GetAllFields(inputDocument));
            }
            var fieldsInCopiedDocument = GetAllFields(copiedDocument);
            fieldsInCopiedDocument.Count.Should().Be(importedFields.Count);

            FillFields(fieldsInCopiedDocument);

            using var fsOut = File.Create(PathHelper.GetInstance().GetAssetPath("FilledForm.multiple.pdf"));
            copiedDocument.Save(fsOut);
        }

        [Fact]
        public void CanFlattenForm()
        {
            using var fs = File.OpenRead(PathHelper.GetInstance().GetAssetPath("DocumentWithAcroForm.pdf"));
            var inputDocument = Pdf.IO.PdfReader.Open(fs, PdfDocumentOpenMode.Import);

            // import into new document
            var copiedDocument = new PdfDocument();
            foreach (var page in inputDocument.Pages)
                copiedDocument.AddPage(page, AnnotationCopyingType.ShallowCopy);

            var fieldsInCopiedDocument = GetAllFields(copiedDocument);
            // fill all fields
            FillFields(fieldsInCopiedDocument);

            // flatten the form. after that, AcroForm should be null and all annotations should be removed
            // (this is true for the tested document, other documents may contain annotations not related to Form-Fields)
            copiedDocument.AcroForm.Flatten();
            copiedDocument.AcroForm.Should().BeNull();
            copiedDocument.Pages[0].Annotations.Count.Should().Be(0);

            using var fsOut = File.Create(PathHelper.GetInstance().GetAssetPath("FilledForm.flattened.pdf"));
            copiedDocument.Save(fsOut);
        }

        [Fact]
        public void CanCreateNewForm()
        {
            var document = new PdfDocument();
            var page1 = document.AddPage();
            var page2 = document.AddPage();
            var acroForm = document.GetOrCreateAcroForm();
            var textFont = new XFont("Arial", 12);
            double x = 40, y = 80;
            var page1Renderer = XGraphics.FromPdfPage(page1);
            var page2Renderer = XGraphics.FromPdfPage(page2);
            page1Renderer.DrawString("Name of Subject", textFont, XBrushes.Black, x, y);
            page2Renderer.DrawString("For Demo purposes. Modify the fields and observe the field on the first page is modified as well.",
                textFont, XBrushes.Black, x, y);
            
            y += 10;
            // Text fields
            var firstNameField = acroForm.AddTextField(field =>
            {
                field.Name = "FirstName";
                field.Font = textFont;
                field.Value = new PdfString("John");
                // place annotation on both pages
                // if the document is opened in a PdfReader and one of the Annotations is changed (e.g. by typing inside it),
                // the other Annotation will be changed as well (as they belong to the same field)
                field.AddAnnotation(annot =>
                {
                    annot.PlaceOnPage(page1, new PdfRectangle(new XRect(x, y, 100, 20)));
                });
                field.AddAnnotation(annot =>
                {
                    annot.PlaceOnPage(page2, new PdfRectangle(new XRect(x, y, 100, 20)));
                });
                return true;
            });
            var lastNameField = acroForm.AddTextField(field =>
            {
                field.Name = "LastName";
                field.Font = textFont;
                field.Value = new PdfString("Doe");
                field.AddAnnotation(annot =>
                {
                    annot.PlaceOnPage(page1, new PdfRectangle(new XRect(x + 10 + 100, y, 100, 20)));
                });
                field.AddAnnotation(annot =>
                {
                    annot.PlaceOnPage(page2, new PdfRectangle(new XRect(x + 10 + 100, y, 100, 20)));
                });
                return true;
            });
            
            y += 40;
            // Checkbox fields
            page1Renderer.DrawString("Subject's interests", textFont, XBrushes.Black, x, y);
            y += 10;
            var cbx1 = acroForm.AddCheckBoxField(field =>
            {
                field.Name = "Interest_cooking";
                field.AddAnnotation(annot =>
                {
                    annot.PlaceOnPage(page1, new PdfRectangle(new XRect(x, y, 12, 12)));
                });
                return true;
            });
            page1Renderer.DrawString("Cooking", textFont, XBrushes.Black, x + 20, y + 10);
            y += 20;
            var cbx2 = acroForm.AddCheckBoxField(field =>
            {
                field.Name = "Interest_coding";
                field.AddAnnotation(annot =>
                {
                    annot.PlaceOnPage(page1, new PdfRectangle(new XRect(x, y, 12, 12)));
                });
                // must be set AFTER adding the annotations !
                field.Checked = true;
                return true;
            });
            page1Renderer.DrawString("Coding", textFont, XBrushes.Black, x + 20, y + 10);
            y += 20;
            var cbx3 = acroForm.AddCheckBoxField(field =>
            {
                field.Name = "Interest_cycling";
                field.AddAnnotation(annot =>
                {
                    annot.PlaceOnPage(page1, new PdfRectangle(new XRect(x, y, 12, 12)));
                });
                return true;
            });
            page1Renderer.DrawString("Cycling", textFont, XBrushes.Black, x + 20, y + 10);
            
            y += 40;
            // RadioButton fields
            page1Renderer.DrawString("Subject's gender", textFont, XBrushes.Black, x, y);
            y += 10;
            // used as parent-field for the radio-button (testing field-nesting)
            var groupGender = acroForm.AddGenericField(field =>
            {
                field.Name = "Group_Gender";
                return true;
            });
            acroForm.AddRadioButtonField(field =>
            {
                field.Name = "Gender";
                // add individual buttons
                field.AddAnnotation("male", annot =>
                {
                    annot.PlaceOnPage(page1, new PdfRectangle(new XRect(x, y, 12, 12)));
                });
                page1Renderer.DrawString("Male", textFont, XBrushes.Black, x + 20, y + 10);
                y += 20;
                field.AddAnnotation("female", annot =>
                {
                    annot.PlaceOnPage(page1, new PdfRectangle(new XRect(x, y, 12, 12)));
                });
                page1Renderer.DrawString("Female", textFont, XBrushes.Black, x + 20, y + 10);
                y += 20;
                field.AddAnnotation("unspecified", annot =>
                {
                    annot.PlaceOnPage(page1, new PdfRectangle(new XRect(x, y, 12, 12)));
                });
                page1Renderer.DrawString("Unspecified", textFont, XBrushes.Black, x + 20, y + 10);
                // must be a name-object ! (starting with a slash)
                field.Value = "/male";
                groupGender.AddChild(field);
                return false;   // this is not a top-level field but a child of another field
            });

            y += 40;
            // ComboBox fields
            page1Renderer.DrawString("Select a number:", textFont, XBrushes.Black, x, y + 10);
            acroForm.AddComboBoxField(field =>
            {
                field.Name = "SelectedNumber";
                field.Options = new[] { "One", "Two", "Three", "Four", "Five" };
                field.SelectedIndex = 2;    // select "Three"
                field.Font = textFont;
                field.AddAnnotation(annot =>
                {
                    annot.PlaceOnPage(page1, new PdfRectangle(new XRect(x + 100, y, 100, 16)));
                });
                return true;
            });

            y += 40;
            // ListBox fields
            page1Renderer.DrawString("Select a color:", textFont, XBrushes.Black, x, y + 10);
            acroForm.AddListBoxField(field =>
            {
                field.Name = "SelectedColor";
                field.Options = new[] { "Blue", "Red", "Green", "Black", "White" };
                field.SelectedIndices = new[] { 1 };    // select "Red"
                field.Font = textFont;
                field.AddAnnotation(annot =>
                {
                    annot.PlaceOnPage(page1, new PdfRectangle(new XRect(x + 100, y, 100, 80)));
                });
                return true;
            });
            // TODO: Signature fields

            var filePath = PathHelper.GetInstance().GetAssetPath("CreatedForm.pdf");
            using var fsOut = File.Create(filePath);
            document.Save(fsOut, true);

            // read back and validate
            document = Pdf.IO.PdfReader.Open(filePath, PdfDocumentOpenMode.Modify);
            var fields = GetAllFields(document);

            fields.Count.Should().Be(9);
            fields.Should().Contain(field => 
                field.FullyQualifiedName == "FirstName"
                && field.GetType() == typeof(PdfTextField)
                && ((PdfTextField)field).Text == "John");
            fields.Should().Contain(field => 
                field.FullyQualifiedName == "LastName"
                && field.GetType() == typeof(PdfTextField)
                && ((PdfTextField)field).Text == "Doe");
            fields.Should().Contain(field =>
                field.FullyQualifiedName == "Interest_cooking"
                && field.GetType() == typeof(PdfCheckBoxField)
                && !((PdfCheckBoxField)field).Checked);
            fields.Should().Contain(field =>
                field.FullyQualifiedName == "Interest_coding"
                && field.GetType() == typeof(PdfCheckBoxField)
                && ((PdfCheckBoxField)field).Checked);
            fields.Should().Contain(field =>
                field.FullyQualifiedName == "Interest_cycling"
                && field.GetType() == typeof(PdfCheckBoxField)
                && !((PdfCheckBoxField)field).Checked);
            fields.Should().Contain(field =>
                field.FullyQualifiedName == "Group_Gender"
                && field.GetType() == typeof(PdfGenericField)
                && field.HasChildFields
                && field.Fields.Elements.Count == 1);
            fields.Should().Contain(field =>
                field.FullyQualifiedName == "Group_Gender.Gender"
                && field.GetType() == typeof(PdfRadioButtonField)
                && ((PdfRadioButtonField)field).SelectedIndex == 0
                && field.Annotations.Elements.Count == 3
                && ((PdfRadioButtonField)field).Options.SequenceEqual(new[] { "/male", "/female", "/unspecified" })
                && field.Value.ToString() == "/male");
            fields.Should().Contain(field =>
                field.FullyQualifiedName == "SelectedNumber"
                && field.GetType() == typeof(PdfComboBoxField)
                && ((PdfComboBoxField)field).SelectedIndex == 2
                && ((PdfComboBoxField)field).Options.SequenceEqual(new[] { "One", "Two", "Three", "Four", "Five" })
                && field.Value is PdfString
                && ((PdfString)field.Value).Value == "Three");
            fields.Should().Contain(field =>
                field.FullyQualifiedName == "SelectedColor"
                && field.GetType() == typeof(PdfListBoxField)
                && ((PdfListBoxField)field).SelectedIndices.Count() == 1
                && ((PdfListBoxField)field).SelectedIndices.Contains(1)
                && ((PdfListBoxField)field).Options.SequenceEqual(new[] { "Blue", "Red", "Green", "Black", "White" })
                && ((PdfString)((PdfListBoxField)field).Value).Value == "Red");
        }

        private static void FillFields(IList<PdfAcroField> fields)
        {
            foreach (var field in fields)
            {
                if (field.ReadOnly)
                    continue;
                // Values for the fields:
                // - TextFields: name of field
                // - CheckBoxes: checked
                // - RadioButtons: second option is checked
                // - ChoiceFields (List, Combo): second option is selected
                if (field is PdfTextField textField)
                    textField.Text = field.Name;
                else if (field is PdfComboBoxField comboBoxField)
                    comboBoxField.SelectedIndex = Math.Min(1, comboBoxField.Options.Count);
                else if (field is PdfCheckBoxField checkboxField)
                    checkboxField.Checked = true;
                else if (field is PdfRadioButtonField radioButtonField)
                    radioButtonField.SelectedIndex = Math.Min(1, radioButtonField.Options.Count);
                else if (field is PdfListBoxField listBoxField)
                    listBoxField.SelectedIndices = new[] { Math.Min(1, listBoxField.Options.Count) };
            }
        }

        private static IList<PdfAcroField> GetAllFields(PdfDocument doc)
        {
            var fields = new List<PdfAcroField>();
            for (var i = 0; i < doc.AcroForm.Fields.Elements.Count; i++)
            {
                var field = doc.AcroForm.Fields[i];
                TraverseFields(field, ref fields);
            }
            return fields;
        }

        private static void TraverseFields(PdfAcroField parentField, ref List<PdfAcroField> fieldList)
        {
            fieldList.Add(parentField);
            for (var i = 0; i < parentField.Fields.Elements.Count; i++)
            {
                var field = parentField.Fields[i];
                if (!string.IsNullOrEmpty(field.Name))
                    TraverseFields(field, ref fieldList);
            }
        }
    }
}
