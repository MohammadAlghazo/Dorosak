# Synthetic Data Policy

تستخدم Local وDev وPreview بيانات اصطناعية فقط. يمنع نسخ بيانات Production أوبيانات أشخاص حقيقيين إلى هذه البيئات.

## القواعد

- تستخدم الأسماء والعناوين المولدة عشوائيًا، ولا تستخدم بيانات العائلة أوالأصدقاء أوالعملاء.
- تستخدم عناوين البريد تحت `example.com`, `example.net`، أو `example.org` فقط.
- تستخدم أرقام هاتف غير قابلة للاتصال ومعلّمة بوضوح كبيانات اختبار.
- تستخدم seed ثابتة في automated tests حتى تكون النتائج قابلة للتكرار.
- تستخدم صورًا وملفات وفيديوهات مولدة أومرخصة للاختبار، ولا تستخدم محتوى تعليميًا مسروقًا.
- تستخدم payment provider test mode وtest tokens فقط عند تنفيذ Commerce.
- لا تخزن JWT, API keys, connection strings، أو passwords داخل seed files.
- تحذف Preview data وbranches تلقائيًا عند انتهاء TTL المحددة.
- أي اختبار يحتاج خصائص PII واقعية يستخدم schema والأنماط فقط، وليس القيم الحقيقية.

## الحظر

- يمنع `pg_dump` من Production إلى Local أو Dev.
- يمنع نسخ production object storage إلى MinIO المحلي.
- يمنع إرسال Mailpit messages إلى SMTP خارجي.
- يمنع استخدام أرقام بطاقات أوهويات أوعناوين حقيقية حتى لو وافق صاحبها.

أي استثناء يحتاج موافقة Security وPrivacy مكتوبة، وخطة masking وretention قبل نقل البيانات.
