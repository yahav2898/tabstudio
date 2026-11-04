# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 767 ms | 11.4 ms | 58.7 ms |
| Memory pressure | 6 ms | 0.1 ms | 0.2 ms |
