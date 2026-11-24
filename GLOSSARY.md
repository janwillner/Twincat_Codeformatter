# TcFormatter

Formatter und Namensprüfung für TwinCAT 3 Structured Text, als Extension für die TcXaeShell und als
Kommandozeilenwerkzeug `tcfmt`.

## Code

**Objekt**:
Eine Einheit des PLC-Projekts mit eigenem Code: POU, Methode, Property-Accessor, Aktion, Transition, DUT, GVL oder Interface.
_Avoid_: Baustein (nur für POUs), Datei

**Code-Teil**:
Einer der beiden getrennt gespeicherten Texte eines Objekts, Deklaration oder Implementierung. Er wird immer als Ganzes formatiert und geparst.
_Avoid_: Abschnitt, Section, Bereich

**Deklaration**:
Der Code-Teil mit Kopf (`FUNCTION_BLOCK …`), VAR-Blöcken, Typdefinitionen oder globalen Variablen.
_Avoid_: Header, Deklarationsteil

**Implementierung**:
Der Code-Teil mit den Anweisungen eines Objekts.
_Avoid_: Body, Rumpf, Code

## Formatierung

**Token-Rolle**:
Die Bedeutung, die der Parse-Baum einem einzelnen Token gibt, etwa unärer Operator, Doppelpunkt eines CASE-Labels, Doppelpunkt einer Sprungmarke oder Argument-Zuweisung.
_Avoid_: Annotation, Heuristik

**Safety-Check**:
Der Vergleich nach dem Formatieren. Das Ergebnis muss dieselben bedeutungstragenden Tokens enthalten wie das Original, sonst bleibt der Code-Teil unverändert.
_Avoid_: Äquivalenzprüfung, Validierung

**Ablehnung**:
Ein Code-Teil wird nicht formatiert oder seine Namen werden nicht geprüft, weil er einen Syntaxfehler enthält oder (beim Formatieren) den Safety-Check nicht besteht. Er bleibt dann unverändert, und der Grund wird gemeldet.
_Avoid_: Abbruch, Fehlschlag

**Verbatim-Bereich**:
Code zwischen `tcfmt: off` und `tcfmt: on` (oder `STweep.Disable` / `STweep.Enable`). Sein Layout bleibt unverändert, er muss aber trotzdem gültige Syntax sein.
_Avoid_: ignorierter Bereich
