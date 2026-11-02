# Typing in a long C# file

- 2509 lines, 10632 colored ranges from Roslyn.
- 200 keystrokes in the middle of the file; a Format Document of 2000 edits spread over it.

| Moving the colors through | Before (copy per edit) | After (in place) |
| --- | --- | --- |
| A keystroke | 332.4 KB and 280 µs per edit | 0.0 KB and 32 µs per edit |
| Format Document, in all | 649.1 MB and 444 ms | 0.0 MB and 38 ms |

A whole keystroke in the editor, every listener included: 238.6 KB and 333 µs per edit.
