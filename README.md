# TcFormatter

Code-Formatter und Namensprüfung für **TwinCAT 3 Structured Text**, als Extension für die
**TcXaeShell 4024 und 4026** und als Kommandozeilenwerkzeug `tcfmt`.

- Formatiert einen **einzelnen Baustein**, einen **Ordner** oder ein **ganzes PLC-Projekt**: POUs, Methoden, Properties, Aktionen, Transitionen, DUTs, GVLs und Interfaces.
- Bietet **über 50 Einstellungen** (vergleichbar mit STweep oder Black). Sie werden über *Extras → Optionen → TcFormatter* oder eine `.tcformat.json` im Projekt gesetzt.
- Prüft Namen mit **einstellbaren Präfixen pro Datentyp**, Variablenbereich und Objektart. Standard ist die Beckhoff-Hungarian-Notation.
- **Formatiert sicher wie Black:** Das Ergebnis muss dieselben Tokens enthalten wie das Original, sonst bleibt der Code unverändert. Ein zweiter Lauf ändert nichts mehr (idempotent).
- Bereiche zwischen `// tcfmt: off` und `// tcfmt: on` bleiben unverändert.

## Installation (IDE)

```powershell
.\build.ps1          # Tests, CLI und VSIX bauen (braucht Visual Studio mit "Extension development")
.\install.ps1        # alle TcXaeShell schließen; installiert in 4026 und/oder 4024 (fragt nach Admin-Rechten)
.\install.ps1 -Uninstall
```

Eine einzige VSIX deckt beide Versionen ab. Das Manifest enthält zwei Ziele:
`[15.0,17.0) x86` für die 4024-Shell (VS 2017/2019) und `[17.0,18.0) amd64` für die 4026-Shell (VS 2022).
Die Extension greift über das TwinCAT Automation Interface (`ITcPlcDeclaration`, `ITcPlcImplementation`)
auf den Code zu. GUIDs und das XML der Projektdateien fasst sie deshalb nicht an.

## Benutzung in TwinCAT XAE

Menü **Extras → TcFormatter**, Kontextmenü im Projektmappen-Explorer und im Editor **→ TcFormatter**:

| Befehl | Wirkung | Tastenkürzel |
|---|---|---|
| Format Selection | markierte Objekte, Ordner oder PLC-Projekte rekursiv formatieren | `Strg+Alt+Shift+K` |
| Format Current Object | das Objekt im aktiven Editor formatieren | `Strg+Alt+Shift+F` |
| Check Naming of Selection | Namensprüfung, Ergebnisse in der **Fehlerliste** (Doppelklick öffnet das Objekt) | |
| Format All PLC Projects / Check Naming of All PLC Projects | ganze Projektmappe | |
| Create / Open .tcformat.json | Projektkonfiguration neben der `.sln` anlegen und öffnen | |
| Settings... | *Extras → Optionen → TcFormatter* | |

Protokoll im Ausgabefenster unter **TcFormatter**. Alle Befehle lassen sich auch skripten, z. B.
`DTE.ExecuteCommand("TcFormatter.FormatSelection", "TIPC^Plc1^Plc1 Project^POUs")`.

> Vor dem Formatieren werden offene Editoren gespeichert. Danach ist das Projekt geändert, aber noch
> nicht gespeichert; das Formatieren lässt sich daher wie jede andere Änderung rückgängig machen oder verwerfen.

## Kommandozeile `tcfmt`

```text
tcfmt format  <pfad>...     .TcPOU/.TcDUT/.TcGVL/.TcIO, Ordner, .plcproj/.tsproj/.sln
tcfmt check   <pfad>...     Exitcode 1, wenn etwas umformatiert würde (CI)
tcfmt format --diff <pfad>  Änderungen als Diff anzeigen, nichts schreiben
tcfmt lint    <pfad>...     Namensprüfung (MSBuild-Format, anklickbar), --json, --fail-on warning
tcfmt stdin --part impl     Text von stdin formatieren
tcfmt init                  .tcformat.json mit allen Optionen und Beschreibungen anlegen
```

Die CLI ändert nur den Inhalt der CDATA-Abschnitte; alles andere bleibt byte-identisch. Vor dem
Formatieren von Dateien mit der CLI die Projektmappe in XAE schließen.

## Konfiguration

Gesucht wird in dieser Reihenfolge, wie bei Black:

1. die nächstgelegene `.tcformat.json` vom Objekt (PLC-Projektordner) aus nach oben,
2. die Benutzereinstellungen `%APPDATA%\TcFormatter\.tcformat.json` (*Extras → Optionen*),
3. die eingebauten Standardwerte.

`tcfmt init` schreibt eine vollständige Datei, in der jede Option kommentiert ist. Beispiel:

```jsonc
{
  "format": {
    "indentStyle": "Spaces",
    "indentSize": 4,
    "keywordCasing": "Upper",
    "endKeywordSemicolon": "Add",
    "maxLineLength": 100,
    "wrapCallsWithArgumentsAtLeast": 4
  },
  "naming": {
    "typePrefixes": { "BOOL": "x", "FB_*": "fb" },
    "scopePrefixes": { "VAR_GLOBAL": "g" },
    "objectPrefixes": { "METHOD": "M_", "PROPERTY": "P_", "ACTION": "A_" },
    "severities": { "TCN002": "Error" }
  }
}
```

### Formatierung (`format`)

| Gruppe | Optionen |
|---|---|
| Einrückung | `indentStyle` (Tabs/Spaces), `indentSize`, `continuationIndent`, `indentVarBlocks`, `indentStructMembers`, `indentCaseLabels`, `indentCaseBody`, `indentRegions` |
| Zeilen | `lineEnding` (Auto/CrLf/Lf), `maxConsecutiveBlankLines`, `removeBlankLinesAtBlockStart`, `removeBlankLinesAtBlockEnd`, `finalNewline`, `oneStatementPerLine`, `expandSingleLineBlocks`, `caseStatementOnNewLine`, `maxLineLength` |
| Leerzeichen | `spaceAroundAssignment`, `spaceAroundOperators`, `spaceAfterComma`, `spaceBeforeComma`, `spaceInsideParentheses`, `spaceInsideBrackets`, `spaceBeforeCallParenthesis`, `spaceAfterArrayKeyword`, `spaceAroundRange`, `spaceBeforeColon`, `spaceAfterColon`, `spaceAfterLineCommentStart`, `spacesBeforeTrailingComment` |
| Groß/Klein | `keywordCasing`, `typeCasing`, `literalCasing` (TRUE, 16#FF, T#, INT#), `timeUnitCasing` (Preserve/Upper/Lower) |
| Deklarationen | `alignDeclarationColons`, `alignDeclarationInitializers`, `declarationCommentAlignment`, `blankLineBreaksAlignment`, `maxAlignedNameLength`, `alignEnumValues` |
| Anweisungen | `endKeywordSemicolon` (Preserve/Add/Remove), `codeCommentAlignment`, `alignConsecutiveAssignments`, `wrapLongCalls`, `wrapCallsWithArgumentsAtLeast`, `callWrapStyle`, `wrappedCallClosingParenthesisOnNewLine`, `alignWrappedArguments`, `rejoinWrappedCalls` |

### Namensprüfung (`naming`)

| Regel | Prüft | Standard |
|---|---|---|
| TCN001 | Objektpräfix | `FB_`, `F_`, `P_` (MAIN ausgenommen), `I_`, `ST_`, `E_`, `U_`, `T_`, `GVL_` |
| TCN002 | Variablenpräfix nach Datentyp (+ optional Bereich/Konstante) | `b` BOOL, `n`/`i` Ganzzahlen, `f` REAL, `s` STRING, `ws` WSTRING, `t` TIME, `d` DATE, `dt` DT, `tod` TOD, `a` ARRAY, `p` POINTER, `ref` REFERENCE, `fb` FBs (TON, R_TRIG, FB_*, MC_*), `st` ST_*, `e` E_*, `i`/`ip` I_*, `c` Konstanten |
| TCN003 | Methoden, Properties, Aktionen | PascalCase ohne Präfix/Unterstrich |
| TCN004 | Enum-Werte | beginnen mit Großbuchstaben |
| TCN005 | ungültige Bezeichner | `__`, führender `_`, Nicht-ASCII |

Weitere Einstellungen:
- Arrays und Pointer können zusätzlich das Präfix des Elementtyps verlangen (`abFlags`, `pnValue`).
- Unbekannte Typen werden nicht geprüft.
- Ausnahmen gibt es per Regex (`ignoreNames`) oder per Kommentar `// tcfmt: ignore-naming` in der Zeile.
- Der Schweregrad ist pro Regel einstellbar (Off/Info/Warning/Error).

## Entwicklung

```text
src/TcFormatter.Core    Lexer, Formatter, Naming-Checker, Konfiguration (netstandard2.0 + net472, ohne Abhängigkeiten)
src/TcFormatter.Cli     tcfmt (net8.0)
src/TcFormatter.Vsix    Extension (net472, VS SDK 15.0 → läuft in VS 15/16/17)
tests/                  xUnit-Tests inkl. Golden-Files aus dem TwinCAT-Testprojekt
TestProject/            TwinCAT-Projekt zum Testen (formatiert, baut mit 0 Fehlern)
lib/TCatSysManagerLib   Interop aus TypeLib 3.3 (eingebettet, kompatibel mit 4024 und 4026)
```
