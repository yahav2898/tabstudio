# Test Explorer: 5,000 results arriving one by one

- 10 namespaces, 10 classes in each, 50 tests in each class; Test Explorer shown in a 1280 × 800 window.
- Discovery (the build's 5,000 tests arrive): 37.2 ms, 15579 KB, 1 GCs.

| Run All, the runner reporting from its threads | From the first start to the last result shown | The UI thread meanwhile |
| --- | --- | --- |
| Each start and result posted on its own, as before 0.21.11 | 2.14 s | 0 over 50 ms (0 over 250 ms, 0 over 1 s), 0.0 s in all; answer p50 1.1 ms, p95 5.3 ms, p99 5.4 ms over 373 questions in 2 s |
| Queued and shown in batches | 0.05 s | 0 over 50 ms (0 over 250 ms, 0 over 1 s), 0.0 s in all; answer p50 1.1 ms, p95 6.5 ms, p99 6.5 ms over 8 questions in 0 s |
