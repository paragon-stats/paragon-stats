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
- The CLI sets `InvariantGlobalization=true` (AOT-friendly, culture-neutral). That
  keeps formatting predictable but does **not** change the console code page.
- If a tool ever must emit non-ASCII, force the stream first
  (`Console.OutputEncoding = System.Text.Encoding.UTF8;`). The check below can't
  detect a forced stream, so it enforces plain ASCII in the scanned paths — treat
  non-ASCII output there as a deliberate exception (and adjust the check for it).

## Enforced

`scripts/dev/check-encoding.cs` (Husky pre-commit + CI) fails on any non-ASCII
**character** in the tooling scripts (`scripts/`) and the CLI
(`src/ParagonStats.Cli/`). A leading UTF-8 BOM is ignored. Docs and other prose are
not scanned.
