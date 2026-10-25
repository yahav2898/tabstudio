<div dir="rtl">

<div align="center">

# TabStudio

**סביבת פיתוח אמיתית לטאבלטים של אנדרואיד. פותחים `.sln`, לוחצים F5, והקוד רץ.**

[![License: Apache-2.0](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)
[![CI](https://github.com/yahav2898/tabstudio/actions/workflows/ci.yml/badge.svg)](https://github.com/yahav2898/tabstudio/actions/workflows/ci.yml)
![Platform: Android 10+](https://img.shields.io/badge/platform-Android%2010%2B%20·%20arm64-3DDC84.svg)
![Built with .NET 10](https://img.shields.io/badge/.NET-10-512BD4.svg)

[English](README.md) · [ארכיטקטורה](docs/ARCHITECTURE.md) · [מפת דרכים](docs/ROADMAP.md) · [תרומה לפרויקט](CONTRIBUTING.md)

</div>

---

## במבט אחד

TabStudio הופך טאבלט אנדרואיד למחשב פיתוח שבאמת תרצו לעבוד עליו.

- **אפליקציה אחת.** מורידים, פותחים, כותבים קוד. בלי Termux, בלי root ובלי התקנות נוספות.
- **הסידור שאתם כבר מכירים.** Solution Explorer, Properties, Error List, Output וסרגל כלים עם כפתור Run גדול. מי שעבד ב-Visual Studio יודע איפה הכול נמצא.
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
- **בגודל של אצבעות.** כל אזור לחיצה הוא לפחות 44 פיקסלים. סרגל פעולות צף שם Rename, ‏Comment, ‏Format, ‏Peek ו-Run to Cursor במרחק נגיעה אחת.
- **מקלדת כשרוצים.** מחברים מקלדת וכל קיצורי הדרך של Visual Studio עובדים: F5, ‏Ctrl+Shift+B, ‏F12, ‏Ctrl+. וכל השאר.
- **מצב שולחני.** מחברים מסך, הסידור המלא עובר למסך הגדול, והטאבלט הופך ללוח Toolbox ו-Properties במגע.

### C# שרץ על הטאבלט

- **Roslyn בתוך האפליקציה.** המהדר האמיתי של C# מפעיל צביעה סמנטית, שגיאות חיות בזמן ההקלדה ואת ה-Error List.
- **F5 בפחות משנייה.** הפרויקטים מתקמפלים ישר לזיכרון ורצים ליד הקוד. קלט ופלט של הקונסולה נמצאים בחלון ה-Output.
- **IntelliSense.** השלמה אוטומטית, עזרה בחתימות, Quick Info, ‏Go to Definition, ‏Find All References, ‏Rename ותיקונים מהירים.
- **דיבאגר.** נקודות עצירה, Step Into, ‏Over ו-Out, ‏Locals, ‏Watch, ‏Call Stack, ‏DataTips ועצירה על חריגות.
- **בדיקות.** Test Explorer ל-xUnit, ‏NUnit ו-MSTest, וסימוני Live Unit Testing ליד כל שורה.
- **NuGet על המכשיר.** החבילות משוחזרות על הטאבלט, כך שפרויקטים אמיתיים נבנים כמו שהם.

### כל עומסי העבודה של Visual Studio, על אנדרואיד

| עומס עבודה | איך הוא רץ על הטאבלט |
| --- | --- |
| אפליקציות קונסולה וספריות | מקומפל בזיכרון, רץ בתוך התהליך |
| ASP.NET Core ו-Blazor | מוגש מקומית, תצוגה חיה ב-WebView |
| Avalonia | מצויר באופן טבעי בחלונית ליד העורך |
| Windows Forms | שכבת Forms חוצת פלטפורמות, עם מעצב גרור ושחרר במגע |
| WPF | ‏OpenSilver, ב-WebView |
| WinUI | ‏Uno Platform, באופן טבעי על אנדרואיד |
| C, ‏C++, ‏Rust, ‏Go, ‏Python, ‏Node, ‏Java | כלי פיתוח טבעיים מה-Toolchain Store |
| כל מה שדורש Windows | ה-Windows Bridge: נבנה ורץ על המחשב שלכם ומוזרם לטאבלט |

### פותחים כל מאגר קוד

- **מדביקים קישור ל-GitHub ולוחצים F5.** ‏TabStudio קורא את `global.json`, ‏`package.json`, ‏`Cargo.toml`, ‏`go.mod`, ‏`CMakeLists.txt` וחבריהם, מתקין את כלי הפיתוח שצריך ומגדיר בנייה, הרצה ודיבאג.
- **Compatibility Doctor.** לכל פרויקט בפתרון הוא מראה איפה הוא רץ על הטאבלט ולמה, ומתקן את מה שמפריע.
- **הרחבות.** הרחבות VS Code מ-Open VSX, כך שכל שפה עם שרת שפה עובדת.
- **מחברות.** מחברות C# שהתאים שלהן רצים בזמן שאתם כותבים.

### שותף תכנות מבוסס AI

- **Claude, בתוך ה-IDE.** הסוכן רואה את כל הפתרון, בונה אותו, מריץ את הבדיקות ומתקן את השגיאות שהוא מוצא, וכל שינוי שלו מגיע כ-diff שאתם מאשרים.
- **נגיעה אחת מהבעיה לתיקון.** כשה-Compatibility Doctor מוצא API שלא רץ על הטאבלט, **Fix with Agent** משכתב את הקוד ומראה לכם את התוצאה.
- **מסקיצה ל-XAML.** מציירים מסך עם העט ומקבלים XAML שרץ.
- **מדבר MCP.** הסוכן מתחבר לכל שרת Model Context Protocol, כך שהכלים והשירותים שלכם במרחק הגדרה אחת.

### חוזים חכמים ו-dApps, עם Supra מובנה

‏TabStudio הוא הדרך המהירה ביותר להגיע מרעיון ל-dApp עובד על [Supra](https://supra.com), מכל מכשיר.

- **Solidity ו-Move.** השלמה ושגיאות חיות משרתי השפה שלהן, Foundry לבנייה ובדיקה של Solidity, והמהדר של Move לחבילות Supra Move.
- **חלונית Supra.** מעבר בין mainnet, ‏testnet ו-devnet, יצירת חשבון ומילוי שלו מה-faucet בנגיעה אחת, עיון במשאבים, במודולים ובאירועים של חשבון, מעקב חי אחרי כל מחיר מה-oracle, ומעקב אחרי משימות האוטומציה שלכם.
- **תבניות שעובדות מהנגיעה הראשונה.** כספת שמופעלת לפי מחיר עם ה-oracle והאוטומציה של Supra, הגרלה הוגנת על Supra dVRF, תשלום בין SupraEVM ל-SupraMVM, ‏market maker ל-SupraFX ולוח בקרה on-chain ב-C#. כל אחת נפתחת עם בדיקות עוברות ונפרסת ל-devnet בנגיעה אחת.
- **Supra SDK ל-.NET.** חשבונות, טרנזקציות, אירועים, מחירי oracle, ‏SupraEVM ו-SupraFX מתוך C#, בקוד פתוח ושמיש בכל אפליקציית ‎.NET.
- **Migration Doctor.** פותחים dApp ב-Solidity שבנוי על price feeds, ‏VRF או Automation של Chainlink, ו-TabStudio מראה את המקבילה של Supra לכל קריאה ומחיל את השינוי בענף נפרד לסקירה שלכם.
- **Supra לסוכני AI.** הסוכן מתחבר ל-SupraFX דרך שרת ה-MCP שלה, כך שאפשר לשאול על יתרות, ספר הפקודות או מחירים בשפה פשוטה. ‏TabStudio גם מגיע עם שרת MCP *לבנייה* על Supra, שעובד עם Claude Code, ‏Cursor וכל לקוח MCP אחר.
- **בטוח כברירת מחדל.** כל תבנית נפרסת קודם ל-testnet, מפתחות ראשיים אף פעם לא מתבקשים ולא נשמרים, סוכני מסחר משתמשים במפתחות delegate של SupraFX עם תקרות הוצאה, וכל טרנזקציה מוצגת במלואה ומאושרת על ידכם.

התכנון המלא, עם קישורים לתיעוד של Supra, נמצא ב-[docs/design/web3-supra.md](docs/design/web3-supra.md).

### בנוי למכשיר שבידיים שלכם

- **בנייה שמודעת לחום.** כשהטאבלט מתחמם, ‏TabStudio מקמפל על פחות ליבות במקום לתת לאנדרואיד להאט את הכול, ושורת המצב מסבירה למה.
- **עובד בלי אינטרנט.** הכול חוץ מהורדות, שרתי Git מרוחקים והסוכן עובד בלי חיבור.
- **Git מובנה.** ‏clone, ענפים, commit, ‏pull ו-push, ופתיחת pull requests ב-GitHub בלי לצאת מה-IDE.

הסדר שבו כל זה יוצא נמצא ב[מפת הדרכים](docs/ROADMAP.md).

## איך זה עובד

‏TabStudio היא אפליקציית אנדרואיד אחת שבנויה ב-.NET וב-[Avalonia](https://avaloniaui.net). כל מה שהיא צריכה נמצא בתוך ה-APK או מורד על ידי האפליקציה עצמה.

**המעטפת** היא החלק שרואים: סידור מעוגן בסגנון Visual Studio, מותאם למגע, עם אזורי לחיצה של 44 פיקסלים וסרגל פעולות צף לדברים שאחרת הייתם עושים במקלדת.

**שירות השפה** מריץ את [Roslyn](https://github.com/dotnet/roslyn), המהדר האמיתי של C#, בתוך האפליקציה. הוא מחזיק מודל חי של כל פרויקט בפתרון, וזה מה שגורם לשגיאות להופיע בזמן ההקלדה ומה שמפעיל צביעה סמנטית, השלמה אוטומטית ו-refactoring. שפות אחרות מקבלות את אותן יכולות משרתי השפה שלהן.

**מנוע הבנייה** הופך פרויקט למשהו שאפשר להריץ. ל-TabStudio יש מערכת פרויקטים משלו שקוראת קובצי פרויקט בסגנון SDK, ‏`Directory.Build.props`, הפניות בין פרויקטים וחבילות NuGet, שמשוחזרות על המכשיר. ב-C#, ‏Roslyn מקמפל ישר לזיכרון מול reference assemblies שמגיעות עם האפליקציה, כך שמחזור העריכה וההרצה לא נוגע בדיסק בכלל. שפות אחרות נבנות עם כלי פיתוח טבעיים מה-Toolchain Store.

**מארחי הרצה** הם המקום שבו התוכנית שלכם באמת רצה. ‏F5 בוחר אחד לפי סוג הפרויקט: אפליקציות קונסולה רצות בתוך התהליך בהקשר טעינה מבודד, אפליקציות web רצות ב-WebView ליד העורך, ואפליקציות שדורשות Windows יכולות לרוץ על מחשב שלכם דרך ה-Windows Bridge.

אנדרואיד מגביל מה אפליקציה רשאית להריץ, ולכן כל החלטת תכנון כאן מתחילה מהכללים של הפלטפורמה במקום להילחם בהם. הפרטים, כולל למה האפליקציה לא תלויה במכונה הווירטואלית של אנדרואיד, נמצאים ב[מסמך הארכיטקטורה](docs/ARCHITECTURE.md).

## דרישות

### כדי להשתמש ב-TabStudio

| | מינימום | מומלץ |
| --- | --- | --- |
| אנדרואיד | ‏10 ‏(API 29) | ‏14 ומעלה |
| מעבד | ARM של 64 ביט (arm64-v8a) | מעבד עדכני עם 8 ליבות |
| זיכרון | ‏6 GB | ‏8 GB ומעלה לפתרונות גדולים |
| אחסון | ‏1 GB פנוי | ‏4 GB פנויים אחרי הוספת כלי פיתוח |
| קלט | מגע | מקלדת ומשטח מגע, לעבודה ארוכה |

מקלדת היא לא חובה. לכל דבר ב-TabStudio אפשר להגיע במגע, וכל קיצורי הדרך של Visual Studio עובדים כשמקלדת מחוברת.

### כדי לבנות את TabStudio מקוד המקור

- [‏.NET 10 SDK](https://dotnet.microsoft.com/download) עם ה-workload של אנדרואיד: `dotnet workload install android`
- ‏JDK 17 ומעלה ו-Android SDK ‏(API 36), שבהם ה-workload של אנדרואיד משתמש
- כל מערכת הפעלה: Windows, ‏macOS או Linux

## איך מתחילים

### התקנה על טאבלט

1. פתחו את [הגרסה האחרונה](https://github.com/yahav2898/tabstudio/releases) בטאבלט.
2. הורידו את `TabStudio.apk` ופתחו אותו. אנדרואיד יבקש פעם אחת לאשר התקנות מהדפדפן.
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
  TabStudio.Compilation/   Roslyn: diagnostics, in-memory compilation, running programs
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

‏TabStudio מופץ תחת [רישיון Apache 2.0](LICENSE).

‏TabStudio הוא פרויקט עצמאי. הוא לא קשור ל-Microsoft, לא מאושר על ידה ולא ממומן על ידה. ‏Visual Studio הוא סימן מסחרי של Microsoft Corporation, ומוזכר כאן רק כדי לתאר את סוג החוויה ש-TabStudio מציע. ‏TabStudio לא מכיל קוד של Microsoft מלבד רכיבי ה-.NET בקוד פתוח שעליהם הוא בנוי, תחת הרישיונות שלהם.

</div>
