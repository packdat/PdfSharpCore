using FluentAssertions;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.AcroForms;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Test.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
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
                TraverseFields(field, ref fieldList);
            }
        }
    }
}
