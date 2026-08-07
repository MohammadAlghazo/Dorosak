# Dorosak - Software Architecture and Delivery Plan

| Field | Value |
|---|---|
| Document | `PROJECT_PLAN.md` |
| Status | Approved architecture baseline |
| Version | `1.4.0` |
| Date | `2026-08-07` |
| Product | `Dorosak` |
| Architecture style | `Clean Architecture` + `Modular Monolith` + feature-based vertical slices |
| Current delivery phase | `6. Catalog and Authoring Drafts` |
| Primary language | Arabic-first with full `RTL`; English is supported with `LTR` |
| Production database | Neon PostgreSQL |

## 1. Document Authority

هذا الملف هو `Single Source of Truth` للقرارات المعمارية وخطة بناء وتشغيل منصة `Dorosak`. لا يبدأ تنفيذ ميزة، ولا تضاف مكتبة، ولا يتغير عقد `API` أو مخطط قاعدة البيانات بما يخالف هذا الملف من دون تحديثه أولًا وتسجيل القرار في `Architecture Decision Record (ADR)`.

القواعد الحاكمة للوثيقة:

- الشرح الموجه للمستخدم يكتب بالعربية.
- أسماء الملفات والمجلدات والأنواع والمتغيرات والتعليقات و`commit messages` والمصطلحات التقنية تبقى بالإنجليزية.
- لا تستخدم حزم `Preview`, `RC`, أو `Experimental` في مسار الإنتاج.
- تثبت الإصدارات الفعلية في ملفات القفل عند بدء التنفيذ، وتستخدم أحدث `stable patch` المتوافق ضمن الإصدارات الرئيسية المعتمدة هنا.
- كل مرحلة تنتهي ببناء ناجح، واختبارات ناجحة، وفحص أمني مناسب، ثم `commit` و`push` إلى GitHub.
- لا تبدأ المرحلة التالية حتى يؤكد المستخدم بكتابة كلمة `تم`.
- أي تغيير على هذه الخطة يجب أن يوضح السبب، والبدائل، وأثر الترحيل، وأثر الأمن والأداء والتشغيل.

### 1.1 Verified Workspace State

تم التحقق من الحالة الفعلية قبل إنشاء هذه الوثيقة:

- المسار هو `D:\Projects\Dorosak`.
- يوجد Git repository مسبقًا على branch `main` ويتتبع `origin/main`.
- remote الموثق هو `https://github.com/MohammadAlghazo/Dorosak.git` للـfetch والـpush.
- يوجد commit أولي وملف `README.md`; لم يتم تعديلهما.
- لذلك تعليمات commit/push في نهاية هذه المرحلة قابلة للتنفيذ الآن، ولا تحتاج إنشاء repository جديد.
- المرحلة التالية اسمها `Workstation and Repository Hardening`: تتحقق من الأدوات وتحمي repository وتكمل إعداداته، ولا تعيد إنشاء ما هو موجود.

### 1.2 Collaboration and Teaching Protocol

- عند كل نقطة تحتاج تدخل المستخدم، يتوقف التنفيذ قبلها ويشرح بالعربية: لماذا، مكان كل click، النص المطلوب نسخه، commands بالترتيب، النتيجة المتوقعة، والأخطاء الشائعة.
- لا يطلب من المستخدم البحث في الإنترنت أو قراءة documentation خارجية؛ تقدم التعليمات المطلوبة داخل المحادثة وبروابط مباشرة فقط عندما تكون click مطلوبة.
- لا يطلب من المستخدم مشاركة password, token, connection string، أو secret في المحادثة. تستخدم شاشة المزود وsecret store والملفات المستبعدة من Git.
- بعد كل جزء كبير، تجرى verification أولًا ثم يطلب commit/push إلى GitHub باسم إنجليزي محدد، وبعدها يتوقف العمل حتى يكتب المستخدم `تم`.
- لا ينفذ assistant commit, push, cloud purchase، أو إجراء غير قابل للعكس من دون طلب صريح من المستخدم.
- إذا اختلفت شاشة cloud provider عند الوصول إليها، يفحص assistant الشاشة المتاحة ويعطي تعليمات محدثة بدل تخمين أسماء الأزرار.

## 2. Vision

`Dorosak` منصة تعليم رقمي عربية أولًا تتيح للمدرسين إنشاء مقررات عالية الجودة ونشرها، وللطلاب اكتشاف المحتوى وشرائه أو الالتحاق به والتعلم والتقييم والحصول على شهادات، وللمشرفين إدارة الجودة والأمان والتشغيل من مكان واحد.

المنتج المستهدف ليس نسخة شكلية من `Udemy` أو `Coursera`. الهدف هو تقديم تجربة تعلم موثوقة، سريعة، قابلة للوصول، وآمنة، مع بنية تشغيل تستطيع النمو من إطلاق أولي صغير إلى ملايين الحسابات من دون إعادة كتابة جوهر النظام.

### 2.1 Product Principles

- `Learning first`: الوصول إلى الدرس التالي والتقدم الفعلي أهم من المؤشرات الاستعراضية.
- `Secure by default`: الرفض هو الوضع الافتراضي، وكل وصول إلى مورد خاص يفحص في الخادم.
- `Arabic first`: التصميم والمحتوى والتنقل يعملان أصليًا في `RTL` وليس بتحويل بصري متأخر.
- `Accessible by design`: معيار `WCAG 2.2 AA` شرط قبول، وليس تحسينًا اختياريًا.
- `Measure before scaling`: لا تضاف `Microservices`, `Read Replicas`, أو محركات بحث خارجية قبل وجود قياس يثبت الحاجة.
- `Reliable state`: PostgreSQL هو مصدر الحقيقة؛ `Redis`, `SignalR`, و`CDN` ليست مصادر حقيقة.
- `No hidden work`: كل عملية طويلة لها حالة واضحة، وكل فشل قابل للتشخيص وإعادة المحاولة الآمنة.
- `No vendor lock-in in the domain`: تفاصيل مزودي البريد والتخزين والدفع والمراقبة تبقى خلف حدود `Infrastructure`.

### 2.2 Business Outcomes

- يستطيع الطالب التسجيل والتحقق من بريده واكتشاف مقرر والالتحاق به وإكماله والحصول على شهادة.
- يستطيع المدرس بناء مقرر منظم، ورفع الفيديو والملفات، وإدارة الاختبارات والواجبات، ومتابعة طلابه.
- يستطيع المشرف مراجعة المحتوى والتقارير والمستخدمين والمدفوعات والإعدادات والتدقيق.
- يستطيع فريق التشغيل نشر إصدار جديد، مراقبته، التراجع عنه، واستعادة البيانات بإجراءات موثقة.
- تعمل الرحلات الحرجة على الهاتف والحاسوب، وفي الوضعين الفاتح والداكن، وباللغتين العربية والإنجليزية.

## 3. Scope and Boundaries

### 3.1 Included Product Scope

- `Guest`, `Student`, `Teacher`, و`Admin` experiences.
- التسجيل وتأكيد البريد وتسجيل الدخول والخروج واستعادة كلمة المرور وإدارة الجلسات.
- `JWT access tokens`, rotating refresh tokens، وتهيئة كاملة لـ`TOTP 2FA`.
- الملفات الشخصية والإعدادات والصورة الشخصية وتفضيلات اللغة والثيم والإشعارات.
- التصنيفات والوسوم والبحث والاقتراحات والقوائم المميزة والشائعة والتوصيات.
- إدارة المقررات والإصدارات والأقسام والدروس والفيديو والوثائق والموارد القابلة للتنزيل.
- التسجيل والاستحقاقات والتقدم و`Continue Learning` و`Recently Viewed` و`Bookmarks` و`Wishlist`.
- الاختبارات والواجبات والتسليم والتصحيح والنتائج.
- المراجعات والتقييمات والمناقشات والتعليقات والردود والإعجابات والتقارير والإشراف.
- الرسائل والإشعارات والإعلانات والدردشة الفورية ومؤشرات الكتابة.
- الشهادات العامة القابلة للتحقق.
- بنية التجارة: المنتجات والأسعار والكوبونات والطلبات والمدفوعات والاسترداد والاشتراكات والاستحقاقات.
- لوحات الطالب والمدرس والمشرف والتحليلات والتقارير وسجل التدقيق.
- صفحات `CMS`: About, Contact, FAQ, Privacy Policy, Terms.
- `SSR`, hydration, PWA، ووضع Offline محدود وآمن.
- Docker وGitHub Actions وبيئات Dev/Staging/Production وخطة نشر ومراقبة ونسخ احتياطي.

### 3.2 Explicit Non-Goals for the Initial Production Release

هذه الحدود قرارات هندسية وليست عناصر منسية:

- لا توجد `Microservices` في البداية؛ الفصل يتم داخل `Modular Monolith`.
- لا توجد `Multi-tenancy` أو `White-label Organizations` في الإصدار الأول. المنصة `marketplace` واحدة، ولا يضاف `TenantId` احترازيًا إلى كل جدول.
- لا توجد فصول فيديو مباشرة أو مؤتمرات مرئية؛ `Live updates` تعني تحديثات بيانات واتصالات فورية عبر SignalR.
- لا يوجد `Event Sourcing`; تستخدم `Domain Events` و`Integration Events` مع الحالة الحالية في PostgreSQL.
- لا يوجد `GraphQL` أو `OData`; الواجهة العامة `REST` موثقة عبر OpenAPI.
- لا يوجد محرك `Elasticsearch/OpenSearch` عند الإطلاق؛ يبدأ البحث بـPostgreSQL ثم ينتقل عند تحقق شروط قياس محددة.
- لا يوجد `Kubernetes` عند الإطلاق؛ منصة حاويات مدارة أقل تعقيدًا وأكثر ملاءمة لحجم الفريق الأولي.
- لا تخزن بيانات بطاقات الدفع. يستخدم `Hosted Checkout/Tokenization` لتقليل نطاق `PCI DSS`.
- لا تقدم المنصة ضمانًا يمنع تسجيل الشاشة؛ حماية الوسائط تعتمد على الاستحقاق والروابط الموقعة، ويمكن إضافة `DRM` لاحقًا إذا فرضه نموذج العمل.

## 4. Roles

| Role | Definition | Account requirement |
|---|---|---|
| `Guest` | زائر مجهول يستطيع تصفح المحتوى العام والبحث وقراءة صفحات CMS | ليس سجلًا في قاعدة البيانات ولا `Identity Role` |
| `Student` | الدور الافتراضي للحساب المؤكد، ويملك موارده التعليمية فقط | بريد مؤكد |
| `Teacher` | مدرس معتمد يستطيع إنشاء وإدارة مقرراته وطلاب مقرراته | بريد مؤكد، ملف مدرس معتمد، ويملك أيضًا صلاحيات `Student` |
| `Admin` | مشرف منصة بصلاحيات دقيقة لإدارة المنصة | حساب إداري منفصل، `2FA` إلزامي، و`recent authentication` للعمليات الحساسة |

قواعد الأدوار:

- المستخدم يمكن أن يكون `Student` و`Teacher` معًا.
- `Admin` ليس تجاوزًا سحريًا. كل إجراء إداري يتطلب `Permission` صريحة ويكتب في `Audit Log`.
- لا يوجد حساب `Admin` بكلمة مرور افتراضية أو بيانات اعتماد محفوظة في المستودع.
- إنشاء أول `Admin` يتم بأمر تشغيل لمرة واحدة باستخدام سر مؤقت، ثم يحذف السر وتلغى إمكانية إعادة الاستخدام.
- إخفاء زر في Angular ليس تفويضًا؛ التفويض النهائي دائمًا في ASP.NET Core.

## 5. Functional Requirements

### 5.1 Identity and Account

- إنشاء حساب باستخدام الاسم والبريد وكلمة مرور قوية.
- منع كشف وجود البريد في التسجيل والدخول واستعادة كلمة المرور.
- تأكيد البريد برابط موقّع محدود الصلاحية مع إمكانية إعادة الإرسال المحدودة بالمعدل.
- تسجيل الدخول والخروج من جلسة واحدة أو كل الجلسات.
- عرض الجلسات النشطة مع اسم جهاز تقريبي وتاريخ آخر استخدام وإلغائها.
- تدوير `Refresh Token` عند كل استخدام وكشف إعادة استخدام token قديمة.
- استعادة كلمة المرور وإلغاء الجلسات القائمة بعد نجاح الاستعادة.
- دعم `TOTP`, recovery codes، و`step-up authentication`، مع إلزامه للمشرفين.
- إدارة الملف الشخصي والصورة والنبذة والروابط المسموحة واللغة والمنطقة الزمنية والثيم.
- طلب التحول إلى مدرس، ومراجعة الطلب والموافقة أو الرفض مع سبب.

### 5.2 Catalog and Discovery

- عرض المقررات المنشورة فقط للضيف والطالب.
- تصفح حسب التصنيف والوسم واللغة والمستوى والسعر والتقييم والمدة والمدرس.
- بحث فوري مع `debounce`, cancellation, suggestions, autocomplete, highlighting, filtering, sorting, وpagination.
- صفحات `Featured Courses`, `Popular Courses`, `New Courses`, و`Recommendations`.
- صفحة مقرر واضحة تعرض الوصف والمخرجات والمتطلبات والمنهج والمدرس والتقييمات والسعر وحالة الالتحاق.
- روابط `slug` صديقة لمحركات البحث مع `canonical URL` وسجل `course_slugs` دائم يحتفظ بالقيم السابقة عند التغيير.
- `Global Search` منفصل حسب الصلاحية: بحث عام في الـCatalog، وبحث إداري في المستخدمين والمحتوى والطلبات والتقارير.

### 5.3 Course Authoring and Publishing

- إنشاء مسودة مقرر وتعديل بياناتها وصورتها ولغتها ومستواها ومتطلباتها وأهدافها.
- إدارة أقسام ودروس مرتبة مع إعادة ترتيب قابلة للوصول عبر لوحة المفاتيح.
- أنواع الدروس: Video, Article, Document, Quiz, Assignment.
- حفظ تلقائي آمن مع `optimistic concurrency`, وتوضيح التعارض بدل الكتابة فوق تعديل آخر.
- معاينة المقرر كما يراه الطالب.
- التحقق من اكتمال المحتوى قبل طلب النشر.
- دورة حياة: `Draft -> InReview -> ChangesRequested -> Published -> Unpublished -> Archived`.
- المراجعة الإدارية، ملاحظات قابلة للتنفيذ، وقبول أو رفض موثق.
- نشر `CourseRelease` غير قابل للتعديل؛ أي تغيير لاحق ينتج release جديدة.
- إبقاء الإصدار المخصص للطالب واضحًا عند تحديث مقرر منشور، وعدم كسر تقدمه أو شهادته.

### 5.4 Media and Files

- رفع صغير متدفق عبر API، ورفع كبير مباشر إلى `Object Storage` باستخدام multipart/chunk upload.
- استئناف ووقف ورفض رفع الأجزاء المكررة أو ذات checksum غير صحيح.
- منطقة `Quarantine` وفحص malware والتحقق من magic bytes قبل إتاحة الملف.
- إعادة ترميز الصور إلى AVIF/WebP، إزالة EXIF، وإنشاء أحجام responsive.
- تحليل الفيديو وتحويله في worker معزول إلى HLS بملفات 360p/720p/1080p عند توافر جودة المصدر.
- subtitles/captions بصيغة WebVTT وtranscript قابل للبحث والوصول.
- تنزيل وروابط بث قصيرة الصلاحية بعد فحص الاستحقاق.
- دعم `Range Requests`, streaming download، و`Content-Disposition` آمن.
- تنظيف upload sessions والملفات اليتيمة والمنتهية تلقائيًا.

### 5.5 Enrollment and Learning

- تسجيل مجاني أو منح الاستحقاق بعد تأكيد الدفع.
- صفحة `My Learning`, `Continue Learning`, والدرس التالي.
- مشغل تعلم قليل التشتيت مع المنهج والملاحظات والنص والمناقشة.
- حفظ موضع الفيديو والفترات المشاهدة بصورة مجمعة، وعدم احتساب القفز وحده مشاهدة.
- قواعد إكمال مختلفة لكل نوع درس، ويكون الخادم صاحب القرار النهائي.
- تقدم للمقرر والقسم والدرس، مع مزامنة آمنة بعد انقطاع الشبكة.
- `Bookmarks`, notes, recently viewed، وdownloadable resources.
- إكمال مقرر وتثبيت release ومتطلبات الإكمال المستخدمة.
- لوحة تقدم حية للطالب، وملخصات مجمعة ومصرح بها للمدرس.

### 5.6 Assessments

- Quiz versions ثابتة عند النشر، مع single choice, multiple choice, true/false, short answer.
- ضبط عدد المحاولات والمدة ودرجة النجاح وترتيب الأسئلة وإظهار الإجابات.
- تخزين محاولة مستقلة وربطها بإصدار الاختبار والتسجيل.
- تصحيح آلي للأسئلة الموضوعية، وتصحيح يدوي للأسئلة النصية عند الحاجة.
- Assignment versions، مواعيد التسليم، ملفات مرفقة، وتسليمات متعددة وفق السياسة.
- grading, feedback، سجل تعديلات الدرجة، وإشعار الطالب.
- حماية من تكرار submit وسباقات المحاولات بواسطة idempotency وdatabase constraints.

### 5.7 Engagement and Moderation

- مراجعة واحدة لكل طالب ومقرر بعد تحقق أهلية محددة.
- تقييم من 1 إلى 5 وملخص موزون يحسب في الخلفية.
- مناقشات مرتبطة بمقرر أو درس، وتعليقات وردود بعمق أقصى مستويين.
- إعجابات فريدة، تعديل وحذف ضمن نافذة وسياسة معلنة، وعلامة edited.
- تقارير على مقرر أو مراجعة أو تعليق أو رسالة أو مستخدم.
- `Moderation Case` موحد بحالات وأسباب وقرارات وسجل تدقيق.
- منع spam والإساءة بالمعدل والقواعد الآلية، مع بقاء القرار النهائي قابلًا للمراجعة البشرية.

### 5.8 Messaging, Notifications, and Realtime

- محادثات مباشرة أو مرتبطة بمقرر، ولا يدخلها إلا المشاركون المصرح لهم.
- إرسال الرسالة عبر REST مع `clientMessageId`; يستخدم SignalR للإشعار والack والتحديث الفوري.
- مؤشرات كتابة وحضور مؤقتة لا تحفظ كمصدر حقيقة.
- إشعارات داخل التطبيق مع unread count وتفضيلات حسب النوع والقناة.
- Email queue موثوقة للرسائل المعاملاتية، مع retries وsuppression عند bounce/complaint.
- إعلانات للمقرر ينشئها المدرس وتصل للمسجلين فقط.
- تحديثات مباشرة عند نشر محتوى، تغيير حالة واجب، وصول درجة، أو تغير لوحة متابعة.
- بعد reconnect يجلب العميل الحالة من REST لأن SignalR قناة `best effort`.

### 5.9 Commerce

- فصل `Product`, `SalesOffer`, `Order`, `Payment`, `Subscription`, و`Entitlement`.
- السعر والعملة والخصم والضريبة تحفظ snapshot داخل الطلب.
- كوبونات بوقت ونطاق وحد استخدام وقواعد أهلية واضحة.
- `Hosted Checkout` من مزود الدفع، والتحقق من webhook قبل منح الوصول.
- idempotency لكل checkout/payment/webhook/refund.
- حالات دفع واسترداد واشتراك مستقلة، ولا يعتمد النجاح على redirect المتصفح.
- `CheckoutSession` تثبت quote محدودة الصلاحية، و`RefundRequest` تفصل طلب المستخدم عن refund المؤكدة من provider.
- invoice/tax snapshots تحفظ وفق market وMerchant of Record policy، ولا يعاد حساب التاريخ المالي من price الحالية.
- disputes وchargebacks لها workflow مستقلة تؤثر في entitlement والledger وفق policy ولا تحذف order.
- double-entry ledger يسجل capture, refund, fee, tax, teacher earning, reserve، وpayout بقيود immutable ومتوازنة.
- commission rate وteacher share تحفظ snapshot لكل order item؛ payout لا يبدأ قبل settlement hold وKYC/provider onboarding.
- reference marketplace payout هو `Stripe Connect` مع adapter مستقل، ويظل teacher payout معطلًا حتى اعتماد Merchant of Record والضرائب والبلدان المدعومة.
- reconciliation job يومية تقارن payments/refunds/disputes/payouts المحلية بتقرير provider، وتنتج run قابلًا للتدقيق وتنبيهًا لأي mismatch.
- تفعيل الدفع العام يكون عبر `Feature Flag` بعد إعداد حساب التاجر والتحقق القانوني. البنية المرجعية تستخدم `Stripe Checkout`; إن لم يكن متاحًا في بلد الشركة، يستبدل adapter فقط بقرار ADR قبل مرحلة Commerce.
- لا تخزن المنصة PAN أو CVV أو بيانات اعتماد دفع خام.

### 5.10 Certificates

- إصدار شهادة بعد `CourseCompletion` مؤكد.
- الشهادة immutable وتحمل رقمًا عامًا غير متسلسل ورمز QR إلى صفحة تحقق.
- صفحة التحقق العامة تعرض الحد الأدنى: اسم المتعلم وفق موافقته، المقرر، تاريخ الإصدار، والحالة.
- الإبطال يسجل كحالة وحدث، ولا يحذف الشهادة.
- PDF ينشأ في background job ويحفظ كملف خاص قابل للتنزيل برابط موقّع.

### 5.11 Administration, CMS, and Analytics

- إدارة المستخدمين والجلسات والأدوار والصلاحيات وحالات المدرسين.
- مراجعة المقررات والتقارير والمحتوى وإجراءات moderation.
- إدارة categories, tags, featured courses, coupons, subscriptions، وإعدادات المنصة.
- إدارة صفحات About, Contact, FAQ, Privacy Policy, Terms بإصدارات ومراجعة قبل النشر.
- لوحات تشغيل تعرض التسجيلات والمبيعات والتفاعل والإكمال وجودة المقررات وصحة queues.
- تقارير كبيرة تنفذ كـbackground export بدل حجز طلب HTTP طويل.
- Audit log قابل للبحث للمشرفين المخولين فقط.
- Analytics events قليلة البيانات الشخصية، مع daily aggregates بدل الاستعلام المتكرر على جداول المعاملات.

## 6. Non-Functional Requirements

### 6.1 Reliability and Service Levels

| Metric | Initial production objective |
|---|---|
| Core Web/API availability | `99.9%` شهريًا |
| Authentication availability | `99.9%` شهريًا |
| Public API read latency | `p95 <= 400 ms`, `p99 <= 1.5 s` خارج زمن الشبكة والملفات |
| API write latency | `p95 <= 800 ms` خارج uploads والمعالجة الخلفية |
| Public SSR TTFB | `p75 <= 800 ms` |
| Web LCP | `p75 <= 2.5 s` على mobile وdesktop |
| Web INP | `p75 <= 200 ms` |
| Web CLS | `p75 <= 0.1` |
| Critical job start delay | `p95 <= 60 s` |
| Default job start delay | `p95 <= 5 min` |
| Transactional email accepted by provider | `p95 <= 2 min` |
| Database PITR RPO | `<= 5 min` داخل نافذة Neon |
| Database restore RTO | `<= 60 min` داخل نافذة Neon |
| Published object storage recovery | `RPO <= 15 min`, `RTO <= 4 h` |
| Regional/vendor disaster baseline | `RPO <= 24 h`, `RTO <= 4 h` من النسخة المستقلة |

هذه أهداف قياس وتشغيل. لا تعد ضمانات حتى تنجح اختبارات الحمل والاستعادة الفعلية.

### 6.2 Capacity Targets

يختبر التصميم قبل الإطلاق العام على ضعف الحمل المتوقع، ويظل قادرًا من دون تفكيك جوهري على بلوغ الحدود التالية بعد التوسع الأفقي وضبط Neon:

- مليون حساب مسجل.
- مائة ألف مستخدم نشط شهريًا.
- عشرة آلاف جلسة Web متزامنة.
- عشرون ألف اتصال SignalR متزامن بعد فصل realtime أو استخدام خدمة مدارة عند الحاجة.
- ألف طلب API في الثانية كذروة مجمعة للطلبات الخفيفة.
- ملايين سجلات التقدم وعشرات الملايين من analytics/audit events.
- تخزين الوسائط خارج قاعدة البيانات وقابل للنمو مستقلًا عنها.

### 6.3 Quality Attributes

- `Security`: خط أساس `OWASP ASVS Level 2` وOWASP Top 10، مع controls إضافية للإدارة والملفات والدفع.
- `Accessibility`: اجتياز `WCAG 2.2 AA`, keyboard-only, screen readers, RTL، وzoom حتى 400%.
- `Maintainability`: حدود dependencies آلية، ملفات صغيرة ذات مسؤولية واضحة، وتحذيرات compiler/analyzers تعامل كأخطاء في CI.
- `Observability`: كل طلب وjob وintegration event قابل للربط عبر trace وcorrelation identifiers.
- `Portability`: التطبيق يبنى كحاويات OCI ولا يعتمد على filesystem محلي أو session داخل replica.
- `Privacy`: تقليل PII، retention معلنة، export/delete workflows، وعدم تسجيل الأسرار أو محتوى الرسائل.
- `Disaster recovery`: نسخ متعددة، PITR، وrestore drills مجدولة.
- `Localization`: كل النصوص UI قابلة للترجمة، وتخزن التواريخ UTC مع IANA time zone عند الحاجة.

## 7. Technology Stack

الإصدارات التالية هي baseline بتاريخ الوثيقة. يثبت أحدث patch مستقر داخل major نفسه عند إنشاء الملفات.

| Area | Technology | Decision |
|---|---|---|
| Backend runtime | `.NET 10 LTS`, `C# 14`, `ASP.NET Core 10` | أحدث LTS مستقر؛ لا يستخدم STS أعلى لمجرد الرقم |
| ORM | `EF Core 10` + `Npgsql` | PostgreSQL-native provider، migrations وcompiled models عند إثبات فائدتها |
| Database | `PostgreSQL 18` on Neon | يستخدم إذا كان `GA` في Neon؛ الخيار الوحيد البديل هو PostgreSQL 17 عند عدم دعم 18 |
| CQRS mediator | `MediatR` latest stable | لتنظيم use cases فقط؛ تعتمد الرخصة المناسبة قبل الإنتاج |
| Validation | `FluentValidation` latest stable | Application validation؛ database constraints تبقى الحارس النهائي |
| Mapping | `AutoMapper` latest stable | Read projections والخرائط الآمنة فقط؛ تعتمد الرخصة المناسبة |
| API versioning | `Asp.Versioning` | URL segment versioning |
| Authentication | `ASP.NET Core Identity` + signed JWT + opaque refresh tokens | لا crypto مخصص لكلمات المرور |
| Jobs | `Hangfire` + `Hangfire.PostgreSql` | Server داخل Worker منفصل، وليس داخل API في Production |
| Realtime | `ASP.NET Core SignalR` + Redis backplane | الأحداث الدائمة تحفظ أولًا في PostgreSQL |
| Cache | `IMemoryCache`, Redis-compatible managed cache, ASP.NET Output Cache | cache-aside وopt-in output caching |
| Logging | `Serilog` structured JSON | console sink في containers مع redaction |
| Telemetry | `OpenTelemetry` + OTLP | traces, metrics, logs عبر collector/backend |
| Backend tests | `xUnit`, `WebApplicationFactory`, `Testcontainers`, `Playwright`, `k6` | لا يستخدم EF InMemory لاختبارات persistence |
| Frontend | `Angular 21.2.x LTS` latest stable patch | Standalone, Signals, zoneless, OnPush, strict mode |
| SSR | `@angular/ssr` | البديل الحديث لـAngular Universal مع hydration وevent replay |
| Frontend runtime | `Node.js 24.19+ LTS`, TypeScript `>=5.9.0 <6.0.0`, RxJS 7.x | Node 20.19+ و22.12+ بدائل توافقية؛ الإصدارات تقفل وفق Angular compatibility table |
| UI | `Bootstrap 5.3.x` Sass/CSS variables | لا Bootstrap JavaScript ولا jQuery |
| UI behavior | `Angular CDK` | overlays, focus, a11y, virtual scrolling, drag/drop |
| Component library | `Angular Material` | يبدأ دون استخدام؛ يضاف فقط لمكون يحقق فائدة ولا يكرر design system |
| Dialogs | `SweetAlert2` | confirmations والنتائج البسيطة فقط، وليس forms المعقدة |
| Icons | `Lucide Angular` | imports محددة قابلة لـtree shaking |
| PWA | Angular Service Worker + IndexedDB application layer | shell/assets عبر SW، والبيانات offline عبر سياسة صريحة |
| Media | `FFmpeg`, `ffprobe`, `libvips`, `ClamAV` | تعمل داخل workers معزولة ومحدودة الموارد |
| Object storage | Azure Blob Storage في reference deployment | private containers وsigned access وCDN |
| Email | Postmark في reference deployment | HTTPS API, SPF, DKIM, DMARC, webhook validation |
| Production compute | Azure Container Apps | Web SSR, API, workers, jobs، ودعم revisions/traffic splitting |
| Edge | Azure Front Door Premium + WAF | TLS, CDN, routing, DDoS/WAF، ونطاق تطبيق واحد |
| Secrets | Azure Key Vault | runtime injection وGitHub OIDC بلا أسرار طويلة العمر |
| Container registry | Azure Container Registry | immutable image digests, scanning, signing |
| Infrastructure as Code | Azure Bicep | كل إعداد دائم يعاد إنشاؤه من المستودع |
| CI/CD | GitHub Actions | build each workload once, promote one immutable release manifest of image digests |

### 7.1 License Policy

- تراجع تراخيص `MediatR`, `AutoMapper`, media codecs، fonts، وأي provider قبل دمجها.
- بما أن MediatR وAutoMapper متطلبات صريحة، تعتمد رخصتهما التجارية المناسبة إن طلب إصدار الإنتاج ذلك.
- يمنع إدخال dependency ذات رخصة غير معتمدة أو غير قابلة للتوزيع في container image.
- ينتج `SBOM` لكل release وتفشل بوابة الإنتاج عند غيابه.

### 7.2 Angular Version Support Decision

| Version | Support status at planning time | Decision |
|---|---|---|
| Angular 21.2.x | `LTS` حتى يونيو 2027 | معتمد للمنصة |
| Angular 20.3.x | `LTS` حتى 28 نوفمبر 2026 | بديل فقط إذا ظهرت library compatibility blocker مثبتة |
| Angular 19.2.x | خارج الدعم | مرفوض للإنتاج رغم تفضيل قدمه |
| Angular 22.1.x | Active وليس مطلوبًا للمنصة الآن | لا يستخدم في هذه المرحلة |

القاعدة: نستخدم آخر patch مستقر من Angular 21.2 مع تطابق major/minor بين `@angular/core`, `@angular/cli`, `@angular/ssr`, `@angular/cdk` و`@angular/service-worker`. لا نخلط Angular 19 أو20 packages مع Angular 21.

## 8. Architecture

### 8.1 Architectural Style

يبدأ النظام كـ`Modular Monolith` منظم بوحدات أعمال واضحة، مع أربعة deployable workloads:

1. `Dorosak.Web`: Angular SSR والملفات الأمامية المبنية.
2. `Dorosak.Api`: REST, authentication, OpenAPI، وSignalR endpoints.
3. `Dorosak.Worker`: Hangfire, outbox dispatch, email, notifications, analytics, cleanup.
4. `Dorosak.MediaWorker`: scanning, image processing، وvideo transcoding بموارد وشبكة معزولة.

يوفر هذا الاختيار معاملات محلية بسيطة، نشرًا مفهومًا، وتكلفة تشغيل أقل من Microservices، مع بقاء حدود الوحدات قابلة للاستخراج مستقبلًا. لا توجد distributed transactions، وتستخدم `Transactional Outbox` لأي side effect يجب ألا يضيع بعد commit.

### 8.2 Major Modules

| Module | Ownership |
|---|---|
| `Identity` | accounts, credentials, sessions, roles, permissions, security events |
| `Profiles` | public profiles, teacher applications, user preferences |
| `Catalog` | course identity, metadata, categories, tags, published discovery models |
| `Authoring` | drafts, curriculum, sections, lessons, revisions, publication workflow |
| `PublishingCoordinator` | application workflow فقط يجمع manifest ويتحقق منها؛ لا يملك schema أوجداولًا |
| `Media` | upload sessions, assets, variants, captions, signed delivery metadata |
| `Learning` | entitlements, enrollments, progress, notes, bookmarks, recently viewed, wishlist |
| `Assessments` | quizzes, attempts, assignments, submissions, grading |
| `Engagement` | reviews, ratings, discussions, comments, likes, reports, moderation |
| `Communications` | conversations, messages, notifications, announcements, email delivery |
| `Commerce` | products, offers, coupons, orders, payments, refunds, subscriptions |
| `Credentials` | completions, certificates, verification, templates |
| `CMS` | pages, FAQs, contact requests, public settings |
| `Analytics` | events, daily aggregates, recommendation and popularity snapshots |
| `Operations` | audit, outbox, inbox, idempotency, background operations, webhooks |

### 8.3 Clean Architecture Dependency Rule

| Layer/Project | Responsibility | Allowed dependencies |
|---|---|---|
| `Dorosak.Domain` | Entities, value objects, aggregate invariants, domain events, domain errors | .NET BCL only |
| `Dorosak.Application` | Commands, queries, handlers, validators, authorization requirements, Result, DTOs, ports | Domain + approved application libraries |
| `Dorosak.Infrastructure` | EF Core, Identity, Neon, Redis, Hangfire, storage, email, JWT signing, integrations | Application + Domain |
| `Dorosak.Api` | Controllers, middleware, ProblemDetails, authentication schemes, SignalR hubs, OpenAPI | Application; Infrastructure only in composition root |
| `Dorosak.Worker` | Background composition root and workers | Application; Infrastructure only in composition root |
| `Dorosak.MediaWorker` | Isolated media processing composition root | Application media contracts + Infrastructure adapters |

ثوابت الاعتماد:

- Domain لا يعتمد على ASP.NET Core أو EF Core أو Identity أو MediatR أو AutoMapper.
- لا تعبر EF entities أو Identity types أو Domain entities حدود HTTP.
- لا يستدعي MediatR handler معالجًا آخر عبر mediator.
- لا يوجد `GenericRepository<T>` أو `GenericService<T>` أو `BaseController` شامل.
- `DbContext` هو Unit of Work الفعلي. توجد واجهة `IUnitOfWork` صغيرة للـcommit والtransactions، ولا يعاد اختراع EF Core.
- تستخدم repositories خاصة فقط عندما يحتاج aggregate إلى تحميل وحفظ بسلوك واضح، مثل Course, Order, Enrollment؛ queries تقرأ مباشرة عبر projections.
- لا تنشأ wrappers احترازية حول `ISender`, `ILogger`, `IMapper`, cache، أو HttpClient.
- interfaces تنشأ عند حدود I/O المتغيرة: email, object storage, malware scanner, payment gateway, current principal، وmedia processor.

#### 8.3.1 Enforceable Module Boundaries

- كل type يقع تحت namespace بالشكل `Dorosak.<Layer>.<Module>`, مثل `Dorosak.Application.Catalog`.
- لكل module ملف ownership في architecture tests يحدد schemas والجداول والcommands/events التي تملكها.
- لا تستورد module أي namespace من `Infrastructure` لوحدة أخرى، ولا تكتب DbSet أو جدولًا تملكه وحدة أخرى.
- cross-module references تستخدم typed IDs وعقودًا تحت namespace `Contracts` صغير داخل الوحدة المالكة، لا domain entities أو repositories.
- write workflow عابر للوحدات ينسق command في الوحدة المالكة أو integration event بعد commit؛ لا يغير handler جداول وحدتين بلا use case منسقة ومراجعة معاملة صريحة.
- cross-module read joins مسموحة فقط داخل read projection مخصصة في Infrastructure، وتبقى read-only ولا تتحول إلى navigation properties.
- `DorosakDbContext` واحد يحقق atomicity، لكن write access يعرض عبر module-specific persistence ports، وتبقى entity configurations داخل ownership folder.
- cross-schema FK لا تمنح runtime write dependency؛ migration configuration في الوحدة صاحبة الجدول المرجِع تستطيع الإشارة إلى table name ثابت من ownership registry من دون استيراد Domain/Infrastructure للوحدة المرجَع إليها.
- `PublishingCoordinator` داخل Application هو الاستثناء التنسيقي المعلن: يقرأ عقود Authoring/Media/Assessments، ثم يرسل manifest متحققًا منها إلى Catalog command واحدة؛ لا يصل مباشرة إلى DbSet ولا يملك جداول.
- architecture tests تفشل عند reference محظورة، namespace cycle، repository عابر للوحدات، أو table mapping خارج ownership registry.

| Consumer module | Allowed direct contracts |
|---|---|
| `Profiles` | Identity account ID/status وMedia ready-image reference |
| `Catalog` | Identity IDs وpublic teacher profile snapshot؛ لا يعتمد على Authoring/Media/Assessments مباشرة |
| `Authoring` | Catalog course identity وMedia asset readiness وAssessments draft-reference contracts |
| `PublishingCoordinator` | Catalog, Authoring, Media، وAssessments publication contracts؛ لا persistence port |
| `Learning` | Identity learner ID وCatalog release manifest |
| `Assessments` | Authoring lesson identity وLearning enrollment eligibility |
| `Engagement` | Identity IDs، Catalog course ID، Learning eligibility، وCommunications reportable-message lookup |
| `Communications` | Identity recipient IDs وCatalog course IDs؛ enrollment audiences تصل كprojection من Learning events |
| `Commerce` | Identity buyer ID وCatalog product/course reference |
| `Credentials` | Learning completion event فقط |
| `Analytics` | versioned integration events/read projections فقط، ولا يكتب في source modules |
| `CMS` | Media ready-asset reference فقط |
| `Operations` | technical envelopes and infrastructure state، لا business decisions |

### 8.4 CQRS and Transaction Model

- `Command` يغير حالة، ويستهدف aggregate واحدة عادة، ويعمل داخل transaction واحدة.
- `Query` لا يغير حالة، ويستخدم `AsNoTracking`, projection، وpagination محدودة.
- لا توجد قاعدة write وأخرى read في البداية؛ الفصل منطقي داخل نفس PostgreSQL.
- Domain events تعالج داخل العملية عند الحاجة إلى اتساق المعاملة.
- Integration events تكتب إلى `operations.outbox_messages` في transaction نفسها.
- Outbox dispatcher ينشر بعد commit بأسلوب `at-least-once`.
- كل consumer وjob idempotent، مع Inbox أو unique operation key.
- الاتصالات الخارجية والبريد والتخزين وSignalR وHangfire لا تستدعى داخل transaction الأعمال.
- `READ COMMITTED` هو isolation الافتراضي، مع optimistic concurrency. تستخدم locks أو `SERIALIZABLE` فقط للمقاعد المحدودة أو redemption بعد قياس واختبار retries.

### 8.5 Extraction Triggers

لا تحول وحدة إلى service مستقل إلا إذا تحقق واحد أو أكثر من الآتي بقياس موثق:

- حاجتها إلى scaling مختلف بأكثر من أربعة أضعاف بقية التطبيق.
- حاجتها إلى release cadence أو ownership فريق مستقل.
- تكرار فشلها يهدد availability لبقية المنصة.
- وجود متطلبات network/data isolation مختلفة.
- تجاوز database workload حدودًا لا تحل بالفهارس والقراءات والworker separation.

أول المرشحين للاستخراج عند تحقق الشروط: Media Processing، Search، Communications، ثم Analytics. لا تصبح Redis Pub/Sub قناة أحداث أعمال دائمة عند الاستخراج؛ يعتمد message broker durable بقرار ADR مستقل.

### 8.6 Major Decision Rationale

| Decision | Selected approach | Rejected initial alternative | Engineering reason |
|---|---|---|---|
| Service topology | Modular Monolith | Microservices from day one | معاملات أبسط، تشغيل أقل تكلفة، tracing/debugging أسهل، مع حدود modules قابلة للاستخراج |
| Code organization | Clean Architecture + vertical slices | مجلدات عامة Services/Repositories | تجمع كل use case وعقده واختباره، وتمنع God services |
| Data platform | Neon PostgreSQL | SQL Server أو document database رئيسية | يحقق requirement المباشر، وعلاقات وconstraints وFTS ومعاملات قوية |
| Persistence | EF Core with narrow repositories where justified | Generic Repository + custom generic UoW | EF يملك change tracking/UoW بالفعل؛ abstraction العامة تكرر API وتخفي قدرات PostgreSQL |
| CQRS | Logical command/query separation | Event Sourcing | ينظم التعقيد من دون تكلفة إعادة بناء الحالة وإدارة event history الدائمة |
| Public API | REST + OpenAPI 3.1 | GraphQL/OData | authorization, caching, pagination، والعقود أسهل في الضبط والاختبار لهذا المنتج |
| Browser auth | JWT in memory + rotating HttpOnly refresh cookie | tokens in localStorage أو cookie JWT طويلة | يقلل أثر token theft ويحافظ على JWT requirement وإلغاء الجلسات |
| Web topology | Same public origin through Front Door | permanent cross-origin Web/API | يقلل CORS, CSRF, cookie، وSSR complexity، مع إبقاء workloads منفصلة داخليًا |
| Search | PostgreSQL FTS + pg_trgm first | OpenSearch from launch | حجم تشغيل أقل واتساق أسهل؛ الانتقال يبقى ممكنًا عبر projection |
| Files | direct multipart upload to quarantine storage | تمرير الفيديو كاملًا عبر API أو تخزينه في DB | يمنع استهلاك API memory/bandwidth ويعزل المحتوى غير الموثوق |
| Jobs | Hangfire + transactional outbox | fire-and-forget أو message broker مبكر | durable retries وdashboard مناسب داخل monolith، مع منع ضياع العمل بعد commit |
| Realtime | SignalR as best-effort notification channel | SignalR as source of truth | reconnect لا يسبب فقد business state، والعميل يستطيع REST resync |
| Angular rendering | SSR public pages, CSR protected workspaces | SSR لكل البيانات أو CSR كامل | SEO وأداء عام جيدان بلا خطر cache/SSR leakage للبيانات الشخصية |
| Frontend state | Signals + RxJS route-scoped state | Global NgRx store from launch | يقلل boilerplate والتسرب بين features؛ يمكن إضافة store مركزي بقرار مثبت لاحقًا |
| Production compute | Azure Container Apps | Kubernetes | يوفر revisions, jobs, autoscaling، وWebSockets مع عبء تشغيلي مناسب لفريق البداية |
| Tenancy | Single marketplace | speculative TenantId everywhere | لا يوجد requirement تعاقدي للعزل؛ يمنع تعقيد كل query/index/cache مسبقًا |

## 9. Planned Folder Structure

هذا الهيكل هو التصميم المستهدف، ولم ينشأ أي مجلد منه في مرحلة التخطيط الحالية.

```text
Dorosak/
|-- .github/
|   |-- workflows/
|   `-- CODEOWNERS
|-- backend/
|   |-- Dorosak.slnx
|   |-- Directory.Build.props
|   |-- Directory.Packages.props
|   |-- global.json
|   |-- src/
|   |   |-- Dorosak.Domain/
|   |   |-- Dorosak.Application/
|   |   |-- Dorosak.Infrastructure/
|   |   |-- Dorosak.Api/
|   |   |-- Dorosak.Worker/
|   |   `-- Dorosak.MediaWorker/
|   `-- tests/
|       |-- Dorosak.Domain.UnitTests/
|       |-- Dorosak.Application.UnitTests/
|       |-- Dorosak.Application.IntegrationTests/
|       |-- Dorosak.Api.IntegrationTests/
|       |-- Dorosak.ArchitectureTests/
|       `-- Dorosak.PerformanceTests/
|-- frontend/
|   |-- src/
|   |   |-- app/
|   |   |   |-- core/
|   |   |   |-- shells/
|   |   |   |-- shared/
|   |   |   `-- features/
|   |   |-- assets/
|   |   |-- styles/
|   |   `-- environments/
|   |-- e2e/
|   `-- public/
|-- deploy/
|   |-- bicep/
|   |-- docker/
|   `-- scripts/
|-- docs/
|   |-- DEPLOYMENT_GUIDE.md
|   |-- OPERATIONS_GUIDE.md
|   |-- BACKUP_RESTORE_GUIDE.md
|   |-- adr/
|   |-- api/
|   |-- diagrams/
|   |-- runbooks/
|   `-- threat-models/
|-- .editorconfig
|-- .gitignore
|-- docker-compose.yml
|-- docker-compose.dev.yml
|-- LICENSE
|-- PROJECT_PLAN.md
`-- README.md
```

### 9.1 Backend Feature Layout

كل مشروع ينظم بالميزة أولًا بدل مجلدات عامة ضخمة. المثال المعماري داخل `Dorosak.Application` هو:

```text
Features/
|-- Identity/
|   |-- Register/
|   |-- SignIn/
|   |-- RefreshSession/
|   `-- ResetPassword/
|-- Courses/
|   |-- CreateCourse/
|   |-- UpdateCourse/
|   |-- GetCourse/
|   `-- PublishCourse/
`-- Learning/
    |-- Enroll/
    |-- UpdateProgress/
    `-- GetContinueLearning/
```

كل use case يجمع request, response, validator, handler، والاختبارات المقابلة. الأنواع المشتركة توضع في أقرب module مشترك، ولا ينقل شيء إلى `Common` لمجرد استخدامه مرتين.

### 9.2 Frontend Feature Layout

```text
app/
|-- core/
|   |-- api/
|   |-- auth/
|   |-- error-handling/
|   |-- i18n/
|   |-- pwa/
|   |-- realtime/
|   |-- telemetry/
|   `-- theme/
|-- shells/
|   |-- public-shell/
|   |-- auth-shell/
|   |-- workspace-shell/
|   |-- learning-shell/
|   `-- admin-shell/
|-- shared/
|   |-- ui/
|   |-- directives/
|   |-- pipes/
|   |-- forms/
|   `-- utilities/
`-- features/
    |-- auth/
    |-- catalog/
    |-- search/
    |-- course-details/
    |-- dashboard/
    |-- my-learning/
    |-- learning-player/
    |-- assessments/
    |-- chat/
    |-- notifications/
    |-- profile-settings/
    |-- instructor/
    `-- admin/
```

- `core` يحتوي singleton infrastructure بلا UI أعمال.
- `shared` لا يستدعي API ولا يملك state خاصًا بميزة.
- كل feature lazy-loaded وله public entry points واضحة.
- يمنع deep imports والدورات بين features بواسطة lint rules.
- لا يستخدم `NgModule` تطبيقي؛ جميع components, directives, pipes standalone.

## 10. Backend Architecture

### 10.1 HTTP Request Pipeline

الترتيب الملزم للـmiddleware:

1. Forwarded headers من proxies موثوقة وhost filtering.
2. Correlation ID وW3C trace context.
3. Serilog request logging مع redaction.
4. Global exception handler وRFC 9457 ProblemDetails.
5. HTTPS enforcement, HSTS، وsecurity headers في Production.
6. Response compression مع استثناء المحتوى الحساس أو المضغوط أصلًا.
7. Static file rules للملفات العامة المبنية فقط؛ ملفات المستخدم لا تقدم من API origin.
8. Routing, API versioning، وحدود request/body/content type.
9. CORS للبيئات التي تحتاج cross-origin فقط وبـallow-list صريحة.
10. Authentication.
11. Rate limiting بحسب endpoint وIP/user/session.
12. Authorization.
13. Antiforgery للعمليات التي تستخدم cookies أو تغير session.
14. Output Cache للـpublic GET endpoints المعتمدة فقط.
15. Controller endpoint ثم MediatR pipeline.

يثبت ترتيب pipeline باختبارات Integration، ولا تسجل request/response bodies بصورة افتراضية.

#### 10.1.1 Rate Limiting Baseline

يطبق WAF حدًا عامًا قبل الوصول إلى التطبيق، ثم يطبق ASP.NET Core سياسات أدق. القيم التالية baseline إطلاق وتعدل فقط من telemetry واختبارات الحمل، لا بالتخمين:

| Policy | Initial limit | Partition |
|---|---:|---|
| Anonymous API | `120 requests/minute` مع burst محدود | normalized client IP |
| Authenticated API | `600 requests/minute` | user + session |
| Sign-in | `5 attempts/5 minutes` و`20/IP/5 minutes` | normalized account identifier + IP |
| Registration | `5/hour` | IP + device risk signal |
| Password reset | `3/account/hour` و`10/IP/hour` | account identifier + IP |
| Refresh | `30/5 minutes` | refresh session + IP signal |
| Public search | `60/minute`; authenticated `180/minute` | IP or user |
| Upload sessions | `20/hour`, وثلاث uploads متزامنة | user |
| Chat messages | `30/minute` | user + conversation |
| SignalR negotiate | `10/minute` | user/IP |
| Heavy exports/transcodes | concurrency limiter حسب queue | user/role |

- تستخدم token bucket أو sliding window للطلبات، وconcurrency limiter للعمليات الثقيلة.
- تطبع IPv6 ويقبل client IP فقط من forwarded headers الخاصة بالـproxies الموثوقة.
- يعيد الرفض `429` مع `Retry-After`، ولا يكشف هل account موجودة.
- WAF يطبق حدود IP العامة، بينما `RedisRateLimitStore` يطبق token/sliding windows ذرية عبر Lua للـuser/session/account partitions على كل replicas؛ limiter المحلي يبقى طبقة أخيرة فقط.
- عند تعطل Redis تستمر public reads تحت WAF وحد محلي محافظ، لكن login/register/reset/refresh, upload issuance، وadmin mutations تفشل مغلقًا بـ`503` و`Retry-After` بعد مهلة قصيرة بدل السماح بتجاوز حد موزع. يطلق ذلك alert فوريًا ويختبر كحالة resilience.

### 10.2 MediatR Pipeline

الترتيب الملزم:

1. `TelemetryBehavior` لإنشاء span وقياس use case كاملة.
2. `LoggingBehavior` لاسم العملية والمدة والنتيجة الآمنة.
3. `ValidationBehavior` باستخدام FluentValidation وCancellationToken.
4. `AuthorizationBehavior` للصلاحيات العامة القابلة لإعادة الاستخدام خارج HTTP.
5. `QueryCacheBehavior` للqueries المعلّمة explicit cacheable فقط، وبعد حسم user/locale/permission scope.
6. `TransactionBehavior` يفتح transaction لأوامر الكتابة.
7. `IdempotencyBehavior` يعمل داخل transaction للأوامر المعلّمة فقط، فيقرأ أو ينشئ operation record بالقفل المناسب.
8. Handler.
9. تحفظ business changes وidempotency result وoutbox في `SaveChangesAsync` واحدة ثم commit واحدة؛ cache invalidation يحدث عبر event بعد نجاح commit.

فحص ownership الذي يحتاج تحميل المورد يبقى داخل use case أو داخل query نفسها، ولا تفتح transaction قبل validation والفحوص الرخيصة. لا يستخدم QueryCacheBehavior للبيانات الشخصية افتراضيًا، ولا يسمح للcache hit بتجاوز authorization. إذا حدث crash قبل commit فلا توجد business change؛ وإذا حدث بعد commit توجد نتيجة idempotency في المعاملة نفسها، ولذلك تعيد المحاولة النتيجة السابقة ولا تنفذ العملية ثانية.

### 10.3 Result and ProblemDetails

- يوجد `Result` واحد للأخطاء المتوقعة: Validation, Unauthorized, Forbidden, NotFound, Conflict, BusinessRule, RateLimited.
- تستخدم exceptions للأخطاء غير المتوقعة أو خرق افتراض برمجي، ولا تستخدم لتدفق أعمال طبيعي.
- Domain وApplication لا يحتويان HTTP status codes.
- كل error له `code` إنجليزي ثابت قابل للاختبار، ورسالة آمنة قابلة للترجمة في الواجهة.
- ProblemDetails يحتوي `type`, `title`, `status`, `code`, `traceId`، و`errors` للحقول عند validation.
- Production لا يعرض stack trace أو SQL أو أسماء constraints أو provider details.

| Situation | HTTP status |
|---|---:|
| Malformed transport input | `400 Bad Request` |
| Valid JSON failing use-case validation | `422 Unprocessable Entity` |
| Missing/invalid access token | `401 Unauthorized` |
| Authenticated without permission | `403 Forbidden` |
| Hidden or absent private resource | `404 Not Found` |
| Duplicate/state conflict | `409 Conflict` |
| ETag concurrency conflict | `412 Precondition Failed` |
| Unsupported media type | `415 Unsupported Media Type` |
| Request too large | `413 Content Too Large` |
| Rate limit | `429 Too Many Requests` مع `Retry-After` |
| Unexpected failure | `500 Internal Server Error` |

### 10.4 Persistence Rules

- EF Core lazy loading ممنوع.
- Queries تستخدم projection إلى DTO و`AsNoTracking`، ولا تحمل aggregate كاملًا للقراءة.
- كل list endpoint له limit أقصى وordering ثابت.
- `CancellationToken` يمر من HTTP إلى MediatR وEF Core وHttpClient وstorage.
- لا يستخدم `SaveChangesAsync` داخل repositories؛ transaction behavior ينفذ commit واحدة.
- تستخدم batch operations عندما تكون semantics واضحة، ولا توجد loop ترسل query لكل row.
- AutoMapper يستخدم `ProjectTo` للقراءات البسيطة فقط. الكتابات الحساسة mapping صريح allow-listed.
- أي query جديدة على مسار حرج تراجع بـ`EXPLAIN (ANALYZE, BUFFERS)` على بيانات واقعية قبل Production.

### 10.5 Background Work

| Queue | Workloads | Initial worker policy |
|---|---|---|
| `critical` | password/email verification, payment fulfillment, security notifications | نسختان في Production، retries محدودة وتنبيه سريع |
| `notifications` | in-app fan-out, SignalR publish, transactional email | scaling حسب oldest job age |
| `default` | certificates, aggregates, recommendations, cleanup | نسخة واحدة على الأقل |
| `bulk` | exports, backfills, large admin operations | concurrency منخفضة لحماية database |
| `media` | scan, image processing, video transcoding | MediaWorker منفصل وحدود CPU/memory |

- Hangfire Server لا يعمل داخل API في Production.
- job arguments تحمل IDs وعقودًا صغيرة فقط، ولا تحمل entities أو tokens أو ملفات أو PII كبيرة.
- كل job idempotent، وتصنف الأخطاء إلى transient وpermanent.
- retries تستخدم exponential backoff مع jitter؛ بعد الاستنفاد تبقى العملية Failed وتطلق alert ولا تدور إلى الأبد.
- Hangfire Dashboard خلف شبكة إدارية وSSO/RBAC و2FA، وليس public.
- recurring jobs تعمل بتوقيت UTC وبـsingleton guard عند الحاجة.

### 10.6 Realtime Architecture

- يستخدم endpoint واحد `/hubs/realtime` لتقليل الاتصالات، مع typed versioned event contracts.
- المجموعات: user session, conversation, course release, instructor dashboard، وadmin operations.
- انضمام group يحدث في الخادم بعد authorization؛ اسم group القادم من العميل لا يثق به.
- الرسائل الدائمة والإشعارات وتحديثات المقرر تحفظ وتلتزم أولًا، ثم تبث عبر outbox/job.
- typing indicators وpresence فقط أحداث مؤقتة، لها TTL ولا تسجل كحقيقة أعمال.
- كل event دائم يحمل `eventId`, `eventType`, `schemaVersion`, `occurredAt`, `resourceId`, و`sequence` المناسب.
- العميل يعيد الاتصال بـexponential backoff وjitter، ثم يستدعي REST للمزامنة منذ آخر sequence.
- payloads محدودة، ولا يرسل video/files عبر SignalR.
- access token في query string مسموح فقط لضرورة WebSocket، ويحذف query string من كل logging وAPM.
- عند تعدد replicas يستخدم Redis backplane وsticky sessions وفق متطلبات النقل المختبرة.
- `CloseOnAuthenticationExpiration` مفعلة؛ لا يبقى connection مصرحًا بعد انتهاء JWT.
- `IHubFilter` يفحص session active و`authz_ver` عند كل hub invocation وgroup join، ويفشل مغلقًا عند تعذر authorization store.
- group keys المحمية تحمل authorization/membership version، مثل user/session أو conversation membership version؛ publishers تستهدف النسخة الحالية فقط بعد permission/session change، فلا تتلقى connections القديمة أحداثًا جديدة.
- revocation أو permission change ينشر event لكل replicas لإزالة membership محليًا وإرسال `ReauthenticateRequired`; العميل يغلق ويعيد المصادقة، بينما لا يعتمد الأمن على التزام العميل لأن hub invocations وoutbound groups versioned.

### 10.7 File and Media Pipeline

حالات الأصل: `Initiated -> Uploaded -> Scanning -> Processing -> Ready` أو `Rejected -> Deleted`.

| Asset type | Default maximum | Processing |
|---|---:|---|
| Profile image | `10 MB` | decode, re-encode, strip metadata, responsive variants |
| Course image | `20 MB` | AVIF/WebP/JPEG variants and dimensions |
| Course document | `100 MB` | type validation, malware scan, safe download/view policy |
| Assignment submission | `250 MB` | quota, scan, private storage |
| Source video | `10 GB` | multipart upload, ffprobe, HLS transcode |

- الحدود قابلة للضبط Configuration لكنها لا تلغى في Production.
- quota baseline: teacher `500 GB` لكل account و`200 GB` لكل course؛ student `10 GB` لكل account للـsubmissions؛ daily upload baseline `100 GB` للteacher و`20 GB` للstudent. تعدل values بخطة المنتج، لكن لا تصدر signed URL قبل فحص quota وconcurrency والرصيد المتوقع.
- بعد complete يتحقق worker من الحجم الفعلي وchecksum ثم يحاسب quota؛ oversized/orphaned uploads تلغى وتحذف، وتراقب storage وegress per user/course.
- الرفع الكبير يذهب مباشرة إلى private quarantine storage عبر short-lived signed multipart URLs.
- API streaming upload متاح للملفات الصغيرة ولا يحمل الملف كاملًا في الذاكرة.
- chunk size يكون بين `8 MiB` و`64 MiB` وفق المزود والشبكة، مع checksum وpart number فريدين.
- أسماء object keys مولدة server-side ولا تستخدم filename كمسار.
- `Content-Type` والامتداد لا يثقان بهما؛ يفحص magic bytes والبنية الفعلية.
- تعطل malware scanner يؤدي إلى بقاء الملف في Quarantine، وليس fail-open.
- FFmpeg يعمل non-root، بلا shell interpolation، وبلا network egress، مع time/CPU/memory limits.
- user-generated HTML وSVG لا يقدمان من application origin.
- private media تقدم عبر signed CDN URL أو cookie قصيرة الصلاحية بعد entitlement check.

### 10.8 Compression, Streaming, and Static Optimization

- Brotli يفضل عبر HTTPS للـJSON/CSS/JS، وGzip fallback.
- لا تضغط video, images, archives، أو responses تحتوي secrets وقيمًا يعكسها المهاجم بما يفتح مخاطر BREACH.
- exports الكبيرة تنفذ كjob ثم signed download؛ لا تنشأ JSON arrays ضخمة في الذاكرة.
- response streaming يستخدم `IAsyncEnumerable` أو NDJSON فقط لعقود bounded ومعلنة يحتاجها العميل؛ القوائم العادية تبقى paginated، والتحديث الحي يستخدم SignalR بدل اتصال HTTP طويل غير ضروري.
- downloads تستخدم streaming وrange support ولا تنسخ كامل الملف عبر API إذا أمكن redirect موقّع.
- static assets تحمل content hash و`Cache-Control: public, max-age=31536000, immutable`.
- HTML SSR لا يحمل immutable caching؛ يستخدم cache قصيرًا أو revalidation حسب الصفحة.

## 11. Frontend Architecture

### 11.1 Angular Rules

- جميع components, directives, pipes standalone.
- التطبيق zoneless إذا اجتازت جميع dependencies الاختبارات؛ هذا هو baseline المعتمد.
- `ChangeDetectionStrategy.OnPush` صريح لكل component تطبيقي.
- Signals للحالة المحلية والمتزامنة والمشتقة؛ `computed` نقي و`effect` للآثار الخارجية فقط.
- RxJS لـHTTP, WebSocket, timing, cancellation، والتدفقات غير المتزامنة.
- لا تخزن الحقيقة نفسها في Signal وObservable معًا.
- `takeUntilDestroyed` أو Angular lifecycle interop لكل subscription غير مستهلكة عبر template.
- لا nested subscriptions. يستخدم `switchMap` للبحث، `exhaustMap` للإرسال غير القابل للتكرار، و`concatMap` للطوابير المرتبة.
- تستخدم typed Reactive Forms؛ لا تستخدم forms API تجريبية في Production.
- built-in control flow و`@for (...; track item.id)` بدل loops غير مستقرة.
- TypeScript وAngular templates في strict mode؛ `any` ممنوع إلا داخل adapter معزول وموثق لا يمكن تجنبه.

### 11.2 Rendering and Routing

| Route group | Shell | Rendering | Indexing |
|---|---|---|---|
| `/:locale`, `/:locale/courses`, `/:locale/courses/:slug` | `PublicShell` | SSR + hydration | indexable مع metadata وcanonical |
| `/:locale/search` | `PublicShell` | SSR للهيكل والنتائج العامة | `noindex,follow` لمنع صفحات filters المكررة |
| `/:locale/auth/**` | `AuthShell` | prerender/SSR generic ثم hydration | noindex |
| `/:locale/dashboard`, `/:locale/my-learning`, `/:locale/profile`, `/:locale/settings` | `WorkspaceShell` | CSR بعد session check | noindex, no Transfer Cache |
| `/:locale/learn/**` | `LearningShell` | CSR | noindex |
| `/:locale/instructor/**` | `WorkspaceShell` | CSR, lazy | noindex |
| `/:locale/admin/**` | `AdminShell` | CSR, lazy, never preloaded | noindex |
| `/:locale/certificates/verify/:code` | `PublicShell` | SSR | index policy حسب الخصوصية |

- protected data لا يدخل SSR HTML ولا Angular Transfer Cache.
- guards: session, anonymous-only, permission/capability, pending changes.
- guard يعيد UrlTree/redirect result ولا ينفذ navigation جانبية.
- resolvers تستخدم فقط لبيانات identity/access أو above-the-fold الصغيرة.
- route metadata تحدد title, breadcrumb, permission, render mode، وpreload hint.
- 404 و403 يعيدان الحالة الصحيحة في SSR.
- قيم `locale` المسموحة هي `ar` و`en`; المسار هو مصدر لغة SSR وSEO والcache، وليس Cookie أو `Accept-Language` غير ظاهر في URL.
- `/` يعيد redirect إلى locale المحفوظة أو `/ar` كقيمة افتراضية، وكل صفحة عامة تنتج `canonical`, `hreflang="ar"`, `hreflang="en"`، و`x-default`.
- `course_slugs` registry تفرض uniqueness على `(locale, slug)` للقيم الحالية والتاريخية معًا، وتحفظ redirects الدائمة ولا تسمح بإعادة الاستخدام.
- sitemap منفصلة أو مفهرسة لكل locale، بينما API يستخدم `Accept-Language` وprofile preference ولا يغير عنوان المورد.
- public SSR/Output Cache keys تشمل locale من path؛ theme cookie لا تغير HTML القابل للتخزين ويطبق الثيم قبل الرسم بطريقة CSP-safe.

### 11.3 State Management

- لا يستخدم NgRx عند البداية.
- global state محدود إلى session, preferences, connectivity, feature flags, realtime connection، وnotification badge.
- كل feature يملك route-scoped signal store/service ويزال عند مغادرة route.
- server state لا ينسخ إلى global store بلا حاجة؛ يستخدم query/resource abstraction محلية مع cache policy واضحة.
- optimistic UI مسموح للbookmark, notification read, chat send, notes، وprogress مع pending/rollback/deduplication.
- الدفع والنشر والحذف الإداري والدرجات ليست optimistic.
- كل async state يميز `idle`, `loading`, `refreshing`, `success`, `empty`, `error`, `offline`.

### 11.4 HTTP and Error Handling

ترتيب functional interceptors:

1. API URL allow-list وlocale/timezone/correlation metadata.
2. Deadline/cancellation policy.
3. Bearer access token من الذاكرة فقط.
4. Single-flight refresh handling لأول 401 صالح للتجديد.
5. Retry للـidempotent GET فقط مع احترام Retry-After؛ mutations لا تعاد تلقائيًا.
6. RFC 9457 ProblemDetails normalization.
7. Telemetry and safe error reporting.

- OpenAPI 3.1 هو مصدر transport contracts، وتولد DTOs ثم تحول إلى frontend domain models.
- UI لا يعتمد على نص error الإنجليزي القادم من الخادم؛ يعتمد على `code` ثابت وترجمة محلية.
- field errors تظهر بجوار الحقل، feature errors قرب المحتوى، وfatal screen فقط للفشل غير القابل للاسترداد.
- لا يوجد endless spinner؛ كل loading له timeout أو error state وإجراء retry.
- global ErrorHandler يبلغ الخطأ بعد إزالة PII ويعرض fallback مناسبًا.
- route-level error boundaries تمنع فشل lazy feature من إسقاط التطبيق كله، وتوفر retry أو عودة آمنة مع trace ID.
- loading indicator عام رفيع يظهر للتنقلات والعمليات التي تتجاوز `150 ms`، بينما تستخدم المكونات Skeleton Loading ثابتة الأبعاد للمحتوى المتوقع.
- Global Toast service واحدة تعرض النجاح أو الأحداث الفورية المهمة في ARIA live region؛ لا تستخدم toast لأخطاء الحقول أو رسائل تحتاج قرارًا.

### 11.5 Lazy Loading and Performance

- جميع features lazy-loaded وتنتج chunks مستقلة.
- Code Splitting وTree Shaking بوابتان في build؛ imports تكون من public entry points ولا تستخدم barrel files توسع bundle بلا حاجة.
- adaptive preloading يبدأ بعد استقرار التطبيق فقط، وعلى شبكة سريعة بلا `saveData`، وحسب الدور والمسار المتوقع.
- Admin, player, editor، وmedia tooling لا تحمل مسبقًا.
- يستخدم `@defer` للcharts, recommendations, transcript, notes panel, rich editor، والمرفقات الثانوية.
- placeholders لها أبعاد ثابتة لمنع layout shift، مع loading/error states.
- reusable Skeleton components تطابق شكل القائمة أو المحتوى الفعلي ولا تعرض نصوصًا وهمية للمستخدم.
- CDK virtual scrolling للقوائم الكبيرة ذات الارتفاع المتجانس فقط.
- infinite scrolling يستخدم cursor وزر `Load more` متاحًا دائمًا للوصول والعودة إلى footer.
- الصور تستخدم responsive AVIF/WebP, dimensions ثابتة، و`NgOptimizedImage`.
- instant search ينتظر `250 ms`, يستخدم `distinctUntilChanged` و`switchMap`, يلغي الطلب السابق، يبدأ من حرفين، ويعرض ثمانية suggestions كحد أقصى.

| Frontend budget | Limit |
|---|---:|
| Initial JavaScript | `<= 200 KiB gzip` |
| Initial CSS | `<= 40 KiB gzip` |
| Critical fonts | `<= 120 KiB` |
| Normal feature chunk | `<= 120 KiB gzip` |
| Player/editor exceptional chunk | `<= 250 KiB gzip`, documented and lazy |

### 11.6 PWA and Offline

- Angular Service Worker يخزن app shell والassets ذات hash فقط.
- `manifest.webmanifest` يحتوي name, short_name, start_url `/ar`, scope `/`, display `standalone`, theme/background colors، وأيقونات `192x192`, `512x512`, وmaskable variants.
- navigation request strategy تكون network-first للحفاظ على SSR، ثم fallback إلى app shell/Offline page؛ تستبعد `/api/**`, `/hubs/**`, provider callbacks، وsigned media paths صراحة.
- installability تختبر على Chromium وAndroid، بينما iOS يحصل على metadata/icons وسلوك fallback المناسب من دون افتراض دعم متطابق.
- IndexedDB application layer تخزن metadata ودروسًا نصية مصرحًا بها وnotes/bookmarks/progress outbox.
- لا تخزن auth session, refresh token, admin data, payment data, notification history، أو signed URLs في Service Worker cache.
- الفيديو المحمي لا يتاح offline في الإصدار الأول.
- queued offline mutations تحمل stable client IDs وidempotency keys.
- chat drafts لا ترسل تلقائيًا عند عودة الاتصال.
- logout أو تبديل الحساب يمسح caches وIndexedDB الخاصة بالمستخدم قبل إظهار شاشة الدخول.
- app update يعرض prompt ولا يجبر reload أثناء درس نشط أو form dirty.
- توجد صفحة Offline واضحة وإدارة storage quota وexpiry.
- CI يتحقق من manifest, icons, Service Worker scope، وعدم cache أي auth/API response حساسة.

### 11.7 Design System and UX

اللغة البصرية عملية وهادئة وليست مجموعة cards زخرفية. لا يستخدم glassmorphism إلا كتأثير محدود في overlay إذا حافظ على التباين، ولا يستخدم كهوية عامة.

| Token | Light baseline | Dark baseline | Use |
|---|---|---|---|
| `--color-canvas` | `#F6F7F9` | `#0B1220` | page background |
| `--color-surface` | `#FFFFFF` | `#121C2D` | primary surfaces |
| `--color-text` | `#101828` | `#F1F5F9` | primary text |
| `--color-muted` | `#667085` | `#94A3B8` | secondary text |
| `--color-brand` | `#0F766E` | `#2DD4BF` | primary actions |
| `--color-link` | `#2563EB` | `#60A5FA` | links and focus |
| `--color-success` | `#15803D` | `#4ADE80` | success |
| `--color-warning` | `#B45309` | `#FBBF24` | warning |
| `--color-danger` | `#B42318` | `#F87171` | destructive/error |

- Arabic font: self-hosted `IBM Plex Sans Arabic`; Latin font: self-hosted `Inter`، بعد تثبيت licenses.
- spacing scale ثابتة، radius بحد أقصى 8px لمعظم surfaces، وظلال قليلة.
- mobile-first من 320px؛ side navigation على desktop وoffcanvas على mobile.
- تختبر breakpoints على phone/tablet/laptop/desktop في portrait وlandscape، ولا يسمح بتداخل النص أو الأزرار أو اللوحات.
- learning player له shell مستقل قليل التشتيت.
- CSS logical properties لكل spacing/alignment لدعم RTL/LTR.
- theme: Light, Dark, System عبر CSS variables و`data-bs-theme`، محفوظ في cookie غير حساسة وprofile.
- هدف اللمس 44x44px، focus ظاهر، ولا تحمل الألوان المعنى وحدها.
- الحركة 120-240ms للتغذية الراجعة فقط وتحترم `prefers-reduced-motion`.
- Angular Animations تنفذ عبر stable native `animate.enter` و`animate.leave` وCSS transitions؛ لا يستخدم legacy animation engine إذا كان deprecated في Angular 21.
- SweetAlert2 للتأكيدات البسيطة؛ CDK Dialog للنماذج المعقدة مع focus trap واستعادة focus.
- الفيديو يدعم keyboard, captions, transcript, speed, quality, volume, PiP، ولا يبدأ بصوت تلقائي.

### 11.8 UX Priorities by Workspace

| Workspace | Primary UX |
|---|---|
| Student | Continue Learning أولًا، المهام والمواعيد والتقدم والإعلانات، والوصول للدرس التالي بإجراء واحد |
| Teacher | review queue, course health, authoring autosave/version conflict, learners, grading, announcements, useful analytics |
| Admin | server-side tables, saved filters, explicit bulk scope, audit reason, reauthentication, conflict UI، وimpersonation banner إن أضيفت الميزة |
| Search | keyboard typeahead, URL-synchronized filters, mobile drawer, accessible highlights, empty and correction states |
| Chat | conversation list, unread state, pending/failed messages, retry, attachments, temporary typing/presence |

## 12. Database Design

### 12.1 Database Principles

- قاعدة Neon واحدة في البداية، مع PostgreSQL schemas حسب ownership.
- PostgreSQL هو مصدر الحقيقة لكل business state وsession state الدائمة.
- الملفات والـbinary content لا تخزن داخل PostgreSQL.
- `UUIDv7` يولد في التطبيق ويخزن `uuid` للـaggregate roots والأحداث العامة.
- أسماء الجداول والأعمدة والقيود والفهارس `snake_case`.
- كل instant يخزن `timestamptz` ويعامل كـUTC.
- time zone للأعمال يخزن باسم IANA مثل `Asia/Amman`، وليس offset ثابتًا.
- المال `numeric(20,4)` مع `currency_code char(3)`، ولا يستخدم float أو PostgreSQL money.
- الحالات تخزن `varchar` مع CHECK constraints بدل PostgreSQL enum لتسهيل migrations.
- JSONB يستخدم فقط لprovider metadata أو payload versioned محدود، وليس بديلًا عن تصميم علائقي.
- كل FK كثير الاستخدام له index صريح لأن PostgreSQL لا ينشئه تلقائيًا.
- optimistic concurrency يستخدم `version bigint` ويعرض كـopaque ETag عند الحاجة.
- لا توجد database triggers لمنطق أعمال معقد. تسمح triggers فقط لسلامة تقنية مثبتة مثل ledger balancing أو immutable audit guard.

### 12.2 PostgreSQL Extensions

| Extension | Purpose |
|---|---|
| `pg_trgm` | autocomplete, typo tolerance، وفهارس similarity |
| `unaccent` | normalization مساعد للغات المدعومة مع عدم اعتباره stemmer عربيًا |
| `pg_stat_statements` | تحليل أداء الاستعلامات |

لا يعتمد إنشاء UUID على database extension لأن UUIDv7 يولد في التطبيق. كل extension تختبر على Neon branch قبل migration الإنتاج.

### 12.3 Schema and Table Inventory

| Schema | Tables | Core rules |
|---|---|---|
| `identity` | `users`, `roles`, `user_roles`, `user_claims`, `role_claims`, `user_logins`, `user_tokens` | ASP.NET Core Identity mappings، normalized email unique، no default admin |
| `identity` | `refresh_sessions`, `refresh_tokens`, `security_events` | token hashes فقط، family rotation، revoke reason، append-only security events |
| `profiles` | `profiles`, `teacher_profiles`, `teacher_applications`, `user_preferences` | account منفصل عن public profile، teacher approval workflow |
| `catalog` | `courses`, `course_localizations`, `course_slugs`, `course_instructors`, `categories`, `category_localizations`, `course_categories`, `tags`, `tag_localizations`, `course_tags` | `courses.owner_user_id` هو المالك الوحيد، collaborators لا يكررون Owner، وسجل slug موحد لكل locale |
| `catalog` | `course_releases`, `course_release_sections`, `course_release_lessons`, `course_release_assessments`, `catalog_documents`, `featured_courses`, `popularity_snapshots`, `recommendation_snapshots` | manifest بجداول concrete FKs، published releases immutable، read models rebuildable |
| `authoring` | `course_drafts`, `sections`, `section_revisions`, `lessons`, `lesson_revisions`, `publication_reviews` | one active draft per course، ordered rank، revision history، review state |
| `media` | `upload_sessions`, `upload_parts`, `media_assets`, `media_variants`, `caption_tracks` | quarantine lifecycle، checksums، immutable ready variants؛ references تأتي من concrete FK tables المالكة |
| `learning` | `entitlements`, `enrollments`, `lesson_progress`, `course_completions`, `learning_sessions` | entitlement لمقرر محدد في v1، one enrollment per learner/course، monotonic completion، release pinned |
| `learning` | `bookmarks`, `notes`, `recently_viewed`, `wishlist_items` | user ownership، unique pairs، retention for recently viewed |
| `assessment` | `quizzes`, `quiz_versions`, `questions`, `question_options`, `quiz_attempts`, `quiz_answers` | version pinned، attempt limits، score constraints |
| `assessment` | `assignments`, `assignment_versions`, `assignment_submissions`, `submission_files`, `grades`, `grade_revisions` | immutable submitted snapshot، authorized grading، grade audit |
| `engagement` | `reviews`, `rating_summaries`, `discussion_threads`, `comments`, `comment_likes` | one review per learner/course، max reply depth، unique likes |
| `engagement` | `content_reports`, `moderation_cases`, `moderation_actions` | target columns concrete مع exactly-one CHECK، state workflow، all actions audited |
| `communication` | `conversations`, `conversation_participants`, `messages`, `message_receipts` | participant authorization، client message id dedupe، soft delete policy |
| `communication` | `notifications`, `notification_deliveries`, `notification_preferences`, `announcements`, `announcement_targets`, `email_messages` | durable notifications، per-channel preferences، delivery attempts |
| `commerce` | `products`, `subscription_plans`, `sales_offers`, `coupons`, `coupon_rules`, `coupon_redemptions`, `checkout_sessions` | product target concrete، price quote snapshot، time-bounded offer، redemption uniqueness |
| `commerce` | `orders`, `order_items`, `payments`, `payment_events`, `refund_requests`, `refunds`, `disputes`, `invoices`, `subscriptions`, `subscription_events`, `reconciliation_runs` | immutable monetary snapshots، provider event dedupe، chargeback/reconciliation workflow، no delete |
| `commerce` | `ledger_accounts`, `ledger_transactions`, `ledger_entries`, `teacher_earning_entries`, `payout_accounts`, `payouts`, `payout_items` | double-entry append-only ledger، commission snapshot، KYC/provider references، balanced entries |
| `credentials` | `certificate_templates`, `certificates` | immutable issue data، public verification code unique، revoke not delete |
| `cms` | `pages`, `page_revisions`, `faqs`, `contact_messages`, `platform_settings` | publish revisions، encrypted/limited contact PII، typed settings |
| `analytics` | `analytics_events`, `course_daily_metrics`, `teacher_daily_metrics`, `platform_daily_metrics`, `search_metrics` | append events with retention، aggregates rebuildable، no raw secrets |
| `operations` | `audit_logs`, `outbox_messages`, `inbox_messages`, `idempotency_records`, `background_operations`, `webhook_receipts`, `feature_flags` | append-only audit، at-least-once dedupe، server-authoritative audited flags، TTL where legal |
| `hangfire` | provider-owned Hangfire tables | separate database role/schema/connection policy |

### 12.4 Aggregate Boundaries and Invariants

| Aggregate | Boundary and invariant |
|---|---|
| `Account` | unique normalized email، valid security state، external identity unique، no email as foreign identity |
| `Course` | owner and collaborators، valid lifecycle transitions، no unbounded lessons/enrollments collection |
| `CourseDraft` | one active draft per course، valid metadata and curriculum rank، optimistic concurrency |
| `CourseRelease` | immutable manifest referencing exact lesson/quiz/assignment/media revisions، publish only Ready assets |
| `MediaAsset` | never public before scan/process Ready، original remains private، references block physical deletion |
| `Entitlement` | learner + concrete course + source + valid period، explicit grant/revoke/expire transitions |
| `Enrollment` | unique learner/course، entitlement and assigned release، explicit status transitions |
| `LessonProgress` | unique enrollment/lesson، completion monotonic، stale heartbeat cannot revert completion |
| `QuizAttempt` | tied to quiz version and enrollment، attempt policy and deadline enforced server-side |
| `AssignmentSubmission` | submission ownership and deadline، submitted revision fixed، files must be Ready |
| `Order` | one currency، immutable price/tax/discount snapshots، total equals components |
| `Payment` | provider transitions idempotent، captured event required before entitlement |
| `Refund` | cumulative refund cannot exceed captured amount |
| `LedgerTransaction` | entries balanced per currency قبل posting، immutable بعد posting، correction بقيد عكسي |
| `Review` | one learner/course، rating 1..5، eligibility server-verified |
| `Certificate` | immutable issue data، revoke only، unique verification code |

### 12.5 Entity Relationships

- `identity.users 1 -> 1 profiles.profiles`.
- `identity.users 1 -> 0..1 profiles.teacher_profiles`.
- `catalog.courses N -> 1 identity.users` عبر `owner_user_id` بصفته مصدر ملكية المقرر الوحيد.
- `catalog.courses N <-> N identity.users` عبر `course_instructors` بأدوار `Editor`, `CoInstructor`, `Reviewer`; لا يسمح بدور Owner داخل الجدول.
- `catalog.courses N <-> N catalog.categories` و`catalog.tags` عبر join tables.
- `catalog.courses 1 -> N catalog.course_releases`.
- `catalog.course_releases 1 -> N course_release_sections/course_release_lessons/course_release_assessments`، وكل صف يملك FK concrete إلى revision محددة وترتيب ثابت.
- `catalog.courses 1 -> 1 authoring.course_drafts` نشطة كحد أقصى.
- `authoring.sections 1 -> N authoring.lessons` بهويات مستقرة وrevisions منفصلة.
- `media.media_assets 1 -> N media.media_variants` و`caption_tracks`; `lesson_revisions`, `submission_files`, profile images، وCMS media تشير إليه بـFK concrete.
- `learning.entitlements N -> 1 identity.users` و`catalog.courses`; مصدره free claim أو order item أو subscription claim موثق.
- `learning.enrollments N -> 1 identity.users`, `catalog.courses`, `course_releases`، و`entitlements`.
- `learning.enrollments 1 -> N lesson_progress`, quiz attempts، وassignment submissions.
- `assessment.quizzes 1 -> N quiz_versions 1 -> N questions 1 -> N question_options`.
- `assessment.assignments 1 -> N assignment_versions 1 -> N assignment_submissions`.
- `catalog.courses 1 -> N engagement.reviews` مع unique `(user_id, course_id)`.
- `discussion_threads 1 -> N comments`، و`comments` تستخدم parent id مع depth أقصى 2.
- `communication.conversations N <-> N identity.users` عبر participants.
- `communication.conversations 1 -> N messages 1 -> N message_receipts`.
- `commerce.orders 1 -> N order_items` و`1 -> N payments 1 -> N refunds`.
- `commerce.checkout_sessions 1 -> 0..1 orders`; quote expiry تمنع تحويل session منتهية إلى order بلا إعادة تسعير.
- `commerce.payments 1 -> N disputes`; provider events وwebhook receipts تمنع التكرار.
- `commerce.ledger_transactions 1 -> N ledger_entries`; order capture/refund/chargeback/payout ينتج قيودًا متوازنة قابلة للمصالحة.
- `commerce.payouts 1 -> N payout_items N -> 1 teacher_earning_entries`; لا يدفع earning قبل KYC وsettlement hold.
- `PaymentCaptured` ينشئ entitlement idempotently عبر event، وليس cascade أو browser redirect.
- `learning.course_completions 1 -> 0..N credentials.certificates` للسماح بإعادة إصدار موثق.

### 12.6 Constraints

- unique normalized email للحسابات غير anonymized.
- unique `(issuer, subject)` للهويات الخارجية.
- `course_slugs` تفرض unique `(locale, slug)` عبر current وhistorical rows، وpartial unique `(course_id, locale) WHERE is_current`; `course_localizations.current_slug_id` FK إلى السجل.
- تغيير slug transaction ذرية تنشئ row جديدة current وتحول السابقة historical؛ الصفوف المنشورة لا تحذف، ولذلك يستحيل تعارض current مع alias في جدول آخر.
- unique `(course_id, user_id)` في `course_instructors`، مع CHECK يمنع role `Owner`; owner transfer يغير `courses.owner_user_id` ذريًا ويسجل audit.
- unique `(course_id, release_number)`.
- unique `(course_id, active_draft_marker)` لمسودة نشطة واحدة.
- unique `(learner_id, course_id)` للـenrollment الفعالة وفق policy.
- unique `(enrollment_id, lesson_id)` للـlesson progress.
- unique `(learner_id, course_id)` للreview.
- unique `(conversation_id, sender_id, client_message_id)` للرسائل.
- unique `(provider, provider_event_id)` للwebhooks.
- unique `(principal_id, operation, idempotency_key)` لسجلات idempotency.
- unique public certificate verification code.
- CHECK rating بين 1 و5، percentages بين 0 و100، duration والمبالغ غير سالبة، و`starts_at < ends_at`.
- `content_reports` تحتوي nullable concrete FKs إلى course/review/comment/message/reported_user، ويضمن `num_nonnulls(...) = 1` هدفًا واحدًا صالحًا.
- `products` تستخدم `product_type` مع concrete `course_id` أو `subscription_plan_id` وCHECK exactly one؛ لا توجد generic target IDs.
- course release manifest tables تملك FKs صريحة إلى section/lesson/assessment revisions، مع unique order rank داخل parent release.
- ledger posting يرفض transaction غير متوازنة لكل currency، ومجموع teacher earnings/payouts لا يتجاوز settled commission.
- FK داخل وعبر schemas حين يمثل المرجع حقيقة ثابتة في هذا Modular Monolith، مع `RESTRICT` افتراضيًا. لا تضحى database integrity من أجل استخراج service افتراضي.

### 12.7 Delete and Retention Rules

| Data | Rule |
|---|---|
| Draft course/profile/user content | soft delete باستخدام `deleted_at`, `deleted_by`, `deletion_reason` عند الحاجة للاستعادة |
| Published course | unpublish/archive؛ releases القديمة لا تحذف ما دام لها learners |
| Published lesson revision | تبقى طالما يشير إليها release أو progress أو assessment |
| Media asset | physical delete بعد إزالة كل references وانتهاء retention/grace period |
| Account | deactivate ثم anonymize وفق طلب الخصوصية مع الاحتفاظ القانوني المحدود |
| Orders/payments/refunds/subscriptions | لا soft delete ولا hard delete؛ state transitions وسجلات عكسية |
| Certificates | revoke، لا delete |
| Audit/security events | append-only مع retention وتصدير immutable |
| Upload sessions/idempotency/inbox | hard delete بعد TTL موثق إذا لم يوجد التزام قانوني |
| Recently viewed/search raw metrics | retention قصيرة، baseline `180 days` أو أقل حسب الخصوصية |

- `ON DELETE CASCADE` مسموح فقط لأجزاء مملوكة كليًا داخل aggregate وغير مطلوبة للتدقيق، مثل upload parts المؤقتة.
- `RESTRICT` هو الافتراضي بين aggregate roots وللمحتوى المنشور والمال.
- `SET NULL` يستخدم فقط عندما يسمح anonymization مع بقاء attribution بديل في audit.
- query filters للsoft delete أداة راحة وليست حاجز أمان، وتختبر مسارات الاستعادة والإدارة صراحة.

### 12.8 Index Strategy

| Query pattern | Index baseline |
|---|---|
| Teacher courses | `(owner_user_id, updated_at DESC, id)` partial where `deleted_at IS NULL` |
| Published catalog | `(status, published_at DESC, id)` partial where status is Published |
| Category listing | join index `(category_id, course_id)` وcatalog projection sort indexes |
| Student enrollments | `(learner_id, status, updated_at DESC, id)` |
| Progress | unique `(enrollment_id, lesson_id)` و`(enrollment_id, completed_at)` |
| Quiz attempts | `(enrollment_id, quiz_id, started_at DESC, id)` |
| Messages | `(conversation_id, created_at DESC, id DESC)` |
| Notifications | `(user_id, is_read, created_at DESC, id DESC)` |
| Orders | `(buyer_id, created_at DESC, id DESC)` |
| Payment/webhook dedupe | unique provider identifiers |
| Outbox polling | partial `(occurred_at, id)` where `processed_at IS NULL` |
| Failed jobs/operations | partial `(next_attempt_at, id)` by pending status |
| Audit/analytics time scans | BRIN on `occurred_at` بعد تحقق الحجم، مع B-tree للاستعلامات الدقيقة |
| Search | GIN on weighted `tsvector`, trigram indexes on normalized title/instructor |

- لا يضاف `INCLUDE`, GIN شامل على JSONB، أو index مكرر بلا query plan يبرره.
- keyset pagination indexes تتضمن sort keys وID كـtie-breaker.
- partitioning لا يستخدم للجداول العادية مبكرًا. يعاد تقييم monthly range partitioning لـanalytics/audit عند عشرات الملايين من الصفوف أو عندما تعجز maintenance/retention عن SLO.

### 12.9 Search Data Model

- `catalog.catalog_documents` projection للمقررات المنشورة والقابلة للاكتشاف فقط، بصف واحد لكل `(course_release_id, locale)` حتى لا تختلط العربية والإنجليزية في ranking أو cache.
- weighted text: title والمدرس وزن A، categories/tags وزن B، subtitle/outcomes وزن C، description وزن D.
- الإنجليزية تستخدم PostgreSQL English text search configuration.
- العربية تستخدم Unicode normalization, removal of diacritics/tatweel، ومعالجة متحفظة للألف والياء، مع الاحتفاظ بالنص الأصلي.
- filters تخزن في typed columns: category, language, level, price, rating, duration, captions.
- highlights يعيدها API كـtext segments آمنة، وليس raw HTML.
- `pg_trgm` للاقتراحات والأخطاء البسيطة، مع minimum query length حرفين وlimit ثمانية اقتراحات.
- ينتقل البحث إلى OpenSearch فقط إذا أخفق PostgreSQL في p95 بعد tuning، أو ظهرت حاجة مثبتة لـfaceting/ranking/synonyms لا يمكن تحقيقها، أو أثر حمل البحث على OLTP.
- محرك البحث الخارجي، إن أضيف، يبقى projection قابلة لإعادة البناء ولا يصبح مصدر الحقيقة.

### 12.10 Migrations and Seed Data

- migrations تنشأ عبر EF Core، وتختبر على قاعدة فارغة وعلى schema الإصدار السابق وعلى Neon branch بحجم قريب من Production.
- migration أصبحت immutable بعد وصولها لأي بيئة مشتركة؛ التصحيح يكون migration جديدة.
- لا ينفذ `Database.Migrate()` عند startup في Production.
- migrator job واحد يتصل بـNeon direct endpoint ويستخدم lock صريحًا.
- الأسلوب `Expand -> Backfill -> Contract` إلزامي لدعم rolling deployments.
- backfills الكبيرة jobs قابلة للاستئناف وبدفعات، وليست transaction نشر طويلة.
- drop, rename, type rewrite، أو blocking constraint تغيير High Risk يحتاج snapshot وموافقتين وmaintenance plan.
- Production seed idempotent ويشمل roles, permissions, role-permission mappings, category taxonomy, notification types، والإعدادات الآمنة.
- لا توجد بيانات مستخدمين تجريبية أو كلمات مرور أو بطاقات أو طلبات وهمية في Production.
- صفحات Privacy وTerms لا تنشر بنص قانوني مصطنع؛ تبقى capability غير منشورة حتى إدخال نص معتمد قبل public launch.

## 13. API Design

### 13.1 Conventions

- Base path: `/api/v1` باستخدام URL segment versioning.
- OpenAPI 3.1 ينتج وثائق منفصلة أو tags واضحة لـPublic, Authenticated, Instructor, Admin, Integrations.
- business REST endpoints تستخدم Controllers رفيعة ومنظمة حسب feature؛ Minimal APIs تقتصر على health/framework endpoints أو hot path يثبت القياس حاجته، ولا يخلط النمطان لنفس المورد.
- Swagger UI مفعلة في Local وDev، ومحمية في Staging، وغير public في Production؛ تبقى OpenAPI JSON الداخلية متاحة لـCI/client generation وفق authorization التشغيلية.
- JSON يستخدم `camelCase`; database يستخدم `snake_case`; C# يستخدم `PascalCase`.
- IDs opaque strings في العميل، حتى لو كانت UUIDv7.
- timestamps بصيغة RFC 3339 UTC.
- money يعاد كـdecimal string مع `currency`, ولا يعتمد JavaScript floating point.
- success المعتاد يستخدم `data` و`meta` عند وجود metadata؛ downloads, streams، و`204` لا تغلف.
- الأخطاء RFC 9457 ProblemDetails فقط.
- لا يزيد nesting غالبًا عن مستويين؛ الموارد الكبيرة تحصل على endpoint مستقل.
- أي filter أو sort field allow-listed؛ لا تمرر أسماء أعمدة أو expressions من العميل إلى EF.
- API لا يعيد domain entities ولا navigation graphs.
- auth responses تحمل `Cache-Control: no-store`.

### 13.2 Pagination, Filtering, Sorting, and Search

- default page size `20`; catalog default `24`; maximum `100`.
- cursor/keyset pagination للfeeds وcatalog/messages/notifications.
- offset pagination مسموحة لجداول Admin التي تحتاج page number وtotal count، بحد أقصى عملي.
- cursor base64url opaque وموقّع أو متحقق منه، ويشمل sort keys وquery hash ولا يقبل التعديل.
- sort syntax محدود مثل `sort=-publishedAt,title`.
- filters صريحة مثل `categoryId`, `language`, `level`, `minRating`, `priceType`; لا يوجد generic query language.
- البحث يستخدم `q`, `cursor`, `limit`, filters، وsort mode من allow-list: relevance, newest, rating, popular, price.
- total counts الدقيقة لا تحسب للfeeds الكبيرة إن كانت مكلفة؛ يعاد `hasMore` و`nextCursor`.

### 13.3 Idempotency and Concurrency

- header `Idempotency-Key` إلزامي للcheckout, payment session, free enrollment, message creation, certificate reissue، وبعض admin bulk commands.
- scope هو `(principal, operation, key)` مع request fingerprint؛ إعادة key نفسها لـpayload مختلفة تعيد `409`, والطلب المتزامن ينتظر النتيجة أو يعيد حالة Processing محدودة بدل تنفيذ ثان.
- idempotency record والbusiness mutation والoutbox تحفظ في transaction نفسها، وتخزن status code والresponse الآمنة وفق TTL العملية.
- progress يستخدم `clientCommandId` وsequence/time window لدمج retries.
- idempotency response تحفظ مدة مناسبة للعملية ولا تتضمن أسرارًا.
- updates الحساسة تستخدم `ETag` و`If-Match`; التعارض يعيد `412` مع معلومات آمنة لإعادة التحميل.
- POST لا يعاد تلقائيًا في frontend لمجرد timeout.

### 13.4 Endpoint Catalog

| Area | Methods and routes | Access |
|---|---|---|
| Auth | `GET /auth/csrf`, `POST /auth/register`, `POST /auth/sign-in`, `POST /auth/refresh`, `POST /auth/sign-out` | Guest/session |
| Email | `POST /auth/email-verification/send`, `POST /auth/email-verification/confirm` | Limited |
| Password | `POST /auth/password/forgot`, `POST /auth/password/reset`, `POST /auth/password/change` | Guest/session |
| MFA | `POST /auth/mfa/setup`, `POST /auth/mfa/confirm`, `POST /auth/mfa/challenge`, `POST /auth/mfa/recovery`, `DELETE /auth/mfa` | Authenticated + recent auth |
| Sessions | `GET /me/sessions`, `DELETE /me/sessions/{sessionId}`, `DELETE /me/sessions` | Own account |
| Profile | `GET /me/profile`, `PUT /me/profile`, `GET/PUT /me/preferences` | Own account |
| Teacher application | `POST /me/teacher-application`, `GET /me/teacher-application` | Verified Student |
| Catalog | `GET /catalog/courses`, `GET /catalog/courses/{slug}`, `GET /catalog/categories`, `GET /catalog/tags` | Public |
| Discovery | `GET /catalog/featured`, `GET /catalog/popular`, `GET /catalog/recommendations` | Public/personalized |
| Search | `GET /search`, `GET /search/suggestions` | Public with rate limits |
| Instructor courses | `GET/POST /instructor/courses`, `GET/PATCH/DELETE /instructor/courses/{courseId}` | Teacher + ownership |
| Curriculum | `GET/PUT /instructor/courses/{courseId}/curriculum`, section/lesson resource endpoints | Teacher collaborator |
| Publishing | `POST /instructor/courses/{courseId}/publication-requests`, `GET /.../publication-status` | Teacher owner |
| Uploads | `POST /uploads`, `POST /uploads/{id}/parts`, `POST /uploads/{id}/complete`, `DELETE /uploads/{id}` | Authorized owner |
| Media | `GET /media/{assetId}/status`, `POST /media/{assetId}/download-grant` | Resource policy |
| Enrollment | `POST /courses/{courseId}/enrollments`, `GET /me/enrollments`, `GET /enrollments/{id}` | Student/own |
| Learning | `GET /enrollments/{id}/manifest`, `GET /enrollments/{id}/lessons/{lessonId}`, `PUT /.../progress` | Enrolled learner |
| Personal learning | `GET /me/continue-learning`, `GET/PUT/DELETE /me/bookmarks`, `GET/POST/PUT/DELETE /me/notes`, `GET /me/recently-viewed` | Own |
| Wishlist | `GET /me/wishlist`, `PUT/DELETE /me/wishlist/{courseId}` | Own |
| Quizzes | `POST /quiz-attempts`, `GET /quiz-attempts/{id}`, `PUT /quiz-attempts/{id}/answers`, `POST /quiz-attempts/{id}/submit` | Enrolled learner |
| Assignments | `GET /assignments/{id}`, `POST /assignments/{id}/submissions`, `GET /submissions/{id}` | Enrolled learner/own |
| Grading | `GET /instructor/courses/{id}/submissions`, `PUT /submissions/{id}/grade` | Course teacher |
| Reviews | `GET /courses/{id}/reviews`, `POST /courses/{id}/reviews`, `PUT/DELETE /reviews/{id}` | Public read; eligible own write |
| Discussions | thread and comment CRUD under course/lesson discussion resources | Enrolled/teacher + ownership |
| Likes | `PUT/DELETE /comments/{id}/like` | Authenticated |
| Reports | `POST /reports`, `GET /me/reports/{id}` | Authenticated |
| Conversations | `GET/POST /conversations`, `GET /conversations/{id}/messages`, `POST /conversations/{id}/messages` | Participant |
| Notifications | `GET /me/notifications`, `PUT /me/notifications/{id}/read`, `POST /me/notifications/read-all`, `GET/PUT /me/notification-preferences` | Own |
| Announcements | CRUD under `/instructor/courses/{id}/announcements` | Course teacher; learners read |
| Certificates | `GET /me/certificates`, `GET /certificates/{id}/download-grant`, `GET /certificates/verify/{code}` | Own/public limited |
| Commerce | `POST /checkouts`, `GET /checkouts/{id}`, `POST /checkouts/{id}/confirm`, `GET /orders/{id}`, `POST /orders/{id}/payment-session` | Buyer/own |
| Refunds/disputes | `POST /refund-requests`, `GET /refund-requests/{id}`, `GET /orders/{id}/refunds`, `GET /orders/{id}/disputes` | Buyer/own limited |
| Invoices | `GET /orders/{id}/invoice`, `POST /orders/{id}/invoice/download-grant` | Buyer/own |
| Coupons | `POST /checkouts/{id}/coupon` | Buyer |
| Subscriptions | `GET /me/subscription`, `POST /subscriptions`, `POST /subscriptions/{id}/cancel` | Own |
| Teacher earnings | `GET /instructor/earnings`, `GET /instructor/payouts`, `GET/PUT /instructor/payout-account` | Approved Teacher + recent auth |
| CMS | `GET /pages/{slug}`, `GET /faqs`, `POST /contact` | Public |
| Admin users | `GET/PATCH /admin/users`, role/session/security actions | Admin permissions + 2FA |
| Admin teachers | review endpoints under `/admin/teacher-applications` | Admin permission |
| Admin publishing | review endpoints under `/admin/publication-reviews` | Reviewer permission |
| Admin moderation | `/admin/reports`, `/admin/moderation-cases`, action resources | Moderator permission |
| Admin catalog | category, tag, featured, course lifecycle endpoints | Catalog permission |
| Admin commerce | offers, coupons, orders, refund requests, disputes, subscriptions, reconciliation, ledger، وpayout endpoints | Commerce permissions + 2FA |
| Admin CMS/settings | page revisions, FAQs, typed settings | CMS/settings permissions |
| Admin analytics | dashboard summaries and async export operations | Analytics permission |
| Admin audit | `GET /admin/audit-logs` | Audit permission + 2FA |
| Operations | `GET /operations/{id}` | Operation owner/admin |
| Webhooks | `/integrations/payments/{provider}/webhooks`, `/integrations/email/{provider}/webhooks` | Provider signature, IP/rate controls |
| Health | `/health/live`, `/health/ready`, `/health/startup` | Public minimal status; details internal only |

### 13.5 API Versioning Policy

- الإضافات المتوافقة تدخل v1.
- حذف property، تغيير معناها، تغيير status semantics، أو تغيير required fields يحتاج v2.
- event contracts لها `schemaVersion` مستقل عن API version.
- يدعم server إصدار API السابق فترة معلنة عند إطلاق major جديد.
- OpenAPI breaking-change detection بوابة CI.

## 14. Authentication Flow

### 14.1 Browser Token Model

- access token هو JWT قصيرة العمر، baseline `10 minutes`، تحفظ في memory فقط.
- refresh token opaque عشوائية عالية entropy، baseline idle `14 days` وabsolute `30 days`.
- refresh token raw لا تخزن في database؛ يخزن hash مع session/family metadata.
- refresh token تنقل في host-only cookie باسم `__Secure-dorosak-refresh` مع `Secure`, `HttpOnly`, `Path=/api/v1/auth`, بلا `Domain`, و`SameSite=Lax`.
- XSRF token تستخدم host-only cookie مقروءة عمدًا باسم `XSRF-TOKEN` مع `Secure`, `SameSite=Lax`, `Path=/` وheader `X-XSRF-TOKEN` وفق ASP.NET Core Antiforgery؛ لا تحمل cookie أي session secret.
- لا يستخدم `localStorage` أو `sessionStorage` لأي access/refresh token.
- Production يقدم Web وAPI تحت origin واحد عبر Front Door، ما يقلل CORS وSameSite complexity. CORS يبقى مضبوطًا لبيئات التطوير فقط.
- تضييق cookie path يعني أن Angular SSR والصفحات العامة والassets لا تستقبل refresh cookie أصلًا. عند API major جديدة توجد migration صريحة لمسار cookie، ولا توسع إلى `/` لمجرد التوافق.
- صفحات SSR العامة غير مخصصة للحساب؛ personalization يجلب بعد hydration. Front Door وOutput Cache يخزنان allow-list من GET العامة فقط، ولا يدخل أي Cookie أو Authorization في cache key أو origin request لتلك الصفحات.

### 14.2 Registration and Email Verification

1. Angular يطلب CSRF token.
2. المستخدم يرسل الاسم والبريد وكلمة المرور عبر HTTPS.
3. الخادم يطبع البريد ويطبق rate limit وFluentValidation ثم ينشئ Identity account وStudent role في transaction.
4. يكتب outbox event لإرسال verification email ولا ينتظر provider داخل request.
5. response محايدة لا تكشف إن كان البريد مستخدمًا في المسارات الحساسة.
6. verification link يبنى من PublicUrl موثوق، وليس Host header القادم من الطلب.
7. بعد التأكيد يصبح الحساب مؤهلًا لجلسة كاملة والعمليات المحمية.

### 14.3 Sign-In

1. يحمي endpoint بـCSRF, Origin validation، rate limits حسب IP وnormalized identifier.
2. ASP.NET Core Identity يتحقق من password hash, email confirmation, lockout، وsecurity stamp.
3. إذا كانت 2FA مطلوبة، يعاد challenge مؤقت ولا يصدر access أو refresh token.
4. بعد اكتمال المصادقة ينشأ server-side RefreshSession مستقل للجهاز.
5. يصدر JWT موقّع asymmetric وتوضع refresh token في HttpOnly cookie.
6. response `no-store`; Angular يحتفظ access token في memory ويجلب profile/capabilities.

### 14.4 Refresh Rotation

1. يرسل Angular POST `/auth/refresh` مع cookie وXSRF header.
2. الخادم يحسب hash ويقفل session/family row داخل transaction قصيرة.
3. إذا token نشطة، تعلم مستهلكة ويصدر token بديلة وJWT جديدة ذريًا.
4. إعادة استخدام token مستهلكة خارج نافذة race قصيرة تلغي family كاملة وتسجل security event.
5. طلب متزامن داخل نافذة race لا يصدر token أخرى ولا يلغي family فورًا؛ يعيد client المحاولة بالcookie الأحدث لمنع false compromise بين tabs.
6. Angular يستخدم single-flight داخل tab وWeb Locks/BroadcastChannel عند توافرهما لتنسيق tabs.

### 14.5 Logout and Revocation

- logout يلغي session server-side ويحذف cookie بنفس خصائص الإنشاء.
- logout all يلغي جميع refresh sessions ويزيد security/authorization version.
- password reset, credential compromise, email ownership change، وتعطيل account تلغي الجلسات المناسبة.
- JWT القائمة قد تبقى حتى عشر دقائق؛ العمليات عالية الخطورة تفحص session state الحديثة وrecent authentication.

### 14.6 JWT Validation

- توقيع asymmetric مع `kid` ودوران مفاتيح بفترة overlap.
- فحص issuer, audience, signature, algorithm allow-list, expiration, not-before, token type، وclock skew لا يتجاوز 60 ثانية.
- claims الدنيا: `sub`, `sid`, `jti`, `amr`, `auth_time`, `authz_ver`.
- لا تتضمن PII أو secrets أو permission list ضخمة.
- signing keys منفصلة عن ASP.NET Data Protection keys.

### 14.7 Password and 2FA Policy

- passphrases طويلة، حد أدنى `12` حرفًا للمستخدم و`14` للإدارة، ودعم 64 حرفًا على الأقل.
- لا يفرض تغيير دوري بلا سبب.
- Identity password hashing iterations تضبط بقياس يستهدف زمنًا آمنًا مقبولًا على Production hardware.
- breached password check يحافظ على الخصوصية.
- lockout تدريجي مع rate limiting؛ لا يستخدم lockout وحده حتى لا يتحول إلى DoS.
- TOTP secrets مشفرة، recovery codes hashed وأحادية الاستخدام، ولا تعرض ثانية.
- 2FA إلزامية لـAdmin وموصى بها لـTeacher.
- تغيير email/password/2FA/permissions وعمليات refund الكبيرة تتطلب recent authentication.

### 14.8 Phase 5 Contract Decisions

لتثبيت العقود غير المحددة في الكتالوج قبل تنفيذ Phase 5:

- تستخدم استجابات النجاح غلافًا بصيغة `{ "data": ... }`، بينما تستخدم عمليات `204` جسمًا فارغًا.
- مسارات التحقق وإعادة تعيين كلمة المرور تستخدم `userId` opaque و`token` في query مؤقتة؛ يستبدل العميل عنوان المتصفح بعد الاستهلاك.
- صلاحية verification token هي `24 hours`، وصلاحية password-reset token هي `1 hour`.
- recent authentication صالحة لمدة `15 minutes`، ونافذة refresh المتزامن هي `10 seconds`.
- نتيجة تسجيل الدخول إما `authenticated` وتحتوي access token فقط مع ضبط refresh cookie، أو `mfaRequired` وتحتوي challenge opaque قصير العمر دون أي access/refresh token.
- يملك MFA challenge صلاحية `5 minutes` وحدًا أقصى `5` محاولات، وتستخدم recovery codes مرة واحدة فقط.
- تستخدم الواجهة `GET /api/v1/me/profile` لاستعادة snapshot الحساب والصلاحيات بعد refresh؛ الصلاحيات في الواجهة إرشاد مرئي فقط، والتفويض النهائي في الخادم.
- يفحص breached-password adapter خدمة HIBP بأسلوب k-anonymity عند تفعيله في Production، ويكون معطلًا افتراضيًا في Development والاختبارات المحلية.
- تخزن permission definitions كثوابت كود، وتخزن علاقة role-to-permission في `identity.role_claims` باستخدام claim type ثابت.
- تطبق حدود التسجيل على IP، وحدود sign-in/password-reset على IP وnormalized identifier، وحد refresh على session token hash؛ يستخدم Redis atomic counters، وتفشل عمليات الأمان الحساسة مغلقًا عند تعذر Redis.

## 15. Authorization Flow

### 15.1 Model

- `RBAC` يجمع permissions في الأدوار.
- `Policy-Based Authorization` يفحص permission keys.
- `Claims-Based Authorization` تستخدم فقط لحقائق مصادقة مستقرة مثل `sub`, `sid`, `amr`, و`auth_time`; لا توضع ملكية الموارد أو permissions المتغيرة كاملة داخل JWT.
- `Resource-Based Authorization` يفحص ownership, enrollment, participation, course collaboration، وحالة المورد.
- claims لا تعد المصدر النهائي للصلاحيات المتغيرة؛ PostgreSQL authority مع Redis cache قصيرة و`authz_ver`.
- query تقيد بالموارد المسموحة قبل pagination وcounting لمنع IDOR وتسريب الأعداد.
- يعاد 404 بدل 403 عندما يجب إخفاء وجود المورد.
- Redis failure لا يمنح السماح؛ يعود handler إلى PostgreSQL أو يفشل مغلقًا للعمليات الحساسة.

### 15.2 Permission Naming

الصيغة: `Resource.Action.Scope`، وكل permission constant إنجليزي ثابت ومخزّن seed.

| Group | Permissions |
|---|---|
| Account | `Profile.ReadOwn`, `Profile.UpdateOwn`, `Security.ManageOwn`, `Sessions.ManageOwn` |
| Teacher onboarding | `TeacherApplication.CreateOwn`, `TeacherApplication.ReviewAny` |
| Courses | `Course.Create`, `Course.ReadOwn`, `Course.UpdateOwn`, `Course.DeleteOwn`, `Course.SubmitOwn`, `Course.ReviewAny`, `Course.PublishAny`, `Course.ManageAny` |
| Media | `Media.UploadOwn`, `Media.ReadOwn`, `Media.ManageAny` |
| Learning | `Enrollment.CreateOwn`, `Enrollment.ReadOwn`, `Learning.AccessOwn`, `Progress.UpdateOwn`, `Learning.ViewCourseLearners` |
| Assessments | `Quiz.AttemptOwn`, `Assignment.SubmitOwn`, `Submission.GradeCourse`, `Assessment.ManageCourse` |
| Engagement | `Review.ManageOwn`, `Discussion.Participate`, `Comment.ManageOwn`, `Moderation.ReviewAny` |
| Communications | `Message.SendAsSelf`, `Conversation.ReadOwn`, `Notification.ReadOwn`, `Announcement.ManageCourse` |
| Certificates | `Certificate.ReadOwn`, `Certificate.VerifyPublic`, `Certificate.RevokeAny` |
| Commerce | `Order.ReadOwn`, `Checkout.CreateOwn`, `Subscription.ManageOwn`, `Commerce.ManageOffers`, `Commerce.ManageOrders`, `Commerce.ManageRefunds`, `Commerce.ReadEarningsOwn`, `Commerce.ManagePayoutAccountOwn` |
| Administration | `User.ReadAny`, `User.ManageAny`, `Role.ManageAny`, `Catalog.ManageTaxonomy`, `Cms.Manage`, `Settings.Manage`, `FeatureFlag.Manage`, `Analytics.Read`, `Audit.Read` |

### 15.3 Role Assignment Matrix

| Capability | Guest | Student | Teacher | Admin |
|---|---:|---:|---:|---:|
| Public catalog/search/CMS | Yes | Yes | Yes | Yes |
| Own profile/security/sessions | No | Yes | Yes | Yes |
| Enrollment and own learning | No | Yes | Yes | By explicit own account use |
| Create/review own content | No | No | Yes | Only with explicit Course permission |
| View learners/grade own course | No | No | Yes | Explicit permission |
| Messaging/discussions/reviews | Read public only | Own/eligible | Own/eligible/course scope | Moderation permission |
| Publishing/moderation/users | No | No | No | Explicit permissions |
| Commerce administration/audit/settings | No | No | No | Explicit permissions + 2FA |

### 15.4 Resource Policies

- `CourseOwnerOrCollaborator` يفحص `courses.owner_user_id` أولًا، ثم role المسموحة داخل `course_instructors`، وحالة course؛ لا يوجد مصدر ثالث للملكية.
- `EnrolledLearner` يفحص enrollment active وentitlement غير منتهية.
- `CourseTeacher` يفحص course assignment قبل عرض learners أو submissions.
- `ConversationParticipant` يفحص participant active قبل قراءة الرسائل أو استقبال group events.
- `SubmissionOwner` يفحص learner/enrollment ويمنع الوصول بتخمين ID.
- `ReviewOwner` يسمح بالتعديل ضمن policy ويحفظ moderation constraints.
- `AdminHighRisk` يتطلب permission + Admin role + 2FA + recent auth + active session + audit reason.

## 16. Caching Strategy

| Layer | Use | Rules |
|---|---|---|
| Browser/CDN | hashed static assets, public images, public course pages where safe | لا بيانات شخصية، immutable للassets فقط |
| ASP.NET Output Cache | opt-in public GET catalog/category/CMS responses | لا Authorization، لا Set-Cookie، vary by locale/query allow-list |
| `IMemoryCache` | small reference/config snapshots داخل replica | TTL قصير، لا state حرجة، يقبل اختلاف replicas |
| Redis distributed cache | popular catalog read models, permissions، recommendation snapshots | cache-aside، versioned keys، PostgreSQL fallback، Redis Cache instance منفصلة في Production |
| Redis Security | distributed rate-limit counters فقط | HA، TLS، noeviction، fail-closed للعمليات الحساسة |
| Redis Realtime | SignalR backplane فقط | HA، TLS، noeviction، ولا تخزن business state أو cache entries |
| Angular in-memory state | route/session-lifetime UI data | تمسح عند logout، ليست authority |
| IndexedDB | approved offline learning data and mutation outbox | user-scoped، expiry، no secrets/admin/payment data |

قواعد cache:

- key namespace: `dorosak:{environment}:{schemaVersion}:{scope}:{key}`.
- تشمل key كل dimensions المؤثرة: user/tenant إن وجد مستقبلًا، locale, filters, sort, permission scope.
- TTL مع jitter لمنع stampede المتزامن.
- stampede protection للـhot keys، وnegative caching قصير فقط للنتائج العامة الآمنة.
- invalidation بعد database commit، ويمر عبر outbox عندما يجب ألا يضيع.
- Redis ليس distributed lock وحيدًا لثابت أعمال؛ database constraints تبقى الحارس.
- Output Cache يبدأ allow-list لا global default.
- auth, profile, orders, messages, notifications، وadmin responses تستخدم `no-store`.
- public catalog baseline TTL `60 s` مع tags؛ categories/CMS `5 min`; permission cache `<= 60 s`; القيم النهائية تضبط بقياس.
- `IMemoryCache` لها `SizeLimit=128 MB` لكل API replica، وكل entry يحدد size وTTL؛ لا تسمح feature بإدخال unbounded collections.
- Output Cache لها حد `256 MB` لكل replica وresponse أقصى `1 MB` وTTL أقصى `5 min`، وتراقب key cardinality وevictions.
- Redis Cache يطبق maxmemory policy `allkeys-lfu`، وTTL إلزامي لكل key؛ Redis Security وRedis Realtime يستخدمان `noeviction` ولا يشتركان مع cache حتى لا تضيع counters أو يسقط backplane بسبب ضغط read cache.

### 16.1 Server-Authoritative Feature Flags

- `operations.feature_flags` تحتوي key, environment, enabled, rollout percentage/segment, starts_at, expires_at, owner, reason, version، وupdated_by.
- التقييم النهائي يحدث في API/Worker من PostgreSQL مع Redis cache قصيرة؛ Angular يستلم capability snapshot عامة للعرض فقط ولا يستطيع تفعيل capability.
- flags الخاصة بالدفع، admin، security، أو data access تفشل مغلقًا عند غياب flag أو تعطل flag store. flags البصرية غير الحساسة يمكن أن تستخدم default آمنًا.
- كل تغيير يحتاج `FeatureFlag.Manage` + 2FA + recent auth + audit reason، وله owner وexpiry؛ لا تحفظ secrets أو authorization decisions الدائمة داخل flag.
- outbox يبطل caches بعد التغيير، وflag مؤقتة تزال خلال 30 يومًا من اكتمال rollout.

## 17. Logging and Audit Strategy

### 17.1 Diagnostic Logging

- Serilog ينتج structured JSON إلى stdout/stderr في containers.
- الحقول الأساسية: timestamp, level, message template, service, environment, release, traceId, spanId, correlationId, request method, route template, status, duration.
- يستخدم route template لا raw URL لتجنب PII وmetric cardinality.
- لا تسجل passwords, tokens, cookies, authorization headers, reset codes, recovery codes, payment secrets, signed URLs، أو message bodies.
- لا تسجل request/response bodies افتراضيًا.
- exceptions تسجل مرة واحدة عند boundary المناسبة، لا في كل طبقة ثم يعاد رميها.
- query parameters الحساسة وSignalR access token تحجب قبل edge/app logs.
- Development logs readable؛ Production logs structured مع minimum levels وضبط sampling للضوضاء.

### 17.2 Audit Logging

- `operations.audit_logs` منفصل منطقيًا عن diagnostic logs وappend-only.
- يسجل actor, action, target type/id, result, occurredAt, correlationId, source, reason، وsafe changed-field list.
- لا يسجل before/after JSON كاملًا افتراضيًا، ولا PII غير لازمة.
- العمليات الملزمة: role/permission changes, admin login, user disable, course publish/unpublish, moderation, grade override, refund, settings/CMS publish, certificate revoke, data export/delete.
- retention baseline `365 days` للأحداث الأمنية والإدارية، مع تصدير دوري إلى immutable storage.
- وصول audit نفسه permission منفصلة ويسجل في audit.

### 17.3 Correlation IDs

- OpenTelemetry W3C trace هو معرف الربط التقني الأساسي.
- يقبل `X-Correlation-ID` من clients موثوقين فقط إذا اجتاز format/length؛ وإلا يولد server قيمة جديدة.
- يعاد correlation ID في response وProblemDetails.
- ينتقل trace/correlation عبر outbox, Hangfire jobs، وHTTP integrations بلا PII في baggage.

## 18. Monitoring and Observability Strategy

### 18.1 Signals

- Traces: ASP.NET Core, HttpClient, Npgsql, Redis, Hangfire, storage, email, payment، وcustom spans للoutbox/media.
- Metrics: request rate/errors/latency, DB latency/pool saturation, Redis hit/latency, outbox lag, queue depth/age, SignalR connections/reconnects, upload scan/transcode duration, email delivery, frontend Web Vitals.
- Logs: structured diagnostics وsecurity events بعد redaction.
- Frontend RUM يرسل route template, release, Web Vitals، وplayer startup/rebuffer metrics دون PII أو search/message content.
- OTLP يحافظ على استقلال التطبيق عن backend؛ reference backend هو Azure Monitor/Application Insights.

### 18.2 Health Checks

| Endpoint | Meaning |
|---|---|
| `/health/live` | العملية تعمل؛ لا يفحص dependencies |
| `/health/startup` | اكتمل initialization الضروري |
| `/health/ready` | النسخة تستطيع خدمة traffic الأساسي وschema متوافقة |
| Internal component health | تفاصيل PostgreSQL, Redis, storage, worker heartbeat، وqueues لمشغلي النظام فقط |

- PostgreSQL dependency حرجة للreadiness.
- Redis لا يسقط API readiness إذا كان المسار الأساسي يملك PostgreSQL fallback، لكنه يطلق alert.
- email provider لا يدخل liveness/readiness.
- health العامة لا تعرض hosts أو connection strings أو exception messages.

### 18.3 Alerts and SLO Operations

- alert على fast burn `14.4x` عبر 5 دقائق وساعة، وslow burn `6x` عبر 30 دقيقة و6 ساعات.
- DB pool warning فوق 80% عشر دقائق، وpage فوق 90% مع errors/latency.
- page عند critical queue oldest age فوق دقيقتين، أو توقف worker heartbeat.
- page عند فشل backup خلال 26 ساعة أو restore drill مجدولة.
- page عند external probes تفشل من موقعين.
- warning عند Redis memory فوق 75%، وpage عند backplane disconnect مؤثر.
- alerts يجب أن تكون قابلة لاتخاذ إجراء ومرتبطة بـrunbook.
- إذا استهلكت نصف error budget قبل نصف الشهر توقف التغييرات غير الضرورية؛ عند نفادها يسمح بإصلاحات reliability/security فقط.

### 18.4 Required Runbooks

- Application rollback.
- Migration failure/lock.
- Neon outage, compute restart, connection saturation, slow query.
- PITR and logical backup restore.
- Redis/backplane outage and reconnect storm.
- Hangfire backlog, failed jobs, duplicate delivery.
- Object storage/CDN outage.
- Email delivery incident.
- DNS/TLS incident.
- Credential/JWT/Data Protection key compromise and rotation.
- Malware scanner/media worker outage.
- Security incident and data breach response.
- Regional evacuation.

كل runbook يحتوي symptoms, impact, dashboards, safe diagnostics, mitigation, escalation, verification، وcommunication template.

## 19. Security Strategy

### 19.1 Baseline Controls

- Threat model بأسلوب STRIDE لكل feature كبيرة قبل التنفيذ.
- OWASP ASVS Level 2 بوابة، مع controls مشددة للAdmin, payments, uploads.
- TLS 1.2/1.3 فقط، redirect HTTP إلى HTTPS، وHSTS تدريجيًا.
- `Content-Security-Policy` مع nonce/hash، بلا `unsafe-eval` وبلا `unsafe-inline` غير موثق.
- `frame-ancestors 'none'`, `X-Content-Type-Options: nosniff`, strict Referrer-Policy، وPermissions-Policy قليلة الصلاحيات.
- Trusted Types وAngular sanitization، ومنع bypass APIs إلا بمراجعة أمنية.
- output encoding حسب السياق، وتعطيل raw HTML داخل Markdown افتراضيًا.
- parameterized EF Core queries؛ يمنع string-concatenated SQL من input.
- request limits على edge, Kestrel، والendpoint، مع JSON depth/header/time limits.
- CORS origins كاملة وصريحة؛ لا wildcard مع credentials ولا origin reflection.
- CSRF protection لكل unsafe cookie/session endpoint؛ CORS ليس بديلًا عن CSRF.
- no state change في GET/HEAD/OPTIONS.
- rate limiting عند edge وداخل التطبيق.
- secure configuration validation مع fail-fast عند غياب secret حرج.

### 19.2 Secret and Key Management

- لا secrets في Git, appsettings, Angular bundles, Docker images, build args، أو GitHub logs.
- Azure Key Vault هو المرجع، وتصل GitHub Actions وworkloads عبر OIDC/managed identity.
- credentials مختلفة لكل environment ولكل workload وبأقل صلاحية.
- runtime DB role لا يملك DDL؛ migrator role منفصل.
- JWT signing keys asymmetric وتدور مع overlap.
- ASP.NET Data Protection key ring دائمة ومشتركة بين replicas، ومشفرة بمفتاح Key Vault، وبـApplicationName ثابت.
- Data Protection keys وJWT keys منفصلة، وكلاهما يدخل backup/restore drill.
- credentials طويلة العمر تدور كل 90 يومًا أو فور الاشتباه.

### 19.3 Application Security

- server-side authorization لكل object reference لمنع IDOR.
- mass assignment ممنوع؛ write mapping allow-listed.
- SSR request state معزول ولا توجد singleton تحمل بيانات مستخدم.
- SSR outbound requests تستخدم allowed hosts لمنع SSRF.
- URL import الخارجي معطل افتراضيًا؛ إن أضيف، يعمل fetcher معزول ويمنع private/link-local/metadata addresses والredirect abuse.
- rich text يعقم بسياسة allow-list ويخزن sanitizer version لإعادة التعقيم عند تغير السياسة.
- الملفات تفحص وتقدم من origin منفصل بلا cookies.
- webhook signatures تفحص على raw body مع replay window وevent dedupe.
- hosted payment pages تقلل PCI scope؛ لا تخزن بيانات البطاقة.
- الحسابات الإدارية منفصلة، 2FA إلزامي، وbreak-glass مؤقت ومدقق.
- privacy workflows تدعم data export, deactivation, anonymization، وretention holds.
- إذا كان المستخدمون القاصرون ضمن السوق، يجب اعتماد سياسة موافقة ولي الأمر وتقليل البيانات قبل فتح التسجيل لهم؛ لا يفعل هذا الجمهور بصمت.

### 19.4 Supply Chain Security

- GitHub secret scanning وpush protection.
- CodeQL/SAST لكل PR.
- NuGet/npm dependency review and vulnerability audit.
- container and IaC scanning.
- license scan وSBOM لكل image.
- Cosign keyless signing وprovenance، وتثبيت GitHub Actions على full commit SHA.
- لا يسمح release بثغرة Critical أو High قابلة للاستغلال. الاستثناء المكتوب له owner, mitigation، وexpiry أقصاه 14 يومًا.
- SLA المعالجة: Critical خلال 24 ساعة، High خلال 7 أيام، Medium خلال 30 يومًا.

## 20. Performance Strategy

### 20.1 Backend and Database

- asynchronous I/O وCancellationToken end-to-end؛ لا sync-over-async.
- no tracking projections، keyset pagination، bounded results، وno N+1.
- batching للprogress, notifications، وanalytics.
- connection pool budget محسوب عبر replicas؛ لا تعالج latency بزيادة pool عشوائيًا.
- command timeout قصير للمسارات التفاعلية، baseline 15 ثانية كحد أعلى عام وأقصر للـhot paths.
- reports الثقيلة jobs/read models.
- Redis للقراءات الساخنة فقط، مع stampede protection.
- response compression للنصوص، streaming للملفات، وCDN للوسائط.
- compiled queries/compiled model لا تضاف إلا بعد profile يثبت فائدتها.
- indexes تراجع دوريًا من pg_stat_statements وquery plans، بما في ذلك unused/duplicate indexes.

### 20.2 Frontend

- SSR/hydration للصفحات العامة، CSR للخاص، وTransfer Cache للبيانات العامة فقط.
- lazy routes, adaptive preloading, defer, tree shaking، وstrict bundle budgets.
- no unnecessary change detection؛ zoneless + OnPush + immutable signal updates.
- RxJS cancellation للبحث والتنقل، وعدم إبقاء subscriptions أو timers بعد destroy.
- virtual scroll للقوائم المناسبة وcursor infinite loading.
- skeletons ثابتة الأبعاد، responsive images، self-hosted subset fonts، وcritical CSS مضبوط.
- RUM هو معيار الأداء الحقيقي؛ Lighthouse أداة تشخيص وليس الدليل الوحيد.

### 20.3 Load Testing

- k6 لاختبارات API وSignalR scenarios، وbrowser tests للرحلات الحرجة.
- load, stress, spike, soak، واختبار media throughput.
- بوابة Production: ضعف peak المتوقع مع 30% headroom ومن دون كسر SLO.
- اختبارات خاصة لسباق refresh tokens, enrollment, coupon redemption, progress heartbeats, message dedupe، وoutbox duplicates.
- يعاد الاختبار قبل المواسم والاختبارات التعليمية والإصدارات المعمارية الكبيرة.

## 21. Scalability Strategy

- Web/API stateless وتبدأ بنسختين Production على الأقل وموزعة على failure zones.
- session source في PostgreSQL، وData Protection keys مشتركة؛ لا sticky in-memory state.
- API scaling حسب CPU, memory, request latency, active SignalR connections، وDB connection budget.
- workers تتوسع حسب oldest job age وqueue depth، لا CPU فقط.
- MediaWorker يتوسع مستقلًا وله concurrency محددة لحماية التكلفة.
- Production تستخدم Redis Cache وRedis Security/RateLimit وRedis Realtime/backplane كثلاث خدمات HA منفصلة في المنطقة نفسها؛ Dev يمكن أن يستخدم instance واحدة مع prefixes واضحة.
- Neon autoscaling بحد أدنى يمنع cold start في Production وحد أعلى اجتاز load test.
- Read Replica لا تضاف إلا بعد إصلاح N+1/index/query issues وثبوت أن القراءة تستهلك غالبية القدرة.
- object storage وCDN يتحملان الملفات والبث بدل API/database.
- analytics daily projections تقلل الضغط على OLTP؛ warehouse مستقل يضاف عند حاجة BI مثبتة.
- multi-region active-active مؤجل لأن اتساق identity, commerce, progress، وrealtime يحتاج تكلفة وتعقيدًا لا يبرره الإطلاق الأول.

## 22. Neon PostgreSQL Strategy

### 22.1 Projects and Branches

- Production وStaging وDev تستخدم Neon projects مستقلة؛ لا تشارك credentials أو compute.
- Production branch محمية، ولا scale-to-zero.
- Preview branches تنشأ من Dev sanitized template فقط، وليست من Production، وTTL أقصاه 48 ساعة.
- migration عالية الخطورة تختبر على branch مؤقتة مغلقة ومحدودة العمر قبل التنفيذ.
- region تختار لتكون الأقرب إلى Azure compute وغالبية المستخدمين. قاعدة القرار: أقل measured p95 latency، ويفضل أقل من 10-15ms بين التطبيق وNeon. لمستخدمي الشرق الأوسط يكون Europe/Frankfurt baseline إن لم توجد منطقة Neon أقرب.

### 22.2 Connections

- API يستخدم Neon pooled endpoint عبر SSL verify-full.
- migrations, pg_dump، وعمليات تحتاج session ثابتة تستخدم direct endpoint.
- Hangfire يستخدم direct endpoint مبدئيًا بسبب locking/session behavior؛ الانتقال للpooled يتطلب integration test مع provider version.
- لا يعتمد التطبيق على persistent `SET`, temp tables, LISTEN/NOTIFY، أو session advisory locks عبر pooled endpoint.
- baseline pool budget يحسب كل pool: `20` application connections لكل API replica، `5` application + `10` Hangfire لكل Worker replica، و`3` application + `2` Hangfire لكل MediaWorker؛ migrator/backup reserve منفصلة. الأرقام تخفض أو ترفع فقط بالاختبار.
- مجموع pools لا يتجاوز 70% من compute connection capacity؛ 20% للإدارة/migrations و10% للطوارئ.
- runtime role DML فقط، application migrator DDL على schemas المحددة، `hangfire_runtime` DML على schema الخاصة، `hangfire_migrator` DDL عليها، وops read-only مؤقت ومدقق.
- Production access يقيد بـprivate networking أو static egress allow-list؛ لا اتصال دائم من أجهزة المطورين.

#### 22.2.1 Hangfire Schema Lifecycle

- Hangfire automatic schema preparation معطلة في API وWorker startup.
- إصدار `Hangfire.PostgreSql` مثبت، وأي upgrade يختبر schema diff على Neon branch ويصدر artifact مستقلًا ضمن release manifest.
- `hangfire_migrator` job واحدة ترقي provider schema قبل نشر worker التي تحتاجها، مع backward compatibility للنسخة N-1.
- job contracts تحمل primitive IDs و`schemaVersion`; worker تحتفظ handlers لـN وN-1 حتى drain كل queued jobs القديمة وانتهاء retention.
- dashboard تعرض provider schema version وoldest serialized contract version، ويمنع contract removal قبل إثبات queue drain.

### 22.3 Backup and Restore

- Neon PITR history سبعة أيام كحد إطلاق أدنى، وترفع إلى 30 يومًا عند المتطلبات التنظيمية أو التجارية.
- Neon snapshots: يومية 14 يومًا، أسبوعية 12 أسبوعًا، شهرية 12 شهرًا حيث تدعم الخطة ذلك.
- logical `pg_dump` مشفر يوميًا إلى Azure Blob account منفصل أو مزود مستقل عن Neon.
- retention للlogical backups: يومي 35 يومًا، أسبوعي 12 أسبوعًا، شهري 12 شهرًا.
- كل backup يحمل checksum, PostgreSQL version، وmigration ID.
- restore test شهري إلى بيئة معزولة، وDR drill ربع سنوي.
- backup تشمل PostgreSQL, object metadata/content policy, Data Protection keys، وIaC/config؛ Redis يعاد بناؤه ولا ينسخ كمصدر حقيقة.
- rollback العادي للتطبيق لا يستخدم PITR لأن ذلك يفقد معاملات المستخدم؛ database rollback غالبًا roll-forward migration.

### 22.4 Object Storage Recovery

- Production Blob Storage تستخدم `GZRS/RA-GZRS` حيث تتاح، مع blob/container soft delete `30 days`, versioning، وpoint-in-time restore وفق دعم الحساب.
- object replication تنسخ published media وbackups إلى storage account في region/subscription منفصلة وبـcredentials مستقلة؛ quarantine المؤقتة لها retention أقصر ولا تعد نسخة منشورة.
- هدف الوسائط `RPO <= 15 minutes` و`RTO <= 4 hours`; يثبت فقط بعد quarterly restore drill.
- PostgreSQL يخزن object key, immutable version ID, checksum, size، وحالة asset؛ signed URLs ليست بيانات backup.
- لا تفترض transaction موزعة بين Neon وBlob. اكتمال asset يسجل بعد التحقق من object، ويكتب consistency checkpoint دوري يربط database time بآخر replication marker.
- عند disaster restore تستعاد database إلى T، ثم blob versions عند أو قبل T؛ job reconciliation تستعيد missing versions من replica، تعزل orphan objects الأحدث، وتبقي asset غير المتاحة في حالة `RecoveryPending` بدل تقديم رابط مكسور.
- restore drill الشهري يفحص عينة فيديو وصور ووثائق بالchecksum، ويشمل Data Protection key ring وcertificate PDFs، لا metadata وحدها.

## 23. Docker Strategy

### 23.1 Production Images

- multi-stage builds، وruntime image لا تحتوي SDK أو source أو tests.
- base images مثبتة major/patch وdigest؛ لا تستخدم tag `latest`.
- containers تعمل non-root، read-only filesystem قدر الإمكان، وtemporary storage محدود.
- `.NET` API/Worker/MediaWorker وAngular SSR لكل منها entrypoint واضح؛ يمكن مشاركة base layers لا lifecycle.
- OCI labels تشمل Git SHA, semantic version, build date, repository.
- graceful shutdown وSIGTERM، مع drain للSignalR وjobs قبل الإنهاء.
- health endpoints منفصلة، وresource requests/limits لكل workload.
- secrets تدخل runtime فقط ولا تنسخ إلى layers.

#### 23.1.1 Dockerfile Inventory

- `backend/src/Dorosak.Api/Dockerfile`: production multi-stage API image.
- `backend/src/Dorosak.Worker/Dockerfile`: production background worker image.
- `backend/src/Dorosak.MediaWorker/Dockerfile`: production isolated media image with pinned native tools.
- `frontend/Dockerfile`: production Angular SSR Node image.
- `deploy/docker/Dockerfile.backend.dev`: development image مع SDK وhot reload فقط.
- `deploy/docker/Dockerfile.frontend.dev`: development Node image مع Angular dev server فقط.
- development Dockerfiles لا تنشر إلى Production registry، وproduction Dockerfiles لا تحتوي development certificates أو tools.
- API/Worker production وbackend development Dockerfiles تنشأ في phase 3، وFrontend files في phase 4، وMediaWorker file في phase 7؛ phase 12 لا تنشئها لأول مرة بل تشددها وتفحصها وتوقعها وتنشرها.

### 23.2 Docker Compose

- Docker Compose للتطوير وCI فقط، وليس Production deployment.
- default local application يتصل مباشرة بـNeon Dev branch؛ لا يعتمد على SQL Server أو local PostgreSQL.
- خدمات Local: Redis, MinIO-compatible storage, Mailpit, ClamAV، وOpenTelemetry Collector عند الحاجة.
- PostgreSQL container يستخدم فقط في hermetic integration tests/CI وبنفس major المستهدف، ولا يغير design الموجه إلى Neon.
- ports ترتبط بـlocalhost، والخدمات الداخلية على isolated network.
- named volumes وhealth checks وresource limits.
- migrations خدمة one-shot صريحة، وليست جزءًا من startup.
- secrets المحلية في ملف مستبعد من Git، مع `.example` يحتوي أسماء فقط بلا قيم حساسة.

## 24. Deployment Strategy

### 24.1 Reference Production Topology

المسار العام:

`DNS -> Azure Front Door Premium/WAF -> Dorosak.Web or Dorosak.Api -> Neon pooled endpoint`

المسارات المساندة:

- Front Door يوجه `/api/*` و`/hubs/*` إلى API، وبقية المسارات إلى Angular SSR تحت origin عام واحد.
- `/assets/*` وmedia delivery من private Azure Blob عبر CDN/signed access.
- API يكتب jobs/outbox فقط؛ Worker وMediaWorker ينفذان.
- API يتصل بخدمات Azure Managed Redis الثلاث عبر TLS وبـACL منفصلة؛ Workers تستخدم Cache/Realtime فقط عند الحاجة ولا تملك صلاحية rate-limit غير لازمة.
- Migration Container Apps Job يتصل بـNeon direct endpoint.
- telemetry يرسل عبر OTLP إلى Azure Monitor/Application Insights.
- Postmark يرسل البريد وتتحقق API من signed webhooks.
- Container Apps ingress لا يقبل الإنترنت مباشرة: يستخدم Front Door Premium Private Link حيث يتاح، وإلا access restrictions تسمح Front Door service tag فقط مع التحقق من `X-Azure-FDID` الخاص بالinstance. أي request مباشر إلى origin يرفض.
- Blob origins private وتسمح Front Door/managed identity أو signed grants فقط؛ لا public container يتجاوز WAF أو entitlement checks.
- direct browser upload يملك Azure Blob CORS allow-list لـ`https://dorosak.com` فقط، methods `PUT, HEAD, OPTIONS`, headers/checksum المطلوبة فقط، ولا wildcard credentials. signed URL تقيد object key, method, size intent، ومدة قصيرة.

### 24.2 Environments

| Environment | Purpose | Data policy | Deployment |
|---|---|---|---|
| `Local` | Development on workstation | synthetic data، Neon Dev branch، local Redis/storage/mail | manual via CLI/Compose |
| `Dev` | Shared integration | independent Neon project, sandbox integrations | automatic from main |
| `Preview` | Trusted PR validation | temporary Neon Dev-derived branch, TTL 48h | automatic for trusted PR only |
| `Staging` | Production-like verification | independent project, synthetic/anonymized data | same release manifest promoted |
| `Production` | Real users | fully isolated accounts, secrets, storage, database | approved canary deployment |

- لا تنسخ Production data إلى بيئات أخرى دون anonymization معتمد.
- Production لا تستخدم scale-to-zero.
- Staging تطابق topology والأمن بقدر يسمح باختبار واقعي.

### 24.3 Release Process

1. Merge إلى protected main بعد required reviews/checks.
2. Build OCI images لكل workload مرة واحدة، وفحصها وتوقيعها وإنتاج SBOM لكل image.
3. إنشاء release manifest immutable تربط release ID بـWeb/API/Worker/MediaWorker image digests وapplication migration ID وHangfire schema version وevent/job contract versions.
4. Deploy release manifest نفسها إلى Dev ثم Staging وتشغيل migrations, integration, E2E, DAST، وload baseline.
5. Production approval عبر GitHub Environment وOIDC.
6. تنفيذ application migration artifact ثم Hangfire schema artifact عند الحاجة، كل منهما job واحدة وبـlocks وصلاحيات منفصلة.
7. نشر consumers المتوافقة مع N/N-1 أولًا، ثم API producers، ثم Web؛ لا تنتج API contract/job version جديدة قبل جاهزية consumers.
8. canary traffic: 5% لعشر دقائق، 25% لخمس عشرة دقيقة، ثم 100% إذا بقيت SLO سليمة، مع مراقبة worker/media contract failures.
9. إيقاف تلقائي عند ارتفاع 5xx, latency, DB saturation, SignalR reconnects، أو failed critical jobs.
10. rollback يعيد previous release manifest كاملة خلال هدف 10 دقائق؛ workers القديمة تبقى متوافقة مع queued contracts وschema تتعامل roll-forward إذا لزم.

### 24.4 Domains and TLS

- canonical product origin المستهدف `https://dorosak.com`، و`www` يحول إليه إذا كان النطاق مملوكًا عند مرحلة النشر.
- `assets.dorosak.com` للملفات العامة/الخاصة عبر CDN بلا application cookies.
- `status.dorosak.com` لصفحة حالة مستقلة عن runtime الأساسي.
- Staging وPreview خلف access control ولا تفهرسهما محركات البحث.
- TLS certificates مدارة وتجدد تلقائيًا؛ TLS 1.2/1.3 فقط.
- HSTS يبدأ بدون preload، ثم يضاف `includeSubDomains/preload` بعد التحقق من جميع subdomains.
- DNS registrar account محمي بـMFA, registrar lock, DNSSEC، ومسؤولين اثنين على الأقل.

## 25. Environment Variable Strategy

الأسماء التالية contract تشغيلية. القيم السرية تحفظ في Key Vault ولا تكتب في Git.

| Variable | Classification | Purpose |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Config | `Development`, `Staging`, `Production` |
| `Deployment__Environment` | Config | `local`, `dev`, `staging`, `prod` |
| `Deployment__Release` | Config | Git SHA/release version |
| `Deployment__ReleaseManifestDigest` | Config | immutable multi-workload release manifest digest |
| `App__PublicUrl` | Config | canonical public origin |
| `ConnectionStrings__Database` | Secret | Neon pooled runtime connection |
| `Migrations__ConnectionString` | Secret | Neon direct migrator connection |
| `ConnectionStrings__Hangfire` | Secret | Hangfire direct/restricted connection |
| `ConnectionStrings__RedisCache` | Secret | TLS distributed cache endpoint/ACL |
| `ConnectionStrings__RedisRateLimit` | Secret | isolated noeviction distributed limiter endpoint/ACL |
| `ConnectionStrings__RedisRealtime` | Secret | isolated SignalR backplane endpoint/ACL |
| `Hangfire__SchemaVersion` | Config | provider schema version required by release |
| `Jwt__Issuer` | Config | token issuer |
| `Jwt__Audience` | Config | API audience |
| `Jwt__SigningKeyReference` | Secret reference | asymmetric signing key/certificate location |
| `DataProtection__KeyStore` | Config | durable shared key ring location |
| `DataProtection__KeyEncryptionKey` | Secret reference | Key Vault protection key |
| `Storage__Endpoint` | Config | object storage endpoint |
| `Storage__Container` | Config | environment-specific private container |
| `Storage__BackupContainer` | Config | independent replicated backup destination |
| `Storage__AllowedOrigins__0` | Config | exact browser upload origin configured by IaC |
| `Storage__Credential` | Secret/reference | least-privilege storage identity |
| `Email__Provider` | Config | `Postmark` reference adapter |
| `Email__ApiKey` | Secret | transactional email API key |
| `Email__WebhookSecret` | Secret | email webhook verification |
| `Payments__Provider` | Config | configured provider when Commerce enabled |
| `Payments__ApiKey` | Secret | provider server credential |
| `Payments__WebhookSecret` | Secret | webhook signature verification |
| `Cors__AllowedOrigins__0` | Config | exact origin, used only where cross-origin exists |
| `OpenTelemetry__Endpoint` | Config | OTLP endpoint |
| `OpenTelemetry__Headers` | Secret | OTLP auth if required |
| `Media__MaxVideoBytes` | Config | bounded upload limit |
| `Media__SignedUrlMinutes` | Config | private delivery lifetime |
| `FeatureFlags__CacheSeconds` | Config | bounded server flag cache TTL |

- Angular browser لا يحتوي secrets.
- public runtime config للAngular يقدم من Web server ويتضمن API public path, release, locale defaults، وfeature availability فقط.
- لا تستخدم `environment.prod.ts` لحفظ secret أو provider private key.
- configuration تتحقق عند startup وتفشل مبكرًا إذا غابت قيمة حرجة.

## 26. CI/CD and GitHub Strategy

### 26.1 Repository Policy

- trunk-based development مع فروع قصيرة مثل `feature/course-authoring` و`fix/refresh-rotation`.
- `main` محمي بعد إنشاء workflows، ولا direct push.
- required pull request review، required status checks، ومنع force push.
- Conventional Commits باللغة الإنجليزية: `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `build:`, `ci:`, `chore:`.
- كل major phase تنتهي بـcommit/push بعد verification وبموافقة المستخدم.
- GitHub Actions token read-only افتراضيًا، وpermissions ترفع للخطوة التي تحتاج فقط.
- لا يستخدم `pull_request_target` لتشغيل كود غير موثوق.

### 26.2 Pull Request Pipeline

1. formatting and lint.
2. locked restore.
3. backend/frontend build with warnings policy.
4. unit tests and coverage.
5. PostgreSQL/Redis integration tests via Testcontainers.
6. API contract and architecture tests.
7. Angular component and accessibility tests.
8. SAST, secret scan, dependency/license review.
9. migration validation from empty and previous schema.
10. Docker image build and vulnerability scan.
11. SBOM generation.
12. trusted preview deployment and automatic cleanup where enabled.

ملفات workflows المخططة هي `ci.yml`, `security.yml`, `deploy-dev.yml`, `deploy-staging.yml`, `deploy-production.yml`, و`backup-restore-check.yml`. كل ملف يملك مسؤولية مستقلة، ويعاد استخدام composite actions صغيرة فقط عند وجود تكرار فعلي.

### 26.3 Main and Production Pipeline

- build images once and tag by Git SHA; deployment uses digest, not mutable tag.
- sign image/provenance with Cosign keyless.
- automatic Dev deploy and smoke test.
- promote the same signed release manifest and its workload digests to Staging and Production.
- Production GitHub Environment requires approvals، وdeployment concurrency واحد.
- cloud access عبر GitHub OIDC بلا client secret طويل العمر.
- migrations تعمل داخل Production network كjob، لا من developer machine.
- deployment record يحفظ release manifest digest, workload digests, application/Hangfire migration IDs, contract versions, approvals، وsmoke results.

## 27. Testing Strategy

| Level | Scope |
|---|---|
| Domain unit | aggregate invariants, value objects, lifecycle transitions |
| Application unit | validators, Result mapping, permission composition, pure policies |
| Application integration | handlers against real PostgreSQL via Testcontainers |
| API integration | middleware order, auth, authorization, CSRF, CORS, rate limits, ProblemDetails |
| Infrastructure | Redis, outbox, Hangfire, storage, email/payment adapters |
| Contract | OpenAPI breaking changes, generated clients, event schema versions |
| Architecture | dependency direction, module boundaries, prohibited generic abstractions |
| Frontend unit/component | signal stores, components, forms, interceptors, guards, a11y |
| End-to-end | Playwright for Guest/Student/Teacher/Admin critical journeys |
| Security | JWT, refresh replay, IDOR, CSRF, XSS, SSRF, uploads, headers, webhooks |
| Concurrency | refresh races, enrollment, coupon, attempts, progress, outbox duplicates |
| Performance | k6 load/stress/soak, SignalR, DB pool, media throughput |
| Resilience | Redis outage, worker crash, duplicate delivery, storage/email timeout |
| Migration | empty DB, N-1 schema, realistic-size Neon branch, rolling compatibility |
| Accessibility | axe automated + manual keyboard/screen reader/zoom/RTL review |

- EF Core InMemory provider ممنوع كبديل عن PostgreSQL tests.
- لا mock لـDbSet أو LINQ provider.
- critical auth matrix تختبر anonymous, unverified, authenticated without permission, owner, non-owner, revoked session, Admin with/without 2FA.
- كل production defect يحصل على regression test.
- coverage baseline 80% إجمالًا و90% branch للsecurity/session/permission/state stores، مع عدم اعتبار النسبة بديلًا عن جودة الاختبار.
- لا serious/critical axe findings، وLighthouse accessibility baseline 95 مع مراجعة يدوية.

## 28. Naming Conventions

### 28.1 C# and .NET

- namespaces, classes, records, methods, properties: `PascalCase`.
- parameters/local variables: `camelCase`.
- private fields: `_camelCase`.
- interfaces: `I` prefix عند وجود contract حقيقي.
- asynchronous methods: `Async` suffix، و`CancellationToken` آخر parameter.
- commands بصيغة فعل: `CreateCourseCommand`; queries بصيغة قراءة: `GetCourseQuery`.
- events بصيغة الماضي: `CoursePublishedDomainEvent`, `PaymentCapturedIntegrationEvent`.
- validators: `<RequestName>Validator`; handlers: `<RequestName>Handler`.
- IDs typed value objects داخل Domain مثل `CourseId`, وليس Guid غير مميز في كل موضع.
- يمنع `Manager`, `Helper`, `Utils`, `CommonService`, `BaseRepository` كأسماء غامضة.

### 28.2 Angular and TypeScript

- files/folders: `kebab-case` مثل `course-card.component.ts`.
- classes/types/components: `PascalCase`.
- variables/functions/signals: `camelCase`.
- Observable فقط تحمل suffix `$`; Signals لا تحمل `$`.
- selectors prefix `drs-`.
- route feature folders nouns، وactions أسماء أفعال واضحة.
- transport DTOs تحمل suffix `Dto`; UI/domain models لا تستخدم DTO مباشرة.
- لا prefixes من نوع `I` لكل TypeScript interface.

### 28.3 Database, API, and Events

- PostgreSQL: `snake_case`, plural table names، وconstraints مثل `pk_courses`, `fk_lessons_courses`, `uq_users_normalized_email`, `ix_enrollments_learner_status`.
- REST paths: lowercase plural kebab-case nouns، ولا أفعال إلا إذا كانت resource transition غير قابلة للنمذجة بصورة أوضح.
- JSON: `camelCase`.
- error codes: uppercase dotted constants مثل `AUTH.INVALID_CREDENTIALS`, `COURSE.VERSION_CONFLICT`.
- permission keys: `Resource.Action.Scope`.
- integration event types and fields تبقى إنجليزية ومؤرخة بـschema version.

## 29. Coding Standards

- SOLID, DRY, KISS، وYAGNI تطبق بالمعنى العملي، لا بكثرة interfaces والطبقات.
- أصغر تغيير صحيح هو المفضل.
- nullable reference types مفعلة، وwarnings/analyzers failures في CI باستثناء generated code الموثق.
- async/await لكل I/O، ولا `.Result`, `.Wait()`, `async void` إلا event handlers UI المبررة.
- CancellationToken إلزامي للمسارات التي تنفذ I/O.
- methods قصيرة ومسؤوليتها واحدة، لكن لا تستخرج helper باسم غامض لمجرد تقليل عدد الأسطر.
- comments باللغة الإنجليزية وتشرح لماذا، لا تعيد وصف السطر.
- لا TODO/FIXME أو pseudo implementation في branch مدمجة. العمل غير المنجز يبقى GitHub Issue ولا يظهر كمسار production ناقص.
- لا secrets, sample passwords، أو real personal data في source/tests.
- input validation عند boundary، invariants داخل Domain، وconstraints داخل database.
- لا catch عام يبتلع exception، ولا logging ثم rethrow في كل طبقة.
- DateTime المباشر لا يستخدم لقرارات قابلة للاختبار؛ يستخدم `TimeProvider`.
- decimal للمال مع currency؛ لا double.
- كل collection API bounded، وكل sort deterministic.
- public API change يبدأ بالعقد والاختبار ثم التنفيذ.
- migrations والgenerated OpenAPI clients تراجع ولا تعدل يدويًا بعد اعتماد generation path.

## 30. Development Rules

### 30.1 Definition of Ready

لا تبدأ feature قبل وجود:

- user journey ومعيار قبول واضح.
- authorization matrix وdata ownership.
- API contract ومسار errors.
- database impact, indexes, retention، وmigration strategy.
- threat model للميزات الحساسة.
- telemetry المطلوبة وSLO impact.
- UX states: loading, empty, error, offline, permission denied، وconcurrency conflict.

### 30.2 Definition of Done

تعد feature مكتملة فقط إذا:

- code compiles ولا توجد warnings غير مبررة.
- unit/integration/E2E/security tests المطلوبة ناجحة.
- authorization server-side وIDOR tests مكتملة.
- OpenAPI وfrontend client متزامنان.
- migrations اختبرت على PostgreSQL/Neon branch.
- logging/metrics/traces لا تسرب PII أو secrets.
- keyboard, mobile, RTL/LTR, light/dark، وWCAG states اختبرت.
- loading/empty/error/offline/concurrency states موجودة.
- docs/runbooks/ADR تحدث عند الحاجة.
- Docker build وCI الخاصان بالworkloads الموجودة ناجحان: backend من phase 3، frontend من phase 4، وMediaWorker من phase 7؛ تبدأ production scan/sign/promotion gates في phase 12.
- التغيير commit/push إلى GitHub بعد شرح المستخدم وتأكيده.

### 30.3 ADR Register

| ADR | Decision | Status |
|---|---|---|
| `ADR-001` | Modular Monolith before Microservices | Accepted |
| `ADR-002` | .NET 10 LTS and Angular 21.2 LTS stable baselines | Accepted |
| `ADR-003` | Neon PostgreSQL is the system of record | Accepted |
| `ADR-004` | Short-lived JWT in memory + path-scoped rotating opaque refresh cookie | Accepted |
| `ADR-005` | Same public origin through Front Door | Accepted |
| `ADR-006` | Direct multipart uploads to quarantine object storage | Accepted |
| `ADR-007` | PostgreSQL full-text/trigram search before external engine | Accepted |
| `ADR-008` | Azure Container Apps reference deployment, no Kubernetes initially | Accepted |
| `ADR-009` | Single marketplace, no speculative multi-tenancy | Accepted |
| `ADR-010` | REST/OpenAPI 3.1, no GraphQL/OData initially | Accepted |
| `ADR-011` | EF Core DbContext as Unit of Work; no generic repository | Accepted |
| `ADR-012` | Transactional outbox and atomic idempotency with at-least-once delivery | Accepted |
| `ADR-013` | Locale-prefixed public routes for Arabic/English SSR and SEO | Accepted |
| `ADR-014` | One immutable release manifest for all workload image digests | Accepted |
| `ADR-015` | Concrete foreign keys instead of generic polymorphic target IDs | Accepted |
| `ADR-016` | Separate Production Redis services for cache, rate limits, and realtime | Accepted |
| `ADR-017` | Phase 5 browser identity, session rotation, MFA, and permission contracts | Accepted |
| `ADR-018` | Phase 6 catalog, authoring drafts, review, and release boundary contracts | Accepted |

## 31. Delivery Roadmap

يتم التنفيذ تسلسليًا. لا يبدأ أي صف قبل اجتياز بوابة الصف السابق وكتابة المستخدم `تم`.

| Phase | Deliverables | Exit gate and GitHub checkpoint |
|---|---|---|
| `0. Architecture` | هذا PROJECT_PLAN، القرارات والحدود وخطة البيانات/API/تشغيل | مراجعة الملف، commit/push `docs: add Dorosak architecture plan` |
| `1. Workstation and Repository Hardening` | تثبيت/تحقق Git, .NET SDK, Node, Angular CLI, Docker Desktop, IDE؛ gitignore, editorconfig, CODEOWNERS، وتصميم branch policy | أوامر versions ناجحة، commit/push foundation docs/config |
| `2. Development Infrastructure and Neon` | Neon Dev project/branch، environment contract، Docker Compose لـRedis/MinIO/Mailpit/ClamAV، synthetic data policy | اتصال Neon آمن وفحوص الخدمات، commit/push `chore: add development infrastructure` |
| `3. Backend Foundation and Initial CI` | solution/projects، dependency rules، Result، ProblemDetails، pipelines، EF/Neon migrations، Serilog، OTel، health، backend dev/production Dockerfiles، أول GitHub Actions، وتفعيل branch ruleset على checks حقيقية | build + container + architecture/integration tests، migration على Neon Dev، protected-main verification، commit/push |
| `4. Frontend Foundation` | Angular standalone SSR/hydration/PWA، locale routes، shells، design tokens، routing، API client، errors، frontend dev/production Dockerfiles، accessibility baseline | SSR/hydration/container/E2E/bundle gates، commit/push |
| `5. Identity and Security` | registration, verification, login, refresh rotation, sessions, reset, TOTP, roles/permissions, CSRF/headers/rate limits | security test matrix كاملة، commit/push |
| `6. Catalog and Authoring Drafts` | categories/tags، teacher onboarding، course drafts/revisions، review workflow وcatalog projection contracts؛ لا تفعيل CourseRelease نهائية بعد | draft concurrency/auth/search-contract tests، commit/push |
| `7. Media` | direct/chunk/stream uploads، quarantine، ClamAV، image variants، HLS، signed delivery، quotas، MediaWorker production image | container + malicious-file/resource exhaustion tests، commit/push |
| `8. Learning, Assessments, and Publishing` | quizzes/assignments versions، PublishingCoordinator، final CourseRelease activation بعد Ready media/assessments، entitlements/enrollment، player/progress/offline، grading | publication invariant + learning integrity/offline/concurrency tests، commit/push |
| `9. Engagement and Realtime` | reviews، discussions، reports، moderation، messaging، notifications، announcements، SignalR | multi-replica reconnect/dedupe/auth tests، commit/push |
| `10. Commerce and Credentials` | checkout sessions، products/offers/coupons/orders، provider adapter، refunds/disputes، subscriptions، ledger/reconciliation/payout-ready، certificates | webhook/idempotency/reconciliation/security tests، commit/push |
| `11. Admin, CMS, Analytics` | dashboards، settings، feature flags، CMS، audit UI، reports/exports/recommendations | permission matrix، performance/accessibility gates، commit/push |
| `12. Production Containers and Staging` | hardening/signing للproduction images الموجودة، release manifests، Azure Container Apps، Key Vault، Redis Cache/Security/Realtime، Blob/Front Door، Postmark sandbox، Dev/Preview/Staging promotion | clean-environment deploy، staging restore test، commit/push IaC |
| `13. Production CI/CD and Cloud Hardening` | protected environments، OIDC، signing/SBOM/scans، domains/TLS، canary/rollback، backups/runbooks/alerts | Production Readiness Review، no open Critical/High |
| `14. Production Launch` | migrations، canary، smoke tests، monitoring، status page، support handover | seven-day stabilization وoperational handover |

## 32. No-Go Conditions

يمنع Production deployment عند تحقق أي بند:

- لا توجد استعادة backup ناجحة ومقاسة.
- migration غير backward-compatible أو غير مختبرة على حجم واقعي.
- secrets موجودة في Git أو image أو logs.
- ثغرة Critical أو High قابلة للاستغلال بلا استثناء ساري ومؤقت.
- authorization matrix أو IDOR tests ناقصة.
- أكثر من API replica مع SignalR من دون Redis HA/backplane strategy مختبرة.
- Data Protection key ring غير دائمة أو غير قابلة للاستعادة.
- لا يوجد owner/on-call/runbook للتنبيهات الحرجة.
- فشل اختبار ضعف peak load أو تجاوز frontend budgets بلا قرار موثق.
- Production Neon متاحة من الإنترنت بلا allow-list/private path، أو runtime role تملك DDL.
- الدفع مفعل قبل webhook verification, idempotency, reconciliation، والمتطلبات القانونية.
- Privacy/Terms غير معتمدتين قبل public launch.

## 33. Phase 0 Completion Report (Completed)

تمت الموافقة على Phase 0 ورفعها إلى `origin/main`. التعليمات أدناه سجل تاريخي للمرحلة المكتملة ولا يعاد تنفيذها.

===========================
### 1. ماذا أنجزت؟
===========================

تم إنشاء `PROJECT_PLAN.md` فقط، من دون إنشاء Backend أو Frontend أو Docker files أو migrations أو أي كود تنفيذي. يحتوي الملف على الرؤية، المتطلبات الوظيفية وغير الوظيفية، الأدوار والصلاحيات، Tech Stack، Clean Architecture، هيكل المجلدات المستهدف، تصميم Backend وAngular، مخطط PostgreSQL وNeon، علاقات البيانات والقيود والفهارس، تصميم API، مسارات Authentication وAuthorization، وخطط caching/logging/monitoring/security/performance/scaling/deployment/CI/CD.

كما تم تحويل المشروع إلى مراحل لها بوابات قبول واضحة. الغرض هو منع البدء العشوائي أو إنشاء ملفات لا تتفق لاحقًا مع الأمن ونموذج البيانات والنشر.

===========================
### 2. لماذا اخترت هذه الطريقة؟
===========================

تم اختيار `Modular Monolith` بدل Microservices لأن المنتج واسع لكن الفريق والمستخدم ما زالا في بداية المشروع. هذا الاختيار يحافظ على معاملات PostgreSQL البسيطة، ويقلل تكلفة DevOps، ويمنع فشلًا موزعًا غير ضروري، مع حدود modules تسمح بالاستخراج لاحقًا عند وجود قياس حقيقي.

تم اختيار `.NET 10 LTS` بدل `.NET 9` لأن LTS هو الأنسب لعمر منصة إنتاجية في تاريخ هذه الخطة. وتم اختيار Angular 21.2 LTS standalone مع SSR/hydration بدل Angular 19 غير المدعوم وAngular 20 الأقصر دعمًا، واختيار Neon PostgreSQL يلغي أي اعتماد على SQL Server. كما تم اعتماد access JWT قصيرة داخل الذاكرة وrefresh token مدورة داخل Secure HttpOnly cookie لتلبية JWT مع تقليل مخاطر سرقة token الدائمة.

تم اختيار Azure Container Apps بدل Kubernetes كبداية لأنه يقدم containers, autoscaling, revisions, jobs, secrets، وWebSockets مع عبء تشغيلي أقل. PostgreSQL يبقى مصدر الحقيقة، بينما Redis وSignalR وcache طبقات قابلة لإعادة البناء، وهذا يمنع فقد البيانات عند تعطلها.

===========================
### 3. ماذا يجب أن أفعل أنا؟
===========================

هذه أول نقطة تحتاج إجراء منك. راجع الملف ثم ارفعه إلى GitHub قبل أن نبدأ المرحلة التالية.

1. افتح `Visual Studio Code`.
2. من الشريط الأيسر اضغط أيقونة `Explorer`.
3. اضغط على الملف `PROJECT_PLAN.md` الموجود مباشرة داخل مجلد `Dorosak`.
4. راجع العناوين والقرارات. لا تعدل أي secret ولا تضف connection string.
5. من القائمة العلوية اضغط `Terminal` ثم `New Terminal`.
6. تأكد أن Terminal يعرض المسار `D:\Projects\Dorosak`.
7. انسخ الأمر التالي والصقه ثم اضغط Enter:

```powershell
git status
```

8. يجب أن ترى `PROJECT_PLAN.md` تحت `Untracked files`.
9. انسخ الأوامر التالية واحدًا واحدًا، واضغط Enter بعد كل أمر:

```powershell
git add PROJECT_PLAN.md
git diff --cached --stat
git commit -m "docs: add Dorosak architecture plan"
git push origin main
```

10. إذا فتح GitHub نافذة تسجيل دخول، اضغط `Sign in with your browser`، وسجل دخولك إلى الحساب `MohammadAlghazo`، ثم وافق على التفويض وعد إلى Visual Studio Code.
11. افتح المتصفح وانتقل مباشرة إلى `https://github.com/MohammadAlghazo/Dorosak`.
12. اضغط قائمة الملفات وتأكد أن `PROJECT_PLAN.md` ظاهر وأن آخر commit اسمه `docs: add Dorosak architecture plan`.
13. بعد نجاح ذلك، ارجع إلى المحادثة واكتب كلمة واحدة فقط: `تم`.

===========================
### 4. ما الذي سيحدث بعد أن أنفذ الخطوات؟
===========================

سيصبح المخطط المعماري محفوظًا محليًا وعلى GitHub، ويمكن الرجوع إلى تاريخ تغييره. بعد أن تكتب `تم` سنبدأ المرحلة `1. Workstation and Repository Hardening`. في تلك المرحلة سأتحقق من البرامج الموجودة على جهازك، ثم أعطيك تعليمات التثبيت الدقيقة لما ينقص فقط، ولن أنشئ Backend أو Frontend قبل نجاح فحوص الأدوات وموافقتك.

===========================
### 5. كيف أتأكد أن كل شيء يعمل؟
===========================

بعد `git push origin main` يجب أن ترى رسالة شبيهة بـ`main -> main` من دون `rejected` أو `fatal`. وعند تشغيل:

```powershell
git status
```

يجب أن ترى `nothing to commit, working tree clean`.

الأخطاء الشائعة في هذه المرحلة:

- `Author identity unknown`: لا تضبط Git عشوائيًا. أرسل لي نص الخطأ وسأعطيك الأمر المناسب للاسم والبريد اللذين تريد إظهارهما في GitHub.
- `Authentication failed`: أعد تنفيذ `git push origin main` واختر تسجيل الدخول عبر المتصفح. لا ترسل لي password أو token.
- `rejected non-fast-forward`: لا تستخدم force push. أرسل لي نص الخطأ كاملًا لأفحص التغييرات بأمان.
- `pathspec PROJECT_PLAN.md did not match`: Terminal ليس داخل `D:\Projects\Dorosak`. أغلقه وافتح `Terminal > New Terminal` من مجلد المشروع.
- الملف لا يظهر في GitHub بعد نجاح push: اضغط فرع `main` في GitHub ثم حدّث الصفحة بـ`Ctrl+F5` وتحقق من اسم commit.

===========================
### 6. هل ننتقل للمرحلة التالية؟
===========================

لا. نفذ خطوات المراجعة ورفع `PROJECT_PLAN.md` أولًا، ثم اكتب:

`تم`

لن تبدأ أي مرحلة أو كتابة كود قبل هذا التأكيد.

## 34. Phase 1 Completion Report (Completed)

تم رفع Phase 1 إلى `origin/main` في commit `829dc7f`.

===========================
### 1. ماذا أنجزت؟
===========================

- تم التحقق من Git `2.51.1`, .NET SDK `10.0.302`, Node.js `24.19.0`, npm `11.17.0`، وAngular CLI `21.2.20` محليًا عبر npx.
- تم تشغيل Docker Desktop `4.85.0`, Docker Engine `29.6.2`, Docker Compose `5.3.1` على Linux/WSL2.
- تم تثبيت GitHub CLI `2.97.0`; يؤجل browser authentication حتى Phase 3 عند إنشاء CI وbranch ruleset.
- بقي Angular CLI `19.2.19` العالمي دون تغيير لحماية المشاريع القديمة؛ Dorosak سيقفل Angular CLI 21.2 داخل المشروع.
- تمت إضافة `.gitattributes`, `.editorconfig`, `.gitignore`, `CODEOWNERS`، وإعدادات وتوصيات VS Code.
- تم تحديث `README.md`، واعتماد architecture baseline كإصدار `1.1.0` وتحويل ADRs إلى `Accepted`.
- لم يتم إنشاء Backend أو Frontend أو database، ولم يتم حذف أي Docker image, container، أو volume.

===========================
### 2. لماذا اخترت هذه الطريقة؟
===========================

توحيد line endings والتنسيق وملفات التجاهل قبل إنشاء code يمنع ضوضاء diffs وتسرب secrets ورفع build outputs. تم تثبيت الأدوات المطلوبة فقط، بينما أجل Azure CLI إلى مرحلة Cloud التي تحتاجه. كما أُجل فرض branch protection إلى Phase 3 لأن required status checks غير موجودة الآن، وتفعيله قبل CI سيعطل workflow بدل حمايته.

===========================
### 3. ماذا يجب أن أفعل أنا؟
===========================

هذا هو التدخل الوحيد المطلوب لإغلاق Phase 1: مراجعة الملفات الثمانية ورفعها إلى GitHub.

```powershell
git status --short
git add .editorconfig .gitattributes .gitignore .github/CODEOWNERS .vscode/extensions.json .vscode/settings.json PROJECT_PLAN.md README.md
git diff --cached --check
git --no-pager diff --cached --stat
git commit -m "chore: harden repository foundation"
git push origin main
```

لا تستخدم `git add .` أو force push في هذه النقطة.

===========================
### 4. ما الذي سيحدث بعد أن أنفذ الخطوات؟
===========================

سيصبح repository foundation محفوظًا ومتزامنًا. تبدأ بعده Phase 2 بإنشاء Neon Dev project والاتصال الآمن به، ثم إعداد Redis/MinIO/Mailpit/ClamAV عبر Docker Compose من دون إنشاء application code قبل اكتمال البنية التطويرية.

===========================
### 5. كيف أتأكد أن كل شيء يعمل؟
===========================

بعد push يجب أن يعرض `git status` النص `nothing to commit, working tree clean`، وأن يظهر commit باسم `chore: harden repository foundation` في GitHub. يجب ألا يظهر تحذير LF/CRLF بعد اعتماد `.gitattributes`.

إذا فتح Git pager اضغط `q`. إذا رفض push لا تستخدم `--force`; أرسل الخطأ. إذا فشل `git diff --cached --check` فلا تنفذ commit وأرسل الناتج.

===========================
### 6. هل ننتقل للمرحلة التالية؟
===========================

لا تبدأ Phase 2 حتى ينجح push ويكتب المستخدم `تم`.

## 35. Phase 2 Completion Report (Ready for Checkpoint)

===========================
### 1. ماذا أنجزت؟
===========================

- تم إنشاء Neon project مستقل باسم `Dorosak Dev` ومعرف `wispy-glitter-91617289` في Frankfurt `aws-eu-central-1`.
- يعمل المشروع على PostgreSQL 18، والنسخة الفعلية المختبرة `18.4`، مع branch `main`, database `dorosak_dev`، وowner role باسم `dorosak_owner`.
- تم اختبار الاتصال المباشر وpooled باستخدام TLS `verify-full`. لم تحفظ connection strings في Git ولم تطبع في logs.
- بقي مشروع Neon القديم `Budgetha` دون تعديل.
- تمت إضافة Docker Compose بصور ثابتة tag + digest لـRedis `8.10.0`, MinIO, Mailpit `1.30.6`، وClamAV `1.5.3`.
- جميع المنافذ مرتبطة بـ`127.0.0.1` وبأرقام لا تتعارض مع defaults الشائعة.
- ينشئ `Initialize-LocalEnvironment.ps1` ملف `.env.local` عشوائيًا ويحفظ Neon owner credentials محليًا فقط، ويقيد Windows ACL بالمستخدم وSYSTEM وAdministrators.
- تمت إضافة `Test-DevelopmentInfrastructure.ps1` لاختبار Redis read/write, MinIO, Mailpit, ClamAV EICAR، وNeon direct/pooled.
- تمت إضافة دليل التشغيل وسياسة Synthetic Data. الخدمات الأربع تعمل بحالة `Healthy` واجتازت الاختبارات السلوكية.

===========================
### 2. لماذا اخترت هذه الطريقة؟
===========================

عُزل Dorosak عن أي مشروع سابق، واختيرت Frankfurt لقربها من مستخدمي الشرق الأوسط. PostgreSQL 18 هو إصدار Neon GA الفعلي. تستخدم migrations الاتصال المباشر، بينما سيستخدم API اتصالًا pooled بحساب `dorosak_app` محدود الصلاحيات ينشأ في Phase 3؛ لا يستخدم التطبيق `dorosak_owner`.

تثبيت image digests يمنع تغير البيئة بصمت، وربط المنافذ بـlocalhost يمنع تعريض الخدمات للشبكة. استخدمت منافذ بديلة حتى يمكن تشغيل مشاريع Docker قديمة بالتوازي. رفعت ذاكرة ClamAV إلى `2 GiB` بعد قياس OOM حقيقي، لا بالتخمين.

===========================
### 3. ماذا يجب أن أفعل أنا؟
===========================

هذا هو تدخل GitHub الوحيد لإغلاق Phase 2. لا تضف `.env.local` تحت أي ظرف.

```powershell
git add docker-compose.yml deploy/env/.env.example deploy/neon/development.json deploy/scripts/Initialize-LocalEnvironment.ps1 deploy/scripts/Test-DevelopmentInfrastructure.ps1 docs/DEVELOPMENT_SETUP.md docs/SYNTHETIC_DATA_POLICY.md README.md PROJECT_PLAN.md
git diff --cached --check
git --no-pager diff --cached --stat
git commit -m "chore: add development infrastructure"
git push origin main
```

===========================
### 4. ما الذي سيحدث بعد أن أنفذ الخطوات؟
===========================

تبدأ Phase 3 بإنشاء .NET solution والمشاريع وClean Architecture boundaries، ثم EF Core/Npgsql وruntime role محدود وmigration أولى على Neon، وبعدها Result/ProblemDetails/pipelines/logging/telemetry/health وCI.

===========================
### 5. كيف أتأكد أن كل شيء يعمل؟
===========================

يمكن إعادة الفحص في أي وقت:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\deploy\scripts\Test-DevelopmentInfrastructure.ps1
git status
```

يجب أن تنتهي الفحوص بالنص `All development infrastructure checks passed.`، وبعد push يعرض Git النص `nothing to commit, working tree clean`.

إذا كان أول تشغيل بعد حذف ClamAV volume فقد يحتاج عدة دقائق لتنزيل signatures. لا تستخدم `docker compose down --volumes`، ولا تشارك `.env.local` لمعالجة أي خطأ.

===========================
### 6. حالة التسليم الحالية
===========================

هذا القسم تاريخي خاص بإغلاق Phase 2. تم تنفيذ Phase 3 وPhase 4 وPhase 5 بعده، ثم أغلقت Phase 6 بعد تحققها في
`2026-08-07`. المرحلة التالية هي Phase 7، ولا تبدأ حتى يؤكد المستخدم بكتابة `تم`.

===========================
### 7. Phase 5 Identity and Security Report
===========================

#### المنجز

- ASP.NET Core Identity مع users/roles/claims، profile واحد لكل user، seeded Student/Teacher/Admin roles، وثوابت
  permissions من catalog.
- تسجيل محايد، password policy بطول `12-64`، lockout، HIBP k-anonymity adapter، email verification، email change
  pending flow، password reset/change، وoutbox email worker مع Mailpit SMTP وretry/lock.
- access JWT غير متماثل RSA (`10 minutes`) مع session validation مقابل DB، وrefresh tokens opaque hashed تدور في كل
  استخدام (`14/30 days`) مع `FOR UPDATE` وreplay detection وsecurity events.
- refresh cookie باسم `__Secure-dorosak-refresh`، `HttpOnly/Secure/SameSite=Lax/Path=/api/v1/auth`، فصل CSRF cookie
  الداخلية عن `XSRF-TOKEN`، وorigin validation لمسارات الهوية والجلسات.
- MFA TOTP secrets محمية بـData Protection، تحدي MFA قصير العمر قبل إصدار credentials، replay-resistant time-step،
  recovery codes hashed single-use، وتعطيل MFA مع recent authentication وAdmin enforcement.
- dynamic permission authorization من `identity.role_claims`، admin high-risk policy، bootstrap تشغيلي لمرة واحدة
  يتطلب secrets من secret manager ويتوقف بعد التنفيذ.
- Angular identity client/session coordinator، memory-only access token، CSRF single-flight، refresh single-flight
  داخل tab وWeb Locks/BroadcastChannel عبر tabs، guards وreturnUrl validation، صفحات التسجيل والدخول/MFA والتحقق
  والاستعادة وتغيير البريد وإعدادات الأمان والجلسات.

#### مخطط البيانات والترحيلات

- `20260806223946_Phase5IdentitySecurity`: identity tables, profiles, sessions, refresh tokens, MFA, security events,
  roles/permission seed, Data Protection key storage.
- `20260807113555_AddPendingEmailChange`: pending email state for verified email-change links.
- security events append-only privilege is applied in migration/bootstrap; migration compatibility maximum is updated
  by each Phase 5 migration.

#### بوابات التحقق المنفذة

- `dotnet build .\backend\Dorosak.slnx --configuration Release --no-restore`
- `dotnet test .\backend\Dorosak.slnx --configuration Release --no-build --no-restore`
- `npm run lint`, `npm run test`, `npm run build`, `npm run format:check`
- `dotnet list .\backend\Dorosak.slnx package --vulnerable --include-transitive`
- `npm audit --audit-level=high`
- PostgreSQL/Redis integration journeys: CSRF, neutral registration, email verification/change, lockout, password
  reset invalidation, MFA/recovery single-use, refresh rotation/replay, session revoke, permission denial, Redis
  fail-closed, outbox retry, and admin bootstrap.

#### القرار

Phase 5 مكتملة من ناحية التنفيذ والاختبارات المحلية. Phase 6 مكتملة من ناحية التنفيذ والاختبارات المحلية، مع تأجيل
تشغيل Playwright العام الخاص بمشكلة SSR المعروفة إلى متابعة منفصلة.

===========================
### 8. Phase 6 Catalog and Authoring Drafts Report
===========================

#### المنجز

- teacher applications/profile approval مع حالات `Pending`, `InReview`, `Approved`, `Rejected`, `Withdrawn`، تعيين
  Teacher role مع إبقاء Student، إبطال الجلسات، والتدقيق الأمني.
- courses/localizations/permanent historical slugs، ownership transfer، collaborators، taxonomy categories/tags، وفهارس
  keyset pagination.
- active drafts، sections/lessons، immutable revisions، ETag/If-Match، cursor HMAC، publication reviews حتى
  `ReadyToPublish` فقط. لا يوجد `CourseRelease` أو نشر عام في Phase 6.
- public catalog/search/suggestions/featured/popular/recommendations بعقود release-backed؛ النتائج العامة تبقى فارغة
  حتى Phase 8 دون كشف drafts.
- Angular catalog/search/detail مع filters/query state/cursor pagination/safe highlights، وصفحات teacher application،
  instructor metadata/curriculum/publication، وadmin teacher/publication/taxonomy review.
- عقد public موحد لحقول release المستقبلية، taxonomy admin ترى inactive terms، وETag مكشوف عبر CORS.
- row locking في teacher/course/draft/review transitions لمنع races، وعدم إسقاط autosave أثناء طلب سابق، ودعم IDs
  الجديدة في curriculum.

#### الترحيلات والبيانات

- `20260807182959_Phase6CatalogAuthoring`: profiles, catalog, authoring schemas/tables, constraints, taxonomy seeds,
  permissions, search extensions, and runtime grants.
- `20260807221624_Phase6ConcurrencyAndCatalogIndexes`: pagination indexes and schema compatibility marker.
- `docs/adr/ADR-018-phase-6-catalog-authoring-contracts.md`: approved scope and contracts.

#### بوابات التحقق

- Backend Release build: `0 warnings`, `0 errors`.
- Backend tests: `78 passed`, including domain, application, PostgreSQL integration, API, and architecture suites.
- Frontend tests: `45 passed` across `17` files; lint, Prettier, and production build passed.
- `git diff --check` passed; NuGet/npm audits and bundle/PWA checks passed in the final checkpoint.

#### القرار

Phase 6 مكتملة. المرحلة التالية هي Phase 7 Media and Content Delivery، ولا تبدأ قبل تأكيد المستخدم بكلمة `تم`.
