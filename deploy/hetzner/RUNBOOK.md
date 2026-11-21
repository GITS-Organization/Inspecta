# نقل Inspecta من Render إلى سيرفر Hetzner (خطوة بخطوة)

الهدف: سيرفر واحد فيه **Caddy** (HTTPS) و**API** و**PostgreSQL** جديدة وفاضية لأول عميل حقيقي، مع backup يومي وتجربة استرجاع.
مفيش بيانات تتنقل من Render، فمفيش downtime: نجهّز السيرفر الجديد على عنوان تجريبي، نجرّبه، وبعدين نحوّل الدومين.

## 0) القرارات اللي اتاخدت
- **المزوّد: Hetzner Cloud** (أرخص وأحسن من Hostinger VPS في الدعم والشبكة والـ API). مفيش عندهم موقع في مصر أو السعودية.
- **الموقع: Falkenstein أو Nuremberg (ألمانيا)**: نفس مسافة Frankfurt الحالية على Render، يعني نفس سرعة الوصول من مصر والسعودية.
- **المواصفات: CX32 أو CPX21** (4 vCPU تقريبًا، 4 إلى 8GB RAM)، **Ubuntu 24.04**. السعر حوالي 8 إلى 16 يورو في الشهر (راجع صفحة الأسعار).
- لو عميل سعودي طلب استضافة **داخل المملكة**: نعمل نسخة منفصلة بنفس هذه الملفات عند مزوّد سعودي. لا تتغير الخطوات.

## 1) إنت تعمل (حوالي ساعة)
1. اعمل حساب Hetzner، وأنشئ **Project** جديد.
2. أضف **SSH key**: من جهازك `ssh-keygen -t ed25519` ثم انسخ محتوى الملف `.pub` في Hetzner > Security > SSH keys.
3. أنشئ **Server**: Falkenstein، Ubuntu 24.04، النوع CX32 أو CPX21، اختر الـ SSH key، وفعّل خيار **Backups** (يزوّد 20% من السعر، لقطة كاملة للسيرفر فوق الـ backup بتاعنا).
4. من Hetzner > Firewalls: اسمح بـ 22 و80 و443 فقط (اختياري، السكريبت بيعمل firewall على السيرفر كمان).
5. **الدومين:** ضيف سجل `A` لعنوان تجريبي، مثلًا `api2.gitsplatform.com`، يشاور على IP السيرفر. (الدومين الأساسي `api.gitsplatform.com` بيتحوّل في الخطوة 9.)

## 2) تجهيز السيرفر (مرة واحدة)
من جهازك (من مجلد المشروع) انسخ السكريبت للسيرفر ثم شغّله:
```
scp deploy/hetzner/server-setup.sh root@<IP>:/root/
ssh root@<IP> "bash /root/server-setup.sh"
```
السكريبت: يحدّث النظام، يثبّت Docker، يعمل مستخدم `deploy`، يقفل الدخول بكلمة سر (مفاتيح فقط)، يشغّل firewall و fail2ban وتحديثات الأمان التلقائية، ويضبط التوقيت على القاهرة.
بعدها ادخل بالمستخدم الجديد: `ssh deploy@<IP>`

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
cd deploy/hetzner
```

## 4) ملف الإعدادات `.env`
```
cp .env.example .env
./gen-secrets.sh          # بيطبع قيم جديدة للأسرار: انسخها بإيدك لملف .env
nano .env                 # املا API_DOMAIN (api2...) و ACME_EMAIL و BOOTSTRAP_ADMIN_* والباقي
chmod 600 .env
```
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
3. **شيل** `BOOTSTRAP_ADMIN_PASSWORD` من `.env` بعد أول دخول، ثم `docker compose up -d`.

## 7) الـ backup (قبل أي شهادة حقيقية)
```
./backup.sh && ls -lh backups/
./restore-test.sh                 # لازم يخلص بـ "the backup can be restored"
crontab -e                        # ضيف السطر ده:
0 3 * * * /home/deploy/inspecta/deploy/hetzner/backup.sh >> /home/deploy/backup.log 2>&1
```
**نسخة خارج السيرفر:** اعمل Hetzner **Storage Box** (حوالي 4 يورو)، اضبطه بـ `rclone config` (اسم الـ remote مثلًا `storagebox`) وحط الاسم في `RCLONE_REMOTE` في `.env`. من غير ده النسخ بتفضل على نفس السيرفر.
جرّب الاسترجاع مرة كل شهر.

## 8) تجربة قبل التحويل (على العنوان التجريبي)
- تسجيل دخول لكل دور (admin، technical manager، coordinator، inspector).
- طلب جديد ← عرض سعر ← قبول ← تعيين inspector ← فحص ← شهادة ← اعتماد.
- QR الشهادة بيفتح صفحة التحقق، وتنزيل الـ PDF شغال.
- رفع report من الـ inspector وظهوره عند المراجعة.
- ربط صندوق Outlook وقراءة إيميل جديد. **الإيميل الخارج:** Hetzner بتقفل بورت 25 (وأحيانًا 465) على الحسابات الجديدة. إحنا بنستخدم 587 مع Outlook، فالمفروض يشتغل. لو أي إرسال فشل بـ "mail server did not answer" افتح تذكرة لـ Hetzner لفتح البورت.
- بعد ما الإرسال التجريبي ينجح بس: `QUOTE_DELIVERY_ENABLED=true` و `CERTIFICATE_DELIVERY_ENABLED=true` في `.env` ثم `docker compose up -d`.

## 9) التحويل (دقايق)
1. قلّل الـ TTL لسجلات الدومين قبلها بيوم (300 ثانية).
2. غيّر سجل `A` لـ `api.gitsplatform.com` ليشاور على IP السيرفر الجديد، وغيّر `API_DOMAIN` في `.env` له، ثم `docker compose up -d`.
3. حدّث العنوان في: الفرونت، تطبيق الـ Flutter، الـ admin console، و**Redirect URI في تطبيق Microsoft** (`https://api.gitsplatform.com/api/v1/email-connection/microsoft/callback`) وفي `MICROSOFT_OAUTH_REDIRECT_URI`.
4. سيب Render شغال أسبوع كـ fallback ثم الغيه. (بما إن مفيش بيانات حقيقية عليه، مفيش داعي لنقل داتا.)

## التحديث بعد كده
```
cd ~/inspecta/deploy/hetzner && ./update.sh          # backup ثم آخر backend ثم build ثم health check
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
