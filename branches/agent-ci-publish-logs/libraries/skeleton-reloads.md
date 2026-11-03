# Skeleton reloads on the UI thread

An app and a library of 300 classes referenced as a skeleton image, as on a 3 GB tablet.

| | Caller waited, before (the reload and compile it did) | Caller waited, after | Longest keystroke wait, after |
| --- | --- | --- | --- |
| Rename edits the library | 859 ms | 4.3 ms | 43.2 ms |
| Memory pressure | 40 ms | 1.2 ms | 23.8 ms |
