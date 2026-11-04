using System.Text;

namespace GroupDocs.Assembly.Mcp.IntegrationTests.Fixtures;

/// Fixture documents for the suite.
///
/// Real templates and data come from the repo's `Files/` folder (sourced from the upstream
/// GroupDocs.Assembly-for-.NET examples; provenance in Files/README.md). Synthetic templates are
/// generated at startup so an assertion can rely on tags the test itself wrote.
///
/// Every phrase constant below is text the engine really produces from the named template and data
/// (measured 2026-10-30) - the suite's "test-correctness" rule: a fixture must exercise what the
/// assertion depends on, or the test can pass while the tool is broken.
internal static class SampleDocuments
{
    // ---- Real templates (Files/) --------------------------------------------------------

    /// Nested foreach over managers and their contracts. Upstream: "Data destination with nested elements.docx".
    public const string ManagersDocx = "managers.docx";
    public const string ManagersName = "managers";
    public const int ManagersTags = 7;

    /// The same report as plain text, fed by CSV. Upstream: "Data destination with nested elements.txt".
    public const string PersonsTxt = "persons.txt";

    /// One table row per contract, in three families. Upstream: "Data table.docx/.xlsx/.pptx/.msg".
    /// The PPTX's foreach spans two paragraphs; the MSG is binary (tags unreadable without the engine).
    public const string ContractsDocx = "contracts-table.docx";
    public const string ContractsXlsx = "contracts-table.xlsx";
    public const string ContractsPptx = "contracts-table.pptx";
    public const string ContractsMsg = "contracts-table.msg";
    public const string ContractsName = "ds";

    /// Contracts grouped by manager, the foreach typed over three lines. Upstream: "Table row.docx".
    public const string ContractsByManagerDocx = "contracts-by-manager.docx";
    public const string ContractsByManagerName = "contracts";

    /// HTML list of customers, tags entity-encoded. Upstream: "Common list.html".
    public const string CommonListHtml = "common-list.html";

    /// Markdown with two plain values, product and description. Upstream: "Readme.md".
    public const string ReadmeMd = "product-readme.md";

    /// A template with a deliberate syntax error ("<< var [name]>>"). Upstream: "Inline error.docx".
    public const string InlineErrorDocx = "inline-error.docx";

    /// Two <<image [expression]>> tags. Upstream: "Inserting image.docx".
    public const string PhotoDocx = "photo.docx";

    // ---- Real data (Files/) ---------------------------------------------------------------

    /// A JSON array of managers with contracts (relaxed JSON: unquoted keys). Upstream: Managers.json.
    public const string ManagersJson = "Managers.json";
    /// The same data as XML. Upstream: Managers.xml.
    public const string ManagersXml = "Managers.xml";
    public const string Manager1 = "John Smith";
    public const string Manager3 = "July James";
    public const string Client1 = "A Company";
    public const string Client10 = "J Ent.";

    /// Name, Age, Birth for three people. Upstream: Persons.csv.
    public const string PersonsCsv = "Persons.csv";
    public const string PersonsName = "persons";
    public const string Person1Line = "Name: John Doe, Age: 30";
    public const string PersonsAverage = "Average age: 36";

    /// One worksheet table: Client, Manager, Price (10 rows). Upstream: Contracts.xlsx.
    public const string ContractsData = "Contracts.xlsx";
    public static readonly string[] ContractsColumns = ["Client", "Manager", "Price"];

    /// Customers with Json.NET "$id"/"$ref" metadata. Upstream: Customers.json.
    public const string CustomersJson = "Customers.json";
    public const string Customer1 = "Ali Ahmed";
    public const string Customer2 = "Atir Tahir";

    public static IReadOnlyList<string> RealSamples { get; } = new[]
    {
        ManagersDocx, PersonsTxt, ContractsDocx, ContractsXlsx, ContractsPptx, ContractsMsg, ContractsByManagerDocx,
        CommonListHtml, ReadmeMd, InlineErrorDocx, PhotoDocx,
        ManagersJson, ManagersXml, PersonsCsv, ContractsData, CustomersJson,
    };

    // ---- Synthetic samples (generated) ------------------------------------------------------

    /// A letter whose tags the tests control: two named objects, a list, a number format and a code
    /// with leading zeros (which must survive: the server reads JSON in Strict mode).
    public const string LetterMd = "letter.md";
    public const string LetterTemplate =
        "Dear <<[customer.Name]>>,\n\nYour order <<[order.Number]>> of <<[order.Total]:\"N2\">> is ready.\n" +
        "<<foreach [i in order.Items]>>- <<[i.Title]>> x <<[i.Qty]>>\n<</foreach>>";
    public const string LetterData =
        """{"customer":{"Name":"Ann Lee"},"order":{"Number":"007","Total":1234.5,"Items":[{"Title":"Desk","Qty":1},{"Title":"Lamp","Qty":2}]}}""";

    /// A plain-text e-mail template: subject and body tags. Its plain-text body keeps the data even in
    /// evaluation mode, which replaces an e-mail's HTML body with the evaluation line (measured).
    public const string LetterEml = "letter.eml";

    /// A template that never closes its foreach.
    public const string UnclosedTxt = "unclosed.txt";

    /// A template written in the {{…}} syntax GroupDocs.Assembly does not process.
    public const string MustacheTxt = "mustache.txt";

    /// Records for per-record assembly: three customers, file names from "Code".
    public const string RecordsJson = "records.json";
    public const string RecordsData =
        """{"title":"Batch","customers":[{"Code":"C-001","Name":"Ann"},{"Code":"C-002","Name":"Bob"},{"Code":"C-003","Name":"Cy"}]}""";
    public const string RecordTemplateTxt = "record.txt";

    /// A 1x1 PNG, base64: image tags accept base64 image data, which works on every channel
    /// (a file path would differ between the host and the container).
    public const string PixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    public static void WriteAll(string directory)
    {
        Directory.CreateDirectory(directory);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(Path.Combine(directory, LetterMd), LetterTemplate, utf8);
        File.WriteAllText(Path.Combine(directory, LetterEml),
            "From: shop@example.com\r\nTo: customer@example.com\r\nSubject: Hello <<[customer.Name]>>\r\nMIME-Version: 1.0\r\n" +
            "Content-Type: text/plain; charset=utf-8\r\n\r\nDear <<[customer.Name]>>, your code is <<[customer.Code]>>.\r\n", utf8);
        File.WriteAllText(Path.Combine(directory, UnclosedTxt), "Items: <<foreach [i in items]>><<[i.Title]>>", utf8);
        File.WriteAllText(Path.Combine(directory, MustacheTxt), "Hello, {{Name}}! Today is {{Now:dd MMM yyyy}}.", utf8);
        File.WriteAllText(Path.Combine(directory, RecordsJson), RecordsData, utf8);
        File.WriteAllText(Path.Combine(directory, RecordTemplateTxt), "Customer <<[Code]>>: <<[Name]>>", utf8);
    }

    /// Copies real samples into the test storage directory. A sample missing from the source is
    /// a hard failure, not a skip: a skipped test counted as a pass is how untested surface ships.
    public static void CopyRealSamples(string targetDirectory, string? sourceDirectory)
    {
        if (string.IsNullOrEmpty(sourceDirectory) || !Directory.Exists(sourceDirectory))
            throw new InvalidOperationException(
                "Real fixtures not found. Expected the repo's Files/ folder next to the test " +
                "assembly (copied by the csproj) or GROUPDOCS_MCP_SAMPLE_DOCS pointing at it.");

        Directory.CreateDirectory(targetDirectory);
        var missing = new List<string>();
        foreach (var name in RealSamples)
        {
            var src = Path.Combine(sourceDirectory, name);
            if (File.Exists(src))
                File.Copy(src, Path.Combine(targetDirectory, name), overwrite: true);
            else
                missing.Add(name);
        }

        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Missing fixtures in '{sourceDirectory}': {string.Join(", ", missing)}. See Files/README.md.");
    }

    /// Resolves the folder holding the real fixtures. Order:
    ///   1. GROUPDOCS_MCP_SAMPLE_DOCS env var (set by docker-compose mounts).
    ///   2. `Files/` next to the test assembly - populated by the csproj copy item.
    ///   3. Walk up from the assembly to find the repo's `Files/`.
    public static string? ResolveSourceSampleDocs()
    {
        var env = Environment.GetEnvironmentVariable("GROUPDOCS_MCP_SAMPLE_DOCS");
        if (!string.IsNullOrEmpty(env) && Directory.Exists(env))
            return env;

        var staged = Path.Combine(AppContext.BaseDirectory, "Files");
        if (Directory.Exists(staged))
            return staged;

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, "Files");
            if (File.Exists(Path.Combine(candidate, ManagersDocx)))
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }
}
