<div dir="rtl">

<div align="center">

# TabStudio

**סביבת פיתוח אמיתית לטאבלטים של אנדרואיד. פותחים `.sln`, לוחצים F5, והקוד רץ.**

[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![CI](https://github.com/yahav2898/tabstudio/actions/workflows/ci.yml/badge.svg)](https://github.com/yahav2898/tabstudio/actions/workflows/ci.yml)
![Platform: Android 9+](https://img.shields.io/badge/platform-Android%209%2B%20·%20ARM-3DDC84.svg)
![Built with .NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)

[English](README.md) · [ארכיטקטורה](docs/ARCHITECTURE.md) · [מפת דרכים](docs/ROADMAP.md) · [תרומה לפרויקט](CONTRIBUTING.md)

</div>

**פיתוח מרחוק דרך SSH.** בתפריט **File > Open Remote Folder (SSH)** אפשר להוסיף שרת או לייבא `~/.ssh/config`, ליצור מפתח במכשיר ולפתוח תיקיית קוד מרוחקת. באנדרואיד המפתח הפרטי נוצר בתוך Android Keystore ואינו ניתן לייצוא. הקבצים מופיעים בסייר הפתרון דרך מטמון SFTP; אפשר לערוך, לשמור עם זיהוי התנגשויות ולחפש בקוד. הטרמינל פותח shell בשרת, F5 מפעיל פקודות בנייה והרצה מרוחקות, ואפשר להעביר פורט לתצוגת ה־web. **Publish to server** מבצע פרסום מקומי של ASP.NET Core, מעלה גרסה דרך SFTP ומפעיל מחדש שירות systemd קיים. [הוראות התקנה, פרסום ומגבלות](docs/REMOTE_SSH.md).

---

## במבט אחד

TabStudio הופך טאבלט אנדרואיד למחשב פיתוח שבאמת תרצו לעבוד עליו.

- **אפליקציה אחת.** מורידים, פותחים, כותבים קוד. בלי Termux, בלי root ובלי התקנות נוספות.
- **הסידור שאתם כבר מכירים, בכל מסך.** סרגל תפריטים, סרגל כלים, לשוניות מסמכים, Solution Explorer עם Properties, ‏Error List, ‏Output ושורת מצב, כמו ב-Visual Studio. פס פעילות בקצה הימני מחזיק את כל חלונות הכלים, עם תגים לקבצים שהשתנו, ל-CI, לבדיקות שנכשלו ולשינויים שהסוכן מציע. במצב אנכי ובטלפון הסרגל הצדדי נפתח כמגירה, וסרגל הכלים ושורת המצב מקפלים את הכפתורים הפחות שימושיים ל-More במקום להיחתך.
- **חיפוש אחד לכל דבר.** ‏Ctrl+Q מוצא קבצים, כל פקודת תפריט עם הקיצור שלה, הגדרות כמו ערכת הצבעים, טיפוסים וחברים בקוד שלכם, תבניות פרויקט ופתרונות אחרונים, מדורגים יחד.
- **הקוד שלכם רץ על הטאבלט.** לא על שרת שאתם שוכרים ולא בענן של מישהו אחר. לוחצים F5 והקוד נבנה ורץ שם, ליד העורך.
- **קוד פתוח מהיום הראשון.** Apache-2.0. קראו, עשו fork, תפיצו.

## למה זה חייב להיות קיים

**למיליארדי אנשים יש מכשיר אנדרואיד, והרבה פחות מהם מחזיקים מחשב.** לסטודנט בלי לפטופ, הטלפון או הטאבלט הם המסך היחיד שיש לו, והיום אף אחד מהם לא יכול לפתוח פתרון C# אמיתי, לבנות אותו ולדבג אותו.

התשובה המקובלת עד היום הייתה "תשתמשו ב-IDE בענן". זה עובד עד שהחיבור נופל, החבילה החינמית נגמרת או שהחשבון דורש כרטיס אשראי. סביבת פיתוח צריכה להיות משהו *שלכם*, שעובד באוטובוס, בכיתה בלי Wi-Fi או במדינה שבה שירותי ענן יקרים.

**שני דברים השתנו ומאפשרים את זה עכשיו:**

1. **ההקלדה כבר לא צוואר הבקבוק.** עם עזרה של AI, מפתחים מבלים יותר זמן בקריאה, בהכוונה ובסקירה מאשר בהקלדה, ובדיוק בזה מסך מגע גדול טוב.
2. **כלי הפיתוח סוף סוף מתאימים.** ‏.NET, ‏Roslyn, ‏clang וכל השאר רצים היום על ARM64 ועל ה-libc של אנדרואיד. בטאבלט של 2026 יש יותר ליבות וזיכרון מבמחשבים הניידים שעליהם רבים מאנשי המקצוע של היום למדו לתכנת.

החלק שהיה חסר הוא ה-IDE שמחבר את הכול. זה TabStudio.

## מה מקבלים

### חוויית Visual Studio אמיתית, בנויה למגע

- **הסידור שאתם כבר מכירים.** סרגל תפריטים, סרגל כלים, לשוניות מסמכים, Solution Explorer, ‏Properties, ‏Error List, ‏Output ושורת מצב, מעוגנים בדיוק איפה שהידיים מצפות למצוא אותם.
- **בגודל של אצבעות, שקט למקלדות.** כפתורים, לשוניות ושורות בגודל של קצה אצבע, בגובה 34 עד 48 פיקסלים. סרגל מגע צף שם Rename, ‏Peek, ‏Comment, ‏Run to Cursor, ‏Format ו-Run במרחק נגיעה אחת; הוא מתרחק מהשורה שעורכים ומ-IntelliSense, ונעלם בזמן הקלדה במקלדת פיזית.
- **גלילה כמו בעורך טבעי.** גרירה עוקבת בדיוק אחרי האצבע, והטלה גולשת ומאטה לפי עקומת ההטלה של אנדרואיד עצמה, בקצב הרענון המלא של המסך, גם בקבצים של אלפי שורות עם צבעים, קווים אדומים, קיפול וכל השוליים. נגיעה שגוללת לא מזיזה את הסמן ולא פותחת את המקלדת.
- **מקלדת כשרוצים.** מחברים מקלדת וכל קיצורי הדרך של Visual Studio עובדים: F5, ‏Ctrl+Shift+B, ‏F12, ‏Ctrl+. וכל השאר.
- **מצב שולחני.** מחברים מסך ב-USB-C או HDMI, או מסך אלחוטי, וכל ה-IDE עובר למסך הגדול עם הקבצים, הסמן והדיבאגר בדיוק כמו שהיו. המקלדת של הטאבלט מקלידה שם, והעכבר או משטח המגע מזיזים שם סמן. הטאבלט הופך לשותף במגע: כפתורי הרצה ודיבאג, Toolbox שמכניס פקדים ל-XAML במקום הסמן, ו-Properties לאלמנט שבמקום הסמן.
- **בהיר, כהה או לפי המערכת.** ערכת צבעים בהירה בהשראת Visual Studio Light, הכהה, או מעבר אוטומטי לפי מצב הכהה של הטאבלט (**Tools > Theme**). המעבר צובע מחדש את כל ה-IDE בבת אחת, כולל צבעי התחביר בעורך, וכל צבע עומד בתקן הנגישות WCAG AA. בחירת ערכת צבעים של תוסף מעבירה את ה-IDE לבהיר או לכהה בהתאם.

### C# שרץ על הטאבלט

- **Roslyn בתוך האפליקציה.** המהדר האמיתי של C# מפעיל צביעה סמנטית, שגיאות חיות בזמן ההקלדה ואת ה-Error List.
- **F5 בפחות משנייה.** הפרויקטים מתקמפלים ישר לזיכרון ורצים ליד הקוד. קלט ופלט של הקונסולה נמצאים בחלון ה-Output.
- **IntelliSense.** השלמה אוטומטית, עזרה בחתימות, Quick Info, ‏Go to Definition, ‏Find All References, ‏Rename ותיקונים מהירים.
- **דיבאגר.** נקודות עצירה, Step Into, ‏Over ו-Out, ‏Locals, ‏Watch, ‏Call Stack, ‏DataTips ועצירה על חריגות.
- **בדיקות.** Test Explorer ל-xUnit, ‏NUnit ו-MSTest, ו-Live Unit Testing: וי ירוק, איקס אדום או קו כחול ליד כל שורה בקוד הנבדק, והבדיקות שעריכה משפיעה עליהן רצות שוב מעצמן רגע אחרי שמפסיקים להקליד.
- **NuGet על המכשיר.** החבילות משוחזרות על הטאבלט, כך שפרויקטים אמיתיים נבנים כמו שהם.

### ‏Python, ‏Node.js, ‏Go, ‏Rust, ‏C/C++ ו-Java

- **ה-Toolchain Store.** ‏**Tools > Toolchain Store** מתקין Python, ‏Node.js, ‏Go, ‏Rust, ‏C/C++ ו-Java על הטאבלט, בסביבת לינוקס שחיה בתוך האפליקציה: Alpine Linux שרץ עם PRoot, בלי root ובלי הרשאות מיוחדות. כל חבילה מגיעה מהשרתים של Alpine עצמה ונבדקת מול החתימות של Alpine לפני שמשהו מותקן.
- **פרויקטים שנפתחים כמו שהם.** במסך הפתיחה יש Python Application ו-Node.js Console App. פותחים תיקייה עם `pyproject.toml`, ‏`requirements.txt` או `package.json`, ו-TabStudio מציע להתקין את כלי הפיתוח ואת החבילות של הפרויקט. F5 מריץ אותו, עם הפלט ו-`input()` בחלון ה-Output, השגיאות ב-Error List, וכל שורה ב-traceback היא קישור לקוד שלה.
- **IntelliSense.** ‏Pyright מביא השלמה אוטומטית, קווים אדומים וניווט ל-Python, ו-typescript-language-server עושה את אותו הדבר ל-JavaScript ול-TypeScript.
- **דיבאגר.** F5 מדבג עם debugpy או vscode-js-debug, כולל תהליכי הבן של Node: נקודות עצירה, צעדים, Locals, ‏Watch, ‏Call Stack וחריגות. Ctrl+F5 מריץ בלי הדיבאגר.
- **התקנה בנגיעה אחת.** כשחסר שרת שפה או מתאם דיבאג, שורת המצב, העורך ו-F5 מציעים להתקין את כלי הפיתוח שמביא אותו, והשרת עולה ברגע שההתקנה מסתיימת.
- **טרמינל.** Ctrl+` פותח shell בסביבת הלינוקס, מצויר כמו ש-xterm מצייר.

Go: פותחים מודול או יוצרים Go Console App. ‏Ctrl+F5 בונה ומריץ; F5 מדבג עם Delve. ‏gopls מספק IntelliSense וניווט בקוד.

Rust: פותחים Cargo.toml או יוצרים Rust Console App. בוחרים חבר ב-workspace מתפריט פרויקט ההפעלה; Ctrl+F5 בונה ומריץ ו-F5 מדבג עם LLDB, לפי נתיב קובץ ההרצה ש-Cargo מדווח עליו.

C/C++: פותחים CMakeLists.txt, ‏Makefile או תיקיית קוד, או יוצרים C++ Console App (CMake). ‏clangd מספק IntelliSense, ‏CMake מייצא מסד נתוני קומפילציה ו-F5 מדבג עם LLDB.

Java: פותחים פרויקט Maven, ‏Gradle או קובצי Java, או יוצרים Java Console App (Maven). ‏Ctrl+F5 בונה ומריץ; F5 מדבג דרך JDT LS ו-java-debug.

### כל עומסי העבודה של Visual Studio, על אנדרואיד

| עומס עבודה | איך הוא רץ על הטאבלט |
| --- | --- |
| אפליקציות קונסולה וספריות | מקומפל בזיכרון, רץ בתוך התהליך |
| ASP.NET Core ו-Blazor | מוגש מקומית, תצוגה חיה ב-WebView |
| Avalonia | מצויר באופן טבעי בחלונית ליד העורך, עם מעצב XAML חי |
| Windows Forms | שכבת Forms חוצת פלטפורמות, עם מעצב גרור ושחרר במגע |
| WPF | ‏OpenSilver 3.3.3 ב-WebView, עם IntelliSense ל-XAML ותיקון מהיר להוספת event handler |
| WinUI | ‏Uno Platform, באופן טבעי על אנדרואיד |
| C, ‏C++, ‏Rust, ‏Go, ‏Python, ‏Node, ‏Java | כלי פיתוח טבעיים מה-Toolchain Store |
| כל מה שדורש Windows | ‏Windows Compatibility: האפליקציה האמיתית רצה בטאבלט עצמו דרך Wine; או ה-Windows Bridge, שבונה ומריץ אותה על המחשב שלכם ומזרים אותה לטאבלט |

**אפליקציות Windows Forms.** יוצרים Windows Forms App, או פותחים אחת מ-Visual Studio, ולוחצים F5. היא רצה ליד הקוד על שכבת ה-Windows Forms של TabStudio עצמו, בחלון של Windows 10, עם דיאלוגים, תפריטים, טבלאות, data binding ו-MessageBox, ושמירת קובץ מפעילה אותה מחדש. ‏`Form1.cs` נפתח במעצב מגע: נוגעים בכלי ואז בטופס, גוררים פקדים על הרשת, משנים את הגודל שלהם בידיות, עורכים אותם בחלון ה-Properties, ונגיעה כפולה בכפתור כותבת את ה-Click handler שלו. המעצב כותב את `Form1.Designer.cs` בדיוק כמו Visual Studio, כך שהפרויקט ממשיך להיפתח במחשב. קוד שקורא ל-Windows עצמו, כמו P/Invoke, ‏COM או ה-registry, רץ דרך Windows Compatibility או גשר Windows; [ה-README של שכבת ה-Forms](src/TabStudio.Forms/README.md) מפרט מה נתמך.

**אפליקציות WPF.** יוצרים **WPF Application**, או פותחים פרויקט WPF ב-C# מ-Visual Studio, ולוחצים F5. ‏TabStudio מקמפל את ה-XAML בעזרת OpenSilver ומריץ את האפליקציה בתצוגת האינטרנט; שמירה מפעילה אותה מחדש, ופלט הקונסול והחריגות מופיעים ב-Output. להרצת תצוגת .NET 10 נדרש Android System WebView עדכני. עורך ה-XAML מציע השלמה, Quick Info, מעבר להגדרה, שגיאות בזמן ההקלדה ונורה שמוסיפה event handler חסר לקובץ הקוד. API של WPF שאינו נתמך מופיע בשמו ב-Error List, עם הפניה לגשר Windows. בתצוגת הדפדפן אין דיבאגר ל-C#, ותכונות שדורשות Windows, כולל תלת-ממד וחלונות מודאליים של WPF, דורשות את גשר Windows.

[השלמת XAML](docs/wpf/images/wpf-01-xaml-completion.png) · [הוספת event handler](docs/wpf/images/wpf-02-add-event-handler.png)

**WinUI 3 ו-Uno Platform.** יוצרים אחת מהתבניות או פותחים פרויקט C# ולוחצים F5. האפליקציה נבנית על הטאבלט ומוצגת ליד הקוד באמצעות Skia של Uno; שמירת XAML או C# מפעילה אותה מחדש.

**גשר Windows.** כל מה שדורש Windows נבנה ורץ על מחשב שלכם, והחלונות שלו משודרים לטאבלט. במחשב פותחים את TabStudio ובוחרים **File > TabStudio Link > Be a Windows Bridge**, ובטאבלט סורקים את קוד ה-QR שלו. מאותו רגע WPF, ‏Windows Forms האמיתי, WinUI 3, ‏.NET Framework ו-Visual C++ מסומנים כמוכנים ב-Compatibility Doctor, וכפתור ההרצה יכול להריץ כל פרויקט על המחשב. הבנייה זורמת לחלון ה-Output והשגיאות שלה מגיעות ל-Error List על הקבצים שלכם. נגיעה היא קליק, לחיצה ארוכה היא קליק ימני ושתי אצבעות גוללות; מקלדת והעתקת טקסט עובדות בשני הכיוונים. אפליקציות ‎.NET מדובגות באותו דיבאגר, כמה תוכניות רצות יחד, שמירה מפעילה מחדש רק את התוכנית שהשתנתה, ומחשב שנרדם מקבל הודעה ברורה.

### כל המסכים בבת אחת

- **‏Run on Swarm.** טלפונים וטאבלטים שמריצים TabStudio מצטרפים ל-Swarm שלכם בסריקה אחת (**File > Join a Swarm** בטלפון). בוחרים Swarm בתפריט ההרצה, לוחצים F5, והאפליקציה שאתם בונים עולה בכולם יחד, וכל שמירה מעדכנת את כולם. אפליקציות Avalonia רצות באופן טבעי, אתרים בדפדפן של כל מכשיר, ואפליקציות קונסול שולחות את הפלט שלהן חזרה.
- **דיבאג בכל מכשיר.** לוחצים על כפתור בטלפון, ונקודת העצירה שלו עוצרת בטאבלט עם ה-Locals וה-Call Stack של הטלפון. עוצרים את המכשיר הזה או את כולם.
- **מגע משוכפל.** נגיעה, גלילה או הקלדה במכשיר אחד קורות בכל המכשירים, כך שמשווים את אותו מסך בכל הגדלים.
- **‏Device Matrix.** כל המסכים זה לצד זה: הטלפונים שלכם, ה-App Preview ומכשירים וירטואליים. אין טלפונים נוספים? הטאבלט מציג את האפליקציה זה לצד זה כטלפון קטן, טלפון גדול וטאבלט, בבהיר ובכהה, משמאל לימין ומימין לשמאל, ועם גופנים גדולים.
- **בדיקות פריסה.** אחרי כל שינוי TabStudio בודק כל מסך: טקסט חתוך, פקדים שנדחפו מחוץ למסך, פקדים חופפים, מטרות מגע קטנות מ-44 פיקסלים וניגודיות נמוכה. כל ממצא מגיע ל-Error List עם המכשיר וצילום מסך, ו-**Fix with Agent** במרחק נגיעה.
- **בדיקות E2E מוקלטות.** מקליטים כמה נגיעות ומקבלים בדיקת UI שמריצה אותן שוב בכל מכשיר וב-CI, עם תוצאה לכל מכשיר ב-Test Explorer.

### סייר מסדי נתונים

ב־**View > Database Explorer**, בשורת הפעילויות ובחיפוש Ctrl+Q: חיבור לקובצי SQLite, ל־PostgreSQL באמצעות Npgsql ול־SQL Server באמצעות Microsoft.Data.SqlClient. העץ מציג מסדי נתונים, סכמות, טבלאות, תצוגות, עמודות, מפתחות ואינדקסים. אפשר לגלות מחרוזות חיבור מתוך `appsettings*.json` ו־user secrets של הפתרון ולבחור אילו לשמור. ב־Android פרטי החיבור מוצפנים באמצעות מפתח ב־Android Keystore; במחשב הם נשמרים בזיכרון למשך ההפעלה.

**New query** פותח לשונית SQL עם השלמה מהסכמה החיה. **Ctrl+Enter** מריץ את הבחירה או את כל השאילתה, ו־Cancel מבטל. התוצאות כוללות מיון, העתקה כ־CSV או JSON, כמה קבוצות תוצאות, מספר שורות וזמני ריצה. **Edit data** מציג נתונים בעמודים ומאפשר עדכון תא עם בדיקה שהשורה לא שונתה בינתיים. לשונית **EF Core** משתמשת בבנייה הקיימת בזיכרון להצגת DbContexts ומיגרציות, עדכון מסד הנתונים ויצירת מיגרציה. [הוראות ומגבלות](docs/DATABASE.md).

### פותחים כל מאגר קוד

- **מדביקים קישור ל-GitHub ולוחצים F5.** ‏TabStudio קורא את `global.json`, ‏`package.json`, ‏`Cargo.toml`, ‏`go.mod`, ‏`CMakeLists.txt` וחבריהם, מתקין את כלי הפיתוח שצריך ומגדיר בנייה, הרצה ודיבאג.
- **Compatibility Doctor.** לכל פרויקט בפתרון הוא מראה איפה הוא רץ על הטאבלט ולמה, ומתקן את מה שמפריע.
- **הרחבות.** הרחבות VS Code מ-Open VSX, כך שכל שפה עם שרת שפה עובדת.
- **מחברות.** מחברות C# שהתאים שלהן רצים בזמן שאתם כותבים.

### ‏GitHub, בתוך ה-IDE

- **Git מובנה.** ‏clone, ענפים, commit, ‏pull ו-push, ופתיחת pull requests ב-GitHub בלי לצאת מה-IDE.
- **סקירת Pull Requests.** ‏pull request נפתח בעמוד עם התיאור, השיחה, הקבצים שהשתנו וה-commits. כותבים הערות על שורות ב-diff, ומסיימים את הסקירה כהערה, כאישור או כבקשת שינויים. אפשר למזג אותו, או לעבור לענף שלו כדי להריץ את הקוד והבדיקות; בענף הזה העורך מציג את השיחות ליד השורות שלהן. ‏**Review with Claude** מנסח טיוטות הערות שאתם שומרים, עורכים או מוחקים לפני שמשהו נשלח.
- **CI בשורת הסטטוס.** הבדיקות של ה-commit שלכם מופיעות ליד הענף: סמל טעינה עם "2/5" בזמן שהן רצות, ואחר כך אדום או ירוק. נגיעה פותחת את חלון GitHub Actions.
- **‏GitHub Actions.** ההרצות של הענף, עם ה-jobs והשלבים שלהן. אפשר להריץ מחדש את כל ה-jobs או רק את אלה שנכשלו, לעצור הרצה, או להפעיל workflow מטופס שנבנה מהקלטים שלו.
- **לוגים שאפשר לקרוא.** הלוג של job נפתח בלשונית, מקופל לפי שלבים וגלול לשגיאה הראשונה. ‏F8 קופץ לשגיאה הבאה.
- **שגיאות CI ב-Error List.** מה שנכשל ב-CI מופיע לצד השגיאות המקומיות, ו-**Fix with Agent** יודע איזו בדיקה דיווחה עליו.
- **מאגרים במעקב.** עוקבים אחרי ה-CI של כל מאגר, ומקבלים התראה בטאבלט כשהרצה מסתיימת.

### שותף תכנות מבוסס AI

- **Claude, בתוך ה-IDE.** הסוכן רואה את כל הפתרון, בונה אותו, מריץ את הבדיקות ומתקן את השגיאות שהוא מוצא, וכל שינוי שלו מגיע כ-diff שאתם מאשרים.
- **נגיעה אחת מהבעיה לתיקון.** כשה-Compatibility Doctor מוצא API שלא רץ על הטאבלט, **Fix with Agent** משכתב את הקוד ומראה לכם את התוצאה.
- **מסקיצה ל-XAML.** מציירים מסך עם העט ומקבלים XAML שרץ.
- **המרה ל-cross-platform.** נגיעה אחת ממירה פרויקט WPF או Windows Forms לפרויקט Avalonia שרץ באופן טבעי בכל מקום. ממיר משכתב את כל מה שממופה במדויק, Claude משלים את השאר, ואתם בודקים הכול כ-diff.
- **מדבר MCP.** הסוכן מתחבר לכל שרת Model Context Protocol, כך שהכלים והשירותים שלכם במרחק הגדרה אחת.

### בנוי למכשיר שבידיים שלכם

- **רץ גם על טאבלטים צנועים.** ‏TabStudio מודד את המכשיר שהוא רץ עליו ומתאים את עצמו. בטאבלט עם 3 GB הוא מחזיק מודל מהדר מלא רק של הפרויקטים שאתם עורכים, קורא את המטא-דאטה של ‎.NET ישר מהאחסון במקום להחזיק אותה בזיכרון, ומחזיר זיכרון ברגע שאנדרואיד מבקש.
- **שואלים כוח מהטלפון.** עם TabStudio Link, אותה אפליקציה בטלפון או במחשב הופכת לשרת בנייה ושפה בשביל הטאבלט, דרך ה-Wi-Fi המקומי. הטאבלט מציג את ה-IDE, והמכשיר החזק יותר עושה את העבודה הכבדה. בלי ענן ובלי חשבון: מצמידים פעם אחת עם קוד QR, והחיבור מוצפן מקצה לקצה. אם הטלפון מתרחק, הטאבלט ממשיך לבנות לבד ומתחבר מחדש מעצמו.
- **אף פעם לא מאבדים את המקום.** במכשירים קטנים אנדרואיד סוגר אפליקציות ברקע. ‏TabStudio שומר את הסשן תוך כדי עבודה, כך שהוא נפתח בדיוק איפה שהפסקתם, כולל שינויים שלא נשמרו.
- **בנייה שמודעת לחום.** כשהטאבלט מתחמם, ‏TabStudio מקמפל על פחות ליבות במקום לתת לאנדרואיד להאט את הכול, ושורת המצב מסבירה למה.
- **עובד בלי אינטרנט.** הכול חוץ מהורדות, שרתי Git מרוחקים והסוכן עובד בלי חיבור.

הסדר שבו כל זה יוצא נמצא ב[מפת הדרכים](docs/ROADMAP.md).

## איך זה עובד

‏TabStudio היא אפליקציית אנדרואיד אחת שבנויה ב-.NET וב-[Avalonia](https://avaloniaui.net). כל מה שהיא צריכה נמצא בתוך ה-APK או מורד על ידי האפליקציה עצמה.

**המעטפת** היא החלק שרואים: סידור מעוגן בסגנון Visual Studio, מותאם למגע, עם אזורי לחיצה בגודל של קצה אצבע וסרגל פעולות צף לדברים שאחרת הייתם עושים במקלדת.

**שירות השפה** מריץ את [Roslyn](https://github.com/dotnet/roslyn), המהדר האמיתי של C#, בתוך האפליקציה. הוא מחזיק מודל חי של כל פרויקט בפתרון, וזה מה שגורם לשגיאות להופיע בזמן ההקלדה ומה שמפעיל צביעה סמנטית, השלמה אוטומטית ו-refactoring. שפות אחרות מקבלות את אותן יכולות משרתי השפה שלהן.

**מנוע הבנייה** הופך פרויקט למשהו שאפשר להריץ. ל-TabStudio יש מערכת פרויקטים משלו שקוראת קובצי פרויקט בסגנון SDK, ‏`Directory.Build.props`, הפניות בין פרויקטים וחבילות NuGet, שמשוחזרות על המכשיר. ב-C#, ‏Roslyn מקמפל ישר לזיכרון מול reference assemblies שמגיעות עם האפליקציה, כך שמחזור העריכה וההרצה לא נוגע בדיסק בכלל. שפות אחרות נבנות עם כלי פיתוח טבעיים מה-Toolchain Store.

**מארחי הרצה** הם המקום שבו התוכנית שלכם באמת רצה. ‏F5 בוחר אחד לפי סוג הפרויקט: אפליקציות קונסולה רצות בתוך התהליך בהקשר טעינה מבודד, אפליקציות web רצות ב-WebView ליד העורך, ואפליקציות שדורשות Windows רצות בטאבלט עצמו דרך Windows Compatibility, או על מחשב שלכם דרך ה-Windows Bridge.

אנדרואיד מגביל מה אפליקציה רשאית להריץ, ולכן כל החלטת תכנון כאן מתחילה מהכללים של הפלטפורמה במקום להילחם בהם. הפרטים, כולל למה האפליקציה לא תלויה במכונה הווירטואלית של אנדרואיד, נמצאים ב[מסמך הארכיטקטורה](docs/ARCHITECTURE.md).

## דרישות

### כדי להשתמש ב-TabStudio

| | מינימום | מומלץ |
| --- | --- | --- |
| אנדרואיד | ‏9 ‏(API 28) | ‏14 ומעלה |
| מעבד | ARM של 32 או 64 ביט (armeabi-v7a, arm64-v8a) | מעבד ARM64 עדכני עם 8 ליבות |
| זיכרון | ‏3 GB לבד, ‏2 GB עם TabStudio Link | ‏8 GB ומעלה לפתרונות גדולים |
| אחסון | ‏1 GB פנוי | ‏4 GB פנויים אחרי הוספת כלי פיתוח |
| קלט | מגע | מקלדת ומשטח מגע, לעבודה ארוכה |

מקלדת היא לא חובה. לכל דבר ב-TabStudio אפשר להגיע במגע, וכל קיצורי הדרך של Visual Studio עובדים כשמקלדת מחוברת.

זה מכסה את רוב הטאבלטים שנמכרו בשש השנים האחרונות, כולל טאבלטים זולים וטאבלטים של בתי ספר שמריצים אנדרואיד של 32 ביט על שבב של 64 ביט.

### כדי לבנות את TabStudio מקוד המקור

- [‏.NET 10 SDK](https://dotnet.microsoft.com/download) עם ה-workload של אנדרואיד: `dotnet workload install android`
- ‏JDK 17 ומעלה ו-Android SDK ‏(API 36), שבהם ה-workload של אנדרואיד משתמש
- כל מערכת הפעלה: Windows, ‏macOS או Linux

## איך מתחילים

### התקנה על טאבלט

1. פתחו את [הגרסה האחרונה](https://github.com/yahav2898/tabstudio/releases) בטאבלט.
2. הורידו את הקובץ `TabStudio-<version>.apk` ופתחו אותו. אנדרואיד יבקש פעם אחת לאשר התקנות מהדפדפן.
3. הפעילו את TabStudio, הקישו **Open the sample** ולחצו **F5**.

### בנייה והרצה מקוד המקור

</div>

```bash
git clone https://github.com/yahav2898/tabstudio.git
cd tabstudio

# Run the tests
dotnet test

# Run the desktop build, handy for development
dotnet run --project src/TabStudio.Desktop

# Build the Android APK
dotnet publish src/TabStudio.Android -c Release -f net10.0-android
```

<div dir="rtl">

הגרסה השולחנית קיימת כדי שתורמים יוכלו לעבוד על TabStudio מכל מחשב. המוצר הוא הטאבלט.

## מבנה המאגר

</div>

```
src/
  TabStudio.Core/          Workspace model: solutions, projects, stack detection, Compatibility Doctor
  TabStudio.Compilation/   Roslyn: diagnostics, IntelliSense, in-memory compilation, running programs
  TabStudio.Packages/      NuGet restore on the device
  TabStudio.Debugging/     The debugger: instrumented builds, breakpoints, stepping, inspection
  TabStudio.TestRunner/    xUnit, NUnit and MSTest discovery and execution
  TabStudio.LiveTesting/   Live Unit Testing: coverage instrumentation, impact analysis, the live test engine
  TabStudio.Notebooks/     C# notebooks: .ipynb and .dib, the C# kernel, value formatting
  TabStudio.SourceControl/ Git through GitHub: changes, commits, pulls and merges, branches, pull requests, Actions
  TabStudio.Agent/         Claude as a coding agent: tools over the workspace, MCP servers, sketch to XAML
  TabStudio.Link/          TabStudio Link: pairing, encrypted transport, QR codes and the remote language service
  TabStudio.Swarm/         Swarm: running, updating, debugging and recording an app on every joined device
  TabStudio.Forms/         Windows Forms on Avalonia: System.Windows.Forms and System.Drawing, drawn as on Windows 10
  TabStudio.Forms.Design/  The touch designer's model: reads and writes InitializeComponent, the canvas and Toolbox
  TabStudio.Web/           Web apps: static web assets, Blazor's scripts, a host for .NET apps on WebAssembly
  TabStudio.Wpf/           WPF on OpenSilver: XAML compilation and the browser preview
  TabStudio.Uno/           WinUI and Uno: XAML compilation and the native Skia preview
  TabStudio.Toolchains/    The Toolchain Store: Alpine Linux on PRoot, packages, processes and the terminal
  TabStudio.Remote/        SSH.NET, Android Keystore, SFTP source caches, remote shells and publishing
  TabStudio.LanguageServers/ Language Server and Debug Adapter Protocol clients for every non-C# language
  TabStudio.Extensions/    VS Code extensions from Open VSX: grammars, language rules, snippets, themes
  TabStudio.App/           The IDE itself: views and view models, shared by every platform
  TabStudio.Desktop/       Desktop host for development
  TabStudio.Android/       The Android app
tests/                     Unit tests for every library, plus shared test helpers
samples/                   The sample solution bundled with the app
docs/                      Architecture, roadmap and design notes
build/                     CI scripts
```

<div dir="rtl">

## תרומה לפרויקט

‏TabStudio נבנה בגלוי, ועכשיו זה עדיין מספיק מוקדם כדי שתורם אחד יעצב פיצ'ר שלם. מקומות טובים להתחיל בהם נמצאים ב[מפת הדרכים](docs/ROADMAP.md). קראו את [CONTRIBUTING.md](CONTRIBUTING.md) כדי לראות איך הקוד מאורגן ואיך נראה pull request טוב.

## רישיון וסימנים מסחריים

‏TabStudio מופץ תחת [רישיון Apache 2.0](LICENSE). התוכנות של צד שלישי שהוא כולל או מוריד, כל אחת תחת הרישיון שלה, מפורטות ב-[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

‏TabStudio הוא פרויקט עצמאי. הוא לא קשור ל-Microsoft, לא מאושר על ידה ולא ממומן על ידה. ‏Visual Studio הוא סימן מסחרי של Microsoft Corporation, ומוזכר כאן רק כדי לתאר את סוג החוויה ש-TabStudio מציע. ‏TabStudio לא מכיל קוד של Microsoft מלבד רכיבי ה-.NET בקוד פתוח שעליהם הוא בנוי, תחת הרישיונות שלהם.

</div>

## שרתי רשת ואפליקציות מובייל

כששרת מקומי שמופעל מהסביבה מתחיל להאזין, מופיעה הודעת **App listening on port N** עם פתיחה בתצוגה
לצד הקוד או בדפדפן המכשיר. גם בסרגל התצוגה של ASP.NET ו-Blazor יש **Open in browser**.

הערך **Android SDK** בחנות הכלים מתקין OpenJDK 17, Gradle, כלי SDK ופלטפורמה 35, וכלי בנייה ל-ARM64/ARMv7
עם אימות SHA-256. התבנית **Android App (Kotlin)** נבנית בטאבלט; F5 מבקש אישור התקנה ממערכת Android ומפעיל
את האפליקציה. חלונית **Logcat** מציגה את הלוגים של תהליך האפליקציה שנבנתה, והקומפיילר שולח שגיאות לרשימת
השגיאות. השלמה ל-Java משתמשת ב-jdtls, ול-Kotlin ב-kotlin-language-server.

התבנית **Capacitor App** יוצרת Vite ו-TypeScript: F5 מפעיל את צד הרשת בתצוגה, ו-**Run Android App** בונה
ומתקין את הצד המקורי. **Build for iOS…** יוצר תהליך GitHub Actions לבנייה בשרת macOS ומסביר את סודות
החתימה הדרושים; **Run iOS Workflow…** פותח את חלון Actions למעקב ולוגים ולהורדת IPA. לא צריך Mac מקומי;
נדרש חשבון Apple Developer ואמצעי חתימה. הוראות ומגבלות נמצאות ב-[מדריך המובייל](docs/MOBILE.md).
