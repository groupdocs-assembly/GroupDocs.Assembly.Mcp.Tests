using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;

/// The visible text of a generated document, so an assertion can check the data really landed in it.
/// Office Open XML is read from its parts (no engine on the test side); text formats are read as-is.
internal static class OutputText
{
    public static string Read(string path)
    {
        var ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "docx" or "docm" or "dotx" => Zip(path, @"^word/(document|header\d*|footer\d*)\.xml$"),
            "xlsx" or "xlsm" => Zip(path, @"^xl/(sharedStrings|worksheets/sheet\d+)\.xml$"),
            "pptx" => Zip(path, @"^ppt/slides/slide\d+\.xml$"),
            "html" or "htm" => WebUtility.HtmlDecode(Regex.Replace(File.ReadAllText(path), "<[^>]+>", " ")),
            _ => File.ReadAllText(path)
        };
    }

    /// Decodes the quoted-printable body parts of a MIME message ("=2E" → ".", soft line breaks joined).
    public static string DecodeQuotedPrintable(string message)
    {
        var joined = Regex.Replace(message, "=\r?\n", string.Empty);
        var bytes = new List<byte>();
        var sb = new StringBuilder();
        for (var i = 0; i < joined.Length; i++)
        {
            if (joined[i] == '=' && i + 2 < joined.Length && Uri.IsHexDigit(joined[i + 1]) && Uri.IsHexDigit(joined[i + 2]))
            {
                bytes.Add(Convert.ToByte(joined.Substring(i + 1, 2), 16));
                i += 2;
                continue;
            }
            if (bytes.Count > 0) { sb.Append(Encoding.UTF8.GetString(bytes.ToArray())); bytes.Clear(); }
            sb.Append(joined[i]);
        }
        if (bytes.Count > 0) sb.Append(Encoding.UTF8.GetString(bytes.ToArray()));
        return sb.ToString();
    }

    /// True when the file starts with the PDF signature.
    public static bool IsPdf(string path)
    {
        var head = new byte[5];
        using var fs = File.OpenRead(path);
        return fs.Read(head, 0, 5) == 5 && Encoding.ASCII.GetString(head) == "%PDF-";
    }

    /// The number of worksheets in an XLSX.
    public static int Worksheets(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        return zip.Entries.Count(e => Regex.IsMatch(e.FullName, @"^xl/worksheets/sheet\d+\.xml$"));
    }

    private static string Zip(string path, string pattern)
    {
        using var zip = ZipFile.OpenRead(path);
        var sb = new StringBuilder();
        foreach (var entry in zip.Entries.Where(e => Regex.IsMatch(e.FullName, pattern)).OrderBy(e => e.FullName))
        {
            using var reader = new StreamReader(entry.Open());
            var xml = reader.ReadToEnd();
            // Paragraph and cell ends become spaces so words from neighbouring runs do not fuse.
            xml = Regex.Replace(xml, "</(w:p|a:p|si|c)>", " ");
            sb.Append(WebUtility.HtmlDecode(Regex.Replace(xml, "<[^>]+>", string.Empty))).Append('\n');
        }
        return Regex.Replace(sb.ToString(), @"[ \t]+", " ");
    }
}
