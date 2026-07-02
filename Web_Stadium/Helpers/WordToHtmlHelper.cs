using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;
using System.Text.RegularExpressions;

namespace Web_Stadium.Helpers
{
    public static class WordToHtmlHelper
    {
        public static string ConvertDocxToHtml(string filePath)
        {
            using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, false))
            {
                StringBuilder html = new StringBuilder();
                var body = doc.MainDocumentPart.Document.Body;
                foreach (var para in body.Elements<Paragraph>())
                {
                    html.Append("<p style='margin:0 0 10px;'>");
                    foreach (var run in para.Elements<Run>())
                    {
                        var text = run.GetFirstChild<Text>()?.Text ?? "";
                        text = Regex.Replace(text, @"\s+", " ");
                        html.Append(text);
                    }
                    html.Append("</p>");
                }
                return html.ToString();
            }
        }
    }
}