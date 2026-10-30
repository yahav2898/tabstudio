# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 1405 ms | 3.1 ms | 25.1 ms |
| Memory pressure | 43 ms | 0.1 ms | 31.2 ms |
