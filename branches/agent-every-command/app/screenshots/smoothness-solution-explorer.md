# Solution Explorer with 5,000 files

- One folder of 3,000 files and 40 folders of 50, opened as a folder in the IDE's window at 1280 × 800.
- Opened in 0.17 s; the UI thread meanwhile: 0 over 50 ms (0 over 250 ms, 0 over 1 s), 0.0 s in all; answer p50 1.1 ms, p95 33.3 ms, p99 33.3 ms over 16 questions in 2 s.

| Action | Cost |
| --- | --- |
| Expanding the folder of 3,000 files | median 84.5 ms, worst 90.9 ms, 6838 KB each |
| Collapsing it | median 70.7 ms, worst 80.5 ms, 6535 KB each |
| Typing a search, letter by letter, and erasing it | median 70.7 ms, worst 88.4 ms, 8074 KB each |

| Stall | Of it, GC | At | During |
| --- | --- | --- | --- |
