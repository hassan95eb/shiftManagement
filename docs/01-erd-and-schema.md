# ShiftFlow — ERD و طراحی نهایی دیتابیس

**دیتابیس:** SQL Server (داخل Docker)
**وضعیت:** پیش‌نویس برای تأیید — قبل از نوشتن هر خط کد

---

## ۱. ERD

```text
                        ┌──────────────┐
                        │    Users     │
                        │──────────────│
                        │ Id (PK)      │
                        │ Username (U) │
                        │ PasswordHash │
                        │ Role         │
                        └──────┬───────┘
                               │ 1:1
                 ┌─────────────┴─────────────┐
                 │                           │
                 ▼                           ▼
         ┌───────────────┐           ┌───────────────┐
         │   Employers   │           │    Experts    │
         │───────────────│           │───────────────│
         │ Id (PK)       │           │ Id (PK)       │
         │ UserId (FK,U) │           │ UserId (FK,U) │
         │ Name          │           │ FullName      │
         └───────┬───────┘           └───┬───┬───┬───┘
                 │ 1:N                   │   │   │
                 ▼                       │   │   │ 1:N
         ┌───────────────┐               │   │   ▼
         │   Projects    │               │   │  ┌──────────────────┐
         │───────────────│               │   │  │  Availabilities  │
         │ Id (PK)       │               │   │  │──────────────────│
         │ EmployerId(FK)│               │   │  │ Id (PK)          │
         │ Name          │               │   │  │ ExpertId (FK)    │
         └───┬───────┬───┘               │   │  │ StartUtc         │
             │       │                   │   │  │ EndUtc           │
             │  N:N  │                   │   │  └──────────────────┘
             │  ┌────▼───────────────────▼┐  │
             │  │     ExpertProjects      │  │ 1:N
             │  │─────────────────────────│  ▼
             │  │ ExpertId  (PK,FK)       │ ┌──────────────────┐
             │  │ ProjectId (PK,FK)       │ │  ExpertRatings   │
             │  └─────────────────────────┘ │──────────────────│
             │ 1:N                          │ Id (PK)          │
             ▼                              │ ExpertId (FK)    │
     ┌────────────────┐                     │ Period  (U)      │
     │     Shifts     │                     │ Score            │
     │────────────────│                     └──────────────────┘
     │ Id (PK)        │
     │ ProjectId (FK) │
     │ StartUtc       │
     │ EndUtc         │
     │ Status         │
     │ RowVersion     │
     └───┬────────┬───┘
         │ 1:N    │ 1:N
         ▼        ▼
┌──────────────────────┐  ┌──────────────────────┐
│  ShiftApplications   │  │   Recommendations    │
│──────────────────────│  │──────────────────────│
│ Id (PK)              │  │ Id (PK)              │
│ ShiftId  (FK) ┐      │  │ ShiftId  (FK) ┐      │
│ ExpertId (FK) ┴ U    │  │ ExpertId (FK) ┴ U    │
│ Status               │  │ Score                │
│ AppliedAtUtc         │  │ Reason               │
│ DecidedByUserId (FK) │  │ ComputedAtUtc        │
│ DecidedAtUtc         │  └──────────────────────┘
│ DecisionNote         │            ▲
└──────────────────────┘            │
                            نوشته می‌شود توسط
                             Python Script
```

---

## ۲. تصمیم‌های عرضی (روی همه‌ی جدول‌ها اثر دارد)

| موضوع | تصمیم | دلیل |
|---|---|---|
| کلید اصلی | `INT IDENTITY(1,1)` | برای این ابعاد کافی است؛ GUID فقط ایندکس را سنگین می‌کند |
| زمان | `DATETIME2(0)` و همیشه **UTC**، با پسوند `Utc` در نام ستون | ابهام منطقه‌ی زمانی صفر می‌شود؛ تبدیل به وقت تهران فقط در فرانت |
| Enumها | `NVARCHAR` + `CHECK Constraint` | در خروجی SQL و در دیتابیس خوانا است؛ در C# با `HasConversion<string>()` مپ می‌شود |
| مقدار پولی/امتیازی | `DECIMAL` نه `FLOAT` | خطای ممیز شناور در امتیازدهی قابل قبول نیست |
| حذف | Soft Delete نداریم | صورت‌مسئله نخواسته؛ رفتار حذف با FK کنترل می‌شود |
| نام‌گذاری | جدول‌ها جمع، ستون‌ها PascalCase | قرارداد پیش‌فرض EF Core |

**شیفت شبانه:** چون `StartUtc` و `EndUtc` هر دو `DATETIME2` کامل هستند (نه `TIME`)، شیفت ۲۲:۰۰ تا ۰۲:۰۰ بامداد خودبه‌خود پشتیبانی می‌شود و به هیچ فلگ اضافه‌ای نیاز نیست.

---

## ۳. تعریف جدول‌ها

### ۳-۱. Users

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| Username | NVARCHAR(64) | ✗ | یکتا |
| PasswordHash | NVARCHAR(256) | ✗ | BCrypt — هرگز Plain Text |
| Role | NVARCHAR(16) | ✗ | `Employer` \| `Expert` |
| IsActive | BIT | ✗ | پیش‌فرض `1` |
| CreatedAtUtc | DATETIME2(0) | ✗ | پیش‌فرض `SYSUTCDATETIME()` |

```sql
CONSTRAINT UQ_Users_Username UNIQUE (Username),
CONSTRAINT CK_Users_Role CHECK (Role IN ('Employer','Expert'))
```

### ۳-۲. Employers

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| UserId | INT | ✗ | FK → Users، **یکتا** (رابطه ۱:۱) |
| Name | NVARCHAR(128) | ✗ | نام کارفرما |
| CreatedAtUtc | DATETIME2(0) | ✗ | |

### ۳-۳. Experts

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| UserId | INT | ✗ | FK → Users، **یکتا** |
| FullName | NVARCHAR(128) | ✗ | |
| IsActive | BIT | ✗ | پیش‌فرض `1` |
| CreatedAtUtc | DATETIME2(0) | ✗ | |

> `Users` از `Employers`/`Experts` جدا مانده چون Authentication و Domain دو مسئولیت متفاوت‌اند. اگر بعداً نقش سومی اضافه شود، `Users` دست نمی‌خورد.

### ۳-۴. Projects

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| EmployerId | INT | ✗ | FK → Employers |
| Name | NVARCHAR(128) | ✗ | |
| IsActive | BIT | ✗ | پیش‌فرض `1` |
| CreatedAtUtc | DATETIME2(0) | ✗ | |

```sql
CONSTRAINT UQ_Projects_Employer_Name UNIQUE (EmployerId, Name)
```
یک کارفرما نباید دو پروژه‌ی هم‌نام داشته باشد.

### ۳-۵. ExpertProjects (جدول واسط N:N)

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| ExpertId | INT | ✗ | PK مرکب + FK |
| ProjectId | INT | ✗ | PK مرکب + FK |
| AssignedAtUtc | DATETIME2(0) | ✗ | |

کلید اصلی مرکب `(ExpertId, ProjectId)` خودش از تخصیص تکراری جلوگیری می‌کند.

### ۳-۶. Availabilities

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| ExpertId | INT | ✗ | FK → Experts |
| StartUtc | DATETIME2(0) | ✗ | |
| EndUtc | DATETIME2(0) | ✗ | |
| CreatedAtUtc | DATETIME2(0) | ✗ | |

```sql
CONSTRAINT CK_Availabilities_Range CHECK (EndUtc > StartUtc)
```

**نکته‌ی مهم:** قانون «هیچ دو بازه‌ای برای یک کارشناس نباید همپوشان یا چسبیده باشند» **در دیتابیس قابل اعمال نیست** (نیاز به Exclusion Constraint دارد که SQL Server ندارد). این قانون در لایه‌ی Service با الگوریتم Merge اجرا می‌شود:

```text
ورودی جدید:  12:00-16:00
موجود:       08:00-12:00
                ↓
نتیجه ذخیره‌شده:  08:00-16:00  (رکورد قبلی به‌روزرسانی، رکورد جدید ساخته نمی‌شود)
```

بنابراین در دیتابیس هیچ‌وقت دو بازه‌ی مجاور وجود ندارد، و قانون «شیفت باید داخل یک بازه جا شود» بدون باگ کار می‌کند.

### ۳-۷. Shifts

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| ProjectId | INT | ✗ | FK → Projects |
| StartUtc | DATETIME2(0) | ✗ | |
| EndUtc | DATETIME2(0) | ✗ | |
| Status | NVARCHAR(16) | ✗ | `Open` \| `Closed`، پیش‌فرض `Open` |
| CreatedAtUtc | DATETIME2(0) | ✗ | |
| RowVersion | ROWVERSION | ✗ | **قفل خوش‌بینانه** |

```sql
CONSTRAINT CK_Shifts_Range  CHECK (EndUtc > StartUtc),
CONSTRAINT CK_Shifts_Status CHECK (Status IN ('Open','Closed'))
```

`RowVersion` باعث می‌شود اگر دو مدیر هم‌زمان روی یک شیفت عمل کنند، دومی `DbUpdateConcurrencyException` بگیرد و خطای تمیز `409 Conflict` برگردد.

### ۳-۸. ShiftApplications

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| ShiftId | INT | ✗ | FK → Shifts |
| ExpertId | INT | ✗ | FK → Experts |
| Status | NVARCHAR(16) | ✗ | `Pending` \| `Approved` \| `Rejected` |
| AppliedAtUtc | DATETIME2(0) | ✗ | Tie-breaker امتیاز مساوی |
| DecidedByUserId | INT | ✓ | FK → Users — چه کسی تصمیم گرفت |
| DecidedAtUtc | DATETIME2(0) | ✓ | کِی |
| DecisionNote | NVARCHAR(256) | ✓ | چرا (مثلاً `Shift filled by another expert`) |

```sql
CONSTRAINT UQ_ShiftApplications_Shift_Expert UNIQUE (ShiftId, ExpertId),
CONSTRAINT CK_ShiftApplications_Status
    CHECK (Status IN ('Pending','Approved','Rejected'))
```

و مهم‌ترین محافظ کل سیستم — **ایندکس یکتای شرطی**:

```sql
CREATE UNIQUE INDEX UX_ShiftApplications_OneApproved
ON ShiftApplications (ShiftId)
WHERE Status = 'Approved';
```

حتی اگر منطق برنامه دچار Race Condition شود، دیتابیس اجازه نمی‌دهد دو کارشناس برای یک شیفت تأیید شوند.

سه ستون `DecidedBy/At/Note` همان Audit Trail است، با هزینه‌ی سه ستون به‌جای یک جدول تاریخچه‌ی جداگانه.

### ۳-۹. ExpertRatings

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| ExpertId | INT | ✗ | FK → Experts |
| Period | CHAR(7) | ✗ | فرمت `2026-08` |
| Score | DECIMAL(2,1) | ✗ | ۱.۰ تا ۵.۰ |
| CreatedAtUtc | DATETIME2(0) | ✗ | |

```sql
CONSTRAINT UQ_ExpertRatings_Expert_Period UNIQUE (ExpertId, Period),
CONSTRAINT CK_ExpertRatings_Score CHECK (Score BETWEEN 1.0 AND 5.0)
```

طبق تصمیم تأییدشده: این جدول **فقط از طریق Seed** پر می‌شود؛ هیچ API یا UI برای ثبت امتیاز ساخته نمی‌شود. کارشناس فاقد رکورد، مقدار پیش‌فرض `3.0` می‌گیرد.

### ۳-۱۰. Recommendations

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| ShiftId | INT | ✗ | FK → Shifts |
| ExpertId | INT | ✗ | FK → Experts |
| Score | DECIMAL(5,2) | ✗ | ۰.۰۰ تا ۱۰۰.۰۰ |
| Reason | NVARCHAR(500) | ✗ | توضیح قابل ردیابی امتیاز |
| ComputedAtUtc | DATETIME2(0) | ✗ | زمان آخرین محاسبه |

```sql
CONSTRAINT UQ_Recommendations_Shift_Expert UNIQUE (ShiftId, ExpertId),
CONSTRAINT CK_Recommendations_Score CHECK (Score BETWEEN 0 AND 100)
```

قید یکتا + عملیات `MERGE` در پایتون = **Idempotency**. اجرای دوباره‌ی اسکریپت فقط `Score` و `Reason` را به‌روز می‌کند، رکورد تکراری نمی‌سازد.

---

## ۴. رفتار حذف (Delete Behavior)

SQL Server اجازه‌ی **دو مسیر Cascade به یک جدول** را نمی‌دهد. اگر این را از قبل مشخص نکنیم، اولین `Add-Migration` با خطای `may cause cycles or multiple cascade paths` شکست می‌خورد. تفکیک نهایی:

| FK | رفتار | دلیل |
|---|---|---|
| Employers → Users | CASCADE | حذف کاربر یعنی حذف پروفایل |
| Experts → Users | CASCADE | همان |
| Projects → Employers | CASCADE | |
| Shifts → Projects | CASCADE | |
| Availabilities → Experts | CASCADE | |
| ExpertRatings → Experts | CASCADE | |
| ExpertProjects → Experts | CASCADE | مسیر اول |
| **ExpertProjects → Projects** | **NO ACTION** | جلوگیری از مسیر دوم |
| ShiftApplications → Shifts | CASCADE | مسیر اول |
| **ShiftApplications → Experts** | **NO ACTION** | مسیر دوم |
| Recommendations → Shifts | CASCADE | مسیر اول |
| **Recommendations → Experts** | **NO ACTION** | مسیر دوم |
| ShiftApplications → Users (DecidedBy) | NO ACTION | تاریخچه‌ی تصمیم نباید پاک شود |

**قاعده‌ی ساده:** مسیر حذف از سمت **کارفرما → پروژه → شیفت** همه‌جا Cascade است؛ هر FK که از سمت **کارشناس** می‌آید NO ACTION است.

---

## ۵. ایندکس‌ها

| ایندکس | ستون‌ها | برای چه کوئری‌ای |
|---|---|---|
| `IX_Shifts_Status_StartUtc` | `(Status, StartUtc)` | **خواسته‌ی صریح صورت‌مسئله** — لیست شیفت‌های باز مرتب بر اساس زمان |
| `IX_Shifts_ProjectId_Status` | `(ProjectId, Status)` | شیفت‌های باز پروژه‌های مجاز یک کارشناس |
| `IX_Availabilities_Expert_Range` | `(ExpertId, StartUtc, EndUtc)` | بررسی قانون ۳ — پوشش بازه |
| `IX_ShiftApplications_Expert_Status` | `(ExpertId, Status)` INCLUDE `(ShiftId)` | بررسی قانون ۵ — تداخل شیفت‌های تأییدشده |
| `IX_ShiftApplications_Shift_Status` | `(ShiftId, Status)` | لیست متقاضیان یک شیفت |
| `UX_ShiftApplications_OneApproved` | `(ShiftId)` WHERE Approved | یکتایی + جلوگیری از Race Condition |
| `IX_ExpertProjects_ProjectId` | `(ProjectId)` | جهت معکوس PK — کارشناسان یک پروژه |
| `IX_Recommendations_Shift_Score` | `(ShiftId, Score DESC)` | خواندن رتبه‌بندی به ترتیب امتیاز |

---

## ۶. چند نکته‌ی پیاده‌سازی که از این طراحی نتیجه می‌شود

**۱. بررسی تداخل شیفت‌های تأییدشده** (قانون ۵) با شرط استاندارد بازه‌ها:
```sql
existing.StartUtc < new.EndUtc  AND  existing.EndUtc > new.StartUtc
```
این شرط عمداً شیفت‌های چسبیده (۱۰-۱۴ و ۱۴-۱۸) را مجاز می‌داند — دقیقاً همان چیزی که صورت‌مسئله می‌خواهد.

**۲. مرحله‌ی Approve** کامل داخل یک تراکنش، با بررسی مجدد قوانین:
```text
BEGIN TRANSACTION
  بررسی مالکیت کارفرما بر پروژه‌ی شیفت
  بررسی Status = Open
  بررسی مجدد قانون ۵ (ممکن است از زمان ثبت درخواست تغییر کرده باشد)
  Application → Approved  +  ثبت DecidedBy/At
  Shift.Status → Closed
  سایر Pendingها → Rejected با DecisionNote
COMMIT
```

**۳. ساعت‌های تأییدشده** بر اساس ماهِ `Shift.StartUtc` محاسبه می‌شود، نه ماهِ امروز (تصمیم تأییدشده).

**۴. پایتون** فقط با `MERGE` روی `Recommendations` می‌نویسد و به هیچ جدول دیگری دست نمی‌زند.

---

## ۷. مواردی که عمداً در این طراحی نیست

| مورد | جایگزین |
|---|---|
| جدول `ScriptRuns` | قید یکتا + MERGE همان Idempotency را می‌دهد |
| وضعیت‌های `Confirmed`/`Completed`/`NoShow` | صورت‌مسئله فقط سه وضعیت خواسته |
| `Capacity` روی شیفت | تصمیم تأییدشده: هر شیفت یک کارشناس |
| Timezone به ازای کارشناس | همه‌چیز UTC، نمایش تهران |
| جدول `ScoringWeights` | وزن‌ها در `appsettings.json` |
| Soft Delete | خارج از Scope |

همه‌ی این موارد در بخش «Assumptions» و «At Scale» فایل README مستند می‌شوند.

---

## ۸. قدم بعدی

اگر این طراحی تأیید شود، ترتیب کار:

```text
۱. ساخت Solution و چهار پروژه‌ی .NET
۲. Entityها در لایه Domain
۳. DbContext + Configurationها در Infrastructure
۴. اولین Migration و تطبیق با همین سند
۵. docker-compose با healthcheck
۶. Seed سناریومحور
```
