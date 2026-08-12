# Development Setup

هذا الدليل يشغّل Portfolio Demo كاملًا على الجهاز. لا يحتاج Azure أو Neon أو Cloudinary أو بوابة دفع أو مزود بريد.
يحتاج التشغيل الأول إلى الإنترنت لتنزيل Docker images وحزم .NET/npm وتحديث تعريفات ClamAV فقط.

## الخدمات

| Service | Local endpoint | Purpose |
|---|---|---|
| PostgreSQL | `127.0.0.1:54329` | قاعدة بيانات الديمو المحلية |
| Redis | `127.0.0.1:6380` | cache, rate-limit، وتجارب SignalR |
| MinIO API | `http://127.0.0.1:9100` | S3-compatible object storage |
| MinIO Console | `http://127.0.0.1:9101` | إدارة التخزين المحلي |
| Mailpit SMTP | `127.0.0.1:1026` | التقاط البريد بدل إرساله خارجيًا |
| Mailpit UI | `http://127.0.0.1:8026` | عرض رسائل البريد المحلية |
| ClamAV | `127.0.0.1:3311` | فحص الملفات المرفوعة |
| Dorosak API | `http://127.0.0.1:5053` | ASP.NET Core API عند تشغيل profile التطبيق |

كل المنافذ مرتبطة بـ`127.0.0.1` فقط، ولا تكون متاحة لأجهزة الشبكة المحلية أوالإنترنت.

## التهيئة الأولى

نفذ من جذر repository:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\Initialize-LocalEnvironment.ps1
```

ينشئ الأمر `.env.local` بقيم عشوائية لخدمات الجهاز فقط. الملف مستبعد من Git، ولا يطبع secrets. على Windows
تُقيد ACL تلقائيًا بالمستخدم الحالي و`SYSTEM` وAdministrators.

إذا كان `.env.local` موجودًا من إعداد Neon القديم، أوقف containers ثم أنشئ العقد المحلي مرة واحدة:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\Initialize-LocalEnvironment.ps1 -Force
```

المنفذ الافتراضي لـPostgreSQL هو `54329`. إذا كان محجوزًا، اختر منفذًا آخر أثناء الإنشاء، مثل
`-PostgresPort 54330`؛ يولد السكربت env والاتصالين المتطابقين تلقائيًا.

شغّل PostgreSQL المحلي ثم أنشئ schema roles وطبّق EF migrations:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\Initialize-LocalPostgresDatabase.ps1
```

يستخدم API حساب `dorosak_app` محدود الصلاحيات، بينما تستخدم EF migrations حساب `dorosak_migrator`. لا يستخدم التطبيق
حساب `dorosak_owner`. يبقى `Initialize-NeonDatabase.ps1` متاحًا فقط إذا اختير Neon مستقبلًا.

بعد الانتقال الأول لا تستخدم `-Force` إلا عند تدوير credentials المحلية عمدًا. التدوير يتطلب إعادة إنشاء containers
التي تعتمد عليها.

## تشغيل الخدمات

```powershell
docker compose --project-name dorosak --env-file .env.local up --detach --wait --wait-timeout 600
docker compose --project-name dorosak --env-file .env.local ps
```

قد يستغرق ClamAV عدة دقائق في التشغيل الأول لتنزيل virus definitions.

لتشغيل API والـWorker من الصور المحلية مع الخدمات:

```powershell
docker compose --project-name dorosak --env-file .env.local --profile app up --detach --build --wait --wait-timeout 600
Invoke-WebRequest -UseBasicParsing http://127.0.0.1:5053/health/ready
```

## إيقاف الخدمات

يحافظ الأمر التالي على containers والـvolumes:

```powershell
docker compose --project-name dorosak --env-file .env.local stop
```

لإزالة containers والشبكة مع الاحتفاظ بالـvolumes:

```powershell
docker compose --project-name dorosak --env-file .env.local down
```

لا تستخدم `docker compose down --volumes` لأنه يحذف Redis وMinIO وMailpit وClamAV local data.

## الهوية والبريد

- يرسل API أحداث verification وpassword reset وemail change إلى outbox، ويعالجها Worker خارج request transaction.
- عند تشغيل Worker محليًا تصل الرسائل إلى Mailpit على `http://127.0.0.1:8026` ولا تُرسل إلى الإنترنت.
- يجب أن يشترك API والـWorker في قاعدة البيانات نفسها لأن ASP.NET Core Data Protection keys مخزنة في
  `operations.data_protection_keys`.

لإنشاء أول Admin استخدم secret manager الخاص بمشروع `Dorosak.Worker` لضبط المفاتيح التالية مؤقتًا، من دون وضع
القيم في `appsettings` أو `.env.local` أو سجل الأوامر:

- `AdminBootstrap:Enabled=true`
- `AdminBootstrap:Email`
- `AdminBootstrap:DisplayName`
- `AdminBootstrap:TemporaryPassword` بطول `14-64`
- `AdminBootstrap:TotpSecret` بصيغة Base32 من تطبيق المصادقة

بعد ذلك شغّل Worker مرة واحدة. يتوقف Worker تلقائيًا بعد إنشاء Admin أو اكتشاف Admin موجود. احذف جميع مفاتيح
`AdminBootstrap` فورًا، ثم غيّر كلمة المرور المؤقتة من واجهة الأمان. لا يطبع Worker كلمة المرور أو TOTP secret.

## Cloudinary الاختياري للصور

يبقى `Media:Cloudinary:Enabled` معطلًا افتراضيًا، وتبقى originals وquarantine وmultipart في MinIO/S3. عند تفعيله بعد
تدوير credentials، مرر `CloudName` و`ApiKey` و`ApiSecret` إلى API وMediaWorker من configuration/environment فقط؛
لا تضع القيم في `appsettings` أو ملفات Git. لا يرفع المتصفح إلى Cloudinary مباشرة، ولا يُستخدم هذا المسار للفيديو أو
`video-poster` أو المستندات أو captions.

## التشخيص

```powershell
docker compose --project-name dorosak --env-file .env.local ps
docker compose --project-name dorosak --env-file .env.local logs --tail 100 redis minio mailpit clamav
```

لتشغيل الفحص السلوكي الكامل، بما فيه PostgreSQL وRedis read/write وEICAR:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\Test-DevelopmentInfrastructure.ps1
```

لا تلصق `.env.local` أو connection strings أو كلمات المرور في issue أو commit أو رسالة محادثة.
