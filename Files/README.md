# Files — real fixtures for the integration suite

Every fixture comes from the upstream
[GroupDocs.Assembly-for-.NET](https://github.com/groupdocs-assembly/GroupDocs.Assembly-for-.NET) examples
repository (MIT), folder `Examples/Data/`, branch `master`. Files were renamed to short, role-based names
and are byte-identical to upstream. Total size is about 0.2 MB.

The test suite copies this folder into a fresh storage directory for each test class. The values the
tests look for in generated documents are constants in
[SampleDocuments.cs](../src/GroupDocs.Assembly.Mcp.Tests/Fixtures/SampleDocuments.cs); each one is text the
engine really produces from these files.

## Templates

| File | Upstream name | Tools exercised | Notes |
|---|---|---|---|
| `managers.docx` | `Templates/Data destination with nested elements.docx` | assemble (JSON, XML → PDF), fields, info | Nested `foreach [in managers]` / `foreach [in Contract]`, 7 tags |
| `persons.txt` | `Templates/Data destination with nested elements.txt` | assemble (CSV), per-record negative | Text template with a date format and `persons.Average(p => p.Age)` |
| `contracts-table.docx` | `Templates/Data table.docx` | assemble (table data), errors | `foreach [c in ds]` |
| `contracts-table.xlsx` | `Templates/Data table.xlsx` | assemble (table → workbook), fields, formats | Tags in worksheet cells |
| `contracts-table.pptx` | `Templates/Data table.pptx` | assemble (→ PDF and PPTX), fields | The foreach spans two paragraphs; needs libgdiplus on Linux |
| `contracts-table.msg` | `Templates/Data table.msg` | assemble with `dataSourceName`, fields, info, errors | Binary: tags cannot be read without the engine |
| `contracts-by-manager.docx` | `Templates/Table row.docx` | assemble, fields | `foreach [group in contracts.GroupBy(…).OrderBy(…)]` typed over three lines |
| `common-list.html` | `Templates/Common list.html` | assemble (JSON with `$id`/`$ref`) | Tags entity-encoded in the HTML |
| `product-readme.md` | `Templates/Readme.md` | assemble (Markdown → DOCX), errors | Two plain values: `product`, `description`. Renamed so it cannot collide with this README on case-insensitive file systems |
| `inline-error.docx` | `Templates/Inline error.docx` | assemble with `inlineErrors` | Deliberate syntax error `<< var [name]>>` |
| `photo.docx` | `Templates/Inserting image.docx` | assemble → HTML with an image folder | `<<image [expression]>>` |

## Data

| File | Upstream name | Notes |
|---|---|---|
| `Managers.json` | `DataSources/Managers.json` | JSON array; relaxed JSON (unquoted keys), which the engine reads |
| `Managers.xml` | `DataSources/Managers.xml` | The same data as XML |
| `Persons.csv` | `DataSources/Persons.csv` | `Name,Age,Birth`, three rows |
| `Contracts.xlsx` | `DataSources/Contracts.xlsx` | One table: `Client, Manager, Price`, ten rows |
| `Customers.json` | `DataSources/Customers.json` | Json.NET `$id`/`$ref` metadata; two customers |

Synthetic fixtures (`letter.md`, `letter.eml`, `unclosed.txt`, `mustache.txt`, `record.txt`, `records.json`)
are generated at test start by `SampleDocuments.WriteAll`.

## Refresh

```bash
B=https://raw.githubusercontent.com/groupdocs-assembly/GroupDocs.Assembly-for-.NET/master/Examples/Data
curl -fL -o managers.docx              "$B/Templates/Data%20destination%20with%20nested%20elements.docx"
curl -fL -o persons.txt                "$B/Templates/Data%20destination%20with%20nested%20elements.txt"
curl -fL -o contracts-table.docx       "$B/Templates/Data%20table.docx"
curl -fL -o contracts-table.xlsx       "$B/Templates/Data%20table.xlsx"
curl -fL -o contracts-table.pptx       "$B/Templates/Data%20table.pptx"
curl -fL -o contracts-table.msg        "$B/Templates/Data%20table.msg"
curl -fL -o contracts-by-manager.docx  "$B/Templates/Table%20row.docx"
curl -fL -o common-list.html           "$B/Templates/Common%20list.html"
curl -fL -o product-readme.md          "$B/Templates/Readme.md"
curl -fL -o inline-error.docx          "$B/Templates/Inline%20error.docx"
curl -fL -o photo.docx                 "$B/Templates/Inserting%20image.docx"
curl -fL -o Managers.json              "$B/DataSources/Managers.json"
curl -fL -o Managers.xml               "$B/DataSources/Managers.xml"
curl -fL -o Persons.csv                "$B/DataSources/Persons.csv"
curl -fL -o Contracts.xlsx             "$B/DataSources/Contracts.xlsx"
curl -fL -o Customers.json             "$B/DataSources/Customers.json"
```

After a refresh, re-run the suite: every value constant in `SampleDocuments.cs` must still be produced.
