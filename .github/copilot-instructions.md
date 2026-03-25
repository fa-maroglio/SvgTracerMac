# Copilot Instructions

## General Guidelines
- All exception/log/output messages in Italian.
- Comments use only `/* comment */` format, never `//`.
- All comments in Italian.
- In case of exceptions, show the original exception message (ex.Message) or the API response.

## Code Style
- Private variables and fields use snake_case_lower (e.g., is_authenticated, user_name).
- Opening brace `{` on the same line as method/constructor signature.
- Always leave an empty line before closing `}` or `);`.
- Exception messages go on a new line with extra indentation.
- Structure main methods with try-catch: logic in try, catch at bottom with logging.
- Always use `#region METODI PUBBLICI` and `#region METODI PRIVATI`.

## Razor/Markup Guidelines
- SVG definitions (`<symbol>`, `<defs>`) in a single `<svg class="svg-defs">` block at the bottom of markup. Only use `<svg><use href="#id" /></svg>` in the body.