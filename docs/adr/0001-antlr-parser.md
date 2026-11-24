# Structured Text wird mit ANTLR geparst, das Layout bleibt beim Token-Formatter

Der Formatter hat Struktur und Kontext (unäre Operatoren, CASE-Labels, Sprungmarken, Argument-Zuweisungen) früher aus
benachbarten Tokens erraten. Jetzt parst eine ANTLR-Grammatik (`src/TcFormatter.Core/Grammar/StLexer.g4`,
`StParser.g4`) jeden Code-Teil vollständig, und der Formatter liest diese Fakten als Token-Rollen aus dem Parse-Baum.
Zeilenbildung, Optionen und Rendering bleiben im bestehenden Token-Formatter (`FormatEngine`, `Renderer`). Ein
Neubau als Pretty-Printer direkt aus dem Baum hätte über 100 Optionen und die Golden-Tests aufs Spiel gesetzt, ohne
dass der Nutzer davon mehr hat.

## Folgen

- **Code mit Syntaxfehler wird nicht formatiert** (wie bei Black). Der Code-Teil bleibt unverändert, gemeldet werden
  Zeile, Spalte und Meldung des Parsers. Es gibt bewusst keinen Rückfall auf die alten Heuristiken, denn zwei
  Code-Pfade wären dauerhaft zu pflegen. Bereiche mit `tcfmt: off` werden mitgeparst: Sie schützen das Layout, nicht
  ungültige Syntax.
- **Der Lexer verliert keinen Text.** Leerzeichen, Zeilenumbrüche, Kommentare und Pragmas liegen auf dem
  Hidden-Channel, unbekannte Zeichen werden zu `ERROR_CHAR`. `Lexer.Tokenize` ist nur noch ein Adapter auf `Token`
  und `TokenKind`, sodass Safety-Check, Renderer und Namensprüfung unverändert bleiben. Die Namensprüfung arbeitet
  vorerst weiter auf Tokens; der Umzug auf den Parse-Baum ist ein eigenes Issue.
- **Die Grammatik muss echten TwinCAT-Code abdecken**, also mehr als IEC 61131-3. Was TwinCAT kompiliert, muss sie
  annehmen: leere Eingaben `x := ,`, ein Komma am Ende einer Argumentliste, mehrzeilige Strings, nackte
  Zugriffsmodifizierer vor den VAR-Blöcken eines Property-Accessors. Der Korpus-Test parst und formatiert ein großes
  Kundenprojekt unter `TestProject/PLC`. Es wird nicht eingecheckt, deshalb läuft der Test nur lokal.
- **Codegenerierung beim Build** über `Antlr4BuildTasks`. Das Paket lädt beim ersten Build ein JRE und das ANTLR-Jar
  herunter. Die Version des Tools folgt `Antlr4.Runtime.Standard` (4.13.1), und die VSIX liefert die Runtime-DLL mit.
