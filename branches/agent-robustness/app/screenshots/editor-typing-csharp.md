# Typing in a long C# file

- 2509 lines, 10632 colored ranges from Roslyn.
- 200 keystrokes in the middle of the file; a Format Document of 2000 edits spread over it.

| Moving the colors through | Before (copy per edit) | After (in place) |
| --- | --- | --- |
| A keystroke | 332.4 KB and 356 µs per edit | 0.0 KB and 26 µs per edit |
| Format Document, in all | 649.1 MB and 312 ms | 0.0 MB and 18 ms |

A whole keystroke in the editor, every listener included: 238.4 KB and 464 µs per edit.
