# Development Setup

هذا الدليل يشغّل البنية التطويرية المحلية لـDorosak. قاعدة البيانات ليست محلية؛ التطبيق يتصل مباشرة بمشروع
`Dorosak Dev` على Neon PostgreSQL.

## الخدمات

| Service | Local endpoint | Purpose |
|---|---|---|
| Neon PostgreSQL | Remote, encrypted | مصدر الحقيقة وقاعدة التطوير |
| Redis | `127.0.0.1:6380` | cache, rate-limit، وتجارب SignalR |
| MinIO API | `http://127.0.0.1:9100` | S3-compatible object storage |
| MinIO Console | `http://127.0.0.1:9101` | إدارة التخزين المحلي |
| Mailpit SMTP | `127.0.0.1:1026` | التقاط البريد بدل إرساله خارجيًا |
| Mailpit UI | `http://127.0.0.1:8026` | عرض رسائل البريد المحلية |
| ClamAV | `127.0.0.1:3311` | فحص الملفات المرفوعة |

كل المنافذ مرتبطة بـ`127.0.0.1` فقط، ولا تكون متاحة لأجهزة الشبكة المحلية أوالإنترنت.

## التهيئة الأولى

نفذ من جذر repository:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\Initialize-LocalEnvironment.ps1
```

ينشئ الأمر `.env.local` بقيم عشوائية واتصالي Neon pooled/direct. الملف مستبعد من Git، ولا يطبع secrets. على Windows
تُقيد ACL تلقائيًا بالمستخدم الحالي و`SYSTEM` وAdministrators.

اتصالا Neon في هذه المرحلة يخصان `dorosak_owner` لاختبارات البنية والمigrations فقط. ينشأ runtime role محدود باسم
`dorosak_app` في Phase 3، ولا يستخدم API حساب المالك.

لا تستخدم `-Force` إلا عند تدوير credentials المحلية عمدًا. التدوير يتطلب إعادة إنشاء containers التي تعتمد عليها.

## تشغيل الخدمات

```powershell
docker compose --project-name dorosak --env-file .env.local up --detach --wait --wait-timeout 600
docker compose --project-name dorosak --env-file .env.local ps
```

قد يستغرق ClamAV عدة دقائق في التشغيل الأول لتنزيل virus definitions.

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

## التشخيص

```powershell
docker compose --project-name dorosak --env-file .env.local ps
docker compose --project-name dorosak --env-file .env.local logs --tail 100 redis minio mailpit clamav
```

لتشغيل الفحص السلوكي الكامل، بما فيه Redis read/write وEICAR وNeon direct/pooled:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\Test-DevelopmentInfrastructure.ps1
```

لا تلصق `.env.local` أو connection strings أو كلمات المرور في issue أو commit أو رسالة محادثة.
