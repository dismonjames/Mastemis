# MAS language reference

MAS 1.0 is a deterministic contest-test generation language, not Lua. A file contains `test count [group name] { ... }` declarations. Blocks support variable assignments, repeated `input = expression`, `output = expression`, `seed = integer`, and `include directive`.

Expressions support integer, finite floating-point, boolean and escaped string literals; arrays; identifiers; calls; parentheses; unary `+`/`-`; arithmetic and comparison tokens. Comments begin with `//` or `#`. Parser diagnostics use stable codes and zero-based line/column locations.

Supported built-ins are `int`, `float`, `bool`, `choice`, `string`, `array`, `uniqueArray`, `permutation`, `shuffle`, `sorted`, `reversed`, `tree`, and `simpleGraph`. Supported directives are `boundaries`, `random`, `sorted`, `reversed`, `duplicates`, and `adversarial`; only boundary and collection ordering strategies currently alter output. Unsupported calls and directives are errors.
