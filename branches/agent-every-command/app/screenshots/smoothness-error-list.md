# Error List with 3,000 rows

- 150 files, 20 diagnostics each, shown in a 1280 × 420 window.
- Every cost is the UI thread's, from the change to the end of the frame that shows it.

| Action | Cost |
| --- | --- |
| The solution's diagnostics arrive | 27.5 ms, 6244 KB |
| One file's analysis adds or removes an error (30 times) | median 4.3 ms, worst 33.2 ms, 807 KB each; 1 list notifications each |
| The same update as before 0.21.7: the list cleared and every row added back (10 times) | median 42.9 ms, worst 74.3 ms, 6446 KB each; 3001 list notifications each |
| Warnings filter on and off | median 53.0 ms, worst 57.1 ms, 6299 KB each |
| Typing a search, letter by letter, and erasing it | median 4.4 ms, worst 55.6 ms, 2197 KB each |
