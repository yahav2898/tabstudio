# Test Explorer: 5,000 results arriving one by one

- 10 namespaces, 10 classes in each, 50 tests in each class; Test Explorer shown in a 1280 × 800 window.
- Discovery (the build's 5,000 tests arrive): 24.1 ms, 15576 KB.

| Run All, the runner reporting from its threads | From the first start to the last result shown | The UI thread meanwhile |
| --- | --- | --- |
| Each start and result posted on its own, as before 0.21.11 | 1.96 s | 0 over 50 ms (0 over 250 ms, 0 over 1 s), 0.0 s in all; answer p50 1.1 ms, p95 4.3 ms, p99 4.3 ms over 351 questions in 2 s |
| Queued and shown in batches | 0.07 s | 0 over 50 ms (0 over 250 ms, 0 over 1 s), 0.0 s in all; answer p50 1.1 ms, p95 20.0 ms, p99 20.0 ms over 8 questions in 0 s |
