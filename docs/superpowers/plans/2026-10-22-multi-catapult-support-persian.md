# پشتیبانی از چند منجنیق (Multi-Catapult Support) — برنامه پیاده‌سازی

> **برای عامل‌های اجرایی (Agentic workers):** مهارت زیر الزامی است: از `superpowers:executing-plans` برای پیاده‌سازی گام‌به‌گام این برنامه استفاده کنید. مراحل از نحو چک‌باکس (`- [ ]`) برای پیگیری استفاده می‌کنند. **قانون پروژه آموزشی (`CLAUDE.md`):** *کاربر* هر مرحله را خودش پیاده‌سازی می‌کند. هرگز برای او فایلی ننویسید یا تغییری در صحنه (Scene) ایجاد نکنید — برنامه را در اختیارش بگذارید و توضیح دهید.

**هدف:** یک صحنه می‌تواند شامل $N$ منجنیق باشد؛ فلش‌ها منجنیق فعال را تغییر می‌دهند (به صورت چرخشی / wrap-around)؛ دوربین به سمت نقطه اتکای (Anchor) منجنیق فعال می‌لغزد (به صورت مستقیم یا در امتداد یک مسیر طراحی‌شده توسط طراح) و سپس به نرمی نشانه آن را دنبال می‌کند؛ هر مرحله انتخاب می‌کند که مهمات مشترک باشد یا به‌ازای هر منجنیق.

**معماری:** یک `CatapultManager` در سطح بالا (Top-level)، رجیستری منجنیق‌ها و تنها `ProjectileSelector` سراسری را در اختیار دارد و مخازن مهمات (مشترک یا به‌ازای هر منجنیق) را می‌سازد. یک `CameraRig` در سطح بالا، وضعیت جهانی (World pose) دوربین را در اختیار دارد و آن را بین وضعیت‌های `CameraAnchor` هر منجنیق جابه‌جا می‌کند، و سپس با کمی تأخیر (Lag) نشانه را دنبال می‌کند. اسکریپت‌های موجود (`AmmoManager`، `Projectile`، `ProjectileSpawner`، `ProjectileLuncher`، `InputController`، `ProjectileCarouselUI`) بازآرایی (Refactor) می‌شوند تا به جای فرض «تنها یک منجنیق»، روی *منجنیق فعال* و *انواع صریح پرتابه* عمل کنند.

**پشته فناوری (Tech Stack):** Unity 6000.6 (URP)، زبان C#، تست‌های NUnit در حالت EditMode بر روی asmdef موجود `CastleCrusher.Core`. بدون پکیج جدید (`com.unity.splines` نصب **نیست** — اسپلاین به صورت دستی در `Core` پیاده‌سازی شده است).

## محدودیت‌های سراسری

* یونیتی **6000.6+**، URP، زبان C#. بدون پکیج جدید.

* `CastleCrusher.Core` (asmdef) باید عاری از تایپ‌های بازی (`SO_ProjectileType`، `AmmoManager` و …) باقی بماند: یک اسمبلی asmdef **نمی‌تواند به `Assembly-CSharp` از پیش تعریف‌شده ارجاع دهد**. ریاضیات موتور (`Vector3`، `Mathf`) مشکلی ندارند (`noEngineReferences: false`).

* **بدون کامیت خودکار (No auto-commit).** پس از هر تسک: فایل‌های تغییر یافته را `git add` کنید و پیام کامیت را پیشنهاد دهید؛ کاربر خودش کامیت می‌کند.

* همگام‌سازی GDD (فاز ۶) طبق `CLAUDE.md` الزامی است — این ویژگی طراحی بازی را تغییر می‌دهد.

* زبان GDD: سرتیترهای انگلیسی، بدنه فارسی. کد و کامنت‌ها: انگلیسی.

## وضعیت فعلی مخزن (تأیید شده در 2026-11-08)

* `Assets/Scripts/Core/AmmoPool.cs` — **پیش‌نویس شده**، کامپایل می‌شود، اما دو باگ نهفته دارد که این برنامه آن‌ها را برطرف می‌کند: `Get`/`Consume` روی یک کلید ناشناخته `KeyNotFoundException` پرتاب می‌کنند، و `Has` به معنای «وجود کلید» است، نه «تعداد $> 0$».

* `Assets/Scripts/Core/CameraPath.cs` — **پیش‌نویس شده**؛ `PolylinePath` روی لیست خالی یا تک‌نقطه‌ای کرش می‌کند (خطای خارج از محدوده بودن ایندکس `points[0]`).

* `Assets/Scripts/Core/RingIndex.cs` — **اسکلت خالی** (بدنه متدها وجود ندارد) — تا زمانی که تسک 1.1 آن را پر نکند، پروژه کامپایل نمی‌شود.

* هنوز هیچ فایل تستی برای این سه کلاس Core وجود ندارد.

* تأیید شده با grep: `GetJumpBtn`/`_jumpBtn` در `InputController` در هیچ کجا **خوانده نمی‌شوند** — حذف آن‌ها بی‌خطر است. `DecreaseCurrentAmmo` دقیقاً **یک** فراخوانی‌کننده (`Projectile.Shoot`) دارد و `GetCurrentAmmoCount` **صفر** فراخوانی‌کننده دارد — حذف آن‌ها بی‌خطر است.

## تصمیمات اتخاذ شده (برنامه بر پایه این‌ها ساخته می‌شود)

| موضوع | تصمیم | 
| ----- | ----- | 
| ساختار دوربین | **ریگ سطح بالا + نقطه اتکای به‌ازای هر منجنیق.** یک GameObject به نام `CameraRig` مالک دوربین است؛ هر منجنیق یک `CameraAnchor` دارد که وضعیت دوربین را توصیف می‌کند. | 
| دوربین هنگام نشانه گرفتن | **دنبال کردن نرم (Soft follow):** هنگام نشانه گرفتن، دوربین چرخش حول محور عمودی (Yaw) منجنیق را *با یک تأخیر* (آهسته‌تر از خود منجنیق) دنبال می‌کند تا حس خوشایندی داشته باشد و خشک نباشد. | 
| انتهای فلش‌ها | **چرخشی (Wrap around / cyclic):** فلش راست از آخرین منجنیق به اولین منجنیق می‌رود، مشابه نحوه چرخش کاروسل پرتابه‌ها. | 
| مدل مهمات | **هر منجنیق کپی خودش را** از تجهیزات مرحله (Loadout) می‌گیرد، زمانی که مرحله این را بخواهد؛ در غیر این صورت یک مخزن مشترک وجود دارد. انتخاب آن به‌ازای هر مرحله در `SO_LevelData` انجام می‌شود. | 
| انتخاب نوع پرتابه | **سراسری (Global).** یک `ProjectileSelector` برای کل مرحله — نوع انتخاب‌شده صرف‌نظر از اینکه کدام منجنیق فعال است، یکسان است. | 
| اسپلاین | **Catmull-Rom سفارشی در `CastleCrusher.Core`** — ریاضیات محض، قابل تست واحد، بدون نیاز به پکیج جدید. | 

## معماری هدف

```
Scene
├── CameraRig                        [CatapultCameraRig]   ← سطح بالا، زیرمجموعه منجنیق نیست
│   └── Main Camera                  [CameraShakeManager]  ← وضعیت محلی (0,0,0)؛ ریگ مالک وضعیت جهانی است
├── CatapultManager                  [CatapultManager]     ← مغز مرحله: رجیستری، منجنیق فعال، مخازن مهمات
│   │                                [InputController]     ← به اینجا منتقل شد (قبلاً روی ریشه منجنیق بود)
│   └── ProjectileSelector           [ProjectileSelector]  ← تنها لیست سراسری انواع پرتابه
├── SiegeCatapult_A                  ← نمونه FBX
│   ├── ProjectileLuncher            [ProjectileLuncher]
│   ├── Catapult                     [Catapult]            ← جدید: نما (Facade) به‌ازای هر منجنیق
│   ├── CameraAnchor                 [CameraAnchor]        ← جدید: وضعیت محلی دوربین + نقاط مسیر اختیاری
│   ├── Highlight                    (بصری اختیاری)
│   └── catapult (head)              ← فرزند FBX
│       ├── AmmoManager              [AmmoManager]         ← یک *ارجاع به مخزن* نگه می‌دارد، نه خود مخزن را
│       └── Spawner                  [ProjectileSpawner]
└── SiegeCatapult_B                  ← نمونه FBX تکثیر شده، با همان مجموعه کامپوننت‌ها

```

جریان داده هنگام جابه‌جایی (Switch):

```
دکمه فلش ─► CatapultManager.NextCatapult()
                ├─ ActiveCatapult = B
                ├─ B.launcher.LoadProjectile()      (فقط منجنیق فعال پرتابه را بارگذاری می‌کند)
                ├─ OnActiveCatapultChanged(B)
                │     ├─► ProjectileCarouselUI      → اشتراک مجدد در مخزن B، به‌روزرسانی اعداد
                │     ├─► InputController           → رها کردن هرگونه کشیدن (Drag) در حال انجام
                │     └─► CatapultCameraRig.MoveTo(B)
                └─ CatapultCameraRig.MoveTo(B)
                      start = وضعیت فعلی ریگ
                      end   = وضعیت B.CameraAnchor (ارزیابی شده در قاب سکون B)
                      path  = B.CameraAnchor.pathPoints (۲ نقطه یا بیشتر ← Catmull-Rom، در غیر این صورت مستقیم)

```

## ترتیب انجام کارها

| \# | فاز | دلیل این ترتیب | 
| ----- | ----- | ----- | 
| 1 | **فاز ۰** (اصلاح داده‌های قدیمی صحنه) | کم‌هزینه؛ صحنه به هر حال قبل از سیم‌کشی کامپوننت‌های جدید دوباره ذخیره می‌شود. | 
| 2 | **فاز ۱** (ریاضیات Core + تست‌ها) | کد محض، بدون کار در صحنه — تست‌های سبز قبل از دست زدن به ادیتور. همچنین مانع کامپایل را برطرف می‌کند (`RingIndex` خالی است). | 
| 3 | **فاز ۲.۱** (`SO_LevelData.ammoMode`) | یک فیلد enum؛ تمام بخش‌های بعدی آن را می‌خوانند. | 
| 4 | **فاز ۲.۲–۲.۵** (`Catapult`، `CatapultManager`، `CameraAnchor`، `CatapultCameraRig`) | ستون فقرات این ویژگی. | 
| 5 | **فاز ۳** (بازآرایی‌ها / Refactors) | منیجر برای تحویل مخازن و بارگذاری مجدد پرتابه‌ها به این‌ها نیاز دارد. | 
| 6 | **فاز ۴** (رابط کاربری فلش‌ها) | لایه نمایشی محض بر روی یک منیجر آماده و کارآمد. | 
| 7 | **فاز ۵** (سیم‌کشی صحنه) | در یک نشست — ترتیب «ابتدا جابه‌جایی، سپس تکثیر» اهمیت دارد. | 
| 8 | **فاز ۶** (GDD) | در آخر، تا نشان‌ها (Badges) آنچه واقعاً پیاده‌سازی شده را توصیف کنند. | 

## فاز ۰ — رفع موانع: اصلاح مواردی که از قبل خراب هستند

این‌ها باگ‌های واقعی هستند (تأیید شده در صحنه کامیت‌شده) که به محض ظاهر شدن منجنیق دوم دردسرساز می‌شوند. ابتدا آن‌ها را در ادیتور انجام دهید، زیرا اصلاحشان نیازمند ذخیره مجدد صحنه است.

### تسک 0.1: صحنه فیلدهایی را ذخیره کرده که دیگر روی اسکریپت‌ها وجود ندارند

**فایل‌ها:**

* تغییر در: `Assets/Scripts/Projectile/ProjectileLuncher.cs` (یک Attribute)

* ذخیره مجدد: `Assets/Scenes/SampleScene.unity`

**مشکل چیست.** `Assets/Scenes/SampleScene.unity` هنوز این موارد را سریال‌سازی می‌کند:

* `bs: {fileID: 799082386}` روی لانچر — این فیلد در کامیت `649c4dd` به `projectileSpawner` تغییر نام داد، اما صحنه پس از تغییر نام هرگز دوباره ذخیره نشد، بنابراین این ارجاع به صورت **null** بازیابی (Deserialise) می‌شود. `ProjectileLuncher.DelaySpawn` در اولین شلیک یک `NullReferenceException` پرتاب خواهد کرد.

* `uiManager: {fileID: 2123016087}` روی `AmmoManager` و `ScoreManager` — این فیلد در کامیت `8217392` حذف شد. یونیتی فیلدهای ناشناخته را نادیده می‌گیرد، بنابراین نویز بی‌خطری است، اما قدیمی و منقضی است.

* `m_EditorClassIdentifier: Assembly-CSharp::BuletLuncher` روی لانچر — کلاس در `649c4dd` به `ProjectileLuncher` تغییر نام داد. بی‌خطر است (یونیتی کامپوننت‌ها را از طریق GUID اسکریپت پیدا می‌کند، نه نام کلاس)، اما یک *مهر زمانی* مفید است: سندی مبنی بر اینکه صحنه آخرین بار قبل از تغییر نام نوشته شده است.

**نکته آموزشی — چرا این اتفاق می‌افتد.** فایل صحنه یک *اسنپ‌شات سریال‌سازی‌شده* است. تغییر نام یا حذف یک فیلد `public` یا `[SerializeField]` باعث بازنویسی خودکار صحنه **نمی‌شود**؛ یونیتی فقط زمانی این موارد را تطبیق می‌دهد که صحنه را باز و ذخیره کنید. دو راه برای جان سالم به در بردن از تغییر نام وجود دارد:

* `[FormerlySerializedAs("bs")]` — یونیتی داده‌های قدیمی را در صحنه نگه می‌دارد و آن‌ها را به فیلد جدید نگاشت می‌کند. بهترین روش زمانی که صحنه قدیمی باید به کار خود ادامه دهد.

* صرفاً باز کردن و ذخیره مجدد — یونیتی کلید ناشناخته را حذف می‌کند. مناسب برای پروژه‌ای که در حال توسعه است.

**واسط‌ها (Interfaces):**

* تولید می‌کند: صحنه‌ای که در آن `ProjectileLuncher.projectileSpawner` واقعاً به شیء Spawner ارجاع می‌دهد. همه‌چیز در فاز ۵ به این وابسته است.

* \[ \] **مرحله ۱: اضافه کردن Attribute** (محکم‌کاری — همچنین تاریخچه را برای هر کسی که کد را می‌خواند مستند می‌کند). در بالای `ProjectileLuncher.cs`، فضای نام را اضافه کرده و فیلد را نشانه‌گذاری کنید:

```
using UnityEngine;
using UnityEngine.Serialization;   // NEW

public class ProjectileLuncher : MonoBehaviour
{
    [FormerlySerializedAs("bs")]   // NEW — the field was called "bs" before commit 649c4dd
    public ProjectileSpawner projectileSpawner;
    // ... rest unchanged

```

*چرا:* `[FormerlySerializedAs("oldName")]` به سریال‌ساز می‌گوید: «اگر فایل صحنه یک ورودی `bs:` دارد، آن را در این فیلد بارگذاری کن». بدون آن، یونیتی در صحنه به دنبال `projectileSpawner:` می‌گردد، چیزی پیدا نمی‌کند و فیلد را null رها می‌کند.

* \[ \] **مرحله ۲: `SampleScene` را باز کنید، اجازه دهید یونیتی دوباره کامپایل کند و صحنه را ذخیره کنید** (`Ctrl+S`). پس از ذخیره، فایل YAML صحنه را باز کنید و مطمئن شوید: هیچ کلید `bs:`، هیچ کلید `uiManager:` و هیچ شناسه `BuletLuncher` باقی نمانده است. (یا به سادگی به Inspector اعتماد کنید — مرحله ۳ را ببینید.)

* \[ \] **مرحله ۳: بررسی و تأیید.** در Hierarchy ریشه منجنیق را انتخاب کنید ← به فیلد `Projectile Spawner` روی `ProjectileLuncher` نگاه کنید — باید شیء `Spawner` را نشان دهد، نه `None` را. `AmmoManager` را انتخاب کنید — ردیف شبح `Ui Manager` باید ناپدید شده باشد.

* \[ \] **مرحله ۴: Stage و پیشنهاد کامیت**

```
git add Assets/Scripts/Projectile/ProjectileLuncher.cs Assets/Scenes/SampleScene.unity

```

پیام پیشنهادی: `fix(scene): restore spawner reference lost in rename, re-save stale scene`

> **مراقب `SampleScene.unity` باشید.** این یک فایل YAML حدوداً ۱۳,۰۰۰ خطی است و یونیتی بخش‌های بزرگی از آن را هنگام ذخیره بازنویسی می‌کند. آن را فقط پس از ذخیره از داخل ادیتور کامیت کنید — هرگز به صورت دستی ویرایشش نکنید — و ابتدا `git diff --stat` را بررسی کنید تا تغییرات تقریباً به همان اندازه‌ای باشد که انتظار دارید، نه یک بازنویسی کامل.

### تسک 0.2: اکشن‌های ورودی EventSystem به پیش‌فرض‌های پکیج اشاره می‌کنند

**فایل‌ها:**

* تغییر در: شیء `EventSystem` در `SampleScene` (فقط در Inspector)

**مشکل چیست.** `InputSystemUIInputModule` روی `EventSystem` به fileIDهای اکشن تحت GUID `ca9f5fa95ffab41fb9a615ab714db018` ارجاع می‌دهد — یعنی فایل `DefaultInputActions.inputactions` که همراه *پکیج* Input System عرضه می‌شود. نقشه UI آن دارای `Navigate / Submit / Cancel / Point / Click / ScrollWheel / …` است اما **`LeftClick` ندارد**. فایل `Assets/InputSystem_Actions.inputactions` خود پروژه (با نقشه‌های مناسب `Player` و `UI`) توسط هیچ چیزی ارجاع داده نشده است — صحنه از یک نمونه کپی شده و هرگز دوباره تنظیم نشده است.

* \[ \] **مرحله ۱:** در ادیتور، `EventSystem` را انتخاب کنید، `Input System UI Input Module` را باز کنید و روی **"Assign Default Actions"** کلیک کنید (یا `Actions Asset` را به `Assets/InputSystem_Actions.inputactions` ارجاع دهید و فیلدهای اکشن را پر کنید).

* \[ \] **مرحله ۲: بررسی و تأیید.** Play را بزنید و روی یک دکمه پرتابه در کاروسل کلیک کنید — باید به کلیک چپ واکنش نشان دهد. (اگر کلیک‌ها از قبل برای شما کار می‌کردند، این مورد ظاهری بوده است — در هر حال آن را اصلاح کنید تا تضمین شود فلش‌های جدید روی دسکتاپ کار می‌کنند.)

* \[ \] **مرحله ۳:** صحنه را ذخیره کنید؛ به همراه کامیت تسک 0.1 آن را Stage کنید.

### تسک 0.3: نشانه گرفتن با ماوس وجود ندارد (در حالی که پروژه ادعا می‌کند دارد)

`InputController` فقط `Input.GetTouch(0)` را می‌خواند، بنابراین در دسکتاپ `touchCount` همیشه $0$ است و نمی‌توانید نشانه بگیرید — با وجود اینکه `CLAUDE.md` و بخش ۳ GDD هر دو می‌گویند ماوس پشتیبانی می‌شود. **اینجا اصلاح جداگانه‌ای نداریم:** تسک 3.4 (بازنویسی `InputController`) یک مسیر اشاره (Pointer) یکپارچه اضافه می‌کند که لمس *یا* ماوس را می‌خواند. `ProjectSettings.asset` دارای `activeInputHandler: 2` ("Both") است، بنابراین API قدیمی `Input` در کنار پکیج Input System کار می‌کند.

## فاز ۱ — ریاضیات Core (`CastleCrusher.Core`) + تست‌های واحد

همه‌چیز در اینجا C# محض است: بدون `MonoBehaviour`، بدون اشیاء صحنه. این همان چیزی است که آن را در حالت **EditMode** قابل تست واحد می‌کند — تست‌ها بدون ورود به حالت Play در چند میلی‌ثانیه اجرا می‌شوند. کدها در `namespace Core` قرار دارند و نباید به `SO_ProjectileType` یا هیچ چیز دیگری از `Assembly-CSharp` ارجاع دهند (یک اسمبلی asmdef نمی‌تواند به `Assembly-CSharp` از پیش تعریف‌شده ارجاع دهد؛ اما *می‌تواند* از `Vector3` / `Mathf` استفاده کند زیرا `CastleCrusher.Core.asmdef` دارای `"noEngineReferences": false` است).

**دو فایل از سه فایل از قبل به عنوان پیش‌نویس وجود دارند** (از یک نشست قبلی): `AmmoPool.cs` و `CameraPath.cs` کامپایل می‌شوند اما باگ‌های واقعی را پنهان کرده‌اند؛ `RingIndex.cs` یک **اسکلت خالی** است — یک متد C# بدون `return` یک خطای کامپایل (`CS0161`) است، بنابراین **کل پروژه در حال حاضر کامپایل نمی‌شود**. این خطای کامپایل همان «قرمز» امروز است — تسک 1.1 ابتدا آن را سبز می‌کند.

**نکته آموزشی — نحوه اجرای تست‌ها.** `Window ▸ General ▸ Test Runner ▸ EditMode tab ▸ Run All`. هر تست یک متد ساده با نشان `[Test]` در کلاسی در کنار `CarouselWindowTests` موجود است (همان اسمبلی `CastleCrusher.EditModeTests` که از قبل به `CastleCrusher.Core` ارجاع می‌دهد — اگر تست جدیدی نتوانست تایپ‌های `Core` را پیدا کند، بخش `references` فایل asmdef آن را بررسی کنید). نام متدهای تست مانند یک جمله خوانده می‌شوند: `Consume_AtZero_ReturnsZero` — *با فرض* X *وقتی* Y رخ دهد *آنگاه* Z.

### تسک 1.1: `RingIndex` — پر کردن اسکلت (مانع کامپایل)

**فایل‌ها:**

* تغییر در: `Assets/Scripts/Core/RingIndex.cs`

* تست: `Assets/Tests/EditMode/RingIndexTests.cs` (جدید)

**واسط‌ها:**

* تولید می‌کند: `Core.RingIndex.Wrap(int index, int count)` و `Core.RingIndex.Next(int current, int count, int delta)` — هر دو `static` و هر دو یک ایندکس معتبر در بازه $[0, \text{count})$ (یا $0$ زمانی که $\text{count} \le 0$) برمی‌گردانند. تسک 2.3 (`CatapultManager.NextCatapult/PreviousCatapult`) و تست‌های خود تسک 1.1 از این استفاده می‌کنند.

* \[ \] **مرحله ۱: نوشتن تست‌ها**

```
using NUnit.Framework;
using Core;

namespace Tests.EditMode
{
    public class RingIndexTests
    {
        [Test]
        public void Wrap_NormalisesAnyIndexIntoRange()
        {
            Assert.AreEqual(0, RingIndex.Wrap(0, 3));
            Assert.AreEqual(2, RingIndex.Wrap(2, 3));
            Assert.AreEqual(0, RingIndex.Wrap(3, 3));   // one past the end wraps to the start
            Assert.AreEqual(1, RingIndex.Wrap(4, 3));
        }

        [Test]
        public void Wrap_HandlesNegativeIndices()
        {
            Assert.AreEqual(2, RingIndex.Wrap(-1, 3));  // one before the start wraps to the end
            Assert.AreEqual(2, RingIndex.Wrap(-4, 3));  // even a full loop backwards
        }

        [Test]
        public void Next_StepsForwardAndWraps()
        {
            Assert.AreEqual(1, RingIndex.Next(0, 3, 1));
            Assert.AreEqual(0, RingIndex.Next(2, 3, 1));   // last → first (right-arrow wrap)
        }

        [Test]
        public void Next_StepsBackwardAndWraps()
        {
            Assert.AreEqual(2, RingIndex.Next(0, 3, -1));  // first → last (left-arrow wrap)
            Assert.AreEqual(0, RingIndex.Next(1, 3, -1));
        }

        [Test]
        public void SingleItemRing_AlwaysWrapsToZero()
        {
            Assert.AreEqual(0, RingIndex.Next(0, 1, 1));
            Assert.AreEqual(0, RingIndex.Next(0, 1, -1));
        }

        [Test]
        public void EmptyRing_ReturnsZeroWithoutDividingByZero()
        {
            // A future caller might not guard against count == 0; RingIndex must not throw.
            Assert.AreEqual(0, RingIndex.Wrap(5, 0));
            Assert.AreEqual(0, RingIndex.Next(0, 0, 1));
        }
    }
}

```

*چرا این موارد خاص انتخاب شده‌اند:* جفت چرخش در دو انتها (`Next(2,3,1) == 0` و `Next(0,3,-1) == 2`) دقیقاً همان رفتاری است که فلش‌های منجنیق وعده می‌دهند («چرخش / wrap around»)؛ جفت حلقه خالی نیز از بروز `DivideByZeroException` پنهان در عملگر باقیمانده جلوگیری می‌کند.

* \[ \] **مرحله ۲: پیاده‌سازی — پر کردن اسکلت**

```
namespace Core
{
    public static class RingIndex
    {
        // Wrap-around index maths for a ring of `count` items.
        public static int Wrap(int index, int count)
        {
            if (count <= 0) return 0;   // empty ring: nothing to wrap into, stay at 0
            return ((index % count) + count) % count;
        }

        public static int Next(int current, int count, int delta)
        {
            return Wrap(current + delta, count);
        }
    }
}

```

دو تغییر نسبت به اسکلتی که دارید: کلاس `static` می‌شود (کلاسی که فقط اعضای ایستا دارد باید خودش `static` *باشد* — در این صورت هرگز به اشتباه نمونه (Instantiate) ساخته نمی‌شود) و بدنه هر دو متد پر می‌شود.

*خط به خط:*

* `count <= 0` $\rightarrow$ بازگرداندن `0`: یک حلقه خالی هیچ ایندکس معتبری ندارد، `0` پاسخ بی‌خطر است. بدون این شرط محافظ (Guard)، `count == 0` درون `%` باعث تقسیم بر صفر می‌شود.

* `((index % count) + count) % count` ترفند **باقیمانده مثبت (positive modulo)** است. عملگر `%` ساده در C# علامت عملوند *چپ* را حفظ می‌کند: `-1 % 3` برابر `-1` می‌شود، نه `2`. یک بار جمع کردن با `count` هر گام منفی را به محدوده مثبت برمی‌گرداند (`-1 + 3 = 2`)، و `% count` بیرونی نتایجی را که از قبل مثبت بوده‌اند دوباره نرمال‌سازی می‌کند (مثلاً `4 % 3 = 1`). برای هر `index > -count` یک `+ count` کافی است؛ فرم بالا نسخه استاندارد و کاملاً ایمن است.

* `Next` صرفاً `Wrap(current + delta, count)` است — فشار دادن فلش چپ یعنی `delta = -1` و فلش راست یعنی `delta = +1`.

**نکته آموزشی — استفاده مجدد.** `ProjectileSelector.SelectIndex` از قبل همین ترفند را به صورت خطی پیاده‌سازی کرده است (`((index % availableTypes.Count) + availableTypes.Count) % availableTypes.Count`). آن را دست نزنید (بافت آن کمی متفاوت است — وقتی ایندکس تغییر نکرده باشد زودتر خارج می‌شود)؛ کدهای جدید از `RingIndex` استفاده می‌کنند.

* \[ \] **مرحله ۳: اجرای تست‌ها** — Test Runner ▸ EditMode ▸ Run All. انتظار می‌رود: `RingIndexTests` **6/6 PASS**، و پروژه دوباره کامپایل شود. (پیش از مرحله ۲ کل اسمبلی تست بیلد نمی‌شد — آن وضعیت قرمز بود.)

* \[ \] **مرحله ۴: Stage و پیشنهاد کامیت**

```
git add Assets/Scripts/Core/RingIndex.cs Assets/Tests/EditMode/RingIndexTests.cs

```

پیام پیشنهادی: `feat(core): implement RingIndex wrap-around maths with tests`

### تسک 1.2: `AmmoPool` — رفع دو باگ نهفته و اثبات با تست‌ها

**فایل‌ها:**

* تغییر در: `Assets/Scripts/Core/AmmoPool.cs`

* تست: `Assets/Tests/EditMode/AmmoPoolTests.cs` (جدید)

**واسط‌ها:**

* تولید می‌کند: `Core.AmmoPool<TKey>` با متدهای `Set(TKey, int)`، `int Get(TKey)`، `bool Has(TKey)`، `int Consume(TKey)`، `int TotalRemaining()`، `void CopyFrom(IReadOnlyList<KeyValuePair<TKey,int>>)` و رویداد `event Action<TKey,int> OnChanged`. **قرارداد (Contract):** `Has` به معنای «تعداد $> 0$» است؛ `Get`/`Consume` روی کلید ناشناخته `0` برمی‌گردانند و هرگز استثنا پرتاب نمی‌کنند. فاز 3.1 (`AmmoManager.AttachPool` / `HasAmmo` / `Consume`) و فاز 2.3 (ساخت مخزن) دقیقاً از همین قرارداد استفاده می‌کنند.

**مشکل پیش‌نویس چیست.** دو باگ که تست‌های واحد برای شکار آن‌ها وجود دارند:

1. `Get` و `Consume` از ایندکسر `_counts[key]` استفاده می‌کنند که برای کلیدی که هرگز تنظیم نشده است **`KeyNotFoundException` پرتاب می‌کند**. در بازی این اتفاق زمانی می‌افتد که یک Loadout نوعی را از قلم می‌اندازد و بازیکن به طریقی آن را شلیک می‌کند — یک کرش به جای یک «۰» محترمانه.

2. `Has` به صورت `_counts.ContainsKey(key)` نوشته شده است — یعنی «*کلید وجود دارد*»، نه «*مهماتی باقی مانده است*». پس از مصرف آخرین تخته سنگ (Boulder)، `Has(Boulder)` همچنان `true` برمی‌گرداند و رابط کاربری دکمه را قابل کلیک نگه می‌دارد.

**نکته آموزشی — جنریک‌ها (Generics) به عنوان ابزاری برای جداسازی وابستگی.** `AmmoPool<SO_ProjectileType>` پای `Assembly-CSharp` را به Core باز می‌کند و اسمبلی تست را می‌شکند. `AmmoPool<TKey>` هسته Core را خالص نگه می‌دارد و تست‌ها `AmmoPool<string>` را نمونه‌سازی می‌کنند تا رفتار را بدون هیچ تایپ مربوط به بازی اثبات کنند. لایه بازی (تسک 3.1) تصمیم می‌گیرد که کلید از نوع `SO_ProjectileType` باشد.

* \[ \] **مرحله ۱: نوشتن تست‌های ناموفق (Failing tests)**

```
using System.Collections.Generic;
using NUnit.Framework;
using Core;

namespace Tests.EditMode
{
    public class AmmoPoolTests
    {
        private AmmoPool<string> _pool;

        [SetUp]
        public void SetUp() => _pool = new AmmoPool<string>();

        [Test]
        public void Set_ThenGet_ReturnsCount()
        {
            _pool.Set("wood", 7);
            Assert.AreEqual(7, _pool.Get("wood"));
        }

        [Test]
        public void Get_UnknownKey_ReturnsZero()
        {
            Assert.AreEqual(0, _pool.Get("nothing"));
        }

        [Test]
        public void Has_MeansCountAboveZero()
        {
            _pool.Set("wood", 2);
            Assert.IsTrue(_pool.Has("wood"));

            _pool.Set("stone", 0);
            Assert.IsFalse(_pool.Has("stone"));       // exists but empty → not "has ammo"

            Assert.IsFalse(_pool.Has("never-added")); // unknown key → not "has ammo"
        }

        [Test]
        public void Consume_DecrementsAndReturnsNewValue()
        {
            _pool.Set("wood", 3);
            Assert.AreEqual(2, _pool.Consume("wood"));
            Assert.AreEqual(2, _pool.Get("wood"));
        }

        [Test]
        public void Consume_AtZero_ReturnsZeroAndStaysZero()
        {
            _pool.Set("wood", 0);
            Assert.AreEqual(0, _pool.Consume("wood"));
            Assert.AreEqual(0, _pool.Get("wood"));    // never goes negative
        }

        [Test]
        public void Consume_UnknownKey_ReturnsZeroWithoutThrowing()
        {
            Assert.DoesNotThrow(() => _pool.Consume("nothing"));
            Assert.AreEqual(0, _pool.Consume("nothing"));
        }

        [Test]
        public void Consume_RaisesOnChanged_WithNewValue()
        {
            string lastKey = null;
            int lastValue = -1;
            _pool.OnChanged += (key, value) => { lastKey = key; lastValue = value; };

            _pool.Set("wood", 2);
            Assert.AreEqual(("wood", 2), (lastKey, lastValue));   // Set raises too

            _pool.Consume("wood");
            Assert.AreEqual(("wood", 1), (lastKey, lastValue));
        }

        [Test]
        public void TotalRemaining_SumsAllKeys()
        {
            _pool.Set("wood", 2);
            _pool.Set("stone", 5);
            Assert.AreEqual(7, _pool.TotalRemaining());
        }

        [Test]
        public void CopyFrom_ReplacesContents()
        {
            _pool.Set("old", 9);
            _pool.CopyFrom(new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("wood", 3),
                new KeyValuePair<string, int>("stone", 1),
            });

            Assert.AreEqual(0, _pool.Get("old"));     // old contents are gone
            Assert.AreEqual(3, _pool.Get("wood"));
            Assert.AreEqual(4, _pool.TotalRemaining());
        }
    }
}

```

*نکته آموزشی — `[SetUp]`.* NUnit متد `[SetUp]` را پیش از **هر** `[Test]` اجرا می‌کند، بنابراین هر تست با یک مخزن خالی و تازه شروع می‌شود — تست‌ها هرگز به باقی‌مانده‌های یکدیگر وابسته نیستند. دستور بررسی تاپل `Assert.AreEqual(("wood", 2), (lastKey, lastValue))` هر دو فیلد رویداد را در یک خط مقایسه می‌کند (تاپل‌های C# دارای برابری داخلی هستند).

* \[ \] **مرحله ۲: اجرای تست‌ها.** انتظار می‌رود: **دقیقاً ۳ تست FAIL شوند** — `Get_UnknownKey_ReturnsZero` و `Consume_UnknownKey_ReturnsZeroWithoutThrowing` (ایندکسر `_counts[key]` در پیش‌نویس خطای `KeyNotFoundException` پرتاب می‌کند)، و `Has_MeansCountAboveZero` (`ContainsKey` در پیش‌نویس برای کلیدی که روی $0$ تنظیم شده `true` برمی‌گرداند). شش تست دیگر پاس می‌شوند — پیش‌نویس از قبل مصرف در حالت صفر و رویداد را مدیریت می‌کند.

* \[ \] **مرحله ۳: اصلاح `AmmoPool` — کمترین تغییر ممکن در سه متد**

```
public int Get(TKey key)
{
    return _counts.TryGetValue(key, out int count) ? count : 0;
}

// "has ammo" = the key exists AND the count is above zero
public bool Has(TKey key)
{
    return _counts.TryGetValue(key, out int count) && count > 0;
}

public int Consume(TKey key)
{
    if (!Has(key)) return 0;   // unknown key or already empty

    int newValue = --_counts[key];
    OnChanged?.Invoke(key, newValue);
    return newValue;
}

```

*خط به خط:*

* `TryGetValue(key, out int count)` روش ایمن جستجو در دیکشنری است: به جای پرتاب استثنا هنگام نبودن کلید، `false` برمی‌گرداند و در صورت موفقیت مقدار را از طریق متغیر `out` به شما می‌دهد. عملگر سه‌تایی `? count : 0` حالت «مفقود» را به `0` نگاشت می‌کند — یک نوع پرتابه ناشناخته به سادگی مهماتی ندارد.

* `Has` عبارت `&& count > 0` را به همان جستجو اضافه می‌کند — یک خط «وجود داشتن» را به «قابل استفاده بودن» تبدیل می‌کند.

* `Consume` اکنون با `Has` محافظت می‌شود، بنابراین ایندکسر `--_counts[key]` که پس از آن می‌آید هرگز به کلید مفقود برخورد نمی‌کند (ابتدا شرط محافظ) و هرگز زیر صفر نمی‌رود (`Has` در مقدار $0$ برابر `false` است).

* متدهای `Set`، `TotalRemaining`، `CopyFrom` و رویداد `OnChanged` دقیقاً همان‌طور که پیش‌نویس شده‌اند باقی می‌مانند.

**نکته آموزشی — انتخاب طراحی آگاهانه:** `Set` هر چه به آن بدهید ذخیره می‌کند، حتی عدد منفی — مخزن یک محفظه ساده و بی‌منطق (Dumb container) است. محدودسازی (`Mathf.Max(0, count)`) یک بار در زمان *ساخت* در `CatapultManager` (تسک 2.3) انجام می‌شود که تنها جایی است که اعداد Loadout وارد مخزن می‌شوند. یک بار محدودسازی در مرز ورودی بهتر از محدودسازی در تک‌تک متدهاست.

* \[ \] **مرحله ۴: اجرای مجدد تست‌ها.** انتظار می‌رود: `AmmoPoolTests` **9/9 PASS** (و `RingIndexTests` همچنان سبز باشد).

* \[ \] **مرحله ۵: Stage و پیشنهاد کامیت**

```
git add Assets/Scripts/Core/AmmoPool.cs Assets/Tests/EditMode/AmmoPoolTests.cs

```

پیام پیشنهادی: `fix(core): make AmmoPool.Has mean count>0, guard Get/Consume against unknown keys`

### تسک 1.3: `CameraPath` — محافظت در برابر مسیر خالی و اثبات ریاضیات

**فایل‌ها:**

* تغییر در: `Assets/Scripts/Core/CameraPath.cs` (فقط محافظ سازنده)

* تست: `Assets/Tests/EditMode/CameraPathTests.cs` (جدید)

**واسط‌ها:**

* تولید می‌کند: `Core.CatmullRom.Sample(IReadOnlyList<Vector3> controlPoints, int samplesPerSegment)` $\rightarrow$ `List<Vector3>` (از `controlPoints[0]` شروع و به `controlPoints[^1]` ختم می‌شود)؛ `Core.PolylinePath` با `new PolylinePath(IReadOnlyList<Vector3>)`، `float TotalLength`، `Vector3 Evaluate(float t)` — با سرعت ثابت و ایمن در برابر ورودی‌های $0$ یا $1$ نقطه‌ای. فاز 2.5 (`CatapultCameraRig`) از هر دو استفاده می‌کند.

**مشکل پیش‌نویس چیست.** سازنده `PolylinePath` بدون هیچ محافظی به `points[0]` دسترسی پیدا می‌کند — یک لیست خالی (یا null) باعث کرش می‌شود. `CatapultCameraRig` مسیرها را از روی `CameraAnchor.pathPoints` می‌سازد و یک نقطه اتکا بدون نقطه مسیر نباید بتواند دوربین را کرش دهد.

**نکته آموزشی — کارکرد این دو کلاس در یک نگاه.** `CatmullRom.Sample` تعدادی نقطه کنترل را به یک چندخطی (Polyline) متراکم تبدیل می‌کند (منحنی از *درون* تک‌تک نقاط عبور می‌کند؛ مماس در هر نقطه میانگین جهت همسایه‌های آن است). سپس `PolylinePath` هر چندخطی را به یک **ریل با سرعت ثابت** تبدیل می‌کند: مسافت تجمعی هر نمونه را از قبل اندازه می‌گیرد و `Evaluate(t)` به این سؤال پاسخ می‌دهد که «پس از طی کردن $t \times \text{TotalLength}$ واحد، کجا هستم؟» — بنابراین $t = 0.5$ همیشه به معنای *نصف مسافت* است، نه نصف پارامتر (پارامتر $t$ یکنواخت روی یک منحنی در قطعات بلند سرعت می‌گیرد و در قطعات کوتاه می‌خزد). پیوست فارسی در انتهای این سند تمام این موارد را از صفر استخراج کرده است.

* \[ \] **مرحله ۱: نوشتن تست‌های ناموفق**

```
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Core;

namespace Tests.EditMode
{
    public class CameraPathTests
    {
        // ---- CatmullRom.Sample -------------------------------------------------

        [Test]
        public void Sample_StartsAtFirstPoint_EndsAtLastPoint()
        {
            var points = new List<Vector3>
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 1, 0),
                new Vector3(2, 0, 0),
                new Vector3(3, 1, 0),
            };

            List<Vector3> samples = CatmullRom.Sample(points, 10);

            // (count - 1) segments × samplesPerSegment, plus the final point
            Assert.AreEqual((points.Count - 1) * 10 + 1, samples.Count);
            Assert.AreEqual(points[0], samples[0]);
            Assert.AreEqual(points[points.Count - 1], samples[samples.Count - 1]);
        }

        [Test]
        public void Sample_CollinearPoints_StaysOnTheLine()
        {
            // A straight line is the one case where the exact answer is known:
            // the spline must NOT overshoot it. 4 collinear points 1 unit apart,
            // 10 samples per segment → samples every 0.1 units from 0 to 3.
            var points = new List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(1, 0, 0),
                new Vector3(2, 0, 0), new Vector3(3, 0, 0),
            };

            List<Vector3> samples = CatmullRom.Sample(points, 10);

            Assert.AreEqual(new Vector3(1.5f, 0, 0), samples[15], "midpoint drifted off the line");
            Assert.AreEqual(0.1f, samples[10].x, 1e-3f);   // first sample of segment 2
        }

        [Test]
        public void Sample_FewerThanTwoPoints_ReturnsEmpty()
        {
            Assert.AreEqual(0, CatmullRom.Sample(new List<Vector3>(), 10).Count);
            Assert.AreEqual(0, CatmullRom.Sample(new List<Vector3> { Vector3.zero }, 10).Count);
        }

        // ---- PolylinePath ------------------------------------------------------

        [Test]
        public void StraightPath_EndpointsAreExact()
        {
            var path = new PolylinePath(new List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(10, 0, 0),
            });

            Assert.AreEqual(10f, path.TotalLength, 1e-4f);
            Assert.AreEqual(new Vector3(0, 0, 0), path.Evaluate(0f));
            Assert.AreEqual(new Vector3(10, 0, 0), path.Evaluate(1f));
        }

        [Test]
        public void StraightPath_MidpointIsHalfTheDistance()
        {
            // The arc-length promise: t = 0.5 means "half the DISTANCE travelled",
            // which on a straight 10-unit path is exactly x = 5.
            var path = new PolylinePath(new List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(10, 0, 0),
            });

            Assert.AreEqual(5f, path.Evaluate(0.5f).x, 1e-4f);
        }

        [Test]
        public void BentPath_MidpointIsHalfTheTotalDistance()
        {
            // Two legs: A→B is 3 units, B→C is 4 units. Half of 7 units = 3.5,
            // which lands 0.5 units past B along the second leg.
            var path = new PolylinePath(new List<Vector3>
            {
                new Vector3(0, 0, 0), new Vector3(3, 0, 0), new Vector3(3, 4, 0),
            });

            Vector3 mid = path.Evaluate(0.5f);
            Assert.AreEqual(3f, mid.x, 1e-4f);
            Assert.AreEqual(0.5f, mid.y, 1e-4f);
        }

        [Test]
        public void EmptyOrSinglePoint_DoesNotThrow()
        {
            // An anchor with no path points must not be able to crash the camera.
            var empty = new PolylinePath(new Vector3[0]);
            Assert.AreEqual(0f, empty.TotalLength);
            Assert.AreEqual(Vector3.zero, empty.Evaluate(0.5f));

            var single = new PolylinePath(new[] { new Vector3(2, 3, 4) });
            Assert.AreEqual(0f, single.TotalLength);
            Assert.AreEqual(new Vector3(2, 3, 4), single.Evaluate(0.5f));
        }
    }
}

```

*چرا تست مسیر خمیده (Bent-path) اهمیت دارد:* این تنها تستی است که می‌تواند باگ طول کمان (Arc-length) را که یک خط مستقیم قادر به آشکار کردنش نیست شکار کند — روی یک مسیر مستقیم، $t$ یکنواخت و مسافت یکنواخت با هم هم‌خوانی دارند. بازوهای $3+4$ باعث می‌شوند `Evaluate(0.5f)` در قطعه *دوم* و در نقطه‌ای فرود بیاید که جز با ریاضیات صحیح مسافت تجمعی به دست نمی‌آید.

* \[ \] **مرحله ۲: اجرای تست‌ها.** انتظار می‌رود: `EmptyOrSinglePoint_DoesNotThrow` **FAIL شود** (`IndexOutOfRangeException` در سازنده). تست‌های ریاضی باید از قبل پاس شوند — پیاده‌سازی پیش‌نویس درست است.

* \[ \] **مرحله ۳: محافظت از سازنده** — بالای سازنده `PolylinePath` را جایگزین کنید:

```
public PolylinePath(IReadOnlyList<Vector3> points)
{
    int count = points?.Count ?? 0;
    if (count == 0)
    {
        // Degenerate path: stand still. Evaluate() returns _points[0] because TotalLength is 0.
        _points = new[] { Vector3.zero };
        _cumDist = new float[] { 0f };
        TotalLength = 0f;
        return;
    }

    _points = new Vector3[count];
    _cumDist = new float[count];
    // ... rest of the constructor unchanged

```

*خط به خط:*

* `points?.Count ?? 0` — عملگر شرطی null (`?.`) برای یک لیست null مقدار `null` برمی‌گرداند و `??` (عملگر ادغام null) مقدار `0` را جایگزین می‌کند. یک خط هم «نبود لیست» و هم «لیست خالی» را مدیریت می‌کند.

* شاخه تبهگن (Degenerate) یک مسیر تک‌نقطه‌ای معتبر در مبدأ می‌سازد، بنابراین `Evaluate` (که از قبل وقتی `TotalLength <= 0` باشد زود خارج می‌شود) بدون هیچ شرط خاص دیگری `Vector3.zero` را برمی‌گرداند.

* \[ \] **مرحله ۴: اجرای تست‌ها.** انتظار می‌رود: `CameraPathTests` **8/8 PASS** (در مرحله ۲ فقط `EmptyOrSinglePoint_DoesNotThrow` شکست خورد — ریاضیات پیش‌نویس از قبل درست بود).

* \[ \] **مرحله ۵: Stage و پیشنهاد کامیت**

```
git add Assets/Scripts/Core/CameraPath.cs Assets/Tests/EditMode/CameraPathTests.cs

```

پیام پیشنهادی: `fix(core): guard PolylinePath against empty paths, add camera path tests`

## فاز ۲ — کامپوننت‌های زمان اجرای جدید (New runtime components)

همه این‌ها در `Assembly-CSharp` (بدون asmdef) زیر پوشه `Assets/Scripts/Catapult/` (پوشه جدید) و `Assets/Scripts/Camera/` (پوشه جدید) قرار می‌گیرند. این قانون را به خاطر بسپارید: **فایل `.cs` را در ادیتور بسازید** (کلیک راست در پنجره Project ▸ Create ▸ C# Script) تا یونیتی فایل `.meta` را با GUID آن تولید کند — هرگز فایل‌های اسکریپت را خارج از یونیتی نسازید.

### تسک 2.1: `SO_LevelData.ammoMode` — کلید انتخاب به‌ازای هر مرحله

**فایل‌ها:**

* تغییر در: `Assets/Scripts/ScriptableObjects/Level/SO_LevelData.cs`

**واسط‌ها:**

* تولید می‌کند: `enum AmmoMode { Shared, PerCatapult }` و `SO_LevelData.ammoMode` (پیش‌فرض `Shared`). تسک 2.3 آن را می‌خواند تا تصمیم بگیرد چند مخزن بسازد.

**نکته آموزشی — enumها در ScriptableObjectها.** یونیتی یک فیلد enum را به عنوان عدد صحیح سریال‌سازی می‌کند و آن را به صورت یک منوی کشویی (Dropdown) در Inspector نمایش می‌دهد، بنابراین طراحان بدون نیاز به کد UI اضافه، یک انتخاب خوانا در اختیار دارند. از آنجا که `Shared` اولین مقدار (`0`) است، با اضافه شدن این فیلد تمام دارایی‌های (Assets) موجود مقدار `Shared` را حفظ می‌کنند — مراحل قدیمی به صورت پنهانی تغییر رفتار نمی‌دهند. همچنین `Shared` *همان* رفتار فعلی است: یک منجنیق، یک مخزن. قرار دادن `PerCatapult` به عنوان پیش‌فرض، مهمات تمام مراحل موجود را به صورت پنهانی دو برابر می‌کرد.

* \[ \] **مرحله ۱: اضافه کردن enum و فیلد**

```
using System;
using System.Collections.Generic;
using UnityEngine;

public enum AmmoMode { Shared, PerCatapult }

[CreateAssetMenu(fileName = "LevelData", menuName = "Scriptable Objects/Level Data")]
public class SO_LevelData : ScriptableObject
{
    public List<ProjectileLoadout> projectileLoadouts = new List<ProjectileLoadout>();

    [Tooltip("Shared = all catapults fire from one pool. PerCatapult = each catapult gets a full copy of the loadout.")]
    public AmmoMode ammoMode = AmmoMode.Shared;
}

[Serializable]
public class ProjectileLoadout
{
    public SO_ProjectileType projectileType;
    public int ammoCount;
}

```

(فقط دو خط مشخص‌شده جدید هستند؛ `ProjectileLoadout` دست‌نخورده است.)

* \[ \] **مرحله ۲: بررسی در Inspector.** دارایی `DefaultLevelData` را انتخاب کنید — یک منوی کشویی جدید **Ammo Mode** با مقدار `Shared` باید ظاهر شود. هیچ دارایی دیگری تغییر نمی‌کند.

* \[ \] **مرحله ۳: Stage و پیشنهاد کامیت**

```
git add Assets/Scripts/ScriptableObjects/Level/SO_LevelData.cs

```

پیام پیشنهادی: `feat(level): add AmmoMode switch to SO_LevelData`

### تسک 2.2: `Catapult` — نمای (Facade) به‌ازای هر منجنیق

**فایل‌ها:**

* ایجاد: `Assets/Scripts/Catapult/Catapult.cs`

**واسط‌ها:**

* مصرف می‌کند: فعلاً هیچ‌چیز (فیلدها در فاز ۵ سیم‌کشی می‌شوند).

* تولید می‌کند: `Catapult` با فیلدهای سریال‌سازی‌شده `launcher, spawner, ammoManager, cameraAnchor, highlightObject`؛ ویژگی‌های `int Index { get; internal set; }`، `bool IsActive { get; internal set; }`، `Vector3 RestPosition { get; }`، `Quaternion RestRotation { get; }`؛ متدهای `void CaptureRestPose()`، `void SetHighlight(bool)`. تسک 2.3 (منیجر)، 2.4/2.5 (دوربین) و 3.5 (کاروسل) از این‌ها استفاده می‌کنند.

**نکته آموزشی — «نمای» (Facade) چیست.** Facade یک شیء کوچک است که درخواست‌ها را به اشیاء واقعی پشت سر خود *هدایت* می‌کند. `Catapult` هیچ رفتار مستقلی از خود ندارد؛ تنها جایی است که کد بازی می‌تواند بپرسد «لانچر *این* منجنیق را به من بده» بدون اینکه از چیدمان صحنه یا نام کامپوننت‌ها اطلاعی داشته باشد. این کار مانع از آن می‌شود که `InputController` و UI با `FindObjectOfType` در سلسله‌مراتب صحنه جستجو کنند.

**نکته آموزشی — `internal set`.** این ویژگی در تمام اسمبلی قابل خواندن است اما فقط از داخل همان اسمبلی قابل نوشتن است — روشی که `CatapultManager` مقادیر `Index`/`IsActive` را تنظیم می‌کند بدون اینکه اجازه دهد کدهای متفرقه بازی رجیستری را خراب کنند.

* \[ \] **مرحله ۱: ایجاد اسکریپت**

```
using UnityEngine;

// Per-catapult facade: one place to ask for this catapult's launcher/spawner/ammo/anchor.
// Owns no behaviour; CatapultManager drives it.
public class Catapult : MonoBehaviour
{
    public ProjectileLuncher launcher;
    public ProjectileSpawner spawner;
    public AmmoManager ammoManager;
    public CameraAnchor cameraAnchor;
    public GameObject highlightObject;   // optional visual; toggled on/off

    public int Index { get; internal set; }
    public bool IsActive { get; internal set; }

    // The authored (un-aimed) pose. The camera anchor is evaluated against this frame,
    // so the camera's destination is stable even while the body yaws during aiming.
    public Vector3 RestPosition { get; private set; }
    public Quaternion RestRotation { get; private set; }

    // Called by CatapultManager.Awake — deliberately NOT Awake() on this component.
    // See "The lifecycle-ordering trap" in the plan for why the manager must call this.
    public void CaptureRestPose()
    {
        RestPosition = transform.position;
        RestRotation = transform.rotation;
    }

    public void SetHighlight(bool on)
    {
        if (highlightObject) highlightObject.SetActive(on);
    }
}

```

* \[ \] **مرحله ۲: Stage و پیشنهاد کامیت** (تایپ فیلد `cameraAnchor` در تسک 2.4 ساخته می‌شود، بنابراین این تسک به تنهایی خطای قرمز نشان می‌دهد — طبیعی است؛ پیش از کامپایل، 2.4 را بنویسید.)

```
git add Assets/Scripts/Catapult/Catapult.cs Assets/Scripts/Catapult/Catapult.cs.meta

```

پیام پیشنهادی: `feat(catapult): add Catapult facade component`

**چرا `CaptureRestPose` متدی است که منیجر صدا می‌زند، نه `Awake` خود این کامپوننت:** یونیتی `Awake` را روی هر شیء قبل از هر `Start` صدا می‌زند، اما *ترتیب نسبی اجرای دو `Awake` نامشخص است*. اگر `Catapult.Awake` وضعیت را ثبت می‌کرد و `Awake` منیجر اتفاقاً زودتر اجرا می‌شد، منیجر مقدار `RestPosition == (0,0,0)` را می‌خواند و دوربین را به مبدأ جهان پرتاب می‌کرد — باگی که فقط در برخی از اجراهای Play ظاهر می‌شود. فراخوانی صریح `CaptureRestPose()` توسط منیجر (پیش از خواندن مقادیر) مستقل از ترتیب است: هر کسی که به مقدار نیاز دارد ابتدا آن را درخواست می‌کند، و هنوز چیزی حرکت نکرده است چون هیچ کد گیم‌پلی پیش از `Start` اجرا نمی‌شود.

### تسک 2.3: `CatapultManager` — مغز مرحله

**فایل‌ها:**

* ایجاد: `Assets/Scripts/Catapult/CatapultManager.cs`

**واسط‌ها:**

* مصرف می‌کند: `Catapult` (تسک 2.2)، `Core.AmmoPool<SO_ProjectileType>` (تسک 1.2)، `RingIndex` (تسک 1.1)، `SO_LevelData.ammoMode` (تسک 2.1)، `ProjectileSelector.Initialize` موجود.

* تولید می‌کند: `CatapultManager` با فیلدهای `SO_LevelData levelData`، `ProjectileSelector selector`، `CatapultCameraRig cameraRig`؛ رویداد `event Action<Catapult> OnActiveCatapultChanged`؛ ویژگی‌های `Catapult ActiveCatapult { get; }`، `IReadOnlyList<Catapult> Catapults { get; }`؛ متدهای `void SetActive(Catapult)`، `void NextCatapult()`، `void PreviousCatapult()`. تسک‌های 2.4 (دوربین)، 3.4 (لانچر)، 3.5 (ورودی)، 3.6 (کاروسل) و 4.3 (UI جابه‌جاگر) از این‌ها استفاده می‌کنند.

**نکته آموزشی — `event Action<Catapult>`.** این الگوی انتشار-اشتراک (Pub-sub) موجود در پروژه است (ببینید: `ProjectileSelector.OnSelectionChanged`، `AmmoManager.OnAmmoChanged`). منیجر نمی‌داند که کاروسل یا ریگ دوربینی وجود دارد؛ فقط اعلام می‌کند «منجنیق فعال تغییر کرد» و مشترکین واکنش نشان می‌دهند. این *وابستگی سست (Loose coupling)* دلیل آن است که UI و دوربین هرگز به ارجاعات مستقیم به اجزای داخلی منیجر نیاز ندارند.

* \[ \] **مرحله ۱: ایجاد اسکریپت**

```
using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

// Level brain: finds the catapults, owns the ONE global type list, builds the ammo
// pool(s) according to SO_LevelData.ammoMode, and tracks which catapult is active.
public class CatapultManager : MonoBehaviour
{
    public SO_LevelData levelData;
    public ProjectileSelector selector;          // the ONE global selector
    public CatapultCameraRig cameraRig;          // optional in code; assign in the scene

    public event Action<Catapult> OnActiveCatapultChanged;

    public Catapult ActiveCatapult { get; private set; }
    public IReadOnlyList<Catapult> Catapults => _catapults;

    private readonly List<Catapult> _catapults = new List<Catapult>();

    private void Awake()
    {
        FindCatapults();

        if (_catapults.Count == 0)
        {
            Debug.LogError("CatapultManager: no Catapult components in the scene. Arrows and aiming are disabled.", this);
            return;
        }

        List<SO_ProjectileType> types = BuildTypeList();   // the ONE place the type list is built

        List<AmmoPool<SO_ProjectileType>> pools = BuildPools(types);
        AttachPools(pools);

        selector.Initialize(types);

        SetActive(_catapults[0]);   // first activation snaps the rig (no glide on frame one)
    }

    private void FindCatapults()
    {
        _catapults.AddRange(FindObjectsByType<Catapult>(FindObjectsSortMode.None));
        for (int i = 0; i < _catapults.Count; i++)
        {
            Catapult catapult = _catapults[i];
            catapult.Index = i;
            catapult.CaptureRestPose();          // BEFORE anything reads RestPosition/RestRotation
            catapult.SetHighlight(false);
        }
    }

    // Loadout wins when non-empty; otherwise fall back to the selector's pre-authored
    // availableTypes + each type's own ammoCount — exactly what AmmoManager.Awake did pre-refactor.
    private List<SO_ProjectileType> BuildTypeList()
    {
        List<SO_ProjectileType> types = new List<SO_ProjectileType>();

        if (levelData && levelData.projectileLoadouts.Count > 0)
        {
            foreach (ProjectileLoadout entry in levelData.projectileLoadouts)
                if (entry.projectileType) types.Add(entry.projectileType);
        }
        else
        {
            types.AddRange(selector.availableTypes);
        }

        return types;
    }

    private List<AmmoPool<SO_ProjectileType>> BuildPools(List<SO_ProjectileType> types)
    {
        // The loadout numbers (or each type's own ammoCount when there is no loadout).
        int CountFor(SO_ProjectileType type)
        {
            if (levelData && levelData.projectileLoadouts.Count > 0)
            {
                foreach (ProjectileLoadout entry in levelData.projectileLoadouts)
                    if (entry.projectileType == type) return Mathf.Max(0, entry.ammoCount);
                return 0;
            }
            return Mathf.Max(0, type.ammoCount);
        }

        var pools = new List<AmmoPool<SO_ProjectileType>>();

        if (!levelData || levelData.ammoMode == AmmoMode.Shared)
        {
            var shared = new AmmoPool<SO_ProjectileType>();
            foreach (SO_ProjectileType type in types) shared.Set(type, CountFor(type));
            pools.Add(shared);                                     // ONE pool...
        }
        else
        {
            for (int i = 0; i < _catapults.Count; i++)
            {
                var pool = new AmmoPool<SO_ProjectileType>();
                foreach (SO_ProjectileType type in types) pool.Set(type, CountFor(type));
                pools.Add(pool);                                   // ...or one per catapult
            }
        }

        return pools;
    }

    private void AttachPools(List<AmmoPool<SO_ProjectileType>> pools)
    {
        for (int i = 0; i < _catapults.Count; i++)
        {
            // Shared mode: everyone indexes [0] and shares the same pool object.
            _catapults[i].ammoManager.AttachPool(pools[i % pools.Count]);
        }
    }

    public void SetActive(Catapult catapult)
    {
        if (catapult == null || catapult == ActiveCatapult) return;

        bool firstActivation = ActiveCatapult == null;

        if (ActiveCatapult)
        {
            ActiveCatapult.IsActive = false;
            ActiveCatapult.SetHighlight(false);
        }

        ActiveCatapult = catapult;
        catapult.IsActive = true;
        catapult.SetHighlight(true);

        // Only the active catapult loads a projectile on demand — inactive ones may have
        // an empty pool for this type (PerCatapult mode) and must not pre-load.
        if (catapult.launcher) catapult.launcher.LoadProjectile();

        OnActiveCatapultChanged?.Invoke(catapult);

        if (!cameraRig) return;
        if (firstActivation) cameraRig.SnapTo(catapult);   // the camera should just BE there at level start
        else cameraRig.MoveTo(catapult);
    }

    // Wrap-around is deliberate: from the last catapult, next goes to the first.
    public void NextCatapult()
    {
        if (ActiveCatapult == null) return;
        SetActive(_catapults[RingIndex.Next(ActiveCatapult.Index, _catapults.Count, 1)]);
    }

    public void PreviousCatapult()
    {
        if (ActiveCatapult == null) return;
        SetActive(_catapults[RingIndex.Next(ActiveCatapult.Index, _catapults.Count, -1)]);
    }
}

```

* \[ \] **مرحله ۲: Stage و پیشنهاد کامیت** (این تسک تا زمانی که `CatapultCameraRig` در تسک 2.5 و `AttachPool`/`LoadProjectile` در تسک 3.1 ایجاد نشوند کامپایل نمی‌شود — یا آن تسک‌ها را در همین نشست بنویسید، یا خطاهای قرمز را بپذیرید و همه‌چیز را پس از تسک 2.5 با هم کامیت کنید.)

**توضیحات خط به خط بخش‌های مهم:**

*`FindObjectsByType<Catapult>(FindObjectsSortMode.None)`* — جایگزین یونیتی ۶ برای متد منسوخ‌شده `FindObjectsOfType`. آرگومان `FindObjectsSortMode` مرتب‌سازی را کنترل می‌کند (`None` کم‌هزینه‌ترین حالت است). *ترتیب آرایه تضمین‌شده نیست* — «منجنیق 0» لزوماً سمت چپ‌ترین منجنیق نیست. این برنامه از آرایه به همان شکل استفاده می‌کند و *فلش‌ها* در آن جابه‌جا می‌شوند، بنابراین ترتیب فقط روی اینکه کدام منجنیق در ابتدا فعال است تأثیر می‌گذارد. اگر ترتیب قطعی می‌خواهید، پس از یافتن، بر اساس `transform.position.x` مرتب‌سازی کنید.

*`BuildTypeList` تنها جایی است که لیست انواع پرتابه ساخته می‌شود.* فراخوانی قدیمی `AmmoManager.Awake()` به `projectileSelector.Initialize(types)` حذف می‌شود (تسک 3.1). **نکته آموزشی — ترتیب فراخوانی‌های `Awake` تضمین‌شده نیست:** یونیتی تمام `Awake`ها را پیش از هر `Start` اجرا می‌کند، اما ترتیب نسبی بین `Awake` دو شیء نامشخص است، بنابراین هیچ‌چیز نباید به «ابتدا `Awake` شیء X اجرا شد» وابسته باشد. داده‌ها را در یک جا بسازید و به بقیه تحویل دهید — تمام وظیفه این کلاس همین است.

*`CountFor` یک **تابع محلی (Local function)** است.* متدی که درون متد دیگری تعریف می‌شود — `levelData` را مستقیماً دریافت (Capture) می‌کند به جای اینکه به عنوان پارامتر پاس دهد، و سطح عمومی (Public surface) کلاس را شلوغ نمی‌کند. C# از نسخه ۷.۰ این قابلیت را دارد و یونیتی از آن پشتیبانی می‌کند.

*چرا مخزن یک شیء مجزاست و نه یک فیلد درون `AmmoManager`:* اگر مخزن درون `AmmoManager` زندگی می‌کرد، «اشتراکی» به این معنا بود که «تمام منجنیق‌ها به یک کامپوننت `AmmoManager` مشترک اشاره کنند» — یعنی *سیم‌کشی صحنه متفاوت* به‌ازای هر حالت که در زمان اجرا قابل تصمیم‌گیری نیست. با قرار دادن مخزن به عنوان یک شیء ساده، سیم‌کشی صحنه **در هر دو حالت یکسان است** (هر منجنیق همیشه `AmmoManager` خودش را دارد) و فقط انتساب در زمان اجرا متفاوت است. عبارت `pools[i % pools.Count]` این موضوع را در یک خط بیان می‌کند: در حالت اشتراکی `pools.Count == 1` است، پس همه ایندکس‌ها به مخزن $0$ نگاشت می‌شوند؛ در حالت به‌ازای هر منجنیق `pools.Count == _catapults.Count` است، پس هر منجنیق مخزن خودش را می‌گیرد.

*چرا `SetActive` پیش از انتشار رویداد، `LoadProjectile()` را صدا می‌زند:* کنترل‌کننده `OnActiveCatapultChanged` کاروسل، تعداد مهمات را دوباره از `ActiveCatapult.ammoManager` می‌خواند؛ بارگذاری لانچر مستقل از آن است. ترتیب بین این دو اهمیتی ندارد — اما فراخوانی ابتدا `LoadProjectile` به این معناست که اگر هر شنونده رویدادی پرتابه بارگذاری‌شده را بررسی کند، از قبل آنجا آماده است.

*موارد مرزی (Edge cases) که صریحاً مدیریت شده‌اند:*

* **صفر منجنیق** $\rightarrow$ ثبت یک خطای واضح و خروج از `Awake`. `ActiveCatapult` null باقی می‌ماند؛ `NextCatapult`/`PreviousCatapult` روی آن شرط محافظ دارند، پس هیچ `NullReferenceException` در `Update` رخ نمی‌دهد.

* **منجنیقی با `ammoManager` تخصیص‌داده‌نشده** $\rightarrow$ `AttachPools` در میانه راه‌اندازی `NullReferenceException` می‌داد و مرحله را نیمه‌کاره رها می‌کرد. به جای آن، بلافاصله پس از یافتن منجنیق‌ها با صدای بلند خطا دهید: `Debug.LogError($"Catapult '{catapult.name}' has no AmmoManager assigned.", catapult); return;` درون حلقه `FindCatapults` (پیش از اینکه `CaptureRestPose` ارزشی داشته باشد).

* **منجنیقی بدون `CameraAnchor`** $\rightarrow$ در تسک 2.5 مدیریت می‌شود (ریگ سر جای خود می‌ماند و هشدار می‌دهد).

* **`levelData` برابر null** $\rightarrow$ به `availableTypes` + `ammoCount` از پیش تعریف‌شده در سلکتور (رفتار فعلی) و یک مخزن مشترک بازمی‌گردد.

* **ورودی Loadout با نوع مفقود** $\rightarrow$ در `BuildTypeList` نادیده گرفته می‌شود (`if (entry.projectileType)`), مشابه `InitializeAmmo` فعلی.

* **نوعی بدون `prefab`** $\rightarrow$ همچنان لیست و شمارش می‌شود، اما `ProjectileSpawner.InstantiateBullet` از قبل `null` برمی‌گرداند؛ تسک‌های 3.2/3.3 آن را از نظر null ایمن نگه می‌دارند.

## تله ترتیب چرخه حیات (پیش از نوشتن هر کدی این بخش را بخوانید)

این ساده‌ترین چیزی است که در این ویژگی می‌تواند اشتباه پیش برود و تا زمانی که با علائمی مثل «گاهی منجنیق اشتباهی بارگذاری می‌شود» یا «دوربین به مبدأ پرید» ظاهر نشود، نامرئی است.

**آنچه یونیتی تضمین می‌کند:**

* برای یک شیء *واحد*: `Awake` $\rightarrow$ `OnEnable` $\rightarrow$ `Start` $\rightarrow$ `Update`.

* در میان *همه* اشیاء: **تمام `Awake`ها پیش از هر `Start` اجرا می‌شوند.**

* در میان همه اشیاء: **ترتیب نسبی اجرای دو `Awake` نامشخص است.** همین‌طور برای دو `Start` و دو `OnEnable`.

**سه قانون قطعی برای این ویژگی:**

1. **تمام راه‌اندازی‌ها در `CatapultManager.Awake` انجام می‌شود و هر چیزی که به آن راه‌اندازی *نیاز* دارد در `Start` یا بعد از آن اجرا می‌شود.** چون تمام `Awake`ها پیش از تمام `Start`ها هستند، `Start` لانچر *تضمین‌شده* است که پس از اتصال مخازن و مقداردهی اولیه سلکتور توسط `Awake` منیجر اجرا شود. ✅

2. **وضعیت سکون (Rest pose) توسط منیجر ثبت می‌شود، نه توسط `Catapult.Awake`.** اگر `Catapult.Awake` آن را ثبت می‌کرد و `Awake` منیجر زودتر اجرا می‌شد، منیجر مقدار `RestPosition == (0,0,0)` را می‌خواند و دوربین را به مبدأ پرتاب می‌کرد — در *برخی* از اجراهای Play، نه همه آن‌ها، که بدترین نوع باگ است. از این رو متد صریح `CaptureRestPose()` از تسک 2.2 وجود دارد.

3. **برای انتخاب *اولیه* به `OnSelectionChanged` تکیه نکنید.** `selector.Initialize(types)` این رویداد را منتشر می‌کند، اما لانچری که هنوز `OnEnable` خود را اجرا نکرده است هرگز آن را نمی‌شنود. به همین دلیل است که `ProjectileLuncher.Start` متد `LoadProjectile()` را صریحاً صدا می‌زند (تسک 3.4). رویدادها برای *تغییرات* هستند، نه برای وضعیت اولیه.

**یک چروک بی‌خطر:** اولین پرتابه ممکن است دو بار ساخته شود — یک بار توسط `HandleSelectionChanged` (اگر لانچر پیش از مقداردهی اولیه سلکتور توسط منیجر فعال شده باشد) و یک بار توسط `Start`. متد `InstantiateBulletSafe` قبلی را نابود می‌کند، بنابراین نتیجه درست است؛ هزینه آن فقط یک `Instantiate` اضافی است. اگر این موضوع آزارتان داد، یک پرچم `_hasLoaded` اضافه کنید. **هرگز** سعی نکنید با تغییر ترتیب فراخوانی‌های `Awake` آن را «اصلاح» کنید — نمی‌توانید.

**نکته آموزشی — چگونه به جای حدس زدن، ترتیب را *ببینیم*.** یک خط موقت `Debug.Log($"{name}.Awake");` به `CatapultManager`، `AmmoManager` و `ProjectileLuncher` اضافه کنید و در اولین فریم Play پنجره Console را تماشا کنید. این کار را یک بار انجام دهید — همین تله در هر پروژه یونیتی دیگری گریبان‌گیرتان خواهد شد.

### تسک 2.4: `CameraAnchor` — جایی که دوربین باید باشد

**فایل‌ها:**

* ایجاد: `Assets/Scripts/Camera/CameraAnchor.cs`

**واسط‌ها:**

* تولید می‌کند: `CameraAnchor` (MonoBehaviour) با `List<Transform> pathPoints`؛ ترنسفورم خود این شیء *همان* وضعیت مطلوب دوربین است. تسک‌های 2.5 و 2.6 از آن استفاده می‌کنند.

```
using System.Collections.Generic;
using UnityEngine;

// Child of a catapult root. Its local transform IS the desired camera pose.
// pathPoints (optional) are children of the SAME catapult root: 2+ of them makes
// the camera glide along a Catmull-Rom path; 0–1 makes it glide straight.
public class CameraAnchor : MonoBehaviour
{
    public List<Transform> pathPoints = new List<Transform>();
}

```

نقطه اتکا هیچ فیلد آفستی ندارد — آن را در نمای Scene مانند هر شیء دیگری قرار می‌دهید و `localPosition`/`localRotation` آن نسبت به ریشه منجنیق *همان* اعداد هستند.

**تنها قانون برای هر چیزی که به دوربین مربوط است:** نقطه اتکا و نقاط مسیر (Waypoints) آن همگی **فرزندان ریشه منجنیق** هستند و همگی در قاب **سکون** (نشانه نگرفته) منجنیق تفسیر می‌شوند.

**نکته آموزشی — وضعیت جهانی (World pose) در برابر وضعیت محلی (Local pose).** فرزند یک منجنیق چرخش آن را به ارث می‌برد، بنابراین وضعیت *جهانی* نقطه اتکا هنگام چرخش منجنیق برای نشانه گرفتن تغییر می‌کند — که این امر مقصد دوربین را به اطراف می‌کشاند. برای ثابت نگه داشتن مقصد، `Catapult` وضعیت سکون خود را ثبت کرده است (تسک 2.2) و ریگ نقطه اتکا را نسبت به آن قاب منجمد ارزیابی می‌کند:

```
worldPos  = RestPosition + RestRotation * anchor.localPosition
worldRot  = RestRotation * anchor.localRotation
waypoint  = RestPosition + RestRotation * waypoint.localPosition

```

**نکته آموزشی — چرا از `Transform.TransformPoint` استفاده نمی‌کنیم؟** `TransformPoint` تبدیل محلی $\rightarrow$ جهانی را با استفاده از چرخش *فعلی* ترنسفورم انجام می‌دهد — و «فعلی» دقیقاً همان چیزی است که می‌خواهیم منجمدش کنیم. انجام ضرب به صورت دستی با چرخش سکون ثبت‌شده فقط یک خط اضافه است و مقصدی می‌دهد که هرگز منحرف نمی‌شود. (مزیت جانبی: چون نقاط مسیر فرزندان *ریشه* منجنیق هستند و نه نقطه اتکا، `localPosition` آن‌ها از قبل در همان قاب نقطه اتکا قرار دارد — بدون نیاز به ریاضیات تبدیل تو در تو.)

* \[ \] **مرحله ۱: ایجاد اسکریپت.** رفتاری برای تست وجود ندارد — کلاس فقط دو فیلد دارد؛ درستی عملکرد در ریگ تسک 2.5 مشخص می‌شود.

* \[ \] **مرحله ۲: Stage و پیشنهاد کامیت**

```
git add Assets/Scripts/Camera/CameraAnchor.cs Assets/Scripts/Camera/CameraAnchor.cs.meta

```

پیام پیشنهادی: `feat(camera): add CameraAnchor component`

### تسک 2.5: `CatapultCameraRig` — جابه‌جاکننده دوربین

**فایل‌ها:**

* ایجاد: `Assets/Scripts/Camera/CatapultCameraRig.cs`

**واسط‌ها:**

* مصرف می‌کند: `Catapult` + `CameraAnchor` (تسک‌های 2.2/2.4)، `Core.CatmullRom.Sample` + `Core.PolylinePath` (تسک 1.3).

* تولید می‌کند: `CatapultCameraRig` با فیلدهای `[Range] float followSharpness = 3f`، `float moveDuration`، `AnimationCurve ease`؛ متدهای `void SnapTo(Catapult)` (فوری) و `void MoveTo(Catapult)` (لغزش انیمیشنی، سپس دنبال کردن). `CatapultManager` (2.3) از قبل هر دو را صدا می‌زند؛ رفتار دنبال کردن نشانه در تسک 2.6 اضافه می‌شود.

**کارکرد آن.** GameObject ریگ، دوربین را به عنوان فرزند در مختصات محلی `(0,0,0)` در اختیار دارد — بنابراین *ترنسفورم خود ریگ همان وضعیت جهانی دوربین است*. `MoveTo` این ترنسفورم را به سمت وضعیت نقطه اتکای منجنیق فعال می‌لغزاند: به صورت مستقیم، یا در امتداد یک مسیر Catmull-Rom ساخته‌شده از `[startPos, …waypoints, endPos]`.

**نکته آموزشی — حرکت مستقیم همان مسیر کد است.** یک حرکت مستقیم از A به B صرفاً یک `PolylinePath` با دو نقطه است. بنابراین ریگ دقیقاً یک مسیر کد دارد: *یک مسیر بساز، سپس روی آن قدم بزن*. اگر `pathPoints.Count >= 2` باشد $\rightarrow$ Catmull-Rom روی نقاط مسیر؛ هر چیزی کمتر از آن $\rightarrow$ مسیر مستقیم دو نقطه‌ای. بدون هیچ شاخه‌بندی شرطی درون خود انیمیشن (Tween).

**نکته آموزشی — چرا Coroutine و نه `async/await`.** `async/await` در یونیتی به طور پیش‌فرض روی یک نخ از استخر نخ‌ها (Thread-pool) ادامه می‌یابد و فراخوانی‌های API یونیتی ایمن در برابر نخ (Thread-safe) نیستند. یک Coroutine (`IEnumerator` + `yield return null`) همیشه در فریم بعدی روی **نخ اصلی (Main thread)** ادامه می‌یابد — تنها جایی که مجاز به دست زدن به `transform` هستید. این الگوی استاندارد یونیتی برای انیمیشن‌های زمان‌بندی‌شده است؛ این پروژه از قبل در `ProjectileLuncher.ResetHeadRotationRoutine` و `ProjectileCarouselUI.TweenButtons` از آن استفاده می‌کند.

**نکته آموزشی — `Quaternion.Slerp`.** درون‌یابی چرخش‌ها با `Vector3.Lerp` اشتباه است: زوایای اویلر در $360^\circ$ دور می‌زنند و کوتاه‌ترین کمان را طی نمی‌کنند. `Quaternion.Slerp` (درون‌یابی خطی کروی) در امتداد کوتاه‌ترین کمان با سرعت زاویه‌ای ثابت می‌چرخد. پروژه از قبل در `ResetHeadRotationRoutine` از آن استفاده می‌کند. چرخش عمداً از مماس مسیر گرفته *نمی‌شود* — نگاه کردن در امتداد منحنی وقتی مسیر به عقب برمی‌گردد خراب می‌شود، در حالی که `Slerp` از شروع به پایان قابل پیش‌بینی است.

* \[ \] **مرحله ۱: ایجاد اسکریپت**

```
using System.Collections;
using System.Collections.Generic;
using Core;
using UnityEngine;

// Top-level camera owner. The camera is a child at local (0,0,0), so this
// transform IS the camera's world pose. Glides between per-catapult anchors;
// straight when the anchor has < 2 path points, Catmull-Rom otherwise.
public class CatapultCameraRig : MonoBehaviour
{
    public float moveDuration = 1.2f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Range(0.1f, 20f)] public float followSharpness = 3f;   // aim-follow lag (used in Task 2.6)

    private enum RigState { Idle, Moving, Following }
    private RigState _state = RigState.Idle;

    private Coroutine _moveRoutine;
    private Catapult _followTarget;

    public void SnapTo(Catapult catapult)
    {
        if (catapult == null || catapult.cameraAnchor == null)
        {
            WarnNoAnchor(catapult);
            return;
        }

        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = null;

        EvaluateAnchorPose(catapult, out Vector3 pos, out Quaternion rot);
        transform.SetPositionAndRotation(pos, rot);

        _followTarget = catapult;
        _state = RigState.Following;
    }

    public void MoveTo(Catapult catapult)
    {
        if (catapult == null || catapult.cameraAnchor == null)
        {
            WarnNoAnchor(catapult);
            return;
        }

        _followTarget = catapult;

        // Capture BEFORE StopCoroutine: the new glide starts from where the camera
        // is right now, so interrupting a move never snaps back to the old start.
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        EvaluateAnchorPose(catapult, out Vector3 endPos, out Quaternion endRot);

        var controlPoints = new List<Vector3> { startPos };
        foreach (Transform waypoint in catapult.cameraAnchor.pathPoints)
        {
            if (!waypoint) continue;
            controlPoints.Add(RelativeToRestFrame(catapult, waypoint.localPosition));
        }
        controlPoints.Add(endPos);

        PolylinePath path = new PolylinePath(controlPoints);

        if (_moveRoutine != null) StopCoroutine(_moveRoutine);
        _moveRoutine = StartCoroutine(MoveRoutine(path, startRot, endRot));
    }

    private IEnumerator MoveRoutine(PolylinePath path, Quaternion startRot, Quaternion endRot)
    {
        _state = RigState.Moving;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float eased = ease.Evaluate(Mathf.Clamp01(elapsed / moveDuration));
            transform.position = path.Evaluate(eased);
            transform.rotation = Quaternion.Slerp(startRot, endRot, eased);
            yield return null;
        }

        // Snap to the exact end pose so no float drift is left behind
        // (same trick as ProjectileLuncher.ResetHeadRotationRoutine).
        transform.SetPositionAndRotation(path.Evaluate(1f), endRot);
        _moveRoutine = null;
        _state = RigState.Following;
    }

    private static void WarnNoAnchor(Catapult catapult)
    {
        if (catapult)
            Debug.LogWarning($"CatapultCameraRig: '{catapult.name}' has no CameraAnchor — camera stays put.", catapult);
    }

    // The anchor is authored as a child of the catapult root, in the catapult's REST
    // frame. RestRotation * localRotation maps it to world space frozen at that rest pose.
    // Both entry points already guarded cameraAnchor != null, so this can assume it.
    private static void EvaluateAnchorPose(Catapult catapult, out Vector3 pos, out Quaternion rot)
    {
        CameraAnchor anchor = catapult.cameraAnchor;
        pos = catapult.RestPosition + catapult.RestRotation * anchor.transform.localPosition;
        rot = catapult.RestRotation * anchor.transform.localRotation;
    }

    private static Vector3 RelativeToRestFrame(Catapult catapult, Vector3 localPosition)
    {
        return catapult.RestPosition + catapult.RestRotation * localPosition;
    }
}

```

**توضیحات خط به خط بخش‌های مهم:**

*enum `_state` به جای متغیرهای بولین.* با داشتن `_isMoving` + `_isFollowing` ممکن است وضعیت‌های غیرممکن ایجاد کنید (هم در حال حرکت *و* در حال دنبال کردن) و هر دو بلوک در هر فریم با نوشتن `transform.position` با هم تداخل پیدا کنند. یک enum به طور ساختاری وضعیت‌ها را ناسازگار با هم (Mutually exclusive) می‌کند: Coroutine در اولین فریم خود `Moving` و در آخرین فریم `Following` را تنظیم می‌کند، و `SnapTo` مستقیماً به `Following` می‌پرد. بلوک دنبال کردن (تسک 2.6) فقط در وضعیت `Following` اجرا می‌شود — بنابراین لغزش و دنبال کردن هرگز نمی‌توانند در یک فریم واحد `transform.position` را تغییر دهند.

*بدون نقطه اتکا* $\rightarrow$ *دوربین حرکت نمی‌کند.* هر دو نقطه ورود شرط `cameraAnchor == null` را بررسی و هشدار می‌دهند، بنابراین طراحی که یادش رفته نقطه اتکا بگذارد یک خط واضح در Console می‌بیند به جای اینکه دوربین به مبدأ تلپورت شود. این شرط محافظ در ورودی قرار دارد، به همین دلیل `EvaluateAnchorPose` می‌تواند فرض کند نقطه اتکا وجود دارد.

*قطع کردن یک حرکت (Interrupting a move).* `MoveTo` مقادیر `startPos`/`startRot` را از ترنسفورم **فعلی** ریگ *پیش* از اینکه `StopCoroutine` لغزش قبلی را متوقف کند ثبت می‌کند — بنابراین کلیک سریع و مجدد روی فلش، مسیر جدید را از جایی که دوربین واقعاً هست شروع می‌کند و هرگز به نقطه شروع حرکت قبلی برنمی‌گردد. به همین دلیل است که عمداً هیچ خروج زودهنگامی مثل «اگر در حال حرکت است، فقط فیلد هدف را تغییر بده» وجود ندارد: یک Coroutine در حال اجرا همچنان به سمت وضعیت پایانی ثبت‌شده خود پرواز می‌کند، بنابراین باید از وضعیت فعلی *مجدداً راه‌اندازی* شود، نه اینکه صرفاً هدفش تغییر کند.

*چرا `Evaluate` در `t` نرمی‌گرفته (eased) و `Slerp` با همان `t` نرمی‌گرفته انجام می‌شود* — هر دو موقعیت و چرخش بر اساس یک ساعت نرمی‌گرفته واحد پیش می‌روند، بنابراین دوربین دقیقاً در همان لحظه‌ای که به نقطه اتکا می‌رسد، رو به نقطه اتکا قرار می‌گیرد.

* \[ \] **مرحله ۲: Stage و پیشنهاد کامیت**

```
git add Assets/Scripts/Camera/CatapultCameraRig.cs Assets/Scripts/Camera/CatapultCameraRig.cs.meta

```

پیام پیشنهادی: `feat(camera): add CatapultCameraRig with anchor glide (straight or Catmull-Rom path)`

### تسک 2.6: دنبال کردن نشانه — دوربین نرم و با «تأخیر» (توسعه `CatapultCameraRig`)

**فایل‌ها:**

* تغییر در: `Assets/Scripts/Camera/CatapultCameraRig.cs`

**واسط‌ها:**

* مصرف می‌کند: `Catapult.RestPosition/RestRotation` (تسک 2.2)، `_state`/`_followTarget` از تسک 2.5.

* تولید می‌کند: زمانی که `_state == Following` باشد، ریگ با یک تأخیر قابل تنظیم به دنبال چرخش (Yaw) منجنیق فعال می‌چرخد. هیچ چیز دیگری این را مصرف نمی‌کند — این صرفاً حس بازی (Feel) است.

**رفتار مورد نظر.** هنگامی که بازیکن نشانه می‌گیرد، دوربین باید به آرامی همراه منجنیق بچرخد — نه به صورت خشک و صلب (که سلسله‌مراتب والد-فرزندی قدیمی به طور تصادفی انجام می‌داد)، بلکه **آهسته‌تر**، به طوری که منجنیق به وضوح جلوتر باشد و دوربین پشت سر آن جای بگیرد.

* \[ \] **مرحله ۱: اضافه کردن بلوک دنبال کردن** — یک `LateUpdate` به `CatapultCameraRig`:

```
private void LateUpdate()
{
    if (_state != RigState.Following || _followTarget == null || _followTarget.cameraAnchor == null)
        return;

    CameraAnchor anchor = _followTarget.cameraAnchor;

    // 1. "How far has the body rotated away from its rest pose", in world space.
    Quaternion bodyDelta = _followTarget.transform.rotation * Quaternion.Inverse(_followTarget.RestRotation);

    // 2. Where the anchor would be if it were welded to that rotation.
    Vector3 anchorRestLocal = anchor.transform.localPosition;
    Vector3 anchorRestWorld = _followTarget.RestPosition + _followTarget.RestRotation * anchorRestLocal;
    Vector3 targetPos = _followTarget.RestPosition + bodyDelta * (anchorRestWorld - _followTarget.RestPosition);
    Quaternion targetRot = bodyDelta * _followTarget.RestRotation * anchor.transform.localRotation;

    // 3. Move toward it at a RATE, not a ratio — frame-rate independent.
    float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, k);
    transform.position = Vector3.Lerp(transform.position, targetPos, k);

    // 4. Kill the drift once it is imperceptible.
    if (Quaternion.Angle(transform.rotation, targetRot) < 0.05f)
    {
        transform.SetPositionAndRotation(targetPos, targetRot);
    }
}

```

**توضیحات خط به خط — این بلوک اصلی تنظیم حس این ویژگی است:**

*`bodyDelta` — «چقدر نسبت به وضعیت سکون چرخیده است».* اختلاف بین دو چرخش را با ضرب یکی در *معکوس* دیگری محاسبه می‌کنید: `current * Inverse(rest)`. نتیجه یک چرخش است که می‌توانید آن را روی هر چیزی که در وضعیت سکون طراحی شده اعمال کنید و دقیقاً در جایی فرود می‌آید که انگار از ابتدا به منجنیق جوش خورده بوده است — «تفریق» برای چرخش‌ها، اما معتبر در فضای سه‌بعدی بدون نگرانی از محورها یا چرخش کامل. *چرا فقط Yaw را نخوانیم و دوباره نسازیم؟* چون `RotateCatapultBody` یک چرخش **جهانی** می‌نویسد (`transform.rotation = Quaternion.Euler(...)`)، بنابراین «مقدار Yaw» یک عدد تمیز و واحد نیست؛ `bodyDelta` از خود ترنسفورم‌ها استخراج می‌شود و صرف‌نظر از نحوه چرخاندن بدنه توسط لانچر، همیشه درست می‌ماند. فرضیات کمتر، احتمال خرابی کمتر.

*چرا `anchorRestWorld - RestPosition`.* چرخش حول مبدأ منجنیق، خود مبدأ را جابه‌جا نمی‌کند — بلکه همه‌چیز را *دور* آن می‌چرخاند. برای بازتولید این رفتار برای دوربین، شما **آفست (فاصله برداری)** از مبدأ را می‌چرخانید و سپس مبدأ را دوباره به آن اضافه می‌کنید. چرخاندن موقعیت جهانی مطلق به جای آن، دوربین را به دور مبدأ *جهان* می‌چرخاند — که وقتی منجنیق در `(0,0,0)` قرار دارد تقریباً درست به نظر می‌رسد، اما به محض اینکه در جای دیگری باشد کاملاً اشتباه است.

*چرا «نرخ، نه نسبت» (Rate, not ratio).* نسخه ساده‌لوحانه آن `transform.rotation = Quaternion.Slerp(current, target, 0.1f)` است — اما آن `0.1` *به‌ازای هر فریم* است، بنابراین در ۱۲۰ فریم بر ثانیه دوربین دو برابر سریع‌تر از ۶۰ فریم بر ثانیه به هدف می‌رسد و بازی در هر دستگاهی حس متفاوتی دارد. فرمول $1 - \exp(-\text{sharpness} \times dt)$ نسخه مستقل از نرخ فریم (Frame-rate-independent) است: کسری که طی می‌شود به **زمان سپری‌شده** بستگی دارد، نه تعداد فریم‌ها. اگر قرار است یک خط از این برنامه را به خاطر بسپارید، همین خط را به خاطر بسپارید. (همان تفکر گام زمانی ثابت در انتگرال‌گیری فنر `CameraShakeManager`.)

*چرا `LateUpdate` و نه `Update`.* یونیتی ابتدا تمام `Update`ها را اجرا می‌کند و سپس تمام `LateUpdate`ها را. `InputController` منجنیق را در `Update` می‌چرخاند؛ اگر دوربین هم در `Update` دنبال می‌کرد، ترتیب نسبی نامشخص بود و دوربین به طور تصادفی یک فریم اضافه عقب می‌ماند (که به صورت لرزش یا Jitter دیده می‌شود). `LateUpdate` تضمین می‌کند که دوربین وضعیت *نهایی* منجنیق را در این فریم می‌خواند — همان دلیلی که `CameraShakeManager` در `LateUpdate` کار می‌کند.

*چرا موقعیت هم حرکت می‌کند، نه فقط چرخش.* نقطه اتکا با فاصله از محور چرخش منجنیق قرار دارد (حدود ۳.۶ واحد به سمت کنار). چرخاندن ریگ در جای خودش، دوربین را حول موقعیت خودش می‌چرخاند، نه حول منجنیق — که در فاصله نزدیک نمای غلطی ایجاد می‌کند. جابه‌جا کردن هر دو با کسر یکسان `k` نقطه اتکا را به بدنه چرخیده «متصل» نگه می‌دارد در حالی که همچنان تأخیر دارد.

*چرا این با `CameraShakeManager` تداخل ندارد.* لرزش دوربین، آن را در فضای **محلی (Local)** می‌لرزاند و در حالت آرامش هر فریم وضعیت سکون خود را دوباره ثبت می‌کند. دوربین فرزند ریگ در مختصات محلی `(0,0,0)` است، بنابراین وضعیت سکون محلی آن صرف‌نظر از اینکه ریگ کجا برود ثابت است — این دو سیستم با هم تداخلی ندارند. (بررسی شده در برابر `CameraShakeManager.RecordRest`/`Apply`.)

*چرا نباید با جابه‌جایی (Switch) تداخل کند.* هنگام اجرای یک Coroutine مربوط به `MoveTo`، مقدار `_state == Moving` است و کل این بلوک نادیده گرفته می‌شود — در غیر این صورت هر دو در هر فریم `transform.position` را بازنویسی می‌کردند. این نتیجه همان enum وضعیت از تسک 2.5 است.

* \[ \] **مرحله ۲: Stage و پیشنهاد کامیت**

```
git add Assets/Scripts/Camera/CatapultCameraRig.cs

```

پیام پیشنهادی: `feat(camera): add frame-rate-independent soft aim follow to camera rig`

## فاز ۳ — بازآرایی (Refactors) اسکریپت‌های موجود

این تسک‌ها فایل‌های موجود را تغییر می‌دهند. جستجوی grep برای تمام اعضای حذف‌شده انجام شده است: `DecreaseCurrentAmmo` دقیقاً یک فراخوانی‌کننده (`Projectile.Shoot`) دارد و `GetCurrentAmmoCount` صفر — حذف هر دو بی‌خطر است. هیچ بخش دیگری در پروژه `AmmoManager.levelData` را نمی‌خواند.

### تسک 3.1: `AmmoManager` — تبدیل شدن از مالک مخزن به نگهدارنده مخزن

**فایل‌ها:**

* تغییر در: `Assets/Scripts/Projectile/AmmoManager.cs` (بازنویسی — فایل کوچک‌تر می‌شود)

**واسط‌ها:**

* مصرف می‌کند: `Core.AmmoPool<SO_ProjectileType>` (قرارداد تسک 1.2: `Has` = تعداد $> 0$، `Get`/`Consume` هرگز استثنا پرتاب نمی‌کنند).

* تولید می‌کند: `void AttachPool(AmmoPool<SO_ProjectileType>)`، `bool HasAmmo(SO_ProjectileType)`، `int GetRemaining(SO_ProjectileType)`، `int Consume(SO_ProjectileType)`، `event Action<SO_ProjectileType,int> OnAmmoChanged`. کلاس‌های `Projectile` (3.2)، `ProjectileSpawner` (3.3)، `CatapultManager` (2.3) و `ProjectileCarouselUI` (3.6) از این‌ها استفاده می‌کنند.

**باگی که حذف می‌شود.** امروز `AmmoManager.Awake()` *دوباره `ProjectileSelector` مشترک را مقداردهی اولیه می‌کند*. دو منجنیق = دو `AmmoManager` = دو کامپوننت که بر سر یک لیست و یک ایندکس انتخاب‌شده با هم می‌جنگند — هر کدام که `Awake` آن دیرتر اجرا شود برنده می‌شود و تجهیزات (Loadout) دیگری به صورت پنهانی ناپدید می‌شود. پس از این تسک، `AmmoManager` مخزنی را نگه می‌دارد که به آن *داده شده* و به سؤالات درباره آن پاسخ می‌دهد؛ هرگز لیست نوع نمی‌سازد و هرگز به انتخاب دست نمی‌زند.

* \[ \] **مرحله ۱: جایگزینی کل فایل با کد زیر:**

```
using System;
using Core;
using UnityEngine;

// Holds a REFERENCE to the level's ammo pool (built by CatapultManager) and
// re-raises its changes for the UI. Deliberately cannot create or select types —
// that is CatapultManager/ProjectileSelector's job.
public class AmmoManager : MonoBehaviour
{
    public ProjectileSelector projectileSelector;   // read-only use: answer "is the selected type available?"

    public event Action<SO_ProjectileType, int> OnAmmoChanged;

    private AmmoPool<SO_ProjectileType> _pool;

    public void AttachPool(AmmoPool<SO_ProjectileType> pool)
    {
        if (_pool != null) _pool.OnChanged -= HandlePoolChanged;   // re-attach must not double-subscribe
        _pool = pool;
        _pool.OnChanged += HandlePoolChanged;
    }

    // Re-raise in the SAME signature the UI already knows, so the carousel needs no changes.
    private void HandlePoolChanged(SO_ProjectileType type, int remaining)
    {
        OnAmmoChanged?.Invoke(type, remaining);
    }

    public bool HasAmmo(SO_ProjectileType type)
    {
        return type && _pool != null && _pool.Has(type);
    }

    public int GetRemaining(SO_ProjectileType type)
    {
        return type && _pool != null ? _pool.Get(type) : 0;
    }

    public int Consume(SO_ProjectileType type)
    {
        return _pool != null ? _pool.Consume(type) : 0;
    }
}

```

**آنچه حذف شد و چرا بی‌خطر است:**

* `Awake()` + `InitializeAmmo()` — لیست انواع اکنون در `CatapultManager.BuildTypeList` (تسک 2.3) قرار دارد، یعنی تنها جایی که ساخته می‌شود.

* فیلد `levelData` — یک مالک (`CatapultManager`)، یک منبع حقیقت واحد. خواندن یک `SO_LevelData` مشترک توسط دو کامپوننت و تصمیم‌گیری جداگانه هر کدام درباره معنای آن، دقیقاً همان دلیلی بود که باگ قدیمی رخ می‌داد.

* `DecreaseCurrentAmmo()` / `GetCurrentAmmoCount()` — این‌ها باگ «نوع اشتباه» را در خود داشتند: آن‌ها `projectileSelector.SelectedType` را *در لحظه فراخوانی* بررسی می‌کردند، بنابراین تغییر نوع پرتابه بین بارگذاری و شلیک، مهمات را از مخزن اشتباهی کم می‌کرد. متد جدید `Consume(type)` نوع را به صورت صریح می‌گیرد (تسک 3.2 آن را پاس می‌دهد) و وابستگی زمانی را به طور کامل حذف می‌کند.

* دیکشنری `_ammo` — با مخزن (Pool) جایگزین شد. توجه کنید که `DecreaseAmmo` قدیمی عمل `--_ammo[type]` را انجام می‌داد که برای نوع ناشناخته `KeyNotFoundException` پرتاب می‌کرد و در مقدار $0$ منفی می‌شد؛ مخزن جدید (تسک 1.2) هیچ‌کدام از این کارها را نمی‌کند.

**نکته آموزشی — چرا به جای افشای مستقیم رویداد مخزن، آن را دوباره منتشر (Re-raise) می‌کنیم.** رابط کاربری با `AmmoManager` صحبت می‌کند و چیزی درباره `AmmoPool` نمی‌داند. ارسال مجدد *همان* امضای رویدادی که از قبل استفاده می‌کند، کد اشتراک کاروسل را بدون تغییر نگه می‌دارد — `AmmoManager` به عنوان نمای (Facade) روی مخزن باقی می‌ماند، همان‌طور که `Catapult` نمای روی لانچر/اسپاونر/مهمات است.

**نکته آموزشی — محافظ اتصال مجدد (Re-attach guard).** در حالت PerCatapult یک منیجر دقیقاً یک بار متصل می‌شود، اما نوشتن تدافعی `-= before +=` فقط یک خط هزینه دارد و `AttachPool` را خنثی در برابر تکرار (Idempotent) می‌کند — در غیر این صورت دو بار صدا زدن آن، هر رویداد آینده را دو برابر می‌کرد.

* \[ \] **مرحله ۲: این کار بیلد را می‌شکند** (`Projectile.Shoot` متد حذف‌شده `DecreaseCurrentAmmo` را صدا می‌زند، `DelaySpawn` همچنان کامپایل می‌شود). این طبیعی است — تسک‌های 3.2–3.4 فراخوانی‌کننده‌ها را اصلاح می‌کنند. هنوز کامیت **نکنید**؛ پس از تسک 3.4 کامیت کنید.

### تسک 3.2: `Projectile` — حمل کردن نوع خود

**فایل‌ها:**

* تغییر در: `Assets/Scripts/Projectile/Projectile.cs` (سه خط)

**واسط‌ها:**

* تولید می‌کند: `public SO_ProjectileType projectileType;` روی `Projectile` — توسط اسپاونر تنظیم می‌شود (3.3)، و توسط تعویض خودکار لانچر خوانده می‌شود (3.4). امضای `Shoot(force)` بدون تغییر است.

* \[ \] **مرحله ۱: اضافه کردن فیلد** در کنار سایر فیلدهای سریال‌سازی‌شده:

```
public class Projectile : MonoBehaviour
{
    public float baseForcePower = 1500;
    public float delayBeforeDestroy = 0.5f;
    public float shakeIntensity = 1f;
    public AmmoManager ammoManager;
    public SO_ProjectileType projectileType;   // NEW — set by ProjectileSpawner at instantiation
    // ...

```

* \[ \] **مرحله ۲: تغییر `Shoot` برای مصرف نوع حمل‌شده** — بلوک آخر را جایگزین کنید:

```
        if (ammoManager)
        {
            ammoManager.Consume(projectileType);   // was: DecreaseCurrentAmmo() — the "wrong type" bug
        }

```

*چرا این کار یک باگ واقعی را برطرف می‌کند:* `DecreaseCurrentAmmo()` مقدار `projectileSelector.SelectedType` را در زمان شلیک می‌خواند. تخته سنگ را بارگذاری کنید، وسط نشانه گرفتن کاروسل را روی بمب بگذارید، رها کنید $\rightarrow$ شلیک تخته سنگ پرتاب می‌کند اما یک بمب کم می‌کند. اکنون پرتابه به یاد می‌سپارد که *چیست*.

### تسک 3.3: `ProjectileSpawner` — دست کشیدن از دور انداختن نوع پرتابه

**فایل‌ها:**

* تغییر در: `Assets/Scripts/Projectile/ProjectileSpawner.cs` (بازنویسی — فایل کوچک‌تر می‌شود)

**واسط‌ها:**

* مصرف می‌کند: `Projectile.projectileType` (3.2).

* تولید می‌کند: `Projectile InstantiateBulletSafe(SO_ProjectileType pType = null)` — همان امضای فعلی. بازگرداندن `null` اکنون به این معناست که «این منجنیق برای نوع درخواستی مهماتی ندارد» و **هرگز** انتخاب سراسری را تغییر نمی‌دهد.

**رفتاری که حذف می‌شود.** امروز شاخه نبود مهمات در `InstantiateBullet` متد `projectileSelector.SelectNextWithAmmo(...)` را صدا می‌زند. در حالت PerCatapult این خطرناک است: لانچر هر منجنیق مشترک رویداد سراسری `OnSelectionChanged` است و پیش‌بارگذاری می‌کند؛ منجنیق B ممکن است $0$ تخته سنگ داشته باشد در حالی که A پنج تا دارد. وقتی بازیکن تخته سنگ را انتخاب می‌کند، لانچر B متد `InstantiateBulletSafe(Boulder)` را صدا می‌زند، به شاخه نبود مهمات می‌خورد و *انتخاب سراسری را از زیر دست بازیکنی که با A نشانه گرفته تغییر می‌دهد*. با حذف این بازگشت (Fallback)، B به سادگی چیزی بارگذاری نمی‌کند و منتظر می‌ماند؛ بارگذاری مجدد هنگام فعال‌سازی در تسک 3.4 آن را مدیریت می‌کند. تعویض خودکار به لانچر *فعال* منتقل می‌شود (3.4) و فقط مخزنی را در نظر می‌گیرد که بازیکن واقعاً از آن شلیک می‌کند.

* \[ \] **مرحله ۱: جایگزینی کل فایل با کد زیر:**

```
using UnityEngine;

public class ProjectileSpawner : MonoBehaviour
{
    public ProjectileSelector projectileSelector;
    public AmmoManager ammoManager;

    private Projectile _currentProjectile;
    private bool _isSwitching;

    private Projectile InstantiateBullet(SO_ProjectileType type)
    {
        // No global auto-switch here — an INACTIVE catapult must never change the
        // global selection. The active launcher auto-switches after a shot (3.4).
        if (!type || !type.prefab || !ammoManager.HasAmmo(type))
        {
            return null;
        }

        Projectile currentProjectile = Instantiate(type.prefab, transform.position, transform.rotation, transform);
        currentProjectile.ammoManager = ammoManager;
        currentProjectile.projectileType = type;   // the projectile remembers what it is (3.2)

        return currentProjectile;
    }

    public Projectile InstantiateBulletSafe(SO_ProjectileType pType = null)
    {
        // Guard against re-entrant calls: selection events can re-enter synchronously.
        if (_isSwitching)
        {
            return _currentProjectile;
        }

        _isSwitching = true;

        // Replace the loaded projectile only if it hasn't been launched yet.
        // Projectile.Shoot() unparents the instance, so launched projectiles are no
        // longer children of this spawner and must not be destroyed here.
        if (_currentProjectile && _currentProjectile.transform.parent == transform)
        {
            Destroy(_currentProjectile.gameObject);
        }

        _currentProjectile = InstantiateBullet(pType ?? projectileSelector.SelectedType);

        _isSwitching = false;

        return _currentProjectile;
    }
}

```

*تفاوت‌ها با فایل فعلی:* `InstantiateBullet` اکنون **نیازمند** یک نوع است (پیش‌فرض `pType ?? SelectedType` به سطح بالاتر در `InstantiateBulletSafe` منتقل شد تا به جای دو نقطه فراخوانی، یک نقطه وجود داشته باشد)؛ بازگشت `SelectNextWithAmmo` حذف شده است؛ `currentProjectile.projectileType = type` جدید است. محافظ ورود مجدد `_isSwitching` و بررسی والد پیش از نابودی دقیقاً همان‌طور که هستند حفظ شده‌اند — آن‌ها از قبل موارد ظریف را مدیریت می‌کنند.

* \[ \] **مرحله ۲: هنوز کامیت نکنید** — `ProjectileLuncher` همچنان کامپایل می‌شود، اما `DelaySpawn` آن `InstantiateBulletSafe()` را بدون آرگومان صدا می‌زند که اکنون به معنای «هر آنچه سلکتور اکنون به آن اشاره می‌کند» است (خوب است)، و تعویض خودکار هنوز ناقص است. تسک 3.4 بازآرایی را کامل می‌کند.

### تسک 3.4: `ProjectileLuncher` — بارگذاری بر حسب تقاضا و تعویض خودکار پس از شلیک

**فایل‌ها:**

* تغییر در: `Assets/Scripts/Projectile/ProjectileLuncher.cs`

**واسط‌ها:**

* مصرف می‌کند: `ProjectileSpawner.InstantiateBulletSafe(type)` (3.3)، `Projectile.projectileType` (3.2)، `AmmoManager.HasAmmo` (3.1، از طریق `projectileSpawner.ammoManager`)، `ProjectileSelector.SelectNextWithAmmo(Func<SO_ProjectileType,bool>)` (موجود).

* تولید می‌کند: `public void LoadProjectile()` — فراخوانی‌شده توسط `CatapultManager.SetActive` (2.3)، `Start`، `HandleSelectionChanged` و `DelaySpawn`.

* \[ \] **مرحله ۱: اضافه کردن `LoadProjectile` و هدایت تمام بارگذاری‌ها از طریق آن.** پس از `Start()` اضافه کنید:

```
    // The ONE place a projectile gets loaded. Every caller goes through here so the
    // guards can never disagree. CatapultManager.SetActive calls this on the newly
    // active catapult — in PerCatapult mode an inactive catapult may have had no ammo
    // for the selected type and is holding nothing.
    public void LoadProjectile()
    {
        if (_isOnDelay || _isCatapultMoving) return;
        _activeProjectile = projectileSpawner.InstantiateBulletSafe(projectileSelector.SelectedType);
    }

```

سپس دو محل بارگذاری موجود را ساده کنید تا آن را صدا بزنند:

```
    private void HandleSelectionChanged(SO_ProjectileType type)
    {
        LoadProjectile();   // was: duplicate guard + InstantiateBulletSafe(type)
    }

```

```
    IEnumerator DelaySpawn(float delayTime)
    {
        _isOnDelay = true;
        yield return new WaitForSeconds(delayTime);
        _isOnDelay = false;
        LoadProjectile();   // was: InstantiateBulletSafe() with no argument
    }

```

و در `Start()`، یک خط پس از ثبت وضعیت سکون اضافه کنید:

```
    void Start()
    {
        if (catapultHead)
        {
            _initiateCatapultHeadRotation = catapultHead.transform.localRotation;
            _initiateCatapultBodyRotation = transform.localRotation;
        }

        LoadProjectile();   // NEW — see "lifecycle-ordering trap" rule 3: events are for changes, not initial state
    }

```

*نکته آموزشی — چرا شرط محافظ `if (_isOnDelay || _isCatapultMoving) return` است.* همان شرط محافظی که کد از قبل در `HandleSelectionChanged` استفاده می‌کرد، در یک مکان واحد نگه داشته شده است: `DelaySpawn` پس از زمان انتظار (Cooldown) دوباره اسپاون می‌کند و اگر در آن لحظه کشیدن (Drag) در جریان باشد، پرتابه جدید نباید جایگزین پرتابه‌ای شود که در آستانه شلیک است. سه نقطه فراخوانی، یک شرط محافظ — آن‌ها هرگز با هم اختلاف نظر نخواهند داشت.

*نکته آموزشی — چرا به تنهایی به رویداد تکیه نمی‌کنیم.* `selector.Initialize(types)` رویداد `OnSelectionChanged` را منتشر می‌کند، اما لانچر در `OnEnable` مشترک می‌شود و `OnEnable` لانچر ممکن است پیش از `Awake` منیجر اجرا شود (ترتیب نسبی نامشخص). اجرا شدن `Start` پس از *تمام* `Awake`ها تضمین‌شده است — بنابراین فراخوانی صریح `LoadProjectile()` در `Start` وابستگی به ترتیب را از بین می‌برد. (چروک شناخته‌شده: اولین پرتابه ممکن است دو بار ساخته شود — یک بار توسط رویداد، یک بار توسط `Start` — و `InstantiateBulletSafe` اولی را نابود می‌کند. درست است، فقط یک `Instantiate` هدررفته.)

* \[ \] **مرحله ۲: تعویض خودکار پس از شلیک.** در `ResetCatapultHeadRotation`، بلوک شلیک را جایگزین کنید:

```
    public void ResetCatapultHeadRotation()
    {
        _isCatapultMoving = false;
        if (_activeProjectile)
        {
            SO_ProjectileType firedType = _activeProjectile.projectileType;   // NEW — remember before Shoot

            float mag = 1 - (Mathf.Clamp(catapultHead.transform.localRotation.eulerAngles.magnitude, 0, 60) / 60);
            if (mag > 0.1f)
            {
                if (luncherReleaseAudio && audioConfig)
                {
                    audioConfig.Play(luncherReleaseAudio, mag);
                }

                _activeProjectile.Shoot(mag);
                StartCoroutine(DelaySpawn(cooldown));

                // GDD §5.2: after the shot that empties a type, switch to the next type
                // with ammo IN THIS CATAPULT'S POOL (the predicate decides whose ammo counts).
                AmmoManager ammo = projectileSpawner ? projectileSpawner.ammoManager : null;
                if (ammo && !ammo.HasAmmo(firedType))
                {
                    projectileSelector.SelectNextWithAmmo(ammo.HasAmmo);
                }
            }
        }

        if (_headResetRoutine != null)
        {
            StopCoroutine(_headResetRoutine);
        }
        _headResetRoutine = StartCoroutine(ResetHeadRotationRoutine());
    }

```

*چرا `projectileType` پیش از `Shoot` خوانده می‌شود:* `Shoot` پرتابه را پس از `delayBeforeDestroy` نابود می‌کند، اما این فیلد در اینجا و بلافاصله خوانده می‌شود — هر دو ترتیب کار می‌کنند؛ خواندن پیش از آن صرفاً واضح‌تر است.

*چرا `ammo.HasAmmo` پس از `Shoot` بررسی می‌شود:* `Projectile.Shoot` مهمات را به صورت *همگام (Synchronously)* پیش از بازگشت مصرف می‌کند، بنابراین بررسی پس از آن تعداد پس از شلیک را می‌بیند. اگر مخزن برای آن نوع خالی شده باشد، `SelectNextWithAmmo(predicate)` در حلقه می‌چرخد و نوع بعدی دارای مهمات را انتخاب می‌کند — در مخزن *این* منجنیق، زیرا `ammo` از طریق اسپاونر خود این منجنیق بازیابی می‌شود.

*نکته آموزشی — `SelectNextWithAmmo` یک گزاره (Predicate) می‌گیرد، نه یک لیست.* این همان چیزی است که آن را با مخازن به‌ازای هر منجنیق قابل استفاده می‌کند: فراخوانی‌کننده تصمیم می‌گیرد که مهمات **چه کسی** به حساب می‌آید. پاس دادن `ammo.HasAmmo` (یک *گروه متد / Method group* — نام متدی که به عنوان یک مقدار `Func<...,bool>` استفاده می‌شود) آن را به مخزن این منجنیق مقید می‌کند. این رفتار حفظ‌شده در بخش ۵.۲ GDD مستند شده است.

*چرا از طریق `projectileSpawner.ammoManager` و نه یک فیلد جدید:* لانچر از قبل اسپاونر را دارد و اسپاونر از قبل منیجر را دارد. یک فیلد سریال‌سازی‌شده اضافه، یک جای خالی دیگر برای سیم‌کشی (و فراموش کردن) در فاز ۵ ایجاد می‌کرد.

* \[ \] **مرحله ۳: بررسی کامپایل + تست دود دستی (Smoke test).** پروژه باید کامپایل شود. با صحنه *فعلی* (تک‌منجنیقی) Play را بزنید: نشانه گرفتن، شلیک، بارگذاری مجدد پس از زمان انتظار و تعویض خودکار هنگام اتمام مهمات همگی باید دقیقاً مانند قبل رفتار کنند (بازآرایی رفتار را حفظ می‌کند؛ سیم‌کشی چندمنجنیقی در فاز ۵ انجام می‌شود).

* \[ \] **مرحله ۴: Stage و پیشنهاد کامیت** (تسک‌های 3.1–3.4 با هم)

```
git add Assets/Scripts/Projectile/AmmoManager.cs Assets/Scripts/Projectile/Projectile.cs Assets/Scripts/Projectile/ProjectileSpawner.cs Assets/Scripts/Projectile/ProjectileLuncher.cs

```

پیام پیشنهادی: `refactor(projectile): explicit ammo pool + per-projectile type, remove selector re-init`

### تسک 3.5: `InputController` — هدایت منجنیق *فعال* و پشتیبانی از ماوس

**فایل‌ها:**

* تغییر در: `Assets/Scripts/InputController.cs` (بازنویسی — کوچک‌تر می‌شود)

* تغییر صحنه (فاز ۵): کامپوننت از ریشه منجنیق به GameObject `CatapultManager` منتقل می‌شود.

**واسط‌ها:**

* مصرف می‌کند: `CatapultManager.ActiveCatapult` + `Catapult.launcher` (2.2/2.3)، `ProjectileLuncher.RotateCatapultHead/Body/ResetCatapultHeadRotation` (موجود).

* تولید می‌کند: فیلد سریال‌سازی‌شده `CatapultManager catapultManager` (در فاز ۵ سیم‌کشی می‌شود). رفتار: کشیدن (Drag) در هر جایی که UI نباشد، منجنیق فعال را نشانه می‌گیرد؛ لمسی یا ماوس.

**نکته آموزشی — ارجاع سریال‌سازی‌شده در برابر `GetComponent`.** `GetComponent<ProjectileLuncher>()` فقط *همین* شیء را جستجو می‌کند. به محض اینکه لانچر روی سر منجنیق (فرزند یک ریشه متفاوت) قرار بگیرد، این روش خراب می‌شود. یک فیلد سریال‌سازی‌شده صریح است — منیجر را روی جایگاه می‌کشید و در یک نگاه می‌بینید به چه چیزی متصل است.

* \[ \] **مرحله ۱: جایگزینی کل فایل با کد زیر:**

```
using UnityEngine;
using UnityEngine.EventSystems;

// Aims the ACTIVE catapult (CatapultManager.ActiveCatapult). Reads touch OR mouse
// through one pointer path; ignores presses that land on uGUI (arrows, carousel).
public class InputController : MonoBehaviour
{
    public CatapultManager catapultManager;   // wired in the Inspector (Phase 5)

    public float touchSensitivityMinY = 1f;
    public float touchSensitivityMaxY = 4f;

    public float touchSensitivityX = 50;

    private float _touchSensitivityY = 1;

    private Vector2 _startPos;
    private bool _dragging;

    private void Update()
    {
        // A switch mid-drag drops the drag: the new active catapult must not inherit
        // the old drag's rotation deltas.
        if (catapultManager && catapultManager.ActiveCatapult == null)
        {
            _dragging = false;
            return;
        }

        if (!TryGetPointer(out Vector2 position, out bool pressedThisFrame, out bool releasedThisFrame))
        {
            return;
        }

        // Ignore presses that land on a UI element (the arrows, the carousel, ...).
        // Legacy Input and uGUI never talk to each other — uGUI "consuming" a click
        // does NOT stop Input.GetMouseButton from reporting it. We must ask ourselves.
        if (pressedThisFrame && EventSystem.current && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (pressedThisFrame)
        {
            _startPos = position;
            _dragging = true;
            return;
        }

        if (releasedThisFrame)
        {
            if (_dragging)
            {
                _dragging = false;
                Launch(catapultManager.ActiveCatapult.launcher);
            }
            return;
        }

        if (_dragging)
        {
            Aim(position);
        }
    }

    // One pointer path: touch wins when a finger is down, otherwise the mouse.
    private static bool TryGetPointer(out Vector2 position, out bool pressedThisFrame, out bool releasedThisFrame)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            position = touch.position;
            pressedThisFrame = touch.phase == TouchPhase.Began;
            releasedThisFrame = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            return true;
        }

        position = Input.mousePosition;
        pressedThisFrame = Input.GetMouseButtonDown(0);
        releasedThisFrame = Input.GetMouseButtonUp(0);
        bool held = Input.GetMouseButton(0);
        return held || releasedThisFrame;
    }

    private void Aim(Vector2 position)
    {
        ProjectileLuncher launcher = catapultManager.ActiveCatapult.launcher;
        if (!launcher) return;

        float windowWidth = Screen.width;
        float windowHeight = Screen.height;

        Vector2 dragPos = position - _startPos;

        // Sensitivity maths kept exactly as it was — only the pointer source changed.
        dragPos.x = (dragPos.x / windowWidth) * touchSensitivityX;
        _touchSensitivityY = Mathf.Lerp(touchSensitivityMaxY, touchSensitivityMinY, dragPos.y / windowHeight);
        dragPos.y = Mathf.Clamp(((dragPos.y / windowHeight) * _touchSensitivityY * 60) + 60, 0, 60);

        launcher.RotateCatapultHead(dragPos.y);
        launcher.RotateCatapultBody(dragPos.x);
    }

    private static void Launch(ProjectileLuncher launcher)
    {
        if (launcher) launcher.ResetCatapultHeadRotation();
    }
}

```

**تغییرات، خط به خط:**

*`catapultManager.ActiveCatapult.launcher` جایگزین `GetComponent<ProjectileLuncher>()` می‌شود* — کنترل‌کننده ورودی دیگر نمی‌داند *کدام* منجنیق را هدایت می‌کند؛ در هر فریم از منیجر می‌پرسد. (همچنین دلیل اینکه چرا کامپوننت باید در فاز ۵ از ریشه منجنیق برداشته شود: تکثیر یک منجنیق باعث تکثیر `InputController` آن می‌شود و $N$ کنترل‌کننده به شما می‌دهد که همگی منجنیق خودشان را نشانه می‌گیرند.)

*`TryGetPointer` لمس و ماوس را یکپارچه می‌کند.* شاخه لمسی: وقتی انگشتی روی صفحه است، `TouchPhase` لبه‌های فشار و رها کردن را می‌دهد. شاخه ماوس: `GetMouseButton(0)` برای «نگه داشته شدن» و `GetMouseButtonDown/Up(0)` برای لبه‌ها. `Input.mousePosition` نیازی به فاز ندارد — پرچم *نگه داشته شدن (held)* همان وضعیت است. *نکته آموزشی:* API قدیمی `Input` همچنان کار می‌کند زیرا `activeInputHandler: 2` ("Both") در `ProjectSettings` تنظیم شده است. معادل «مدرن» آن (`Mouse.current.leftButton`، `EnhancedTouch`) یک تسک مهاجرت جداگانه است — هدف اینجا فقط کار کردن ماوس است تا بتوانید در ادیتور تست کنید.

*`_dragging` + `_startPos` صریح جایگزین switch روی `TouchPhase` می‌شوند.* کد قدیمی برای نگهداری وضعیت به فازهای لمسی متکی بود؛ ماوس فاز ندارد، بنابراین وضعیت اکنون در یک متغیر بولین ذخیره می‌شود. رفتار مطابقت دارد: فشار = به خاطر سپردن نقطه شروع، نگه داشتن و حرکت = نشانه گرفتن، رها کردن = شلیک.

*شرط محافظ `IsPointerOverGameObject` اختیاری نیست.* بدون آن، ضربه زدن به فلش راست *همچنین* یک کشیدن نشانه روی منجنیقی که از آن خارج می‌شوید شروع می‌کند، و ضربه زدن به دکمه کاروسل نشانه را تکان می‌دهد. **نکته آموزشی — چرا «مصرف کردن» کلیک توسط UI از شما محافظت نمی‌کند:** `EventSystem` + `GraphicRaycaster` در uGUI متوجه کلیک روی یک `Button` می‌شوند و `onClick` آن را اجرا می‌کنند، اما `Input` قدیمی یک سیستم مجزا و سطح پایین‌تر است که هر فشار خام را گزارش می‌دهد. این دو هرگز با هم صحبت نمی‌کنند؛ شما باید خودتان بپرسید «آیا اشاره‌گر روی UI است؟» و از کشیدن صرف‌نظر کنید. این محافظ همچنین از مشکل ترتیب فریم‌ها جلوگیری می‌کند: چه `onClick` دکمه و چه `Update` شما زودتر در یک فریم اجرا شود، فشاری که روی UI شروع شده در هر حال نادیده گرفته می‌شود.

*حذف‌شده به عنوان کد مرده:* `_jumpBtn`، `GetAxisRaw("Jump")`، `GetJumpBtn()` — grep تأیید کرد که هیچ‌چیز آن‌ها را نمی‌خواند. همچنین فراخوانی کامنت‌شده `ResetCatapultBodyRotation` حذف شد.

*مورد مرزی — جابه‌جایی در میانه کشیدن (Mid-drag):* اگر `ActiveCatapult` null شود یا تغییر کند، کشیدن رها می‌شود (بازگشت زودهنگام `_dragging` را بازنشانی می‌کند؛ شاخه رها کردن فقط زمانی شلیک می‌کند که کشیدنی روی *این* کنترل‌کننده در جریان بوده باشد). UI تسک 4.3 + این مورد = جابه‌جایی سریع و تمیز.

* \[ \] **مرحله ۲: بررسی و تأیید (تک‌منجنیقی، صحنه فعلی).** Play: با ماوس بکشید $\rightarrow$ منجنیق نشانه می‌گیرد؛ رها کنید $\rightarrow$ شلیک می‌کند؛ کشیدن از روی دکمه کاروسل $\rightarrow$ نشانه نمی‌گیرد. بدون هیچ استثنایی.

* \[ \] **مرحله ۳: Stage و پیشنهاد کامیت**

```
git add Assets/Scripts/InputController.cs

```

پیام پیشنهادی: `feat(input): unified mouse/touch pointer driving the active catapult, ignore UI presses`

### تسک 3.6: `ProjectileCarouselUI` — دنبال کردن مخزن منجنیق فعال

**فایل‌ها:**

* تغییر در: `Assets/Scripts/UI/ProjectileCarouselUI.cs`

**واسط‌ها:**

* مصرف می‌کند: `CatapultManager.OnActiveCatapultChanged` + `Catapult.ammoManager` (2.2/2.3).

* تولید می‌کند: فیلد سریال‌سازی‌شده `CatapultManager catapultManager` (در فاز ۵ سیم‌کشی می‌شود). همچنین جستجوی نوع مبتنی بر نام را با یک دیکشنری اصلاح می‌کند.

**بخشی که به راحتی از قلم می‌افتد.** امروز `OnEnable` دستور `ammoManager.OnAmmoChanged += HandleAmmoChanged;` را اجرا می‌کند — که برای همیشه مشترک *یک* `AmmoManager` است. وقتی بازیکن منجنیق‌ها را عوض می‌کند، کاروسل باید **اشتراک خود را از قبلی لغو کند و مشترک جدیدی شود**، وگرنه همچنان تعداد منجنیق A را نشان می‌دهد و هرگز از شلیک‌های منجنیق B باخبر نمی‌شود.

**نکته آموزشی — چرا اشتراک باید جابه‌جا شود، نه صرفاً تازه‌سازی (Refresh).** `OnAmmoChanged` یک `event` در C# است — لیستی از توابع بازخوانی (Callbacks). اضافه کردن اشتراک دوم بدون حذف اولی باعث می‌شود تابع بازخوانی به‌ازای هر تغییر دو بار اجرا شود (نشت حافظه کلاسیک)، و `AmmoManager` قدیمی پس از تخریب صحنه همچنان کاروسل را در حافظه زنده نگه می‌دارد. به همین دلیل است که هر `+=` در این پروژه با یک `-=` جفت می‌شود.

* \[ \] **مرحله ۱: اضافه کردن فیلدها و ویژگی منبع مهمات.** در بلوک `[Header("Wiring")]`:

```
    [Header("Wiring")]
    public ProjectileSelector selector;
    public AmmoManager ammoManager;             // fallback when no manager is assigned
    public CatapultManager catapultManager;     // NEW — wired in Phase 5
    public RectTransform contentRoot;
    public GameObject buttonPrefab;

```

و ویژگی + کنترل‌کننده رویداد جدید را در کنار سایر کنترل‌کننده‌ها اضافه کنید:

```
    // The ammo source is the ACTIVE catapult's manager; fall back to the serialized one
    // so the component stays usable in a test scene without a CatapultManager.
    private AmmoManager ActiveAmmo => catapultManager && catapultManager.ActiveCatapult
        ? catapultManager.ActiveCatapult.ammoManager
        : ammoManager;

```

*نکته آموزشی — ویژگی با بدنه عبارتی (Expression-bodied property).* `=> expr;` روی یک ویژگی، خلاصه‌ای برای یک getter است که `expr` را برمی‌گرداند. در هر بار دسترسی دوباره ارزیابی می‌شود، که دقیقاً همان چیزی است که می‌خواهیم: «چه کسی *همین الان* مالک مهمات است».

* \[ \] **مرحله ۲: هدایت تمام استفاده‌های `ammoManager` از طریق `ActiveAmmo` و اشتراک مجدد هنگام جابه‌جایی.** `OnEnable`/`OnDisable` و دو کنترل‌کننده را جایگزین کنید:

```
    private void OnEnable()
    {
        if (selector) selector.OnSelectionChanged += HandleSelectionChanged;
        if (catapultManager) catapultManager.OnActiveCatapultChanged += HandleActiveCatapultChanged;
        SubscribeAmmo();
        UpdateVisibleSideCount();
        Rebuild(false);
    }

    private void OnDisable()
    {
        if (selector) selector.OnSelectionChanged -= HandleSelectionChanged;
        if (catapultManager) catapultManager.OnActiveCatapultChanged -= HandleActiveCatapultChanged;
        UnsubscribeAmmo();
    }

    private void SubscribeAmmo()
    {
        AmmoManager ammo = ActiveAmmo;
        if (ammo) ammo.OnAmmoChanged += HandleAmmoChanged;
    }

    private void UnsubscribeAmmo()
    {
        AmmoManager ammo = ActiveAmmo;
        if (ammo) ammo.OnAmmoChanged -= HandleAmmoChanged;
    }

    private void HandleActiveCatapultChanged(Catapult catapult)
    {
        // Move the ammo subscription to the new catapult's pool, then refresh the
        // NUMBERS only — a full Rebuild would make the row animate for no reason.
        UnsubscribeAmmo();
        SubscribeAmmo();
        RefreshAmmoDisplays();
    }

    private void HandleSelectionChanged(SO_ProjectileType type) => Rebuild(true);

    private void HandleAmmoChanged(SO_ProjectileType type, int remaining) => RefreshAmmoDisplays();

```

*چرا `RefreshAmmoDisplays` و نه `Rebuild(true)`:* هنگام جابه‌جایی فقط اعداد مهمات و پرچم‌های `interactable` تغییر می‌کنند — خود دکمه‌ها همان انواع قبلی هستند. `RefreshAmmoDisplays` از قبل `interactable` را به‌روز می‌کند (خط موجود: `uiButton.interactable = ammoManager.HasAmmo(type)`).

* \[ \] **مرحله ۳: جایگزینی ارجاعات باقی‌مانده `ammoManager`** درون `Rebuild` با `ActiveAmmo` — سه جا: شرط `if (ammoText && ammoManager) ammoText.text = …`، شرط `if (ammoManager) uiButton.interactable = …` (هر دو در مرحله ۴ بازنویسی شده‌اند) و هر فراخوانی مستقیم `ammoManager.GetRemaining(type)`. تغییر نام مکانیکی — همان فراخوانی‌ها، منبع متفاوت. محل‌های اشتراک را همان‌طور که در مرحله ۲ نوشته شد رها کنید (آن‌ها خودشان `ActiveAmmo` را بازیابی می‌کنند).

* \[ \] **مرحله ۴: جایگزینی رفت‌وآمد مبتنی بر نام با یک دیکشنری.** امروز `FindTypeForButton` نام GameObject را دوباره تجزیه می‌کند (`"ProjectileButton_" + type.displayName`) — که اگر دو نوع دارای `displayName` یکسان باشند خراب می‌شود. یک فیلد اضافه کنید:

```
    private readonly Dictionary<Button, SO_ProjectileType> _buttonTypes = new Dictionary<Button, SO_ProjectileType>();

```

آن را در `Rebuild` در جایی که دکمه پیکربندی می‌شود پر کنید:

```
            Button uiButton = button.GetComponent<Button>();
            if (uiButton)
            {
                _buttonTypes[uiButton] = type;   // NEW — replaces the name round-trip
                uiButton.onClick.AddListener(() => selector.SelectIndex(typeIndex));
                AmmoManager ammo = ActiveAmmo;
                if (ammo) uiButton.interactable = ammo.HasAmmo(type);
            }

```

آن را در بالای `Rebuild` (در کنار `_buttons.Clear()`) پاک کنید:

```
        _buttons.Clear();
        _buttonTypes.Clear();

```

سپس دو جستجو را ساده کنید:

```
    private void CapturePreviousSlotPositions()
    {
        _previousSlotX.Clear();
        foreach (GameObject button in _buttons)
        {
            if (!button.TryGetComponent(out Button uiButton)) continue;
            if (!_buttonTypes.TryGetValue(uiButton, out SO_ProjectileType type) || !type) continue;

            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect) _previousSlotX[type] = rect.anchoredPosition.x;
        }
    }

```

```
    private void RefreshAmmoDisplays()
    {
        AmmoManager ammo = ActiveAmmo;
        if (!ammo || !selector) return;
        foreach (GameObject button in _buttons)
        {
            if (!button.TryGetComponent(out Button uiButton)) continue;
            if (!_buttonTypes.TryGetValue(uiButton, out SO_ProjectileType type) || !type) continue;

            TMP_Text ammoText = button.transform.Find("Ammo")?.GetComponent<TMP_Text>();
            if (ammoText) ammoText.text = ammo.GetRemaining(type).ToString();

            uiButton.interactable = ammo.HasAmmo(type);
        }
    }

```

و `FindTypeForButton` را به طور کامل حذف کنید. کلیدگذاری با `Button` انجام شده نه `GameObject`، بنابراین تازه‌سازی به‌ازای هر دکمه فقط یک جستجو در دیکشنری است.

*نکته آموزشی — `TryGetComponent`.* `GetComponent` + بررسی null الگوی قدیمی است؛ `TryGetComponent(out var x)` هر دو را در یک عبارت انجام می‌دهد و در شرط‌های محافظ خواناتر است.

* \[ \] **مرحله ۵: بررسی و تأیید (تک‌منجنیقی).** Play: کاروسل همچنان رندر می‌شود، تغییر انواع همچنان انیمیشن دارد، شلیک کردن اعداد را کم می‌کند و خالی شدن یک نوع دکمه را خاکستری (غیرفعال) می‌کند. (رفتار PerCatapult در چک‌لیست دستی فاز ۵ تأیید می‌شود.)

* \[ \] **مرحله ۶: Stage و پیشنهاد کامیت**

```
git add Assets/Scripts/UI/ProjectileCarouselUI.cs

```

پیام پیشنهادی: `refactor(ui): carousel follows active catapult's ammo pool, dictionary-based button types`

## فاز ۴ — رابط کاربری: فلش‌ها

### تسک 4.1: پریفب دکمه فلش

**فایل‌ها:**

* ایجاد (در ادیتور): `Assets/Prefabs/UI/CatapultArrowButton.prefab`

**از `ProjectileTypeButton.prefab` استفاده مجدد نکنید** — آن یک دکمه ۴۰۰×۴۰۰ دارای آیکون/نام/مهمات است؛ فلش‌ها باید کوچک و ساده باشند.

**از `<` / `>` استفاده کنید — نه `‹` / `›` و نه `◀` / `▶`.** (پیش‌نویس قدیمی این برنامه در یک نمودار `‹`/`›` و در قانون `<`/`>` داشت — `<`/`>` قانون است.) دارایی فونت TMP پروژه یعنی `LiberationSans SDF` شامل کاراکترهای ASCII `<` (`0x3C`) و `>` (`0x3E`) است اما کاراکترهای تایپوگرافی `‹`/`›` (`0x2039`/`0x203A`) یا فلش‌های بلوکی `◀`/`▶` (`0x25C0`/`0x25B6`) را **ندارد**. کاراکتری که فونت فاقد آن باشد به صورت یک مربع توخالی رندر می‌شود. (اگر فلش واقعی می‌خواهید: یکی از PNGهای فلش Kenney را دوباره به عنوان **Sprite (2D and UI)** ایمپورت کنید — PNGهای فعلی Kenney به عنوان تکسچر ساده ایمپورت شده‌اند و بدون ایمپورت مجدد در جایگاه `Image` در uGUI قرار نمی‌گیرند.)

* \[ \] **مرحله ۱:** `GameObject ▸ UI ▸ Button (TextMeshPro)` (ممکن است درخواست ایمپورت ملزومات TMP را بدهد — پروژه از قبل آن‌ها را دارد، پس نباید ظاهر شود). نام آن را `CatapultArrowButton` بگذارید. اندازه **۹۶×۹۶** (عرض/ارتفاع در `RectTransform`).

* \[ \] **مرحله ۲:** `Image.Source Image` را خالی بگذارید — یک `Button` در uGUI بدون اسپرایت، مستطیل سفید داخلی را رسم می‌کند؛ یک ته‌رنگ (Tint) مایل به سفید به آن بدهید.

* \[ \] **مرحله ۳:** متن فرزند TMP را روی `<`، اندازه فونت حدود ۴۸ و ترازبندی را مرکز/وسط (center/middle) تنظیم کنید.

* \[ \] **مرحله ۴:** شیء را به داخل `Assets/Prefabs/UI/` بکشید تا پریفب ساخته شود، سپس نمونه داخل صحنه را حذف کنید (تسک 4.2 نمونه‌ها را زیر جابه‌جاگر دوباره می‌سازد).

* \[ \] **مرحله ۵:** در این تسک اسکریپتی وجود ندارد — `CatapultSwitcherUI` (تسک 4.3) دکمه‌ها را در سطح صحنه سیم‌کشی می‌کند.

### تسک 4.2: شیء صحنه `CatapultSwitcher` (چیدمان)

**فایل‌ها:**

* تغییر صحنه (آن را در فاز ۵ در کنار بقیه سیم‌کشی‌ها انجام دهید — اینجا فهرست شده چون تصمیمات چیدمان متعلق به این فاز است)

```
UI (Canvas)
├── ScoreLable, Score              (موجود، بالا)
├── ProjectileButtons              (موجود، نوار پایین: لنگر در پایین، ارتفاع ۴۰۰)
└── CatapultSwitcher                (جدید — محفظه تمام-بوم)
    ├── LeftArrow    (Button + TMP "<")
    ├── RightArrow   (Button + TMP ">")
    └── Counter      (TMP "1 / 2")

```

**حقایق تأییدشده درباره بوم (Canvas) موجود** (از `SampleScene.unity`):

* `CanvasScaler`: `m_UiScaleMode: 0` (**Constant Pixel Size**)، رزولوشن مرجع ۸۰۰×۶۰۰ $\rightarrow$ **۱ واحد بوم = ۱ پیکسل واقعی**، و رابط کاربری با تغییر اندازه صفحه مقیاس نمی‌شود. فلش‌ها را به لبه‌ها لنگر (Anchor) کنید تا در هر اندازه‌ای کار کنند.

* `ProjectileButtons` نوار **۴۰۰ تا ۸۰۰ واحد بالاتر از پایین** را اشغال می‌کند (anchor min `(0,0)`، max `(1,0)`، sizeDelta `(0,400)`، anchoredPosition `(0,400)`).

**بنابراین فلش‌ها متعلق به لبه‌های چپ و راست در نیمه ارتفاع هستند** — دور از ردیف امتیاز (بالا) و نوار پرتابه‌ها (نوار پایین):

| شیء | Anchor min=max | Pivot | Anchored pos | اندازه | 
| ----- | ----- | ----- | ----- | ----- | 
| `CatapultSwitcher` | کشیده شده `(0,0)→(1,1)` | `(0.5,0.5)` | `(0,0)`، sizeDelta `(0,0)` | — | 
| `LeftArrow` | `(0, 0.5)` | `(0, 0.5)` | `(24, 0)` | ۹۶×۹۶ | 
| `RightArrow` | `(1, 0.5)` | `(1, 0.5)` | `(-24, 0)` | ۹۶×۹۶ | 
| `Counter` | `(0, 0)` | `(0, 0)` | `(24, 24)` | خودکار | 

(شمارنده `Counter` در پایین-چپ قرار می‌گیرد، زیر نوار پرتابه‌ها که از ۴۰۰ واحد بالاتر شروع می‌شود.)

**نکته آموزشی — لنگرها (Anchors) در برابر موقعیت‌ها.** *لنگر* یک نقطه نرمال‌شده روی والد است (`0,0` = پایین-چپ، `1,1` = بالا-راست)؛ *موقعیت لنگرشده (anchored position)* آفست از آن نقطه است. «لنگر `(1, 0.5)`، موقعیت لنگرشده `(-24, 0)`» = «۲۴ واحد سمت چپِ لبه راست، در نیمه راه به سمت بالا» — و *در هر اندازه صفحه‌ای همان‌جا می‌ماند*، زیرا لنگر یک نسبت است در حالی که آفست ثابت است. به لبه‌ها لنگر کنید؛ موقعیت‌ها را فقط برای چیزهایی که واقعاً در مرکز هستند ثابت نگه دارید.

* \[ \] ایجاد واقعی اشیاء را در فاز ۵، مرحله ۵ انجام دهید — اما این جدول را در آنجا مطالعه کنید.

### تسک 4.3: `CatapultSwitcherUI` — اسکریپت

**فایل‌ها:**

* ایجاد: `Assets/Scripts/UI/CatapultSwitcherUI.cs`

**واسط‌ها:**

* مصرف می‌کند: `CatapultManager.NextCatapult/PreviousCatapult/OnActiveCatapultChanged/Catapults/ActiveCatapult` (تسک 2.3).

* تولید می‌کند: هیچ‌چیز — مصرف‌کننده نهایی UI.

**نکته آموزشی — `Button.onClick` در برابر `EventTrigger`.** `onClick` ساده‌ترین راه برای واکنش به یک ضربه (Tap) است؛ `EventTrigger` جایگزین همه‌منظوره برای سایر ژست‌های حرکتی است. دو دکمه ساده $\rightarrow$ `onClick`.

* \[ \] **مرحله ۱: ایجاد اسکریپت**

```
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Left/right arrows + "current / total" counter. Wrap-around means the arrows are
// never disabled at the ends — only when there is exactly one catapult.
public class CatapultSwitcherUI : MonoBehaviour
{
    public CatapultManager manager;
    public Button leftButton;
    public Button rightButton;
    public TMP_Text counterText;

    private void OnEnable()
    {
        if (manager) manager.OnActiveCatapultChanged += HandleActiveCatapultChanged;
        if (leftButton) leftButton.onClick.AddListener(manager.PreviousCatapult);
        if (rightButton) rightButton.onClick.AddListener(manager.NextCatapult);
        Refresh();
    }

    private void OnDisable()
    {
        if (manager) manager.OnActiveCatapultChanged -= HandleActiveCatapultChanged;
        if (leftButton) leftButton.onClick.RemoveListener(manager.PreviousCatapult);
        if (rightButton) rightButton.onClick.RemoveListener(manager.NextCatapult);
    }

    private void HandleActiveCatapultChanged(Catapult catapult) => Refresh();

    private void Refresh()
    {
        if (!manager || counterText == null) return;

        int count = manager.Catapults.Count;
        int index = manager.ActiveCatapult ? manager.ActiveCatapult.Index : 0;
        counterText.text = $"{index + 1} / {count}";

        // Only a single-catapult level gets non-interactable arrows — wrap-around
        // makes the ends safe (1/3 → left → 3/3), so never grey them out mid-ring.
        bool multiple = count > 1;
        if (leftButton) leftButton.interactable = multiple;
        if (rightButton) rightButton.interactable = multiple;
    }
}

```

**توضیحات خط به خط:**

*`onClick.AddListener(manager.PreviousCatapult)`* — یک *گروه متد* که به عنوان شنونده پاس داده شده است: دکمه در هر کلیک آن را صدا می‌زند. `RemoveListener(manager.PreviousCatapult)` متناظر با *همان* گروه متد، دقیقاً همان ورودی را حذف می‌کند (این قانون جفت‌سازی مشابه `+=`/`-=` در رویدادهای C# است).

*`Refresh` در `OnEnable`* — منیجر رویداد `OnActiveCatapultChanged` را در `Awake` خودش منتشر می‌کند که **پیش** از `OnEnable` این UI اجرا می‌شود، بنابراین این کامپوننت هرگز رویداد اولیه را نمی‌بیند. تمام رابط‌های کاربری رویدادمحور در این ویژگی یک قانون یکسان دارند: *هنگام فعال شدن یک بار تازه‌سازی کن، سپس برای تغییرات به رویدادها اعتماد کن* (`Start` $\rightarrow$ `LoadProjectile` لانچر همان الگو از بخش «تله ترتیب چرخه حیات» است).

*چرا محافظ null برای `manager` وجود دارد اما در شنونده‌ها محافظ null برای `manager.ActiveCatapult` وجود ندارد:* `NextCatapult`/`PreviousCatapult` از قبل محافظت شده‌اند (`if (ActiveCatapult == null) return;`) — منیجر مالک آن ناوردایی (Invariant) است؛ UI آن را تکرار نمی‌کند.

* \[ \] **مرحله ۲: Stage و پیشنهاد کامیت**

```
git add Assets/Scripts/UI/CatapultSwitcherUI.cs Assets/Scripts/UI/CatapultSwitcherUI.cs.meta Assets/Prefabs/UI/CatapultArrowButton.prefab Assets/Prefabs/UI/CatapultArrowButton.prefab.meta

```

پیام پیشنهادی: `feat(ui): catapult switcher arrows with wrap-around and counter`

## فاز ۵ — سیم‌کشی صحنه و پریفب (مراحل ادیتور، به همین ترتیب)

تمام کارهای ادیتور در یک نشست. ترتیب کار در دو جا اهمیت دارد و هر دو یک درس مشترک دارند: **یک سلسله‌مراتب تکثیرشده، کامپوننت‌ها و ارجاعات داخلی خود را تکثیر می‌کند، اما ارجاعات خارجی خود را حفظ می‌کند.**

### مرحله ۱ — انتقال دو کامپوننت سطح مرحله به خارج از منجنیق

1. `ProjectileSelector` را روی **سر (head)** منجنیق (فرزند `catapult`) انتخاب کنید $\rightarrow$ `Ctrl+X` (برش) $\rightarrow$ GameObject خالی جدید `CatapultManager` را انتخاب کنید $\rightarrow$ `Ctrl+V`. یونیتی کامپوننت و GUID اسکریپت آن را حفظ می‌کند.

2. `InputController` را به همین روش از ریشه منجنیق به GameObject `CatapultManager` منتقل کنید.

3. **ابتدا** GameObject خالی `CatapultManager` را بسازید (در ریشه صحنه، بدون والد)، کامپوننت `CatapultManager` را به آن اضافه کنید و `levelData` (`DefaultLevelData`) را تخصیص دهید.

4. ارجاعاتی را که قبلاً به سلکتورِ سر منجنیق اشاره می‌کردند، دوباره تنظیم کنید:

   * `ProjectileLuncher.projectileSelector` $\rightarrow$ سلکتورِ منیجر

   * `ProjectileSpawner.projectileSelector` $\rightarrow$ سلکتورِ منیجر

   * `AmmoManager.projectileSelector` $\rightarrow$ سلکتورِ منیجر

   * `ProjectileCarouselUI.selector` $\rightarrow$ سلکتورِ منیجر

> **این کار را پیش از تکثیر منجنیق انجام دهید.** اگر ابتدا تکثیر کنید، کپی با `ProjectileSelector` خودش می‌آید؛ اگر ابتدا جابه‌جا کنید، ارجاعات نسخه تکثیرشده به تنها سلکتور سطح منیجر اشاره می‌کنند.

### مرحله ۲ — ساخت ریگ دوربین

1. یک شیء خالی به نام `CameraRig` در ریشه صحنه (**هم‌تراز / Sibling** منجنیق، نه فرزند آن) بسازید.

2. `Main Camera` را به داخل `CameraRig` بکشید تا فرزند ریگ شود.

3. موقعیت **محلی** دوربین را `(0,0,0)` و چرخش **محلی** آن را یکه (Identity) قرار دهید. اکنون ترنسفورم خود ریگ وضعیت جهانی دوربین است (این همان چیزی است که به `CameraShakeManager` اجازه می‌دهد بدون دستکاری به لرزش در فضای محلی ادامه دهد).

4. کامپوننت `CatapultCameraRig` را به `CameraRig` اضافه کنید؛ `moveDuration` را حدود `1.2` قرار دهید.

5. یک فرزند خالی به نام `CameraAnchor` زیر **ریشه** منجنیق بسازید، کامپوننت `CameraAnchor` را به آن اضافه کنید و آن را در وضعیتی قرار دهید که دوربین قبلاً داشت: دوربین قدیمی فرزند ریشه منجنیق در موقعیت محلی `(3.59, 2.93, 0)` و زوایای اویلر محلی `(8.71, -90, 0)` بود — این اعداد را کپی کنید تا نمای اولین منجنیق بدون تغییر بماند.

### مرحله ۳ — اضافه کردن کامپوننت `Catapult` و سیم‌کشی منیجر

1. کامپوننت `Catapult` را به **ریشه** منجنیق اضافه کنید؛ `launcher`، `spawner`، `ammoManager` (سر) و `cameraAnchor` را تخصیص دهید. اختیاری: اگر شیء Highlight دارید، `highlightObject` را تخصیص دهید.

2. روی شیء `CatapultManager`: فیلدهای `selector` (همان که منتقل شد) و `cameraRig` را تخصیص دهید. متد `Awake` آن بقیه کارها را انجام می‌دهد (وضعیت‌های سکون، مخازن، `selector.Initialize`، اولین `SetActive` و `SnapTo`).

### مرحله ۴ — تکثیر منجنیق

1. ریشه منجنیق را انتخاب کنید $\rightarrow$ `Ctrl+D`. کپی را جابه‌جا کنید (مثلاً به سمت دیگر میدان) و چرخش (Yaw) متفاوتی به آن بدهید.

2. روی **کپی**: از قبل دارای کامپوننت `Catapult` (تکثیرشده) است — بررسی کنید که `launcher`/`spawner`/`ammoManager` آن به کامپوننت‌های **خود کپی** اشاره کنند (یونیتی باید این کار را کرده باشد — بررسی کنید)، و `cameraAnchor`… فرزند تکثیرشده `CameraAnchor` هم همراه آن می‌آید؛ آن را نگه دارید یا تغییر وضعیت دهید — هر منجنیق می‌تواند وضعیت خودش را داشته باشد.

3. ۲ تا ۳ فرزند خالی به `CameraAnchor` کپی اضافه کنید، آن‌ها را در یک کمان بین دو منجنیق قرار دهید و به لیست `pathPoints` آن بکشید — این کار لغزش Catmull-Rom را به کار می‌اندازد. (برای تست لغزش مستقیم، آن‌ها را حذف کنید.)

### مرحله ۵ — فلش‌ها

1. `CatapultArrowButton.prefab` را دو بار زیر یک شیء تمام-بوم جدید به نام `CatapultSwitcher` زیر بوم `UI` نمونه‌سازی کنید، با استفاده از جدول چیدمان تسک 4.2 (چپ/راست، ۹۶×۹۶، لنگرهای لبه)؛ نام آن‌ها را به `LeftArrow` / `RightArrow` تغییر دهید و کاراکترهای `<` / `>` را تنظیم کنید.

2. یک متن TMP به نام `Counter` طبق جدول اضافه کنید.

3. کامپوننت `CatapultSwitcherUI` را به `CatapultSwitcher` اضافه کنید؛ `manager`، `leftButton`، `rightButton` و `counterText` را سیم‌کشی کنید.

4. روی شیء `ProjectileButtons` (`ProjectileCarouselUI`): فیلد `catapultManager` را تخصیص دهید.

5. روی `InputController` منتقل‌شده (که اکنون روی `CatapultManager` است): فیلد `catapultManager` را تخصیص دهید.

6. `DefaultLevelData.ammoMode` را به دلخواه تنظیم کنید (ابتدا `Shared`، و برای تست حالت دیگر `PerCatapult`).

### مرحله ۶ — ذخیره مجدد و بررسی Console

`SampleScene` را باز کنید، یک تغییر جزئی بدهید، `Ctrl+S`. سپس Console را بررسی کنید: بدون استثنا، بدون هشدار ارجاع مفقود. پیش از کامیت کردن هر چیزی، یک بار Play را بزنید.

## فاز ۶ — همگام‌سازی GDD (الزامی طبق `CLAUDE.md`)

`docs/GDD.md` — سرتیترهای انگلیسی، بدنه فارسی، نشان‌های (Badges) صادقانه. **فقط پس از** اینکه تمام موارد بالا واقعاً پیاده‌سازی و آماده شدند، به‌روزرسانی کنید:

1. **§7 Multi-Catapult Levels** — نشان `[Planned]` $\rightarrow$ `[Implemented]`. بولت «انتخاب منجنیق» را بازنویسی کنید: جابه‌جایی از طریق **فلش‌های سمت چپ و راست صفحه** (به صورت چرخشی) انجام می‌شود، نه ضربه زدن روی منجنیق (ضربه زدن به `[Future]` تبدیل می‌شود). رفتار دوربین (لغزش به سمت منجنیق فعال، مستقیم یا در امتداد مسیر طراح؛ سپس دنبال کردن نرم نشانه با تأخیر) و حالت‌های مهمات (اشتراکی در برابر به‌ازای هر منجنیق، قابل تنظیم برای هر مرحله) را اضافه کنید. خط **«محدودیت تیر: سقف کل مرحله است (نه به‌ازای هر منجنیق)»** باید **بازنویسی** شود، نه اصلاح جزئی: با `ammoMode` سقف مهمات می‌تواند در سطح کل مرحله (`Shared`) یا به‌ازای هر منجنیق (`PerCatapult`) باشد و مرحله تصمیم می‌گیرد؛ جمله قدیمی به عنوان حالت پیش‌فرض (`Shared`) همچنان صادق است.

2. **§5.2 مدیریت تیر** — یک بولت برای حالت اشتراکی/به‌ازای هر منجنیق اضافه کنید؛ بولتی که مخزن را «جدا برای هر پرابه» نامیده اصلاح کنید — آن مورد به‌ازای هر *نوع* است (که همچنان صادق است)، محور جدید به‌ازای هر *منجنیق* است. این دو مفهوم را از هم متمایز نگه دارید.

3. **§3 Controls & Camera** — ادعای پشتیبانی از ماوس پس از اعمال تسک 3.5 حقیقت پیدا می‌کند؛ بخش دوربین را از «دنبال‌کردن اکشن» به ریگ + نقطه اتکا + مسیر اختیاری + **دنبال کردن نرم نشانه با تأخیر قابل تنظیم** به‌روز کنید.

4. **§9 Scoring** — پاداش مهمات باقی‌مانده هنوز به پایان مرحله متصل نشده است (`[Partial]` باقی می‌ماند)؛ یادداشتی اضافه کنید که با مخازن به‌ازای هر منجنیق، پاداش باید **در میان تمام مخازن جمع زده شود**. آن را به عنوان پیاده‌سازی‌شده علامت نزنید.

5. **§10 Meta & UI** — یک ردیف برای جابه‌جاگر منجنیق (فلش‌ها + شمارنده `n / m`) اضافه کنید و تنها زمانی که واقعاً در بیلد قرار گرفت، آن را `[Implemented]` علامت بزنید.

6. **§2 Core Loop** — گام اول «انتخاب منجنیق (در سطوح چندمنجنیقی)» اکنون واقعی است؛ عبارت را حفظ کنید اما به عنوان جابه‌جایی با فلش به جای ضربه زدن.

## بررسی و تأیید (Verification)

### خودکار (EditMode)

`Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All`. سبز شدن مورد انتظار: سه فایل جدید (`RingIndexTests` با ۶ تست، `AmmoPoolTests` با ۹ تست، `CameraPathTests` با ۸ تست) به علاوه فایل‌های موجود `CarouselWindowTests`، `CarouselLayoutCalculatorTests` و `LeftoverScoreCalculatorTests`.

### دستی (ادیتور، نمای Game) — پس از فاز ۵

 1. **تک‌منجنیق همچنان درست رفتار می‌کند** — در حالی که فقط منجنیق A فعال است Play را بزنید. نشانه گرفتن، شلیک، کاروسل: مانند قبل. دوربین در همان *مکان* قبلی قرار دارد اما اکنون پشت سر نشانه *تأخیر* دارد (تغییر مورد نظر از تسک 2.6).

 2. **جابه‌جایی، با چرخش (wrap-around)** — فلش راست: شمارنده `1 / 2 → 2 / 2`، دوربین به پشت B می‌لغزد. از آخرین منجنیق، راست به اولی می‌رود؛ چپ در جهت عکس می‌چرخد. فلش‌ها فقط زمانی غیرقابل کلیک هستند که دقیقاً یک منجنیق وجود داشته باشد.

 3. **مستقیم در برابر مسیر** — وقتی `pathPoints` منجنیق B پر است، جابه‌جایی به B کمان را دنبال می‌کند و به وضعیت نقطه اتکای B می‌رسد. نقاط را حذف کنید، جابه‌جا شوید و مستقیم بودن حرکت را تأیید کنید.

 4. **مهمات — اشتراکی (Shared)** (پیش‌فرض): یک نوع را روی A خالی کنید، به B بروید — همان نوع برای B نیز `0` نشان می‌دهد؛ تعدادها یک مخزن واحد هستند.

 5. **مهمات — به‌ازای هر منجنیق (PerCatapult)**: مقدار `DefaultLevelData.ammoMode = PerCatapult` را تنظیم کنید. چوب را روی A خالی کنید، به B بروید — B همچنان تعداد کامل را نشان می‌دهد. هر دو به طور مستقل شلیک می‌کنند.

 6. **انتخاب سراسری نوع** — تخته سنگ (Boulder) را در کاروسل انتخاب کنید؛ هر دو منجنیق تخته سنگ بارگذاری می‌کنند (غیرفعال از طریق `OnSelectionChanged` پیش‌بارگذاری می‌کند؛ فعال قطعاً از طریق `LoadProjectile` یکی دارد).

 7. **دنبال کردن نرم نشانه** — به صورت افقی بکشید: منجنیق بلافاصله می‌چرخد، دوربین با تأخیر مشهود پشت سر آن می‌چرخد و سپس جای می‌گیرد. مقدار `followSharpness` را تنظیم کنید (`1` $\approx$ شناور، `8` $\approx$ تقریباً صلب، `3` نقطه شروع است). رها کنید: دوربین همراه با بازگشت سر منجنیق به جای اول برمی‌گردد.

 8. **کلیک‌های UI نشانه نمی‌گیرند** — روی فلش راست ضربه بزنید و کمی بکشید: منجنیق **نباید** بچرخد. روی دکمه کاروسل ضربه بزنید: باز هم چرخشی رخ نمی‌دهد. (محافظ `IsPointerOverGameObject`.)

 9. **جابه‌جایی در میانه کشیدن** — کشیدن را روی A شروع کنید، هنگام نگه داشتن روی فلش کلیک کنید (چندلمسی یا دکمه دوم ماوس): جابه‌جایی تمیز، بدون استثنا، و کشیدن رها می‌شود.

10. **جابه‌جایی سریع** — سه بار سریع روی فلش کلیک کنید: دوربین هر بار از وضعیت *فعلی* خود هدف‌گیری مجدد می‌کند (بدون پرش به عقب) و شمارنده روی عدد درست متوقف می‌شود.

11. **منجنیق غیرفعال با مخزن خالی برای آن نوع** — حالت PerCatapult، چوب را روی A خالی کنید، به B بروید: B یک چوب بارگذاری می‌کند. چوب را روی B هم خالی کنید، به A برگردید: هیچ پرتابه‌ای روی هیچ‌کدام نیست، **بدون هیچ استثنایی** (اسپاونر null برمی‌گرداند؛ لانچر چیزی نگه نمی‌دارد).

## کامیت

طبق قانون همیشگی شما، هیچ‌چیز به صورت خودکار کامیت نمی‌شود. فایل‌ها را Stage کنید و پیام زیر را بازبینی نمایید:

```
feat(catapult): add multi-catapult support with camera rig and per-level ammo modes

```

موارد مورد انتظار در آن: اسکریپت‌های جدید (`Catapult/`، `Camera/`، `CatapultSwitcherUI`، تست‌ها)، اسکریپت‌های تغییر یافته (`AmmoManager`، `Projectile`، `ProjectileSpawner`، `ProjectileLuncher`، `InputController`، `ProjectileCarouselUI`، `SO_LevelData`)، `SampleScene.unity`، `CatapultArrowButton.prefab`، `DefaultLevelData.asset` (ammoMode)، `docs/GDD.md` و همین برنامه.

> **دوباره درباره `SampleScene.unity`:** حدود ۱۳,۰۰۰ خط YAML؛ یونیتی بخش‌های بزرگی از آن را هنگام ذخیره بازنویسی می‌کند. آن را فقط پس از ذخیره از ادیتور کامیت کنید و ابتدا `git diff --stat` را بررسی کنید — انتظار «یک جابه‌جایی کامپوننت + چند شیء جدید» را داشته باشید، نه یک بازنویسی کلی.

## پیوست الف — برگه تقلب مفاهیم (Concept cheat sheet)

| مفهوم | دلیل حضور در اینجا | 
| ----- | ----- | 
| **تعریف اسمبلی (`asmdef`)** | `CastleCrusher.Core` ریاضیات محض بدون وابستگی به صحنه را نگه می‌دارد و در EditMode قابل تست است. یک asmdef **نمی‌تواند به `Assembly-CSharp` ارجاع دهد** — از این رو `AmmoPool<TKey>` جنریک است. اما *می‌تواند* از `Vector3`/`Mathf` استفاده کند (`noEngineReferences: false`). | 
| **جنریک‌ها (Generics)** | `AmmoPool<TKey>` هسته Core را از تایپ‌های بازی آزاد نگه می‌دارد در حالی که در محل فراخوانی کاملاً تایپ‌شده (Typed) باقی می‌ماند. | 
| **ترتیب چرخه حیات یونیتی** | `Awake` $\rightarrow$ `OnEnable` $\rightarrow$ `Start`؛ تمام `Awake`ها پیش از تمام `Start`ها؛ ترتیب نسبی دو `Awake` نامشخص است. هرگز به آن وابسته نشوید — داده‌ها را صریحاً تحویل دهید (`CaptureRestPose`، `AttachPool`، `LoadProjectile` در `Start`). | 
| **رویدادها (`Action<T>`)** | منیجر اعلام می‌کند «منجنیق فعال تغییر کرد»؛ دوربین و UI مشترک می‌شوند. وابستگی سست (Loose coupling). | 
| **Coroutine (`IEnumerator`)** | روش ایمن در برابر نخ اصلی برای انیمیشن در طول زمان. `async/await` به طور پیش‌فرض روی یک نخ استخر نخ‌ها ادامه می‌یابد. | 
| **`Quaternion.Slerp`** | درون‌یابی صحیح چرخش (کوتاه‌ترین کمان). `Vector3.Lerp` روی زوایای اویلر این‌طور نیست. | 
| **پارامتری‌سازی طول کمان** | «پیشرفت» = مسافت طی‌شده، بنابراین دوربین با سرعت ثابت حرکت می‌کند. `PolylinePath`. | 
| **ترنسفورم محلی در برابر جهانی** | `localPosition/localRotation` نسبت به والد هستند؛ `position/rotation` جهانی هستند. نقطه اتکا به صورت محلی طراحی می‌شود و در برابر قاب *سکون* منجنیق ارزیابی می‌گردد؛ دنبال کردن نشانه یک تأخیر عمدی بر روی آن است. | 
| **`FindObjectsByType` / `FindAnyObjectByType`** | جایگزین‌های یونیتی ۶ برای متدهای منسوخ‌شده `FindObjectsOfType`/`FindObjectOfType`. | 
| **`[FormerlySerializedAs]`** | داده‌های صحنه را در برابر تغییر نام فیلد زنده نگه می‌دارد. جایگزین: ذخیره مجدد صحنه. | 
| **enum در ScriptableObject** | منوی کشویی برای طراح، سریال‌سازی‌شده به عنوان int؛ اولین مقدار همان چیزی است که دارایی‌های قدیمی حفظ می‌کنند (`Shared`). | 
| **`Button` + `onClick` در uGUI** | واکنش استاندارد به ضربه. `AddListener`/`RemoveListener` را مانند `+=`/`-=` جفت کنید. | 
| **`EventSystem.current.IsPointerOverGameObject()`** | تنها راه مطمئن برای فهمیدن اینکه فشار روی UI فرود آمده است — `Input` قدیمی و uGUI هرگز با هم صحبت نمی‌کنند. | 
| **ترتیب `LateUpdate`** | تمام `Update`ها پیش از تمام `LateUpdate`ها؛ دوربین‌ها وضعیت نهایی فریم را در آنجا می‌خوانند. | 
| **نرم‌سازی مستقل از نرخ فریم** | $1 - \exp(-k \cdot dt)$ کسر یکسانی در *ثانیه* را صرف‌نظر از fps طی می‌کند. هرگز حس بازی را با یک ضریب خام به‌ازای هر فریم تنظیم نکنید. | 
| **`Quaternion.Inverse`** | «تفریق» چرخش: `current * Inverse(rest)` = «چقدر نسبت به وضعیت سکون چرخیده است». | 
| **تابع محلی (Local function)** | متدی که درون متد دیگری تعریف می‌شود (`CountFor` در `CatapultManager`)؛ متغیرهای محلی را می‌گیرد و سطح کلاس را کوچک نگه می‌دارد. | 
| **گروه متد (Method group)** | نام متدی که به عنوان مقدار نماینده (Delegate) استفاده می‌شود (`ammo.HasAmmo` به عنوان `Func<SO_ProjectileType,bool>`)؛ تصمیم «مهمات چه کسی به حساب می‌آید؟» را به فراخوانی‌کننده واگذار می‌کند. | 
| **`TryGetValue` / `TryGetComponent`** | جستجوی ایمن + مقدار در یک عبارت؛ جایگزین الگوهای پرتاب استثنای ایندکسر و بررسی null. | 
| **لنگرها در برابر موقعیت لنگرشده (uGUI)** | لنگر = نسبتی از والد (چسبیده به لبه)؛ موقعیت لنگرشده = آفست ثابت از آن. | 

## پیوست ب — اسپلاین از صفر — اول مفهوم، بعد کد

(استخراج اسپلاین از پیش‌نویس قبلی، بدون تغییر حفظ شده است — توضیح می‌دهد *چرا* `CatmullRom.Sample` و `PolylinePath` به این شکل هستند.)

## ۱. مسئله: ما فقط چند نقطه داریم، نه منحنی

کامپیوتر نمی‌تواند «یک منحنی» را ذخیره کند. چیزی که ما داریم چند **نقطه** است (waypoint های مسیر دوربین). پس سؤال اصلی این است:

> تابعی بساز که به ازای هر عدد `t` بین ۰ و ۱، یک «مکان روی مسیر» برگرداند — طوری که در `t=0` روی نقطه اول باشیم، در `t=1` روی نقطه آخر، و بین این دو، مسیر **نرم** باشد.

به این کار **درون‌یابی (interpolation)** می‌گویند: ساختن مقادیر میانی از روی مقادیر معلوم.

## ۲. پایه‌ای‌ترین ابزار: تابع پارامتریک و Lerp

ساده‌ترین حالت — حرکت خطی بین دو نقطه `A` و `B`:

$$
q(t) = (1-t)\cdot A + t\cdot B
$$

مثال عددی: $A=(0,0)$، $B=(10,0)$، $t=0.25 \rightarrow q=(2.5, 0)$.

* `t` را مثل یک **دستگاه زمان** ببین: از ۰ تا ۱ می‌چرخانی، و خروجی `q(t)` نقطه‌ای است که روی صفحه جابه‌جا می‌شود.

* چرا فرمول این شکلی است؟ در `t=0` وزنِ `A` یک و وزنِ `B` صفر است $\rightarrow$ دقیقاً روی `A`. در `t=1` برعکس. بین این دو، وزن‌ها کم‌کم جابه‌جا می‌شوند.

* `Vector3.Lerp` یونیتی دقیقاً همین تابع است.

نکته کلیدی: `q` تابعی است که به جای عدد، **مکان** برمی‌گرداند. به چنین توابعی «پارامتریک» می‌گویند و همه‌ی اسپلاین‌ها همین شکل‌اند: **یک dial به نام t، که هر جایش یک نقطه تحویل می‌دهد.**

## ۳. مشکل: چند نقطه = زیگزاگ

اگر چند نقطه را با Lerp به هم وصل کنیم، مسیر شکسته می‌شود و دوربین در هر گوشه ناگهان می‌پیچد.

راه‌حل اولی که به ذهن می‌رسد: *یک* چندجمله‌ای بزرگ که از همه نقاط رد شود. مشکلش این است که چندجمله‌ایِ درجه‌بالا بین نقاط **نوسان** می‌کند — دوربین وسط راه اوج می‌گیرد و پایین می‌آید، در حالی که نقاطت اصلاً این را نمی‌گویند.

راه درست (که قرن‌هاست مهندسان استفاده می‌کنند): **قطعه‌قطعه ساختن (piecewise)**. بین هر دو نقطه‌ی مجاور یک «قطعه»ی کوچک می‌سازیم و قطعه‌ها را در محل اتصال نرم به هم می‌دوزیم. اسم انگلیسیش هم از همین‌جا آمده: طراحان نقشه‌کشی خط‌کش چوبی انعطاف‌پذیری داشتند به نام spline که زیرش وزنه می‌گذاشتند تا از نقاط دلخواه عبور کند — خود خط‌کش به‌طور طبیعی منحنی نرم می‌گرفت.

## ۴. راز نرمی: در محل اتصال چه شرطی لازم است؟

دو قطعه داریم: قطعه ۱ به نقطه `P` می‌رسد و قطعه ۲ از `P` شروع می‌شود. دو سطح کیفیت:

* **اتصال موقعیتی (**$C^0$**):** هر دو دقیقاً از `P` رد شوند — منحنی «پاره» نیست. کافی نیست! هنوز گوشه تیز داریم.

* **اتصال جهت‌دار (**$C^1$**):** **جهت حرکت** هنگام رسیدن به `P`، با جهت حرکت هنگام ترک `P` یکی باشد. مثل رانندگی: فرمان ناگهان نمی‌چرخد.

یک تعریف ساده لازم داریم: **تانجانت (tangent)** = بردار «سرعت» نقطه متحرک در آن لحظه — یعنی به چه سمتی و با چه تمایلی دارد می‌رود. (این همان مفهوم «مشتق» است؛ فعلاً فقط همین تصویرِ سرعت را نگه دار.)

هدف ما: منحنی‌ای که همه اتصال‌هایش $C^1$ باشد. **Catmull-Rom دقیقاً همین است.**

## ۵. ساختن یک قطعه: قرارداد هرمیت

حالا فقط بین دو نقطه `p1` و `p2`. چند «خواسته» داریم؟

1. از `p1` شروع شود

2. به `p2` برسد

3. با سرعت اولیه `m1` راه بیفتد (تانجانت شروع)

4. با سرعت `m2` برسد (تانجانت پایان)

چندجمله‌ای درجه ۳ این شکلی است: $q(t) = a\cdot t^3 + b\cdot t^2 + c\cdot t + d$ — دقیقاً **۴ ضریب مجهول برای ۴ خواسته**. این تطابق عددی است که «مکعبی» بودن را انتخاب می‌کند:

* خط (درجه ۱): ۲ مجهول $\rightarrow$ فقط دو سر را می‌تواند تنظیم کند، اصلاً نمی‌خمد.

* درجه ۲: ۳ مجهول $\rightarrow$ کم می‌آورد.

* درجه ۳: ۴ مجهول $\rightarrow$ نه کم، نه زیاد.

اگر دستگاه ۴ معادله را حل کنی (الان بی‌اثبات قبول کن)، به فرم **هرمیت** می‌رسی:

$$
q(t) = h_{00}(t)\cdot p_1 + h_{10}(t)\cdot m_1 + h_{01}(t)\cdot p_2 + h_{11}(t)\cdot m_2
$$

شهودِ واقعی این‌جاست — هر `h` یک چندجمله‌ای کوچک است که مثل «وزن» عمل می‌کند:

| تابع | رفتارش | نقشش | 
| ----- | ----- | ----- | 
| $h_{00} = 2t^3 - 3t^2 + 1$ | از ۱ شروع، به ۰ می‌رسد | وزنِ نقطه شروع که کم‌کم **محو** می‌شود | 
| $h_{01} = -2t^3 + 3t^2$ | از ۰ به ۱ می‌رسد | وزنِ نقطه پایان که کم‌کم **ظاهر** می‌شود | 
| $h_{10} = t^3 - 2t^2 + t$ | خودش صفر است ولی شیبش در $t=0$ برابر ۱ است | «ضربه اولیه» سرعت `m1` | 
| $h_{11} = t^3 - t^2$ | خودش صفر است ولی شیبش در $t=1$ برابر ۱ است | اثر سرعت `m2` موقع رسیدن | 

یعنی منحنی = مخلوط وزن‌دارِ چهار اثر: *«کجا شروع کردم، به کجا می‌روم، چطور راه افتادم، چطور می‌رسم.»*

## ۶. تانجانت‌ها را از کجا بیاوریم؟ ترفند Catmull-Rom

هرمیت می‌گوید «تانجانت بده»، ولی ما فقط **نقطه** داریم. Catmull-Rom یک قاعده ساده دارد: تانجانت هر نقطه = جهت از نقطه‌ی قبل به نقطه‌ی بعد، نصف‌شده:

$$
m = \frac{p_{\text{next}} - p_{\text{prev}}}{2}
$$

چرا این منطقی است؟ در نقطه `p1`، جهتِ ورود $(p_1 - p_0)$ و جهتِ خروج $(p_2 - p_1)$ است. میانگین‌شان:

$$
\frac{(p_1 - p_0) + (p_2 - p_1)}{2} = \frac{p_2 - p_0}{2}
$$

و این دقیقاً همان چیزی است که $C^1$ را «مجانی» می‌کند: قطعه‌ی چپِ `p1` سرعتِ پایانش را از همین میانگین می‌گیرد، قطعه‌ی راست سرعتِ شروعش را از *همان* میانگین $\rightarrow$ هر دو یک جهت $\rightarrow$ پیوستگی برقرار، بدون هیچ هماهنگی اضافه.

دو انتها که همسایه ندارند: نقطه اول را دوبار حساب می‌کنیم ($p_0 = p_1$) و نقطه آخر را هم ($p_3 = p_2$) — منحنی ته‌وبته می‌ماند و از نقاط واقعی بیرون نمی‌زند. (در کد: `points[Mathf.Max(0, i-1)]` و `points[Mathf.Min(count-1, i+2)]`.)

## ۷. رساندن هرمیت به فرمول معروف

حالا در فرمول هرمیت، جای `m1` و `m2` همین انتخاب Catmull-Rom را بگذار و جمله‌ها را بر حسب `p0..p3` جمع کن. حاصل، همان فرمول پلن است:

$$
q(t) = 0.5 \cdot \left( 2p_1 + (-p_0 + p_2)t + (2p_0 - 5p_1 + 4p_2 - p_3)t^2 + (-p_0 + 3p_1 - 3p_2 + p_3)t^3 \right)
$$

خواندنش حالا این است:

* جمله $2p_1$ (بدون $t$): «از کجا شروع می‌کنم»

* جمله $t$: «با چه سرعت اولیه»

* جمله‌های $t^2$ و $t^3$: تصحیح‌هایی که کاری می‌کنند دقیقاً به `p2` برسی *و* با سرعت درستِ `m2` برسی.

محک خودت (این را واقعاً انجام بده، دو دقیقه است): در فرمول $t=0$ بگذار — همه جملات جز $0.5 \cdot 2p_1$ صفر می‌شوند $\rightarrow q(0) = p_1$ ✅. بعد $t=1$ بگذار و ساده کن $\rightarrow p_2$ درمی‌آید. اگر این دو تا دستت آمد، فرمول را فهمیده‌ای — و دقیقاً همین دو مورد را در `Sample_StartsAtFirstPoint_EndsAtLastPoint` تست کرده‌ایم.

## ۸. از فرمول به کامپیوتر: نمونه‌برداری

فرمول بی‌نهایت نقطه تولید می‌کند؛ ما نمی‌توانیم بی‌نهایت نقطه داشته باشیم. راه‌حل: `t` را در گام‌های ریز (مثلاً ۲۰ بار در هر قطعه) ارزیابی می‌کنیم و نقاط را با پاره‌خط‌های خیلی کوتاه به هم وصل می‌کنیم — چشم فرقش را نمی‌بیند.

این دقیقاً کار `CatmullRom.Sample(points, samplesPerSegment)` است: برای هر قطعه، `samplesPerSegment` نقطه می‌سازد. خروجی یک **چندخطی (polyline)** است: لیست مرتبی از نقاط که پشت‌سرهم وصل‌اند.

## ۹. تله مهم: t = سرعت نیست! (طول کمان / arc length)

قدم‌های برابر در `t`، فاصله‌های برابر روی منحنی **نمی‌دهند** — «سرعت» نقطه متحرک در طول قطعه ثابت نیست؛ جاهایی که منحنی کشیده است، سریع‌تر رد می‌شود.

اثر عملی برای ما: اگر دوربین با `t` یکنواخت حرکت کند، وسط مسیر تند و ته مسیر کند می‌شود — شتابِ بی‌دلیل.

راه‌حل: **اندازه بگیر.**

1. منحنی را به پاره‌خط‌های ریز تبدیل کن (همان خروجی `Sample`).

2. طول هر پاره‌خط را حساب کن و **جدول فاصله تجمعی** بساز. مثلاً برای نقاط $A \rightarrow B \rightarrow C$ که $|AB|=3$ و $|BC|=4$: جدول `[0, 3, 7]` — یعنی «نقطه i چند واحد از مبدا دور است».

3. حالا پیشروی را با «متر» تعریف کن، نه با `t`: وقتی گفتی ۵۰٪ مسیر، یعنی «نصفِ طول کل واحد رفته‌ام». در جدول پیدا کن این مقدار بین کدام دو ورودی می‌افتد، و داخل همان پاره‌خط یک Lerp کوچک بزن.

نتیجه: حرکت با **سرعت ثابت**. این دقیقاً کار `PolylinePath` است — سازنده‌اش جدول را می‌سازد و `Evaluate(t)` همان جست‌وجو در جدول است. و حرکت مستقیم؟ همان چیزی است با دو نقطه — برای همین دوربین فقط یک مسیر کد دارد: *بساز، بعد قدم بزن.*

## ۱۰. پل به کد

| مفهوم ریاضی | کجا می‌نشیند | 
| ----- | ----- | 
| تابع پارامتریک $q(t)$ | `PolylinePath.Evaluate(float t)` | 
| فرمول مکعبی روی ۴ نقطه | بدنه داخلی `CatmullRom.Sample` | 
| تکرار انتها برای $p_0$/$p_3$ | `points[Mathf.Max(0, i−1)]` و `points[Mathf.Min(count−1, i+2)]` | 
| نمونه‌برداری | حلقه روی `t` داخل `Sample` | 
| جدول فاصله تجمعی | سازنده `PolylinePath` | 
| «چند واحد رفته‌ام» $\rightarrow$ کدام پاره‌خط | `Evaluate` | 
