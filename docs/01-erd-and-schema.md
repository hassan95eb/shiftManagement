# ShiftFlow — ERD و طراحی نهایی دیتابیس (v2)

**دیتابیس:** SQL Server (داخل Docker)
**وضعیت:** مرجع معتبر برای پیاده‌سازی v2 — نسخه‌ی v1 در پایان این سند آرشیو شده
**نسخه‌ی مدل:** v2 — مطابق `docs/04-v2-prompts.md` (پرامپت‌های V0 تا V12)

> اگر کد و این سند در تضاد بودند، این سند برنده است. اگر جایی از این سند اشتباه به نظر
> می‌رسد، متوقف شو و مطرح کن — چیز دیگری پیاده‌سازی نکن.

---

## ۱. ERD

```text
                                   ┌──────────────┐
                                   │    Users     │
                                   │──────────────│
                                   │ Id (PK)      │
                                   │ Username (U) │
                                   │ PasswordHash │
                                   │ Role         │  Manager|Supervisor|CallAgent
                                   └──────┬───────┘
                          1:1 ┌───────────┼───────────┐ 1:1
                              │           │           │
                              ▼           │           ▼
                     ┌────────────────┐   │   ┌────────────────┐
                     │  Supervisors   │   │   │   CallAgents   │
                     │────────────────│   │   │────────────────│
                     │ Id (PK)        │   │   │ Id (PK)        │
                     │ UserId (FK,U)  │   │   │ UserId (FK,U)  │
                     │ Name           │   │   │ FullName       │
                     └───────┬────────┘   │   │ AnnualLeaveDays│
                             │ 1:N         │   └─┬─┬─┬─┬─┬─┬───┘
                             ▼             │      │ │ │ │ │ │
                     ┌────────────────┐    │      │ │ │ │ │ │ 1:N
                     │    Projects    │    │      │ │ │ │ │ └──►┌──────────────────┐
                     │────────────────│    │      │ │ │ │ │     │  Availabilities  │
                     │ Id (PK)        │    │      │ │ │ │ │     │──────────────────│
                     │ SupervisorId   │    │      │ │ │ │ │     │ CallAgentId (FK) │
                     │ Name           │    │      │ │ │ │ │     │ StartUtc/EndUtc  │
                     └───┬────────┬───┘    │      │ │ │ │ │     └──────────────────┘
                         │       │         │      │ │ │ │ │ 1:N
                         │  N:N  │         │      │ │ │ │ └──►┌──────────────────┐
                         │ ┌─────▼─────────▼─┐    │ │ │ │     │     Ratings      │
                         │ │CallAgentProjects│    │ │ │ │     │──────────────────│
                         │ │─────────────────│    │ │ │ │     │ CallAgentId (FK) │
                         │ │CallAgentId(PK,FK)│   │ │ │ │     │ Period (U)       │
                         │ │ProjectId (PK,FK)│    │ │ │ │     │ Score, Breakdown │
                         │ └─────────────────┘    │ │ │ │     └──────────────────┘
                         │ 1:N                    │ │ │ │ 1:N
                         ▼                        │ │ │ └──►┌──────────────────┐
                 ┌────────────────┐               │ │ │    │  AgentRequests   │
                 │     Shifts     │◄──────────────┘ │ │    │──────────────────│
                 │────────────────│  N:1 (nullable) │ │    │ CallAgentId (FK) │
                 │ Id (PK)        │  AssignedCallAgentId│  │ ShiftId (FK)     │
                 │ ProjectId (FK) │                 │ │    │ RequestType      │
                 │ StartUtc/EndUtc│                 │ │    │ Status           │
                 │ Status         │◄────────────────┘ │    └──────────────────┘
                 │ RowVersion     │  1:N (AttendanceSessions.ShiftId)
                 └─┬────┬────┬───┘                    │ 1:N
       1:N ┌────────┘    │    └────────┐               └──►┌──────────────────────┐
           ▼             │ 1:N          ▼                    │ AttendanceSessions   │
┌──────────────────────┐ │      ┌──────────────────────┐    │──────────────────────│
│  ShiftApplications   │ │      │   Recommendations    │    │ CallAgentId (FK)     │
│──────────────────────│ │      │──────────────────────│    │ ShiftId (FK)         │
│ ShiftId  (FK) ┐      │ │      │ ShiftId  (FK) ┐      │    │ StartedAtUtc         │
│ CallAgentId(FK)┴ U   │ │      │ CallAgentId(FK)┴ U   │    │ LastSeenUtc          │
│ Status, Kind         │ │      │ Score                │    │ EndedAtUtc (NULL)    │
│ AppliedAtUtc         │ │      │ Reason               │    └──────────────────────┘
│ DecidedByUserId (FK) │ │      │ ComputedAtUtc        │
│ DecidedAtUtc         │ │      └──────────────────────┘
│ DecisionNote         │ │               ▲
└──────────────────────┘ │               │  نوشته می‌شود توسط Python Script
                          │
                          ▼ N:N (از دو طرف Supervisor و CallAgent)
                  ┌──────────────────────┐
                  │ SupervisorEvaluations│
                  │──────────────────────│
                  │ CallAgentId (FK)     │
                  │ SupervisorId (FK)    │
                  │ Period (U با هم)     │
                  │ ScoreValue, Note     │
                  └──────────────────────┘
```

**خلاصه‌ی روابط تازه یا تغییریافته نسبت به v1:**

| رابطه | نوع | توضیح |
|---|---|---|
| `Users.Role` → سه مقدار | — | `Manager` بدون جدول پروفایل جداگانه |
| `Shifts.AssignedCallAgentId` → `CallAgents` | N:1، Nullable | تخصیص مستقیم، بدون رکورد `ShiftApplications` |
| `AttendanceSessions` → `CallAgents`, `Shifts` | N:1، N:1 | هر دو FK با `NO ACTION` |
| `AgentRequests` → `CallAgents`, `Shifts` | N:1، N:1 | مرخصی/عدم‌حضور بسته به یک شیفت مشخص |
| `SupervisorEvaluations` → `CallAgents`, `Supervisors` | N:1، N:1 | کلید یکتا روی هر سه ستون |

---

## ۲. تصمیم‌های عرضی (روی همه‌ی جدول‌ها اثر دارد)

| موضوع | تصمیم | دلیل |
|---|---|---|
| کلید اصلی | `INT IDENTITY(1,1)` | برای این ابعاد کافی است؛ GUID فقط ایندکس را سنگین می‌کند |
| زمان | `DATETIME2(0)` و همیشه **UTC**، با پسوند `Utc` در نام ستون | ابهام منطقه‌ی زمانی صفر می‌شود؛ تبدیل به وقت تهران فقط در فرانت (به‌جز محاسبه‌ی داخلی سال مرخصی جلالی — بند ۴-۹) |
| Enumها | `NVARCHAR` + `CHECK Constraint` | در خروجی SQL و در دیتابیس خوانا است؛ در C# با `HasConversion<string>()` مپ می‌شود |
| مقدار پولی/امتیازی | `DECIMAL` نه `FLOAT` | خطای ممیز شناور در امتیازدهی قابل قبول نیست |
| حذف | Soft Delete نداریم | صورت‌مسئله نخواسته؛ رفتار حذف با FK کنترل می‌شود |
| نام‌گذاری | جدول‌ها جمع، ستون‌ها PascalCase | قرارداد پیش‌فرض EF Core |

**شیفت شبانه:** بدون تغییر نسبت به v1 — چون `StartUtc`/`EndUtc` هر دو `DATETIME2` کامل هستند، شیفت ۲۲:۰۰ تا ۰۲:۰۰ بامداد خودبه‌خود پشتیبانی می‌شود.

---

## ۳. نقش‌ها و قواعد دسترسی

سه نقش، با سلسله‌مراتب دید **`Manager > Supervisor > CallAgent`** — ولی سلسله‌مراتب فقط
روی **دید (Read)** اثر دارد، نه روی نوشتن. هر عملیات نوشتن دقیقاً به یک نقش تعلق دارد.

### Manager

- هیچ جدول پروفایل ندارد (`Users.Role = 'Manager'` کافی است) — چون هیچ ویژگی‌ی
  اختصاصی (نام شرکت، سقف مرخصی و…) ندارد.
- **مالک** `Projects` است: ایجاد، ویرایش، حذف، و تغییر `SupervisorId` پروژه.
- روی `Shifts`, `ShiftApplications`, `AgentRequests`, `SupervisorEvaluations`
  فقط **خواندن سراسری** دارد — هیچ Endpoint override برای این منابع وجود ندارد.
- چون همه‌چیز را می‌بیند، قاعده‌ی «۴۰۴ به‌جای ۴۰۳» هیچ‌وقت برای Manager فعال نمی‌شود.

### Supervisor

- فقط پروژه‌هایی که `Projects.SupervisorId` آن‌ها برابر با خودش است را می‌بیند و روی
  آن‌ها عمل می‌کند (شیفت، تأیید/رد درخواست، تخصیص مستقیم، ارزیابی).
- **نمی‌تواند** پروژه بسازد/حذف کند یا `SupervisorId` را تغییر دهد — این کار Manager
  است.
- درخواست برای منبعی که مالکش نیست → **۴۰۴، نه ۴۰۳** (منبع اصلاً دیده نمی‌شود).

### CallAgent

- فقط بازه‌های در دسترسی، درخواست‌ها (اپلای/مرخصی/عدم‌حضور)، حضور، و پروفایل خودش را
  می‌بیند.
- فقط به شیفت‌های پروژه‌هایی دسترسی دارد که در `CallAgentProjects` عضو آن‌هاست.

### چگونه اجرا می‌شود (فقط ارجاع — جزئیات در Application layer)

فیلترهای پراکنده‌ی `EmployerId`/`SupervisorId` که در v1 داخل هر سرویس تکرار می‌شد، در
v2 پشت یک انتزاع واحد (`IAccessScope`) قرار می‌گیرد: برای Manager «همه»، برای
Supervisor «فقط `SupervisorId` خودش» برمی‌گرداند. این یک جزئیات پیاده‌سازی Application
است، نه بخشی از اسکیمای دیتابیس — اینجا فقط برای ردیابی قاعده‌ی دسترسی ذکر شد.

---

## ۴. تعریف جدول‌ها

### ۴-۱. Users

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| Username | NVARCHAR(64) | ✗ | یکتا |
| PasswordHash | NVARCHAR(256) | ✗ | BCrypt — هرگز Plain Text |
| Role | NVARCHAR(16) | ✗ | `Manager` \| `Supervisor` \| `CallAgent` |
| IsActive | BIT | ✗ | پیش‌فرض `1` |
| CreatedAtUtc | DATETIME2(0) | ✗ | پیش‌فرض `SYSUTCDATETIME()` |

```sql
CONSTRAINT UQ_Users_Username UNIQUE (Username),
CONSTRAINT CK_Users_Role CHECK (Role IN ('Manager','Supervisor','CallAgent'))
```

### ۴-۲. Supervisors *(تغییر نام از `Employers`)*

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| UserId | INT | ✗ | FK → Users، **یکتا** (رابطه ۱:۱) |
| Name | NVARCHAR(128) | ✗ | نام سرپرست/تیم |
| CreatedAtUtc | DATETIME2(0) | ✗ | |

### ۴-۳. CallAgents *(تغییر نام از `Experts`)*

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| UserId | INT | ✗ | FK → Users، **یکتا** |
| FullName | NVARCHAR(128) | ✗ | |
| IsActive | BIT | ✗ | پیش‌فرض `1` |
| AnnualLeaveDays | INT | ✗ | پیش‌فرض `26` — سقف سالانه‌ی مرخصی (بند ۴-۹) |
| CreatedAtUtc | DATETIME2(0) | ✗ | |

> `Users` از `Supervisors`/`CallAgents` جدا مانده چون Authentication و Domain دو
> مسئولیت متفاوت‌اند. اضافه‌شدن نقش سوم (`Manager`) دقیقاً همین جدایی را ثابت کرد:
> `Users` فقط یک مقدار CHECK جدید گرفت و دست هیچ جدول دیگری نخورد.

### ۴-۴. Projects

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| SupervisorId | INT | ✗ | FK → Supervisors — **همیشه NOT NULL** (مسیر Cascade به آن وابسته است) |
| Name | NVARCHAR(128) | ✗ | |
| IsActive | BIT | ✗ | پیش‌فرض `1` |
| CreatedAtUtc | DATETIME2(0) | ✗ | |

```sql
CONSTRAINT UQ_Projects_Supervisor_Name UNIQUE (SupervisorId, Name)
```

مالکیت پروژه با Manager است (ایجاد/حذف/تغییر `SupervisorId`)، ولی خود ستون
`SupervisorId` — نه یک ستون `ManagerId` جدید — همچنان تعیین می‌کند کدام Supervisor
دید و کنترل عملیاتی روی شیفت‌های آن پروژه دارد. یک سرپرست نباید دو پروژه‌ی هم‌نام
داشته باشد.

### ۴-۵. CallAgentProjects *(تغییر نام از `ExpertProjects`، جدول واسط N:N)*

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| CallAgentId | INT | ✗ | PK مرکب + FK |
| ProjectId | INT | ✗ | PK مرکب + FK |
| AssignedAtUtc | DATETIME2(0) | ✗ | |

کلید اصلی مرکب `(CallAgentId, ProjectId)` خودش از تخصیص تکراری جلوگیری می‌کند.

### ۴-۶. Availabilities

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| CallAgentId | INT | ✗ | FK → CallAgents |
| StartUtc | DATETIME2(0) | ✗ | |
| EndUtc | DATETIME2(0) | ✗ | |
| CreatedAtUtc | DATETIME2(0) | ✗ | |

```sql
CONSTRAINT CK_Availabilities_Range CHECK (EndUtc > StartUtc)
```

بدون تغییر منطقی نسبت به v1: قانون «دو بازه‌ی همپوشان/چسبیده در یک رکورد Merge
می‌شوند» در لایه‌ی Service اجرا می‌شود، نه در دیتابیس (بدون تغییر — SQL Server
Exclusion Constraint ندارد).

### ۴-۷. Shifts

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| ProjectId | INT | ✗ | FK → Projects |
| StartUtc | DATETIME2(0) | ✗ | |
| EndUtc | DATETIME2(0) | ✗ | |
| Status | NVARCHAR(16) | ✗ | `Open` \| `Assigned` \| `Released` \| `Closed`، پیش‌فرض `Open` |
| AssignedCallAgentId | INT | ✓ | FK → CallAgents، **NO ACTION** — کارشناسی که مستقیم تخصیص گرفته |
| CreatedAtUtc | DATETIME2(0) | ✗ | |
| RowVersion | ROWVERSION | ✗ | **قفل خوش‌بینانه** |

```sql
CONSTRAINT CK_Shifts_Range  CHECK (EndUtc > StartUtc),
CONSTRAINT CK_Shifts_Status CHECK (Status IN ('Open','Assigned','Released','Closed'))
```

جزئیات کامل گذارهای وضعیت در بند ۷ (ماشین حالت). زمان شیفت فقط وقتی `Open` است **و**
صفر متقاضی دارد قابل اصلاح است — بدون تغییر از v1.

### ۴-۸. ShiftApplications

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| ShiftId | INT | ✗ | FK → Shifts |
| CallAgentId | INT | ✗ | FK → CallAgents |
| Status | NVARCHAR(16) | ✗ | `Pending` \| `Approved` \| `Rejected` |
| Kind | NVARCHAR(16) | ✗ | `Extra` \| `Cover`، پیش‌فرض `Extra` |
| AppliedAtUtc | DATETIME2(0) | ✗ | Tie-breaker امتیاز مساوی |
| DecidedByUserId | INT | ✓ | FK → Users، **NO ACTION** — چه کسی تصمیم گرفت |
| DecidedAtUtc | DATETIME2(0) | ✓ | کِی |
| DecisionNote | NVARCHAR(256) | ✓ | چرا |

```sql
CONSTRAINT CK_ShiftApplications_Status CHECK (Status IN ('Pending','Approved','Rejected')),
CONSTRAINT CK_ShiftApplications_Kind   CHECK (Kind IN ('Extra','Cover'))
```

`Kind = 'Extra'` همان جریان v1 (اپلای روی شیفت `Open`) است. `Kind = 'Cover'` برای
اپلای روی شیفت `Released` است (بند ۷).

> **قید یکتای فعلی و ایندکس یکتای شرطی، هر دو در حال بازطراحی هستند.**
> در v1، `UNIQUE(ShiftId, CallAgentId)` و
> `UX_ShiftApplications_OneApproved ON (ShiftId) WHERE Status = 'Approved'` این جدول
> را محافظت می‌کردند. با اضافه‌شدن `Kind`، این دو باید طوری بازطراحی شوند که:
> ۱) یک دور Cover مشروع دوم را مسدود نکنند، و ۲) بگذارند یک `Extra` تأییدشده و یک
> `Cover` تأییدشده هم‌زمان و **قابل‌تفکیک** باشند.
> طراحی نهایی این دو قید عمداً اینجا مشخص نشده — پرامپت V6 صراحتاً می‌خواهد طراحی
> انتخابی پیش از پیاده‌سازی در گزارش فاز بیاید. این سند فقط الزام را ثبت می‌کند تا
> کسی به‌اشتباه قید فعلی v1 را بدون تغییر نگه ندارد.

سه ستون `DecidedBy/At/Note` همچنان Audit Trail است، بدون جدول تاریخچه‌ی جداگانه.

### ۴-۹. AgentRequests *(جدید)*

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| CallAgentId | INT | ✗ | FK → CallAgents، **NO ACTION** |
| ShiftId | INT | ✗ | FK → Shifts، CASCADE (مسیر Supervisor→Project→Shift) |
| RequestType | NVARCHAR(16) | ✗ | `Leave` \| `Downtime` |
| RequestedAtUtc | DATETIME2(0) | ✗ | برای قاعده‌ی «اطلاع دیرهنگام» (کمتر از ۲۴ ساعت) |
| StartUtc | DATETIME2(0) | ✗ | |
| EndUtc | DATETIME2(0) | ✗ | |
| Reason | NVARCHAR(256) | ✓ | توضیح کارشناس |
| Status | NVARCHAR(16) | ✗ | `Pending` \| `Approved` \| `Rejected` |
| DecidedByUserId | INT | ✓ | FK → Users، NO ACTION |
| DecidedAtUtc | DATETIME2(0) | ✓ | |
| DecisionNote | NVARCHAR(256) | ✓ | |

```sql
CONSTRAINT CK_AgentRequests_RequestType CHECK (RequestType IN ('Leave','Downtime')),
CONSTRAINT CK_AgentRequests_Status      CHECK (Status IN ('Pending','Approved','Rejected')),
CONSTRAINT CK_AgentRequests_Range       CHECK (EndUtc > StartUtc)
```

یک جدول برای هر دو نوع درخواست (مرخصی و عدم‌حضور برنامه‌ریزی‌شده)، به‌جای دو جدول
جدا — چون هر دو دقیقاً همان شکل «یک بازه، وابسته به یک شیفت، با تأیید/رد سرپرست»
را دارند. تفکیک منطق (سهمیه‌ی مرخصی در برابر سقف ماهانه‌ی Downtime) در Application
است، نه در اسکیما.

**مانده‌ی مرخصی محاسبه می‌شود، ذخیره نمی‌شود:**
`AnnualLeaveDays منهای مجموع روزهای مرخصیِ تأییدشده در سال جلالی جاری`.
مرز روز مرخصی، **تاریخ تقویم تهران (UTC+03:30، بدون DST)** است، نه تاریخ UTC — تبدیل
در مرز UTC می‌تواند درخواست نزدیک نوروز را به سال اشتباه بیندازد.

### ۴-۱۰. AttendanceSessions *(جدید)*

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| CallAgentId | INT | ✗ | FK → CallAgents، **NO ACTION** |
| ShiftId | INT | ✗ | FK → Shifts، **NO ACTION** |
| StartedAtUtc | DATETIME2(0) | ✗ | |
| LastSeenUtc | DATETIME2(0) | ✗ | با هر Heartbeat به‌روز می‌شود |
| EndedAtUtc | DATETIME2(0) | ✓ | NULL یعنی هنوز باز است |

هر دو FK این جدول عمداً `NO ACTION` هستند — با اینکه `ShiftId` روی مسیر
Supervisor→Project→Shift قرار دارد، حذف یک شیفت نباید سابقه‌ی حضور را بی‌صدا پاک کند؛
این داده مبنای محاسبه‌ی غیبت و امتیاز است.

هیچ Background Job یا SignalR در کار نیست: یک نشست رهاشده (بدون Logout، بدون
Heartbeat) صرفاً «کهنه» می‌شود، نه اینکه عقب‌گرد بسته شود.

### ۴-۱۱. SupervisorEvaluations *(جدید)*

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| CallAgentId | INT | ✗ | FK → CallAgents، NO ACTION |
| SupervisorId | INT | ✗ | FK → Supervisors، NO ACTION |
| Period | NVARCHAR(7) | ✗ | فرمت `2026-08` |
| ScoreValue | DECIMAL(2,1) | ✗ | ۱.۰ تا ۵.۰ |
| Note | NVARCHAR(500) | ✗ | غیرخالی — بخش کیفیِ ارزیابی |
| CreatedAtUtc | DATETIME2(0) | ✗ | |
| UpdatedAtUtc | DATETIME2(0) | ✗ | |

```sql
CONSTRAINT UQ_SupervisorEvaluations_Agent_Supervisor_Period
    UNIQUE (CallAgentId, SupervisorId, Period),
CONSTRAINT CK_SupervisorEvaluations_ScoreValue
    CHECK (ScoreValue BETWEEN 1.0 AND 5.0),
CONSTRAINT CK_SupervisorEvaluations_Note CHECK (LEN(Note) > 0)
```

کلید یکتا **سه ستونه** است، نه دو — چون وقتی یک کارشناس در یک دوره زیر نظر چند
سرپرست کار کرده، هرکدام یک ارزیابی جدا ثبت می‌کنند و میانگین در موتور Rating خوانده
می‌شود. `SupervisorId` هم `NO ACTION` گرفته (نه Cascade از مسیر Supervisor)، به همان
دلیلی که `DecidedByUserId` در `ShiftApplications` همیشه `NO ACTION` بوده: این جدول
ورودی مستقیم محاسبه‌ی Rating است و حذف حساب یک سرپرست نباید امتیازهای گذشته را
بازنویسی کند.

### ۴-۱۲. Ratings *(تغییر نام از `ExpertRatings`)*

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| CallAgentId | INT | ✗ | FK → CallAgents |
| Period | CHAR(7) | ✗ | فرمت `2026-08` |
| Score | DECIMAL(2,1) | ✗ | ۱.۰ تا ۵.۰ |
| Breakdown | NVARCHAR(400) | ✓ | رشته‌ی قابل‌ردیابی اجزای محاسبه (پیوست A) |
| CreatedAtUtc | DATETIME2(0) | ✗ | |

```sql
CONSTRAINT UQ_Ratings_CallAgent_Period UNIQUE (CallAgentId, Period),
CONSTRAINT CK_Ratings_Score CHECK (Score BETWEEN 1.0 AND 5.0)
```

طبق تصمیم تأییدشده‌ی v1 که در v2 هم پابرجاست: این جدول از طریق موتور Rating (سمت
C# یا Python) پر می‌شود، نه از طریق API/UI مستقیم. کارشناس بدون رکورد در دوره‌ی
جاری (یعنی صفر شیفت متعهدشده در آن دوره)، مقدار پیش‌فرض `3.0` می‌گیرد.

### ۴-۱۳. Recommendations

| ستون | نوع | Null | توضیح |
|---|---|---|---|
| Id | INT IDENTITY | ✗ | PK |
| ShiftId | INT | ✗ | FK → Shifts |
| CallAgentId | INT | ✗ | FK → CallAgents |
| Score | DECIMAL(5,2) | ✗ | ۰.۰۰ تا ۱۰۰.۰۰ |
| Reason | NVARCHAR(500) | ✗ | توضیح قابل ردیابی امتیاز |
| ComputedAtUtc | DATETIME2(0) | ✗ | زمان آخرین محاسبه |

```sql
CONSTRAINT UQ_Recommendations_Shift_CallAgent UNIQUE (ShiftId, CallAgentId),
CONSTRAINT CK_Recommendations_Score CHECK (Score BETWEEN 0 AND 100)
```

بدون تغییر منطقی — فقط تغییر نام ستون FK. رتبه‌بندی همچنان روی **متقاضیان یک
شیفت** اجرا می‌شود، نه همه‌ی کارشناسان سیستم.

---

## ۵. رفتار حذف (Delete Behavior)

قاعده‌ی ساده‌ی v1 بدون تغییر پابرجاست: مسیر حذف از سمت **Supervisor → Project →
Shift** همه‌جا Cascade است؛ هر FK که از سمت **CallAgent** می‌آید `NO ACTION` است.
دو جدول جدید (`AttendanceSessions`, `SupervisorEvaluations`) که از هر دو سمت FK
دارند، همیشه سمت مرتبط به عملکرد/امتیاز فرد را `NO ACTION` نگه می‌دارند تا سابقه
حذف نشود.

| FK | رفتار | دلیل |
|---|---|---|
| Supervisors → Users | CASCADE | حذف کاربر یعنی حذف پروفایل |
| CallAgents → Users | CASCADE | همان |
| Projects → Supervisors | CASCADE | مسیر اصلی |
| Shifts → Projects | CASCADE | مسیر اصلی |
| Availabilities → CallAgents | CASCADE | |
| Ratings → CallAgents | CASCADE | |
| CallAgentProjects → CallAgents | CASCADE | مسیر اول |
| **CallAgentProjects → Projects** | **NO ACTION** | جلوگیری از مسیر دوم |
| ShiftApplications → Shifts | CASCADE | مسیر اول |
| **ShiftApplications → CallAgents** | **NO ACTION** | مسیر دوم |
| ShiftApplications → Users (DecidedBy) | NO ACTION | تاریخچه‌ی تصمیم نباید پاک شود |
| Recommendations → Shifts | CASCADE | مسیر اول |
| **Recommendations → CallAgents** | **NO ACTION** | مسیر دوم |
| Shifts → CallAgents (AssignedCallAgentId) | **NO ACTION** | سمت CallAgent |
| AgentRequests → Shifts | CASCADE | مسیر اصلی |
| **AgentRequests → CallAgents** | **NO ACTION** | سمت CallAgent |
| AgentRequests → Users (DecidedBy) | NO ACTION | تاریخچه‌ی تصمیم |
| **AttendanceSessions → Shifts** | **NO ACTION** | سابقه‌ی حضور نباید با حذف شیفت پاک شود |
| **AttendanceSessions → CallAgents** | **NO ACTION** | سمت CallAgent |
| **SupervisorEvaluations → CallAgents** | **NO ACTION** | سمت CallAgent |
| **SupervisorEvaluations → Supervisors** | **NO ACTION** | ورودی مستقیم Rating؛ حذف حساب سرپرست نباید امتیاز را پاک کند |

---

## ۶. ایندکس‌ها

| ایندکس | ستون‌ها | برای چه کوئری‌ای |
|---|---|---|
| `IX_Shifts_Status_StartUtc` | `(Status, StartUtc)` | لیست شیفت‌های باز مرتب بر اساس زمان |
| `IX_Shifts_ProjectId_Status` | `(ProjectId, Status)` | شیفت‌های باز پروژه‌های مجاز یک کارشناس |
| `IX_Shifts_AssignedCallAgentId` | `(AssignedCallAgentId)` WHERE NOT NULL | بررسی تداخل شیفتِ تخصیص‌مستقیم (V3) |
| `IX_Availabilities_CallAgent_Range` | `(CallAgentId, StartUtc, EndUtc)` | بررسی پوشش بازه (قانون ۳) |
| `IX_ShiftApplications_CallAgent_Status` | `(CallAgentId, Status)` INCLUDE `(ShiftId)` | بررسی تداخل شیفت‌های تأییدشده (قانون ۵) |
| `IX_ShiftApplications_Shift_Status` | `(ShiftId, Status)` | لیست متقاضیان یک شیفت |
| `UX_ShiftApplications_OneApproved` | در حال بازطراحی (بند ۴-۸) | یکتایی + جلوگیری از Race Condition |
| `IX_CallAgentProjects_ProjectId` | `(ProjectId)` | جهت معکوس PK — کارشناسان یک پروژه |
| `IX_Recommendations_Shift_Score` | `(ShiftId, Score DESC)` | خواندن رتبه‌بندی به ترتیب امتیاز |
| `IX_AttendanceSessions_CallAgent_StartedAtUtc` | `(CallAgentId, StartedAtUtc)` | تاریخچه‌ی حضور یک کارشناس |
| `IX_AttendanceSessions_Open` | `(CallAgentId, ShiftId)` WHERE `EndedAtUtc IS NULL` | یافتن/استفاده‌ی مجدد نشست باز همان شیفت |
| `IX_AgentRequests_CallAgent_Status` | `(CallAgentId, RequestType, Status)` | مانده‌ی مرخصی، سقف ماهانه‌ی Downtime |
| `IX_SupervisorEvaluations_CallAgent_Period` | `(CallAgentId, Period)` INCLUDE `(ScoreValue)` | میانگین ارزیابی‌های یک دوره برای موتور Rating |

---

## ۷. ماشین حالت شیفت

```text
                 ┌──────────────────────────┐
                 │  Assignment API (Sup.)    │
                 ▼                          │
   ┌──────┐   Open → Assigned        Assigned → Open   ┌──────────┐
   │ Open │◄──────────────────────────────────────────►│ Assigned │
   └──┬───┘                                             └────┬─────┘
      │ Application approval                                 │ Leave approval (V5)
      │ (Extra, Open → Closed)                                ▼
      │                                                  ┌──────────┐
      ▼                                                  │ Released │
  ┌────────┐                                             └────┬─────┘
  │ Closed │                        Cover approval (V6)        │
  └────────┘                        Released → Assigned ◄──────┘
```

| گذار | چه کسی مالک این عملیات است |
|---|---|
| `Open → Assigned` | `POST /api/shifts/{id}/assignment` (Supervisor) — بدون رکورد `ShiftApplications` |
| `Assigned → Open` | `DELETE /api/shifts/{id}/assignment` — فقط اگر هیچ `AttendanceSessions` برای آن شیفت ثبت نشده |
| `Open → Closed` | تراکنش تأیید اپلیکیشن `Kind = 'Extra'` (بند ۹) |
| `Assigned → Released` | تأیید درخواست مرخصی (`AgentRequests`, V5) در همان تراکنش |
| `Released → Assigned` | تأیید اپلیکیشن `Kind = 'Cover'` (V6)، با `AssignedCallAgentId` جدید |

قاعده‌ی سخت: **`Status` هیچ‌وقت مستقیم از طریق یک Endpoint بروزرسانی عمومی قابل
تنظیم نیست** — هر گذار دقیقاً به یک عملیات تعلق دارد که در جدول بالا آمده. برخلاف
گمانی که ممکن است پیش بیاید، هیچ وضعیت `Completed`/`NoShow` اضافه نشده: یک شیفت
`Assigned` که زمانش گذشته همچنان `Assigned` می‌ماند — غیبت و ساعت حضور همیشه در لحظه‌ی
خواندن محاسبه می‌شوند، نه با تغییر Status توسط یک Job (بدون Background Job، طبق
قانون کلی v2).

---

## ۸. پیوست A — فرمول‌های Rating و Score (عیناً از `docs/04-v2-prompts.md`)

### Rating

```
CommittedShifts = assigned + approved extra + approved cover
ExpectedHours   = committed shift hours
                  - approved leave hours
                  - approved downtime hours

Attendance      = min(PresentHours / ExpectedHours, 1)
Punctuality     = OnTimeShifts / AttendedShifts          (0 when AttendedShifts = 0)
Reliability     = max(1 - (UnexcusedAbsences + LateNoticeLeaves)
                          / CommittedShifts, 0)

AutoRaw         = 0.50*Attendance + 0.30*Punctuality + 0.20*Reliability
SupRaw          = (mean SupervisorScore - 1) / 4

Rating          = 1.0 + 4.0 * (0.50*AutoRaw + 0.50*SupRaw)
Rating          = 1.0 + 4.0 * AutoRaw          when no evaluation exists
```

- On time: first heartbeat ≤ shift start + grace (default 5 minutes).
- Late-notice leave: requested less than 24 hours before shift start, **even if
  approved**.
- Unexcused absence: committed shift, ended, zero present hours, no approved
  leave or downtime.
- Period: the month **before** the shift's month — unchanged from v1.
- No committed shifts in the period → no `Ratings` row → default 3.0 applies.

#### Breakdown format

```
Attendance 152/160h -> 0.475 | Punctuality 18/20 -> 0.270 | Reliability 1 absence, 1 late leave -> 0.180 | Auto 0.925 -> 0.463 | Supervisor 4.5/5 -> 0.438 | Rating 4.6/5
```

ASCII `->`, components at 3 dp, Rating at 1 dp. When no evaluation exists, the
`Supervisor` segment is replaced by `Supervisor none -> normalized`.

### Score — shape unchanged from v1

```
Score             = (0.30*RatingScore + 0.30*WorkloadScore + 0.40*AvailabilityScore) * 100
RatingScore       = (rating ?? 3.0) / 5
WorkloadScore     = 1 - min(CommittedHours / 160, 1)     ← committed, not attended
AvailabilityScore = shift duration / covering availability duration
```

Reason format, rounding and the sum-of-rounded-components rule are all unchanged.

---

## ۹. پیوست B — تنظیمات (عیناً از `docs/04-v2-prompts.md`)

```
SCORING__RATINGWEIGHT          0.30
SCORING__WORKLOADWEIGHT        0.30
SCORING__AVAILABILITYWEIGHT    0.40
SCORING__MONTHLYCAP            160
SCORING__RATINGDEFAULT         3.0

RATING__AUTOWEIGHT             0.50
RATING__SUPERVISORWEIGHT       0.50
RATING__ATTENDANCEWEIGHT       0.50
RATING__PUNCTUALITYWEIGHT      0.30
RATING__RELIABILITYWEIGHT      0.20
RATING__GRACEMINUTES           5
RATING__LEAVENOTICEHOURS       24
RATING__DOWNTIMECAPHOURS       8

LEAVE__ANNUALDAYSDEFAULT       26
ATTENDANCE__STALENESSSECONDS   120
ATTENDANCE__HEARTBEATSECONDS   60
```

هیچ‌کدام از این وزن‌ها/سقف‌ها Hard-code نمی‌شوند — همان قاعده‌ی v1، فقط این‌بار هم
سمت C# (`ScoringOptions`, `RatingOptions`) و هم سمت Python از یک منبع پیکربندی
می‌خوانند (V8، V12).

---

## ۱۰. چند نکته‌ی پیاده‌سازی که از این طراحی نتیجه می‌شود

**۱. بررسی تداخل شیفت‌های تأییدشده** با شرط استاندارد بازه‌ها، بدون تغییر از v1:
```sql
existing.StartUtc < new.EndUtc  AND  existing.EndUtc > new.StartUtc
```
این شرط از V3 به بعد باید هم اپلیکیشن‌های تأییدشده و هم شیفت‌های تخصیص‌مستقیم
(`AssignedCallAgentId`) یک کارشناس را در نظر بگیرد.

**۲. مرحله‌ی Approve اپلیکیشن `Extra`** کامل داخل یک تراکنش، بدون تغییر از v1:
```text
BEGIN TRANSACTION
  بررسی مالکیت Supervisor بر پروژه‌ی شیفت
  بررسی Status = Open
  بررسی مجدد تداخل شیفت‌های تأییدشده/تخصیص‌یافته
  Application → Approved  +  ثبت DecidedBy/At
  Shift.Status → Closed
  سایر Pendingها → Rejected با DecisionNote
COMMIT
```

**۳. مرحله‌ی Approve اپلیکیشن `Cover`** مشابه، ولی به‌جای `Closed`:
```text
BEGIN TRANSACTION
  بررسی مجدد تداخل برای کارشناسِ Cover
  Application → Approved
  Shift.AssignedCallAgentId → کارشناس Cover
  Shift.Status: Released → Assigned
  سایر Pendingهای Cover همان شیفت → Rejected با DecisionNote
COMMIT
```

**۴. ساعت‌های تأییدشده/متعهدشده** بر اساس ماهِ `Shift.StartUtc` محاسبه می‌شود، نه
ماهِ امروز — بدون تغییر از v1.

**۵. پایتون** فقط با `MERGE` روی `Recommendations` و (از V12) روی `Ratings.Breakdown`
می‌نویسد؛ به هیچ جدول دیگری دست نمی‌زند.

**۶. مانده‌ی مرخصی و سقف Downtime** هر دو در لحظه‌ی خواندن محاسبه می‌شوند، هرگز
ذخیره نمی‌شوند — دقیقاً مثل غیبت (بند ۷).

---

## ۱۱. مواردی که عمداً در این طراحی نیست

| مورد | جایگزین |
|---|---|
| جدول `ScriptRuns` | قید یکتا + MERGE همان Idempotency را می‌دهد |
| وضعیت `Completed`/`NoShow` روی شیفت | غیبت/حضور همیشه محاسبه‌شده در لحظه‌ی خواندن است |
| `Capacity` روی شیفت | هر شیفت حداکثر یک کارشناس متعهد (Assigned/Approved) دارد |
| Timezone به ازای کارشناس | همه‌چیز UTC؛ فقط مرز روز مرخصی به تقویم تهران تبدیل می‌شود (بند ۴-۹) |
| جدول `ScoringWeights`/`RatingWeights` | وزن‌ها در `appsettings.json` (پیوست B) |
| جدول Activity Log مستقل | تاریخچه از جدول‌های موجود Aggregate می‌شود (V10) |
| Withdraw کردن اپلیکیشن | خارج از Scope در v1 و همچنان در v2 (V6) |
| Soft Delete | خارج از Scope |

همه‌ی این موارد در بخش «Assumptions» و «At Scale» فایل README مستند می‌شوند (V12).

---

## ۱۲. چه چیزی از v1 تغییر کرد و چرا

| تغییر | چرا |
|---|---|
| `Employers`/`Experts` → `Supervisors`/`CallAgents` | نام‌ها باید سلسله‌مراتب سه‌نقشی جدید را منعکس کنند؛ `Employer` دیگر دقیق نبود چون یک نقش بالاتر (`Manager`) اضافه شد |
| نقش سوم `Manager` بدون جدول پروفایل | جداسازی `Users` از پروفایل دامنه (تصمیم v1) دقیقاً همین توسعه را بدون دست‌زدن به `Users` ممکن کرد |
| مالکیت پروژه از Supervisor به Manager منتقل شد | یک نفر باید کل مجموعه را ببیند و پروژه‌ها را بین سرپرست‌ها جابه‌جا کند؛ Supervisor کنترل عملیاتی روزمره را نگه می‌دارد |
| `Shifts.Status` از دو مقدار به چهار مقدار | تخصیص مستقیم (بدون اپلیکیشن) و چرخه‌ی مرخصی/Cover نیاز به بازنمایی صریح حالت دارند |
| `AttendanceSessions` جدید | بدون این جدول، «چند نفر الان کار می‌کنند» و غیبت قابل محاسبه نبودند |
| `AgentRequests` جدید | مرخصی و Downtime قبلاً اصلاً وجود نداشتند؛ یک جدول برای هر دو چون شکل داده یکسان است |
| `SupervisorEvaluations` جدید | نیمه‌ی کیفی Rating قبلاً هیچ ورودی واقعی نداشت (`ExpertRatings` فقط Seed بود) |
| `Ratings.Breakdown` | فرمول Rating چندجزئی شد؛ بدون این ستون ردیابی «چرا این عدد» ممکن نبود |
| `ShiftApplications.Kind` | یک مکانیزم اپلای، حالا هم برای شیفت باز (`Extra`) و هم برای شیفت رهاشده (`Cover`) استفاده می‌شود |
| `CallAgents.AnnualLeaveDays` | سهمیه‌ی مرخصی باید جایی ذخیره شود؛ بقیه‌ی محاسبه مشتق‌شده می‌ماند |
| بخش «قدم بعدی» v1 حذف شد | ترتیب ساخت اکنون در `docs/04-v2-prompts.md` (V0–V12) زندگی می‌کند؛ نگه‌داشتن دو منبع برای یک چیز خطای هم‌گام‌سازی می‌سازد |
| فرمول Score | شکل کلی **بدون تغییر** — فقط `WorkloadScore` در V9 به ساعت متعهدشده (نه حضوریافته) اشاره می‌کند؛ جزئیات در پیوست A |

---

*نسخه‌ی v1 این سند در تاریخچه‌ی Git این ریپازیتوری باقی می‌ماند و دیگر به‌روزرسانی
نمی‌شود؛ این فایل از این پس فقط v2 را توصیف می‌کند.*
