# نقل Inspecta من Render إلى سيرفر VPS (Hostinger) خطوة بخطوة

الهدف: سيرفر واحد فيه **Caddy** (HTTPS) و**API** و**PostgreSQL** جديدة وفاضية لأول عميل حقيقي، مع backup يومي وتجربة استرجاع.
مفيش بيانات تتنقل من Render، فمفيش downtime: نجهّز السيرفر الجديد على عنوان تجريبي، نجرّبه، وبعدين نحوّل الدومين.
الملفات في هذا المجلد بتشتغل على أي VPS فيه Ubuntu (Hostinger أو Hetzner أو Vultr...)، مش مربوطة بمزوّد.

## 0) القرارات اللي اتاخدت
- **المزوّد: Hostinger VPS** (بديل Hetzner اللي تحققه بطيء). نختار **KVM 2** (2 vCPU، 8GB RAM، 100GB NVMe). السعر حوالي 9 دولار في الشهر بعرض 24 شهر، لكن **اشترِ شهر أو سنة بس** علشان ما تتحبسش، وعندهم ضمان استرجاع 30 يوم (تأكد من الشروط وقت الشراء).
- **الموقع:** **ألمانيا (Frankfurt)** لو متاح في قايمة الأماكن وقت الشراء، وإلا فرنسا. **المكان بيتثبت بعد الإعداد**، فاختاره صح من أول مرة.
- **نظام التشغيل:** **Ubuntu 24.04 عادي** (مش قالب فيه لوحة تحكم ولا Docker جاهز؛ السكريبت بيثبّت Docker بنفسه).
- **الإيميل:** لازم السيرفر يوصل لـ `smtp.office365.com:587` و`outlook.office365.com:993`. بنختبرهم في الخطوة 2 قبل أي حاجة.
- لو عميل سعودي طلب استضافة **داخل المملكة**: نعمل نسخة منفصلة بنفس هذه الملفات عند مزوّد سعودي.

## 1) إنت تعمل (حوالي ساعة)
1. **مفتاح SSH:** من جهازك (PowerShell) `ssh-keygen -t ed25519` ثم اعرض المفتاح العام: `Get-Content $env:USERPROFILE\.ssh\id_ed25519.pub` وانسخه.
2. في Hostinger: اشترِ **VPS KVM 2** ثم في إعداد النظام اختر **Ubuntu 24.04 (OS only)**، والموقع **Germany/Frankfurt**، وحط **مفتاح الـ SSH** اللي نسخته (أو أضفه من hPanel > VPS > Settings > SSH keys). كلمة سر الـ root اللي بيطلبوها احفظها في مدير كلمات السر، بس إحنا هنقفل الدخول بكلمة سر.
3. **من لوحة Hostinger:** فعّل **Backups/Snapshots** لو متاحة (لقطة كاملة للسيرفر فوق الـ backup بتاعنا). لو فيه خدمة Malware scanner (Monarx) سيبها شغالة.
4. **الدومين:** ضيف سجل `A` لعنوان تجريبي، مثلًا `api2.gitsplatform.com`، يشاور على IP السيرفر. (الدومين الأساسي `api.gitsplatform.com` بيتحوّل في الخطوة 9.)

## 2) اختبار أول (دقيقتين، قبل أي تجهيز)
ادخل بـ `ssh root@<IP>` وجرّب:
```
nc -zv -w 5 smtp.office365.com 587
nc -zv -w 5 outlook.office365.com 993
```
لو الاتنين `succeeded` كمّل. لو أي واحد فشل، **ما تكمّلش**: افتح تذكرة لـ Hostinger عن بورت 587 الخارج، أو ننقل لمزوّد تاني (Vultr مثلًا) قبل ما نضيع وقت.

## 2b) تجهيز السيرفر (مرة واحدة)
من جهازك (من مجلد المشروع) انسخ السكريبت للسيرفر ثم شغّله:
```
scp deploy/vps/server-setup.sh root@<IP>:/root/
ssh root@<IP> "bash /root/server-setup.sh"
```
السكريبت يرفض يشتغل لو مفيش مفتاح SSH للـ root (علشان ما يقفلش عليك الدخول). هو: يحدّث النظام، يثبّت Docker، يعمل مستخدم `deploy`، يقفل الدخول بكلمة سر (مفاتيح فقط)، يشغّل firewall و fail2ban وتحديثات الأمان التلقائية، ويضبط التوقيت على القاهرة.
**قبل ما تقفل الجلسة:** افتح نافذة تانية وجرّب `ssh deploy@<IP>` وتأكد إنها دخلت.

## 3) الكود على السيرفر
الـ repo خاص، فنحتاج **Deploy key** للقراءة فقط:
```
ssh-keygen -t ed25519 -f ~/.ssh/inspecta_deploy -N ""
cat ~/.ssh/inspecta_deploy.pub     # انسخه
```
في GitHub: Repo Settings > Deploy keys > Add (بدون Write). ثم:
```
printf 'Host github.com\n  IdentityFile ~/.ssh/inspecta_deploy\n' >> ~/.ssh/config
git clone git@github.com:GITS-Organization/Inspecta.git ~/inspecta
cd ~/inspecta && git checkout backend
cd deploy/vps
```

## 4) ملف الإعدادات `.env`
**الطريقة الأسهل (موصى بها):** على السيرفر شغّل `./init-env.sh`. هيسألك عن العنوان وإيميلك وبيانات الأدمن (كلمة السر بتكتبها إنت وهي مخفية)، ويولّد باقي الأسرار لوحده من غير ما يطبعها، ويعمل `.env` و`backups/`. **ما تلصقش أي سر في شات.** بعدها افتح `nano .env` على شاشتك إنت وانسخ `IMAP_ENCRYPTION_KEY` و`POSTGRES_PASSWORD` لمدير كلمات السر.الطريقة اليدوية تحت بديل لو حبيت.
```
cp .env.example .env
./gen-secrets.sh          # بيطبع قيم جديدة للأسرار: انسخها بإيدك لملف .env
nano .env                 # املا API_DOMAIN (api2...) و ACME_EMAIL و BOOTSTRAP_ADMIN_* والباقي
chmod 600 .env
mkdir -m 700 backups
```
**`ACME_EMAIL` لازم إيميل حقيقي (مش `you@example.com`) وإلا Let's Encrypt هيرفض الشهادة.** القيم اللي فيها مسافات (زي الاسم الكامل) اكتبها بين علامتي تنصيص.
**ما تبعتش قيم الـ `.env` لأي حد ولا في شات.** احفظ نسخة منها في مدير كلمات السر عندك: لو ضاع `IMAP_ENCRYPTION_KEY` كلمات سر الصناديق المحفوظة مش هتتفك.

## 5) التشغيل
```
docker compose up -d --build
docker compose ps
docker compose logs api --tail 50
```
أول تشغيل: الـ API بيبني الجداول لوحده (migrations) على قاعدة فاضية، وCaddy بيجيب شهادة HTTPS. افتح `https://api2.gitsplatform.com/health` والمفروض يرجّع `Healthy`.

## 6) أول حساب وأول عميل حقيقي
1. ادخل بحساب الـ Bootstrap Admin (من `.env`) وغيّر كلمة السر فورًا.
2. من الـ admin console (غيّر عنوان الـ API فيه للعنوان الجديد) أنشئ شركة Integrity والـ technical manager والـ coordinators.
3. بعد أول دخول **امسح القيم التلاتة** `BOOTSTRAP_ADMIN_EMAIL` و`BOOTSTRAP_ADMIN_FULL_NAME` و`BOOTSTRAP_ADMIN_PASSWORD` من `.env` (سيبهم فاضيين كلهم، مش واحد بس، وإلا الـ API مش هيقوم)، ثم `docker compose up -d`.

## 7) الـ backup (قبل أي شهادة حقيقية)
```
./backup.sh && ls -lh backups/
./restore-test.sh                 # لازم يخلص بـ "the backup can be restored"
crontab -e                        # ضيف السطر ده:
0 3 * * * /home/deploy/inspecta/deploy/vps/backup.sh >> /home/deploy/backup.log 2>&1
```
**نسخة خارج السيرفر:** اعمل مكان تخزين خارجي رخيص (**Hetzner Storage Box** حوالي 4 يورو، أو أي bucket متوافق مع S3 زي Backblaze B2 أو Cloudflare R2)، اضبطه بـ `rclone config` (اسم الـ remote مثلًا `storagebox`) وحط الاسم في `RCLONE_REMOTE` في `.env`. من غير ده النسخ بتفضل على نفس السيرفر.
جرّب الاسترجاع مرة كل شهر.

## 8) تجربة قبل التحويل (على العنوان التجريبي)
- تسجيل دخول لكل دور (admin، technical manager، coordinator، inspector).
- طلب جديد ← عرض سعر ← قبول ← تعيين inspector ← فحص ← شهادة ← اعتماد.
- QR الشهادة بيفتح صفحة التحقق، وتنزيل الـ PDF شغال.
- رفع report من الـ inspector وظهوره عند المراجعة.
- ربط صندوق Outlook وقراءة إيميل جديد. **الإيميل الخارج:** بعض المزوّدين بيقفلوا بورت 25 و465. إحنا بنستخدم 587 مع Outlook وبنختبره في الخطوة 2. لو أي إرسال فشل بـ "mail server did not answer" افتح تذكرة للمزوّد لفتح البورت.
- بعد ما الإرسال التجريبي ينجح بس: `QUOTE_DELIVERY_ENABLED=true` و `CERTIFICATE_DELIVERY_ENABLED=true` في `.env` ثم `docker compose up -d`.

## 9) التحويل (دقايق)
1. قلّل الـ TTL لسجلات الدومين قبلها بيوم (300 ثانية).
2. غيّر سجل `A` لـ `api.gitsplatform.com` ليشاور على IP السيرفر الجديد، وغيّر `API_DOMAIN` في `.env` له، ثم `docker compose up -d`.
3. حدّث العنوان في: الفرونت، تطبيق الـ Flutter، الـ admin console، و**Redirect URI في تطبيق Microsoft** (`https://api.gitsplatform.com/api/v1/email-connection/microsoft/callback`) وفي `MICROSOFT_OAUTH_REDIRECT_URI`.
4. سيب Render شغال أسبوع كـ fallback ثم الغيه. (بما إن مفيش بيانات حقيقية عليه، مفيش داعي لنقل داتا.)

## التحديث بعد كده
```
cd ~/inspecta/deploy/vps && ./update.sh          # backup ثم آخر backend ثم build ثم health check
./update.sh <commit>                                 # للرجوع لنسخة قديمة من الكود
```
الـ migrations بتتطبق لوحدها عند التشغيل. الرجوع بالكود لا يرجّع بيانات: للبيانات استخدم الـ backup.

## أوامر مفيدة
```
docker compose logs api --tail 100 -f      # لوج الـ API
docker compose ps                          # الحالة
docker compose restart api
df -h ; docker system df                   # المساحة
```
**المساحة:** ملفات الـ reports بتتخزن في قاعدة البيانات وبتتمسح بعد 30 يوم. راقب `df -h` شهريًا.

## الاسترجاع (لو حصلت مشكلة في البيانات)
1. وقف الـ API: `docker compose stop api caddy`
2. امسح القاعدة وأنشئها فاضية:
   `docker compose exec -T db dropdb -U inspecta inspecta && docker compose exec -T db createdb -U inspecta inspecta`
3. استرجع الملف: `docker compose exec -T db pg_restore -U inspecta -d inspecta --no-owner < backups/<الملف>.dump`
4. ارجع بالكود للنسخة اللي تناسب الـ backup (`update.sh` بيطبع اسم الـ backup ورقم النسخة قبل كل تحديث): `git checkout --detach <commit>`
5. `docker compose up -d`
(لو غيّرت `POSTGRES_USER` أو `POSTGRES_DB` في `.env` استخدم نفس القيم بدل `inspecta`.) جرّب الخطوات دي مرة على سيرفر تجريبي قبل ما تحتاجها.

## ملاحظات
- **شهادات التجربة (الخطوة 8):** أي شهادة تتصدر على العنوان التجريبي بيفضل الـ QR بتاعها شاير على `api2...` وهي بتفضل في القاعدة الحقيقية بعد التحويل. الأحسن تجرّب بحساب شركة تجريبية وتمسحها، أو تبقى `api2` شغال وشاير على نفس السيرفر.
- **الـ backup بيحتوي بيانات العملاء:** الملفات في `backups/` بصلاحية المستخدم `deploy` بس. لو بتنسخهم برّه السيرفر استخدم remote مشفّر (`rclone crypt`) أو Storage Box خاص بيك.
- **تنبيه فشل الـ backup:** فعّل خدمة مجانية زي healthchecks.io وضيف في آخر `backup.sh` أمر `curl -fsS <رابط الـ ping>` علشان توصلك رسالة لو الـ backup وقف.
