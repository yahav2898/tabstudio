# Error List with 3,000 rows

- 150 files, 20 diagnostics each, shown in a 1280 × 420 window.
- Every cost is the UI thread's, from the change to the end of the frame that shows it.

| Action | Cost |
| --- | --- |
| The solution's diagnostics arrive | 63.8 ms, 6241 KB, 1 GCs |
| One file's analysis adds or removes an error (30 times) | median 5.3 ms, worst 31.0 ms, 806 KB each; 1 list notifications each |
| The same update as before 0.21.7: the list cleared and every row added back (10 times) | median 43.2 ms, worst 63.3 ms, 6435 KB each; 3001 list notifications each |
| Warnings filter on and off | median 43.3 ms, worst 66.9 ms, 6295 KB each |
| Typing a search, letter by letter, and erasing it | median 5.4 ms, worst 54.5 ms, 2196 KB each |
