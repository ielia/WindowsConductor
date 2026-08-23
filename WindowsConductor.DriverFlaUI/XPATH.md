# XPath Reference

WindowsConductor implements an XPath engine for querying UIAutomation element
trees. Selectors that start with `/` or `.` are treated as XPath expressions;
everything else goes through the simple-selector path (see below).

---

## Grammar

```
Path           ::= AbsolutePath | RelativePath
AbsolutePath   ::= ('/' | '//') Step (StepSep Step)*
RelativePath   ::= Step (StepSep Step)*
StepSep        ::= '/' | '//'

Step           ::= AxisSpecifier? NodeTest Filter*
                 | '.'                          (* self *)
                 | '..'                         (* parent *)

AxisSpecifier  ::= AxisName '::'
AxisName       ::= 'ancestor' | 'ancestor-or-self'
                 | 'child' | 'descendant' | 'descendant-or-self'
                 | 'following-sibling' | 'preceding-sibling' | 'sibling'
                 | 'leafmost' | 'parent' | 'self'
                 | 'attribute'

NodeTest       ::= Identifier                  (* ControlType name *)
                 | '*'                         (* any element *)
                 | '@' Identifier              (* single attribute *)
                 | '@' '*'                     (* all attributes *)

Filter         ::= '[' Expression ']'
                 | '[' Integer ']'             (* positional, 1-based *)

GroupedPath    ::= '(' Path ')' Filter+        (* apply filters to an entire path result set *)

Expression     ::= OrExpr
OrExpr         ::= AndExpr ('or' AndExpr)*
AndExpr        ::= Comparison ('and' Comparison)*
Comparison     ::= Additive (CompOp Additive)*
CompOp         ::= '=' | '!=' | '<' | '>' | '<=' | '>='
Additive       ::= Multiplicative (('+' | '-') Multiplicative)*
Multiplicative ::= Unary (('*' | 'div' | 'idiv' | 'mod') Unary)*
Unary          ::= '-' Unary | '+' Unary | Primary
Primary        ::= FunctionCall | Number | String | '@' Identifier
                 | SubPath | Sequence | '(' Expression ')' | '.'

Sequence       ::= '(' ')'                    (* empty sequence *)
                 | '(' Expression (',' Expression)+ ')'

FunctionCall   ::= QualifiedName '(' (Expression (',' Expression)*)? ')'
SubPath        ::= Path                        (* nested path used as boolean *)

String         ::= "'" (char | "''")* "'"
                 | '"' (char | '""')* '"'
Number         ::= Digit+ ('.' Digit+)?
QualifiedName  ::= Identifier (':' Identifier)?   (* e.g. math:sin *)
Identifier     ::= (Letter | '_') (Letter | Digit | '_' | '-')*
```

---

## Absolute vs. Relative Paths

| Form | Starting point | Example |
|---|---|---|
| `/`&hellip; | The **root** of the automation tree (the Desktop element). | `/Window/Button` |
| `//`&hellip; | All **descendants** of the root (absolute). | `//Button` |
| `.//`&hellip; | All **descendants** of the context node (relative). | `.//Button` |
| `.`&hellip; | The **context node** passed to the engine (e.g. the element a locator resolved to). | `./Button` |
| `..`&hellip; | The **parent** of the context node. | `../Button` |

### Top-level paths

| Form | Starting point |
|---|---|
| `/`&hellip; | **Absolute.** The root of the automation tree (Desktop). |
| `//`&hellip; | **Absolute.** All descendants from the desktop root. |
| `.//`&hellip; | **Relative.** All descendants of the context node. |
| `.`&hellip; / `..`&hellip; | **Relative.** Context node / parent of context node. |

Use `.//` when searching within a subtree: `element.Locator(".//Button")`
searches within that element's descendants. Plain `//Button` always
searches from the desktop root.

### Sub-path predicates (inside `[…]`)

Inside filter expressions the same rules apply:

| Form | Starting point | Example |
|---|---|---|
| `//`&hellip; | Desktop root (absolute). | `//Pane[//Button]` &mdash; any Button anywhere. |
| `.//`&hellip; | Context element (relative). | `//Pane[.//Button]` &mdash; Button under that Pane. |
| `./`&hellip; | Context element (relative). | `//Pane[./Button]` &mdash; direct child Button. |
| `..`&hellip; | Parent of context element. | `//Pane[../Group]` &mdash; sibling Group. |
| `/`&hellip; | Desktop root (absolute). | `//Pane[/Window]` &mdash; root Window. |

---

## Axes

| Syntax | Axis | Description |
|---|---|---|
| `/` or `child::` | child | Direct children of the context node. |
| `//` or `descendant::` | descendant | All descendants (children, grandchildren, ...). |
| `descendant-or-self::` | descendant-or-self | The context node plus all its descendants. |
| `..` or `parent::` | parent | Parent of the context node. |
| `.` or `self::` | self | The context node itself (with optional type filter). |
| `sibling::` | sibling | All siblings (parent's children excluding self). |
| `preceding-sibling::` | preceding-sibling | Siblings that come before the context node. |
| `following-sibling::` | following-sibling | Siblings that come after the context node. |
| `ancestor::` | ancestor | All ancestors up to the root. |
| `ancestor-or-self::` | ancestor-or-self | The context node plus all its ancestors. |
| `leafmost::` | leafmost | Leaf-most descendants only (elements with no descendant in the result set). Traverses descendants internally, so use `./leafmost::` not `.//leafmost::`. Predicates are applied before the leaf-most filter. |
| `@` or `attribute::` | attribute | Element properties (see **Attributes** below). |

`self::Type` is particularly useful in predicates to filter by type:
`./*[self::Button]` selects all child Buttons (equivalent to `./Button`).

Where applicable, translatable predicates are pushed down into UIA
`ConditionBase` filters for the `child`, `descendant`, `descendant-or-self`,
and `sibling` axes to reduce cross-process COM marshalling.

---

## Grouped Paths

A path can be wrapped in parentheses and followed by one or more filters.
The filters apply to the **entire result set** of the inner path, enabling
positional or expression-based filtering across all matched elements.

```xpath
(//Button)[1]                             First Button in the entire tree
(//Button[@IsEnabled='True'])[last()]     Last enabled Button
(//Edit)[position() > 2]                  All Edit elements after the second
```

Without the parentheses, `//Button[1]` selects the first Button in each
sibling group, whereas `(//Button)[1]` selects the first Button overall.

---

## Attributes (Element Properties)

Attribute references (`@Name`, `@AutomationId`, etc.) resolve against FlaUI's
`AutomationElement.Properties`. All names are **case-insensitive**.

Common properties:

| Attribute | Description |
|---|---|
| `Name` | Display name of the element. |
| `AutomationId` | Developer-assigned identifier. |
| `ControlType` | UIA control type (`Button`, `Edit`, `Window`, ...). |
| `ClassName` | Win32 class name. |
| `IsEnabled` | Whether the element is interactive. |
| `IsOffscreen` | Whether the element is off-screen. |
| `FrameworkId` | UI framework (`Win32`, `WPF`, `WinForm`, ...). |
| `ProcessId` | OS process ID that owns the element. |
| `HelpText` | Tooltip or help text. |
| `AriaRole` | ARIA role, when available. |
| `HeadingLevel` | Heading level, when available. |

**Aliases:** `class` &rarr; `ClassName`, `type` &rarr; `ControlType`.

**Special property:** `text` resolves to the TextBox `Text` value (or null if
the element is not a TextBox). Accessible via `text()` in expressions or
`@text` in attribute references.

---

## Operators

### Arithmetic

| Operator | Description |
|---|---|
| `+` | Addition. |
| `-` | Subtraction (binary) or negation (unary). |
| `*` | Multiplication. |
| `div` | Division (floating-point). |
| `idiv` | Integer division (truncates toward zero). |
| `mod` | Modulus (integer remainder). |

### Comparison

| Operator | Description |
|---|---|
| `=` | Equal. |
| `!=` | Not equal. |
| `<` | Less than. |
| `>` | Greater than. |
| `<=` | Less than or equal. |
| `>=` | Greater than or equal. |

### Logical

| Operator | Description |
|---|---|
| `and` | Logical AND (short-circuit). |
| `or` | Logical OR (short-circuit). |

---

## Functions

### Boolean

| Function | Description |
|---|---|
| `true()` | Returns `true`. |
| `false()` | Returns `false`. |
| `not(expr)` | Boolean negation. |

### Context

| Function | Description |
|---|---|
| `position()` | 1-based position within the current sibling group (by type). |
| `last()` | Total count of siblings in the current group. |

### String

| Function | Description |
|---|---|
| `text()` | Text content of the context element. |
| `concat(s1, s2, ...)` | Concatenates two or more strings. |
| `string-length()` or `string-length(s)` | Length of string (defaults to `text()`). |
| `contains(haystack, needle)` | Case-insensitive substring test. |
| `starts-with(s, prefix)` | Case-insensitive prefix test. |
| `ends-with(s, suffix)` | Case-insensitive suffix test. |
| `string-join(sequence)` | Joins a sequence of strings with no separator. |
| `string-join(sequence, sep)` | Joins a sequence of strings with the given separator. |
| `matches(s, pattern)` | Returns `true` if `s` matches the regular expression `pattern`. |
| `matches(s, pattern, flags)` | Regex match with flags (see below). |
| `replace(s, pattern, replacement)` | Replaces all occurrences of `pattern` in `s` with `replacement`. |
| `replace(s, pattern, replacement, flags)` | Regex replace with flags. |
| `tokenize(s)` | Splits `s` on whitespace (default pattern `\s+`). Returns a sequence. |
| `tokenize(s, pattern)` | Splits `s` by the given regex pattern. Returns a sequence. |
| `tokenize(s, pattern, flags)` | Splits with flags. Returns a sequence. |
| `codepoints-to-string(seq)` | Converts a sequence of Unicode codepoints (integers) to a string. |
| `string-to-codepoints(s)` | Converts a string to a sequence of Unicode codepoint integers. |
| `codepoint-equal(s1, s2)` | Compares two strings by codepoints. Returns empty sequence if either argument is an empty sequence. |

A sequence is a comma-separated list in parentheses: `()`, `('a')`,
`('a', 'b', 'c')`. A single string argument is also accepted.

**Regex flags** (for `matches`, `replace`, `tokenize`):

| Flag | Description |
|---|---|
| `i` | Case-insensitive matching. |
| `m` | Multiline — `^` and `$` match at line boundaries. |
| `s` | Single-line (dot-all) — `.` matches newline characters. |
| `x` | Ignore unescaped whitespace in the pattern. |
| `q` | Quote — treat the entire pattern as a literal string (metacharacters are escaped). |

Flags can be combined: `matches(@Name, 'hello', 'iq')` does a case-insensitive literal match.

### Numeric

| Function | Description |
|---|---|
| `abs(n)` | Absolute value. |
| `ceiling(n)` | Smallest integer &gt;= n. |
| `floor(n)` | Largest integer &lt;= n. |
| `round(n)` | Round to nearest integer (banker's rounding). |
| `round(n, precision)` | Round to the given number of decimal places. |
| `round-half-to-even(n)` | Round half-to-even (banker's rounding). |
| `round-half-to-even(n, precision)` | Round half-to-even with the given precision. |

### Aggregate

All aggregate functions accept a sequence or a single value. Non-numeric items
are coerced to numbers via `AsNumber()` (strings are parsed; unparseable values
become `NaN`).

| Function | Description |
|---|---|
| `count(seq)` | Number of items in the sequence. |
| `sum(seq)` | Sum of all items. |
| `avg(seq)` | Arithmetic mean. Returns `NaN` for an empty sequence. |
| `max(seq)` | Largest value. Returns `NaN` for an empty sequence. |
| `min(seq)` | Smallest value. Returns `NaN` for an empty sequence. |

### Sequence

| Function | Description |
|---|---|
| `head(seq)` | First item of the sequence, or empty sequence if empty. |
| `tail(seq)` | All items except the first, or empty sequence if 0–1 items. |
| `subsequence(seq, start)` | Items from 1-based `start` to the end. |
| `subsequence(seq, start, length)` | `length` items starting at 1-based `start`. |
| `empty(seq)` | `true` if the sequence has no items. |
| `exists(seq)` | `true` if the sequence has at least one item. |
| `reverse(seq)` | The sequence in reverse order. |
| `index-of(seq, value)` | Sequence of 1-based positions where `value` occurs. |
| `distinct-values(seq)` | The sequence with duplicate values removed (first occurrence kept). |
| `insert-before(seq, pos, items)` | Insert `items` before position `pos` (1-based). |
| `remove(seq, pos)` | The sequence with the item at position `pos` (1-based) removed. |
| `deep-equal(a, b)` | `true` if the two values (or sequences) are structurally equal. |

### Spatial

| Function | Description |
|---|---|
| `bounds()` | Bounding rectangle of the context element, relative to its top-level container. |
| `point(x, y)` | Constructs a point value. |
| `contains-point(rect, pt)` | Tests whether a point lies within a rectangle. |
| `at(x, y)` | Shorthand for `contains-point(bounds(), point(x, y))`. |

### math: namespace

All trigonometric functions work with radians.

| Function | Description |
|---|---|
| `math:pi()` | The constant pi. |
| `math:exp(n)` | e raised to the power n. |
| `math:exp10(n)` | 10 raised to the power n. |
| `math:log(n)` | Natural logarithm of n. |
| `math:log10(n)` | Base-10 logarithm of n. |
| `math:pow(base, exp)` | base raised to the power exp. |
| `math:sqrt(n)` | Square root of n. |
| `math:sin(n)` | Sine of n (radians). |
| `math:cos(n)` | Cosine of n (radians). |
| `math:tan(n)` | Tangent of n (radians). |
| `math:asin(n)` | Arcsine of n, result in radians. |
| `math:acos(n)` | Arccosine of n, result in radians. |
| `math:atan(n)` | Arctangent of n, result in radians. |
| `math:atan2(y, x)` | Two-argument arctangent, result in radians. |

---

## Comparison Semantics

When two values are compared (`=`, `!=`, `<`, etc.):

1. Both booleans &rarr; boolean comparison.
2. One boolean &rarr; the other is coerced to boolean.
3. Both numbers &rarr; numeric comparison.
4. One number &rarr; the other is coerced to number (only if parseable).
5. Otherwise &rarr; case-insensitive string comparison (`InvariantCultureIgnoreCase`).

When either side of a comparison is a sequence, general comparison semantics
apply: the comparison is true if **any** pair of items (one from each side)
satisfies the operator.

---

## Top-level Function Expressions

Function calls can be used as standalone selectors (not only inside predicates).
The result is an `ExpressionResult` containing the evaluated value.

```xpath
concat('a', 'b')                          Returns "ab"
string-join(//Button/@AutomationId, ',')  Comma-separated list of all Button AutomationIds
true()                                    Returns true
math:pi()                                 Returns 3.14159…
```

This is useful for aggregating or inspecting values across the element tree
without selecting elements themselves.

---

## Simple Selectors

Selectors that do **not** start with `/` or `.` use a lightweight syntax
instead of XPath. Multiple clauses can be combined with `&&`.

| Selector | Meaning |
|---|---|
| `[automationid=value]` | Match by AutomationId. |
| `[name=value]` | Match by Name. |
| `text=value` | Shorthand for name match. |
| `type=Button` | Match by ControlType. |
| `[classname=Foo]` | Match by ClassName. |
| `[property=value]` | Match any element property. |
| `bare text` | Treated as a name search. |

---

## Examples

### Basic navigation

```xpath
//Button                              Find all Button descendants
/Window/Button                        Buttons that are direct children of a Window
./Button                              Child Button relative to context node
.//Button                             Descendant Button relative to context node
../Button                             Sibling Buttons (parent → child)
```

### Wildcards

```xpath
//*                                   All descendants regardless of type
//Button/@*                           All attributes of every Button
```

### Predicates: attribute match

```xpath
//Button[@Name='OK']                  Button whose Name is "OK"
//Button[@AutomationId='num7']        Button by AutomationId (case-insensitive)
//Edit[@ClassName='TextBox']          Edit element by class name
```

### Predicates: positional

```xpath
//Button[1]                           First Button in each sibling group
//Button[3]                           Third Button
//Button[last()]                      Last Button
//Button[position() > 2]             All Buttons after the second
//Button[position() = last() - 1]    Penultimate Button
//Button[position() mod 2 = 1]       Odd-positioned Buttons
```

### Grouped path: positional over full result set

```xpath
(//Button)[1]                         First Button in the entire tree
(//Button)[last()]                    Last Button in the entire tree
(//Button[@Name!='Close'])[3]         Third non-Close Button overall
```

### Predicates: string functions

```xpath
//Button[contains(@Name, 'Save')]           Name contains "Save"
//Button[starts-with(@Name, 'Start')]       Name starts with "Start"
//Button[ends-with(@Name, 'End')]           Name ends with "End"
//Window[text()='Calculator']               TextBox text equals "Calculator"
//Edit[string-length(text()) > 0]           Non-empty text boxes
```

### Predicates: regex functions

```xpath
//Button[matches(@Name, '^\d+$')]              Name is all digits
//Button[matches(@Name, 'calc', 'i')]          Case-insensitive regex match
//Edit[replace(@Name, '\d+', '#') = 'Item#']   Replace digits, then compare
//*[count(tokenize(@Name, '\s+')) > 2]         Name has more than 2 words
//Button[matches(@Name, 'a.b', 'q')]           Literal match (dot not a wildcard)
```

### Compound predicates

```xpath
//Button[@Name='OK' and @IsEnabled='True']
//Button[@Name='Yes' or @Name='No']
//Button[contains(@Name, 'num') and @Name='Three']
```

### Spatial predicates

```xpath
//Button[at(100, 200)]                              Button whose bounds contain point (100, 200)
//Button[contains-point(bounds(), point(10, 50))]   Equivalent long form
/leafmost::Button[at(10, 50)]                       Front-most (leaf) Button at point
```

### Sibling navigation

```xpath
//Button/sibling::Edit                All Edit siblings of any Button
//Button/following-sibling::Edit      Edit elements after a Button
//Button/preceding-sibling::Edit      Edit elements before a Button
```

### Ancestor navigation

```xpath
//Button/ancestor::Group              All Group ancestors of any Button
//Button/ancestor-or-self::Group      Ancestors-or-self that are Groups
//Button/ancestor::*[@Name='numbers'] Ancestor with a specific Name
```

### Descendant-or-self axis

```xpath
//Group/descendant-or-self::Button    The Group itself (if it's a Button) plus all descendant Buttons
```

### Attribute axis

```xpath
//Button/@AutomationId                AutomationId of every Button
//Button/@Name[.='OK']                Name attribute, filtered to value "OK"
//Button/@*                           All attributes of every Button
```

### Sub-path predicates

```xpath
//Pane[.//Button]                           Pane that contains a descendant Button (relative)
//Pane[./Button]                            Pane with a direct child Button (relative)
//Pane[//Button]                            Pane, if any Button exists anywhere (absolute)
//Pane[not(.//Button)]                      Pane with no descendant Buttons
//Group[./Button[@AutomationId='num3']]     Group containing a specific Button
//Pane[//Group[//Button]]                   Pane where any Group in the tree contains a Button
```

### String escaping

```xpath
//Button[@Name='it''s']               Escaped single quote → it's
//Button[@Name="say ""hello"""]       Escaped double quote → say "hello"
```

XPath has no backslash escape sequences. Use `codepoints-to-string()` for special characters:

```xpath
//Text[contains(@Name, codepoints-to-string((10)))]          Element whose name contains a newline
//Text[@Name=concat('Line1', codepoints-to-string((10)), 'Line2')]  Match multiline text
codepoints-to-string((72, 101, 108, 108, 111))               Returns "Hello"
string-to-codepoints('Hi')                                    Returns (72, 105)
codepoint-equal('abc', 'abc')                                 Returns true
codepoint-equal((), 'abc')                                    Returns empty sequence
```

### Combining axes

```xpath
//Button/..                           Parents of all Buttons
//Button/@class/ancestor::Group       Navigate from attribute back to elements
//Window/leafmost::Button[at(10, 50)]
```

### Arithmetic

```xpath
//Button[position() * 2 = 4]         Button at position 2
//Edit[string-length(text()) div 2 > 5]
//Button[position() idiv 3 = 1]      Integer division
//Button[position() mod 2 = 0]       Even-positioned Buttons
```
