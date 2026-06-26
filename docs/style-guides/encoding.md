# Console / CLI output encoding

Terminal output must be **encoding-safe**: stick to ASCII, or explicitly force
UTF-8 stdout. A Windows console defaults to a legacy code page (cp1252), so a
stray em-dash or smart quote in output mojibakes — and in Python it throws
`UnicodeEncodeError`. (This bit the bootstrap installer once; fixed in `d9ac4a1`.)

This rule is about **terminal output only**. Markdown, comments, and other prose
may use rendered Unicode (em-dashes, arrows, …) freely — they are never written to
a console.

## Do

- Keep `Console.Write*` and script `print` output to **printable ASCII**
  (U+0020–U+007E). Write `-` not `—`, `->` not `→`, `"` not `"` `"`.
- If you genuinely need non-ASCII output, **force the encoding first**:

  ```csharp
  Console.OutputEncoding = System.Text.Encoding.UTF8;
  ```

  The CLI sets `InvariantGlobalization=true` (AOT-friendly, culture-neutral). That
  keeps formatting predictable but does **not** change the console code page — force
  UTF-8 explicitly if you emit non-ASCII.

## Enforced

`scripts/dev/check-encoding.cs` (Husky pre-commit + CI) fails on non-ASCII bytes in
the tooling scripts (`scripts/`) and the CLI (`src/ParagonStats.Cli/`). Docs and
other prose are not scanned.
