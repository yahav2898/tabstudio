# Error List with 3,000 rows

- 150 files, 20 diagnostics each, shown in a 1280 × 420 window.
- Every cost is the UI thread's, from the change to the end of the frame that shows it.

| Action | Cost |
| --- | --- |
| The solution's diagnostics arrive | 52.1 ms, 6227 KB, 1 GCs |
| One file's analysis adds or removes an error (30 times) | median 4.3 ms, worst 23.7 ms, 806 KB each; 1 list notifications each |
| The same update as before 0.21.11: the list cleared and every row added back (10 times) | median 36.4 ms, worst 95.7 ms, 6400 KB each; 3001 list notifications each |
| Warnings filter on and off | median 35.8 ms, worst 46.4 ms, 6261 KB each |
| Typing a search, letter by letter, and erasing it | median 3.9 ms, worst 35.7 ms, 2187 KB each |
