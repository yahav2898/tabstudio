# Solution Explorer with 5,000 files

- One folder of 3,000 files and 40 folders of 50, opened as a folder in the IDE's window at 1280 × 800.
- Opened in 0.14 s; the UI thread meanwhile: 1 over 50 ms (0 over 250 ms, 0 over 1 s), 0.1 s in all, longest 67 ms during opening the folder, 0.0 s of it garbage collection; answer p50 1.1 ms, p95 67.4 ms, p99 67.4 ms over 14 questions in 1 s.

| Action | Cost |
| --- | --- |
| Expanding the folder of 3,000 files | median 74.8 ms, worst 81.0 ms, 6769 KB each |
| Collapsing it | median 57.0 ms, worst 70.4 ms, 6465 KB each |
| Typing a search, letter by letter, and erasing it | median 75.6 ms, worst 78.3 ms, 7987 KB each |

| Stall | Of it, GC | At | During |
| --- | --- | --- | --- |
| 67 ms | 29 ms | 0.0 s | opening the folder |
