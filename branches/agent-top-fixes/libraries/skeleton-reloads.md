# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 934 ms | 4.4 ms | 9.6 ms |
| Memory pressure | 35 ms | 0.1 ms | 17.1 ms |
