# ShiftFlow — ساختار Repository و استراتژی Git

**وضعیت:** پیش‌نویس برای تأیید — قبل از ساخت Solution

---

## ۱. نام Repository

پیشنهاد: **`shiftflow`**

نام محصول باشد، نه نام شرکت. `irancell-shift-management` هم قابل قبول است ولی اگر بعداً بخواهی این پروژه در پورتفولیوی عمومی‌ات بماند، نام محصول تمیزتر است.

---

## ۲. ساختار کلی

```text
shiftflow/
│
├── .github/
│   └── workflows/
│       └── ci.yml                  build + test خودکار
│
├── backend/
│   ├── ShiftFlow.sln
│   ├── src/
│   │   ├── ShiftFlow.Domain/
│   │   ├── ShiftFlow.Application/
│   │   ├── ShiftFlow.Infrastructure/
│   │   └── ShiftFlow.Api/
│   └── tests/
│       └── ShiftFlow.Tests/
│
├── frontend/
│   ├── src/
│   ├── index.html
│   ├── package.json
│   ├── tsconfig.json
│   └── vite.config.ts
│
├── python/
│   ├── recommendation.py
│   ├── scoring.py
│   ├── db.py
│   ├── config.py
│   ├── test_scoring.py
│   ├── requirements.txt
│   └── README.md
│
├── database/
│   ├── 01-schema.sql               تولیدشده از Migration
│   ├── 02-indexes.sql              تولیدشده از Migration
│   └── 03-seed.sql                 دستی
│
├── docker/
│   ├── api.Dockerfile
│   ├── web.Dockerfile
│   ├── recommender.Dockerfile
│   └── nginx.conf
│
├── docs/
│   ├── 01-erd-and-schema.md        سند طراحی دیتابیس
│   ├── 02-repository-structure.md  همین سند
│   └── erd.png
│
├── docker-compose.yml
├── .env.example
├── .editorconfig
├── .gitignore
└── README.md
```

پوشه‌ی `docs/` عمداً هست. Reviewer وقتی می‌بیند طراحی **قبل** از کد مستند شده، برداشتش از پروژه کاملاً فرق می‌کند.

---

## ۳. ساختار داخلی Backend

### ShiftFlow.Domain
هیچ وابستگی‌ای ندارد — نه به EF Core، نه به ASP.NET.

```text
Domain/
├── Entities/          User, Employer, Expert, Project, ExpertProject,
│                      Availability, Shift, ShiftApplication,
│                      ExpertRating, Recommendation
├── Enums/             UserRole, ShiftStatus, ApplicationStatus
└── Exceptions/        DomainException, BusinessRuleViolationException
```

### ShiftFlow.Application
فقط به `Domain` وابسته است.

```text
Application/
├── Abstractions/
│   ├── IAppDbContext.cs        ← به‌جای Repository Pattern
│   ├── IJwtTokenService.cs
│   ├── IPasswordHasher.cs
│   ├── ICurrentUser.cs         ← برای Authorization سطح منبع
│   └── IClock.cs               ← زمان تزریق‌شونده، تست را قطعی می‌کند
│
├── Features/
│   ├── Auth/            LoginService + DTOs
│   ├── Projects/
│   ├── Experts/
│   ├── Availabilities/  ← منطق Merge اینجاست
│   ├── Shifts/
│   ├── Applications/    ← پنج Business Rule اینجاست
│   └── Recommendations/
│
├── Common/
│   └── ScoringOptions.cs       وزن‌های امتیازدهی از appsettings
└── DependencyInjection.cs
```

هر Feature یک پوشه با همین سه چیز: `Dtos/`, `XxxService.cs`, `Validators/`.

> `IClock` را جدی بگیر. هر جا `DateTime.UtcNow` مستقیم صدا زده شود، تست «ساعت‌های ماه جاری» غیرقطعی می‌شود.

### ShiftFlow.Infrastructure

```text
Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/         یک فایل IEntityTypeConfiguration به ازای هر Entity
│   ├── Migrations/             ← حتماً Commit می‌شود
│   └── SeedData.cs
├── Identity/
│   ├── JwtTokenService.cs
│   └── BCryptPasswordHasher.cs
├── SystemClock.cs
└── DependencyInjection.cs
```

هر `Configuration` مستقیماً همان چیزی است که در سند ERD تعریف شد: نوع ستون، قید CHECK، ایندکس، رفتار حذف. یعنی سند طراحی و کد یک‌به‌یک قابل تطبیق‌اند.

### ShiftFlow.Api

```text
Api/
├── Controllers/                فقط HTTP — بدون Business Logic
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs   خروجی خطای یکنواخت JSON
├── Extensions/
│   ├── SwaggerExtensions.cs             دکمه Authorize برای JWT
│   └── AuthExtensions.cs
├── appsettings.json
└── Program.cs
```

### ShiftFlow.Tests
تست‌ها بر اساس **قانون** سازمان‌دهی شوند، نه بر اساس کلاس:

```text
Tests/
├── Applications/
│   ├── ApplyForShift_AvailabilityTests.cs      ← تست الزامی ۱
│   ├── ApplyForShift_OverlapTests.cs           ← تست الزامی ۲
│   ├── ApplyForShift_DuplicateTests.cs
│   ├── ApplyForShift_ProjectMembershipTests.cs
│   └── ApproveApplicationTests.cs
├── Availabilities/
│   └── MergeAvailabilityTests.cs
└── TestBase.cs                 SQLite in-memory + داده‌ی پایه
```

**نکته:** برای تست از `EF Core InMemory` استفاده نکن — قیدهای یکتا و CHECK را نادیده می‌گیرد و تست‌ها دروغ می‌گویند. `SQLite in-memory` استفاده کن که رفتار رابطه‌ای واقعی دارد.

---

## ۴. ساختار داخلی Frontend

```text
frontend/src/
├── app/
│   ├── router.tsx              مسیرها + محافظت بر اساس Role
│   ├── providers.tsx           QueryClient + AuthProvider
│   └── auth-context.tsx        تنها State سراسری
│
├── features/
│   ├── auth/           api.ts · types.ts · hooks.ts · pages/
│   ├── projects/
│   ├── experts/
│   ├── availability/
│   ├── shifts/
│   ├── applications/
│   └── recommendations/
│
├── shared/
│   ├── api/client.ts           نمونه axios + اینترسپتور توکن
│   ├── ui/                     Button, Input, Table, Modal
│   └── lib/datetime.ts         تبدیل UTC ↔ تهران + نمایش شمسی
│
└── main.tsx
```

بدون Redux و بدون Zustand. با TanStack Query تنها چیزی که باقی می‌ماند اطلاعات کاربر لاگین‌شده است که یک Context ساده کافی است.

`shared/lib/datetime.ts` تنها جایی است که تبدیل زمان انجام می‌شود. اگر این تبدیل در چند فایل پخش شود، باگ‌های زمانی اجتناب‌ناپذیر می‌شوند.

---

## ۵. ساختار Python

```text
python/
├── config.py           خواندن connection string و وزن‌ها از متغیر محیطی
├── db.py               اتصال pymssql + کوئری‌ها + MERGE
├── scoring.py          ← توابع خالص، بدون دیتابیس
├── recommendation.py   نقطه ورود
└── test_scaring.py     تست همان توابع خالص
```

جدا کردن `scoring.py` از `db.py` عمدی است: منطق امتیازدهی به تابع خالص تبدیل می‌شود و بدون دیتابیس قابل تست است. این همان جایی است که می‌شود نشان داد اسکریپت پایتون هم مثل بک‌اند طراحی شده، نه اینکه یک فایل ۲۰۰ خطی باشد.

**وزن‌ها باید در هر دو طرف یکسان باشند.** از متغیر محیطی مشترک در `.env` خوانده می‌شوند تا بین C# و Python واگرا نشوند.

---

## ۶. Docker Compose

```text
services:
  db          SQL Server 2022 + healthcheck + volume
  api         depends_on: db (condition: service_healthy)
  web         nginx، proxy مسیر /api به api
  recommender اجرای یک‌باره، با profile فعال می‌شود
```

سه نکته‌ای که اگر رعایت نشوند اجرای اول Reviewer شکست می‌خورد:

1. **`condition: service_healthy`** — `depends_on` خالی فقط منتظر *شروع* کانتینر می‌ماند، نه *آماده شدن* SQL Server. بدون این، بک‌اند قبل از دیتابیس بالا می‌آید و کرش می‌کند.
2. **رمز SA باید قوی باشد** — حداقل ۸ کاراکتر با حروف بزرگ، کوچک، عدد و نماد. در غیر این صورت کانتینر SQL Server بی‌صدا خاموش می‌شود. این یکی از رایج‌ترین دلایل «چرا بالا نمی‌آید» است.
3. **`recommender` نباید سرویس دائمی باشد.** با `profiles` تعریف شود تا با `docker compose up` اجرا نشود:
   ```bash
   docker compose run --rm recommender
   ```

Volume دیتابیس هم تعریف شود تا با ری‌استارت داده‌ها نپرند.

---

## ۷. چه چیزی در Git باشد و چه چیزی نباشد

### حتماً Commit شود

| مورد | چرا |
|---|---|
| `Migrations/` | بدون آن Reviewer نمی‌تواند دیتابیس بسازد |
| `database/*.sql` | خواسته‌ی صریح صورت‌مسئله |
| `.env.example` | نقشه‌ی متغیرهای لازم |
| `docs/` | نشان‌دهنده‌ی طراحی قبل از کد |
| `package-lock.json` | نصب قابل تکرار |

### هرگز Commit نشود

```gitignore
# .NET
bin/
obj/
*.user
.vs/
appsettings.Development.json

# Node
node_modules/
dist/

# Python
__pycache__/
*.pyc
.venv/

# Secrets
.env
*.pfx

# OS
.DS_Store
Thumbs.db
```

**قانون مطلق:** هیچ رمز واقعی در Git نرود. رمزها فقط در `.env` (ignore شده) و نمونه‌شان در `.env.example`.

---

## ۸. استراتژی شاخه‌بندی

```text
main
 ├── feat/project-scaffold
 ├── feat/domain-and-database
 ├── feat/auth-jwt
 ├── feat/projects-experts
 ├── feat/availability
 ├── feat/shifts
 ├── feat/applications-business-rules
 ├── feat/tests
 ├── feat/frontend
 ├── feat/python-recommendation
 └── feat/docker-compose
```

هر شاخه با `--no-ff` در `main` ادغام شود:

```bash
git merge --no-ff feat/availability
```

اینطوری در `git log --graph` هر قابلیت یک بلوک مشخص است، نه یک ردیف صاف از commitهای درهم. در پایان کار:

```bash
git tag v1.0.0
```

> اگر ترجیح می‌دهی ساده‌تر باشد، commit مستقیم روی `main` هم قابل دفاع است — به شرطی که ترتیب commitها منطقی باشد. چیزی که **قابل دفاع نیست** یک commit بزرگ به اسم `initial commit` با کل پروژه است. تاریخچه‌ی گیت بخشی از چیزی است که ارزیابی می‌شود.

---

## ۹. الگوی Commit

طبق Conventional Commits، با بدنه‌ای که **دلیل** را توضیح دهد نه اینکه چه چیزی تغییر کرده:

```text
feat(applications): enforce availability coverage rule

An expert may only apply when the entire shift falls inside a single
availability window. Adjacent windows are merged on insert, so this
check stays a simple range containment query instead of a gap-filling
algorithm.

Refs: docs/01-erd-and-schema.md §6
```

```text
fix(db): break multiple cascade paths on Experts

SQL Server rejects two cascade paths into the same table. FKs coming
from the Expert side are set to NO ACTION; the Employer → Project →
Shift path keeps cascade.
```

ارجاع دادن به سند طراحی در بدنه‌ی commit، ارتباط بین «چه فکری کردیم» و «چه کدی نوشتیم» را برای Reviewer آشکار می‌کند.

---

## ۱۰. CI

یک فایل ساده‌ی GitHub Actions:

```text
on: push, pull_request
jobs:
  backend:  dotnet restore → build → test
  frontend: npm ci → tsc --noEmit → build
  python:   pip install → pytest
```

هزینه‌اش ۲۰ دقیقه است و یک نشان سبز روی صفحه‌ی Repository می‌گذارد که یعنی پروژه واقعاً build می‌شود.

---

## ۱۱. اسکلت README

```text
ShiftFlow
├── Overview                    یک پاراگراف + اسکرین‌شات
├── Architecture                دیاگرام لایه‌ها + چرا این معماری
├── Tech Stack
├── Quick Start (Docker)        سه دستور، نه بیشتر
├── Manual Setup                اجرای بدون Docker
├── Demo Accounts               نام کاربری و رمز هر دو نقش
├── API Documentation           لینک Swagger + جدول endpointها
├── Business Rules              پنج قانون با مثال
├── Scoring Formula             فرمول + وزن‌ها + مثال محاسبه
├── Database Design             لینک به docs/01-erd-and-schema.md
├── Testing                     چطور تست‌ها اجرا شوند + چه چیزی پوشش داده شده
├── Assumptions                 هر جای مبهم صورت‌مسئله + تصمیم ما
├── At Scale                    آنچه در سیستم واقعی اضافه می‌شد
└── AI Tools Used
```

دو بخش **Assumptions** و **At Scale** مهم‌ترین بخش‌های README هستند. آنجاست که تفاوت بین «کد را نوشت» و «فهمید چه می‌کند» دیده می‌شود — و همان‌جاست که مواردی مثل Timezone چندگانه، وضعیت‌های بیشتر شیفت و Refresh Token را مستند می‌کنیم بدون اینکه وقتی صرفشان کنیم.

**نکته:** بخش `AI Tools Used` را از همین الان به‌صورت یادداشت جاری نگه دار، نه اینکه آخر کار از حافظه بنویسی.

---

## ۱۲. قدم بعدی

با تأیید این ساختار:

```text
۱. git init + .gitignore + .editorconfig + README اولیه
۲. ساخت Solution و چهار پروژه + ارجاعات بین لایه‌ها
۳. Entityها و Enumها در Domain
۴. DbContext و Configurationها
۵. اولین Migration و تطبیق خط‌به‌خط با سند ERD
```
