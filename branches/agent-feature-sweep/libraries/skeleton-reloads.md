# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 1103 ms | 6.6 ms | 23.3 ms |
| Memory pressure | 43 ms | 0.1 ms | 18.3 ms |
