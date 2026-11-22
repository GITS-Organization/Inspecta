# دليل إدارة سيرفر Inspecta (بالعربي، للمبتدئ)

الدليل ده بيشرح السيرفر بالبساطة، وإيه اللي بتعمله كل يوم وكل أسبوع وكل شهر، وإزاي تحل المشاكل المعروفة. كل أمر منسوخ جاهز.

## 1) فكرة السيرفر في 60 ثانية
- السيرفر ده **كمبيوتر Ubuntu (لينكس) مأجّر من Hostinger** على الإنترنت، عنوانه `2.24.150.201`.
- الدومين `api.gitsplatform.com` (على Cloudflare) بيشاور عليه.
- عليه **3 برامج** شغّالة جوه **Docker** (علبة لكل برنامج):
  1. **caddy**: بيستقبل الزوار على HTTPS، ويجيب شهادة الأمان بنفسه، وبيدّي الطلبات للـ API.
  2. **api**: برنامج Inspecta نفسه (.NET).
  3. **db**: قاعدة البيانات PostgreSQL (كل البيانات هنا).
- **ملفاته** في `~/inspecta` (كود المشروع من GitHub)، وإعداداته وأسراره في `~/inspecta/deploy/vps/.env` (ما بيترفعش لـ GitHub أبدًا).
- كل ليلة الساعة 3 صباحًا بياخد **نسخة احتياطية** من القاعدة ويرفعها لـ Backblaze.

## 2) الدخول للسيرفر
من PowerShell على جهازك:
```
ssh deploy@2.24.150.201
```
- بتدخل بالـ **مفتاح SSH** بتاعك (مفيش كلمة سر). المستخدم `deploy` عادي من غير صلاحيات إدارية، وده أمان.
- للخروج: اكتب `exit`.
- أول ما تدخل روح لمجلد التشغيل: `cd ~/inspecta/deploy/vps`
- **المستخدم `root`** (صلاحيات كاملة) بتدخله بس لو لازم: `ssh root@2.24.150.201` (مثلًا لإعادة تشغيل السيرفر).
- لو المفتاح ضاع أو ما بقاش بيدخل: من لوحة Hostinger فيه **Browser terminal** بتدخل بيه بكلمة سر الـ root اللي حفظتها.

## 3) أوامر Docker الأساسية (اتعلمها دي وتكفي)
كلها من `~/inspecta/deploy/vps`:

| عايز تعمل إيه | الأمر |
|---|---|
| تشوف حالة البرامج | `docker compose ps` |
| تشوف لوج الـ API (آخر 100 سطر) | `docker compose logs api --tail 100` |
| تتابع اللوج لايف (Ctrl+C للخروج) | `docker compose logs api -f` |
| لوج قاعدة البيانات أو Caddy | `docker compose logs db --tail 50` / `docker compose logs caddy --tail 50` |
| تعيد تشغيل الـ API بس | `docker compose restart api` |
| تطبّق تغيير في `.env` | `docker compose up -d api` |
| تشوف استهلاك الرام والمعالج | `docker stats --no-stream` |
| تدخل قاعدة البيانات (للقراءة) | `docker compose exec db psql -U inspecta -d inspecta` (اخرج بـ `\q`) |

**قاعدة ذهبية:** ما تشغّلش `docker compose down -v` أبدًا. الـ `-v` بيمسح **قاعدة البيانات كلها**. و`docker compose down` من غير `-v` بيوقف بس.

## 4) الفحص اليومي (دقيقة، تقدر تعمله من الموبايل)
1. افتح `https://api.gitsplatform.com/health` في المتصفح: لازم يكتب **Healthy**.
2. لو عايز أقوى: على السيرفر `docker compose ps` والتلاتة لازم **Up** (والـ api والـ db **healthy**).
3. تأكد إن الـ backup اشتغل بالأمس: `tail -5 ~/backup.log` (لازم تلاقي سطر `Backup written` و`Copied to b2backup...` بتاريخ إمبارح).

اقتراح: اعمل مراقبة مجانية على `https://api.gitsplatform.com/health` من **UptimeRobot** (بتبعتلك إيميل لو وقع).

## 5) الفحص الأسبوعي (5 دقايق)
```
df -h /                      # المساحة: لازم تكون أقل من 70%
free -h                      # الرام: المستخدم أقل بكتير من الإجمالي
docker system df             # حجم Docker
ls -lh ~/inspecta/deploy/vps/backups   # النسخ الاحتياطية
```
- لو الديسك عدّى 70%: امسح كاش البناء القديم `docker builder prune -f` وصور Docker القديمة `docker image prune -f`.

## 6) الفحص الشهري
- **جرّب الاسترجاع:** `cd ~/inspecta/deploy/vps && ./restore-test.sh` (لازم يخلص بـ `the backup can be restored`).
- **تحديثات الأمان:** بتتحمل لوحدها، لكن أحيانًا بتطلب restart: لو شفت `*** System restart required ***` وقت الدخول، أعد تشغيل السيرفر في وقت هادي (`ssh root@2.24.150.201 reboot`)، وبعد دقيقة اتأكد من `/health`.
- **فواتير:** Hostinger (السيرفر) وBackblaze (غالبًا صفر) والدومين.

## 7) تحديث السيرفر بنسخة جديدة من الكود
1. الكود لازم يتعمل له push لـ GitHub (فرع `backend`).
2. على السيرفر:
```
cd ~/inspecta/deploy/vps
./update.sh
```
السكريبت بيعمل: **backup** ثم يسحب آخر كود ثم يبني ويعيد التشغيل ثم يفحص الصحة. لو طلع `API is healthy` يبقى تمام.
- **قبل التحديث:** اعرف لو فيه **migration** جديدة (تغيير في شكل الجداول). بتتطبق تلقائي على بيانات العميل الحقيقية وقت التشغيل.
- **الرجوع لنسخة قديمة من الكود:** `./update.sh <رقم الـ commit>` (الرقم من `git log --oneline`). ده بيرجّع **الكود بس**، مش البيانات.
- **الرجوع للبيانات:** من الـ backup (القسم 9).

## 8) تغيير إعدادات التطبيق (`.env`)
```
cd ~/inspecta/deploy/vps
nano .env
```
- اتحرك بالأسهم، عدّل القيمة بعد `=` (من غير مسافات)، **Ctrl+O** ثم Enter للحفظ، **Ctrl+X** للخروج.
- بعدها `docker compose up -d api` علشان التغيير يتطبق.
- **ما تلصقش أسرار في أي شات.** اكتبها هنا بنفسك.
- إعدادات مهمة: `CORS_ALLOWED_ORIGINS` (عناوين الفرونت المسموحة)، `MICROSOFT_OAUTH_*` (ربط Outlook)، `QUOTE_DELIVERY_ENABLED` و`CERTIFICATE_DELIVERY_ENABLED` (إرسال الإيميل)، `RCLONE_REMOTE` (النسخة الخارجية).
- **`IMAP_ENCRYPTION_KEY` و`POSTGRES_PASSWORD`:** احتفظ بنسخة منهم في مدير كلمات السر. لو `IMAP_ENCRYPTION_KEY` ضاع، كلمات سر الصناديق المحفوظة مش هتتفك.

## 9) النسخ الاحتياطي والاسترجاع
- **نسخة يدوية دلوقتي:** `./backup.sh`
- **الملفات:** في `~/inspecta/deploy/vps/backups` (آخر 14 يوم) وفي Backblaze (`b2backup:inspecta-backups-02134/inspecta`).
- **تشوف اللي على Backblaze:** `rclone ls b2backup:inspecta-backups-02134/inspecta`
- **استرجاع كامل (لو حصلت كارثة في البيانات):**
  1. `docker compose stop api caddy`
  2. `docker compose exec -T db dropdb -U inspecta inspecta && docker compose exec -T db createdb -U inspecta inspecta`
  3. `docker compose exec -T db pg_restore -U inspecta -d inspecta --no-owner < backups/<الملف>.dump`
  4. (لو لسه عايز نسخة من Backblaze) `rclone copy b2backup:inspecta-backups-02134/inspecta/<الملف> backups/` الأول.
  5. `docker compose up -d`
- **قبل أي حاجة خطيرة** اعمل `./backup.sh` الأول.

## 10) حل المشاكل المعروفة
| العَرَض | افحص إيه |
|---|---|
| الموقع/الـ API مش بيفتح | `docker compose ps` (هل البرامج Up؟) ثم `docker compose logs api --tail 80` |
| `/health` رجّع 502 أو 503 | الـ api واقع: `docker compose restart api` وشوف اللوج |
| Caddy مش بيجيب شهادة | الـ DNS لازم يشاور على `2.24.150.201` و**DNS only** في Cloudflare، والبورتات 80 و443 مفتوحة (`sudo ufw status`) |
| الفرونت بيقول CORS/Network error | عنوان الفرونت لازم يكون في `CORS_ALLOWED_ORIGINS` في `.env` |
| ربط Outlook فشل | `MICROSOFT_OAUTH_*` الثلاثة في `.env`، والـ Redirect URI في Microsoft Entra نفس `https://api.gitsplatform.com/api/v1/email-connection/microsoft/callback`، وIMAP وSMTP Auth مفعّلين للصندوق |
| الديسك ممتلئ | `df -h /` ثم `docker system df` ثم `docker builder prune -f` و`docker image prune -f` |
| السيرفر بطيء | `docker stats --no-stream` و`free -h` (رام أو معالج ممتلئ؟) |
| نسيت إن كان الـ backup اشتغل | `tail -20 ~/backup.log` |
| مش قادر تدخل SSH | جرّب Browser terminal من لوحة Hostinger، وتأكد إن المفتاح الصح (`ssh -i` لو عندك أكتر من مفتاح) |

**لو حصل شيء غريب وإنت مش فاهمه:** خد لقطة من اللوج (`docker compose logs api --tail 100`) من غير أي كلمات سر، وابعتها للمساعد.

## 11) الأمان (بسيط ومهم)
- **مفاتيح SSH:** مفتاحك الخاص (`id_ed25519` من غير `.pub`) سر، ما تبعتوش لأي حد.
- **الدخول بكلمة سر مقفول** على السيرفر (مفاتيح بس). الـ firewall بيفتح بس 22 (SSH) و80 و443.
- **أي سر اتبعت في شات أو إيميل** (مفتاح Backblaze، Microsoft secret، باسورد أدمن): غيّره. التغيير:
  - Microsoft: Entra > التطبيق > Certificates & secrets > New client secret، ثم احذف القديم، وحط الجديد في `.env` بـ `nano`.
  - Backblaze: Application Keys > احذف القديم وأنشئ جديد، ثم `./setup-offsite.sh`.
- **باسورد أدمن المنصة:** غيّره دوري من `POST /api/v1/auth/change-my-password`.
- ما تفتحش بورت قاعدة البيانات (5432) للإنترنت أبدًا. هي أصلًا على شبكة داخلية.

## 12) مصطلحات
- **VPS:** كمبيوتر افتراضي مؤجّر. **SSH:** طريقة الدخول الآمنة بالأوامر. **Docker/compose:** بيشغّل كل برنامج في علبة مستقلة ويديرهم مع بعض.
- **Reverse proxy (Caddy):** البوابة اللي بتستقبل الزوار وتوصلهم للـ API. **HTTPS/شهادة:** التشفير بين المتصفح والسيرفر.
- **Migration:** تغيير في شكل جداول القاعدة بيتطبق مع التحديث. **Backup/restore:** نسخة احتياطية / استرجاعها.
- **DNS:** دفتر العناوين اللي بيقول للدومين يروح فين. **CORS:** إذن للفرونت إنه يكلّم الـ API من عنوانه.
- **Health check:** رابط `/health` بيقول إن التطبيق سليم.

## 13) مين أكلّم لو احتجت مساعدة
- **Hostinger:** دعم من لوحتهم (السيرفر نفسه، الشبكة، الفواتير).
- **Cloudflare:** DNS الدومين.
- **GitHub:** مفتاح الـ Deploy key للسيرفر (Settings > Deploy keys في الـ repo).
- **المساعد (Claude):** لأي مشكلة في التطبيق أو الأوامر، وأنا ممكن أنفّذ الأوامر على السيرفر لو طلبت.
