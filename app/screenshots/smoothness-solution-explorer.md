# Solution Explorer with 5,000 files

- One folder of 3,000 files and 40 folders of 50, opened as a folder in the IDE's window at 1280 × 800.
- Opened in 0.16 s; the UI thread meanwhile: 0 over 50 ms (0 over 250 ms, 0 over 1 s), 0.0 s in all; answer p50 1.1 ms, p95 35.7 ms, p99 35.7 ms over 15 questions in 2 s.

| Action | Cost |
| --- | --- |
| Expanding the folder of 3,000 files | median 89.5 ms, worst 100.3 ms, 6830 KB each |
| Collapsing it | median 64.2 ms, worst 106.3 ms, 6527 KB each |
| Typing a search, letter by letter, and erasing it | median 93.1 ms, worst 107.9 ms, 8064 KB each |

| Stall | Of it, GC | At | During |
| --- | --- | --- | --- |
