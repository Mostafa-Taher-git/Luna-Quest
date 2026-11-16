# Luna-Quest — Changelog

## v3.5 — the owner playtested it; all ten defects fixed

**Tag:** `v3.5` · **Previous release:** `v3.4` · **Date:** 17 November 2026
**Shipping desktop builds:** Godot 4.7.2 .NET · Godot 4.7.2 GDScript.
**Unity 6000.3.0f1 (6.3 LTS):** source only — no Unity binary ships. Build it locally. Its
playtest harness was extended (EVA jobs, suit scale, settings roundtrip); compile-clean by review
only, with compile/runtime verification PENDING.

The owner played the built game and filed ten defects. All ten are fixed, each with before/after
captures in `docs/screenshots/playtest-v3.5/` and with regression LOCK tests in every engine that
runs (GDScript audit, .NET RoverAudit, Unity RoverPlayTest, and the shared LunaQuest.Tests core
harness). One change rides on top of the fixes: a boot race between `RobotCompanion._Process` and
`_Ready`, found while verifying the F10 fixes, is fixed with a plain null guard.

### The universal control contract (F0)
`W`/`Up` drives the rover forward toward the camera, `S`/`Down` reverses, `A`/`D` turn toward
screen-left and screen-right — measured against the camera basis, not a hardcoded world axis.
Walking was already correct in all three engines and is now LOCKED by regression tests (same four
assertions for walking and driving in all three engines), including walking after any driving
episode ends. The camera is synced to the vehicle heading every frame while driving, so the view
is never stale behind a turning rover.

### A real Earth and a real sky (F1, F9)
Earth is NASA's Blue Marble (public domain), sun-lit by the same colony sun — day side lit, night
side dark, thin blue atmospheric limb, about 2 degrees across, fixed over the ridge with slow
libration. Stars are points of light with a magnitude distribution (many faint, few bright), slight
colour variation, no twinkle, a faint milky way band, pure black background, and they dim during
bright lunar day.

### The kit colony ships (F2)
Every walkable module is wrapped in its matching CC0 "Moon Base Colony and Lunar Rovers" kit shell
(rigid cylinders, inflatable dome, extraction plant, workshop, greenhouse, medical), scaled to the
content footprint. Plain box wall meshes are hidden; all colliders (doorways, console reach, blockers)
are untouched and verified by raycast in the audit. Gold-foil porches with MLI collar + hazard sill
at every door, rib rings, radiator panels and cable trays; interiors are furnished per building
purpose (bunks, grow racks, workbenches, sample benches, medical bed, crates, console desks, rover
bay). Unity shells are primitive (no glTFast without an editor).

### Crew at exactly 1.75 m, walking not statues (F3)
The suit now scales off one shared constant (the 2.311 m visor-to-sole pose height) in all three
engines — the old local-only AABB (bind-pose ~6 m across) is gone. A real GLB pose is asserted at
flight time. The GDScript crew play the GLB's idle clip and a walk cycle; crew steer around building
footprints instead of walking through walls; their y is re-set from the terrain heightfield each
frame. .NET crew use the baked navigation mesh. The trim band rides the scaled body.

### Only the selected language is visible (F4)
World signs, console labels and crew name labels store both languages and render exactly the one
selected. Arabic- and English-active assertions walk every label in both directions (the audit
proves both ways).

### Settings, and third person that works (F5)
Camera view (First/Third), Language (EN/AR), Mouse sensitivity, Field of view, Master volume,
Voice, Subtitles, Quality preset, Fullscreen — persisted to `user://settings.json` (Unity:
`Application.persistentDataPath/settings.json`), applied on launch and on every change, and the
GDScript self-test, the Godot .NET RoverAudit and the Unity playtest all roundtrip the file exactly.
Third person: over-the-shoulder boom with collision (ray-clamped; SpringArm3D in .NET); the suit at
1.75 m finally shows up properly.

### Doing things, not reading panels (F6)
Hold-E progress actions for physical jobs (brush the array, sweep the airlock, back-flush the
filter, replace the scrubber) with a live progress readout; releasing early cancels cleanly.
EVA suit energy drains outside and recharges indoors; the drill refuses on an empty suit and the
reserve writes a journal entry naming the real reason. Drill at the ice site, carry the sample,
seal the chain of custody at the cache — `obj_sample` only completes through the sealed chain, with
the real numbers in the log. The pad crate run: pick up at the landing pad, deliver to the workshop
spares desk before the board window closes — +15 data on time, +8 late, told honestly. The 14-row
board is unchanged (H9); the new work appears as journal sub-steps with the same toast + confirm
bark feedback.

### The rover communicates before the instruments do (F7)
A 55 Hz two-stroke hum generated at runtime (no downloads), pitched to speed, positional; regolith
puffs kicked from the rear wheels when moving; the body rolls and pitches on the four contact
patches; the speed/battery prompt is already in place on all three engines.

### Real ground, near-zero ambient, feel at the feet (F8)
The scanned CC0 Moon Dusted 03 albedo tiles the ground every ~3.5 m at full strength (white scan,
no double-darkening). Lunar ambient is a very low warm regolith bounce with a faint cool earthshine
term, low enough that shadow sides read dark. Footstep dust, landing thump above 2.4 m/s, a slight
slide above 30 degrees of slope. New: a visible traverse warning before the 118 m rim (9 s cooldown,
then the rim holds the line).

### NEXO ships the Quaternius robot (F10)
The CC0 Animated LowPoly Robot (Poly Pizza QCm7qe9uNJ) replaces the sphere placeholder for the two
Godot editions: measured, scaled to 1.05 m, played as a rig with its own idle clip. The procedural
fallback is kept and is itself a full v2 redesign (panel seams, curved dark visor, cyan waist ring,
glowing eyes, antenna with orange tip, solar fins, hover skirt, thruster bell). Everywhere: 6 Hz
hover bob, polite 3.4 m follow, faces the player, objective pointer pointing at the first unfinished
objective, eyes turn orange and pulse during incidents, happy chirp on task completion.

### Verification
GDScript self-test 691 checks (blanket: 659 at v3.4), audit 468; Godot .NET ROVER TEST 175, core
harness 445 (up from 426; +19 checks for EVA/custody/cargo); Unity: Roslyn comparison vs control
shows no new diagnostic kinds; compile/runtime PENDING. Guards: content 16/7/15/11/17/44, PARITY
OK, immutable 8 locked / 12 hash checks. Before/after captures live in `docs/screenshots/playtest-v3.5/`.

### Known limitations
The one-click `LunaQuest-Windows.exe` is still untested on a real Windows machine. Unity binary
DOES NOT SHIP — source only — and its compile/runtime verification is PENDING outside this pass.
NEXO's GLB fits are computed at load time from the bind-pose AABB; the `--import` log shows no rig
warnings. CI builds the C# edition and runs the parity and immutable guards on every push/PR.

---

## v3.4 — three engines, one Moon, no browser build

**Tag:** `v3.4` · **Previous release:** `v3.3` · **Date:** 16 November 2026
**Shipping desktop builds:** Godot 4.7.2 .NET · Godot 4.7.2 GDScript.
**Unity 6000.3.0f1 (6.3 LTS):** source only — no Unity binary ships. Build it locally. Its
playtest harness was extended (board + save/restore); compile-clean by review only, with
compile/runtime verification PENDING.

### One Moon in every engine

The GDScript edition now draws the shared `LunarTerrain` heightfield (`scripts/terrain.gd`, an
exact port of `LunarTerrain.cs`) instead of its own flat noise field, and its ground collider is
baked from the drawn mesh, so the commander walks on the real surface rather than a slab under
it. Unity's ground is generated from the same heightfield with a `MeshCollider` instead of a flat
1600 m plane. A new guard, `tools/check-engine-parity.py`, proves one simulation, one content set
and one Moon by comparing terrain values point by point.

### A driveable crew rover in all three engines

Enter with `E`, drive with `W`/`S`, steer with `A`/`D`, boost with `Shift`, hold `Space` for the
handbrake, and press `E` again to step out. The rover drains its battery as it drives, recharges
on the garage apron, counts an odometer and latches the ice-site milestone. Its state saves and
restores through the schema-2 extras keys, and an old save written before the rover existed loads
without complaint. Steering was corrected so `A` turns left in every engine; the suit is parked
while seated and set down on the ground beside the rover on exit. The .NET edition gains
`RoverAudit` and Unity gains `RoverPlayTest`, both runnable headlessly.

### The shift task board

`hud.gd`, `Hud.cs` and `ColonyHud.cs` draw a 14-row board: the 11 objectives from
`content/learning.json` (done = a journal entry), the rover's two milestones and the mission
line. The row for the current shift leads, the header names that shift, a finished row gets a
check and the calm teal colour, and the whole board speaks English and Arabic. `learning.json`
itself stays at 11 objectives.

### No browser build anywhere (H1/A1)

v3.4 ships three engines — Godot .NET, Godot GDScript and Unity — and nothing that runs in a
browser. The GDScript `Web` export preset is deleted, `pages.yml` and the CI web job are gone,
and the release workflow builds, attaches and publishes desktop artifacts only. The retired
Three.js edition and its locked intro poster now live under `archive/`.

### Real lunar assets

The Poly Haven `moon_02` regolith PBR set (CC0, 2K: albedo, AO, height, normal, roughness) joins
the scanned regolith, rocks and kit models already in the tree. The intro film and the Cute
Astronaut are pinned by SHA-256 in `docs/design/asset-lock.json`, guarded by
`tools/check-immutable-assets.sh` (8 locked files, 12 hash checks).

### Release artifacts

The GitHub Release attaches the one-click **GDScript build** — `LunaQuest-Windows.exe`, the raw
Linux binary `LunaQuest-Linux-x86_64`, and the Debian package `LunaQuest-Linux-amd64.deb`
(built by `tools/package-desktop.sh` from the GDScript project; no .NET runtime needed), plus
the Godot .NET Windows/Linux archives (the `.NET` builds) and the GDScript Windows/Linux
archives. `SHA256SUMS` covers every attached file, and the intro film MP4 is attached unchanged.

### Verification

GDScript self-test 659 checks, scene audit 238, .NET rover test 81 and LunaQuest.Tests 426 —
each count comes from a fresh local run. Unity: harness extended (board + save/restore),
compile-clean by review only; compile/runtime PENDING — build locally in Unity 6000.3.0f1.

### Known limitations

The one-click `LunaQuest-Windows.exe` is verified to be a single self-contained PE32+ x86-64
binary produced by the GDScript export, but it has NOT been launched on a real Windows machine
yet — please report any launch problem. Unity ships as SOURCE ONLY; its harness is extended but
checked by review only, with compile/runtime PENDING. Build it locally in Unity 6000.3.0f1.
CI runs the parity and immutable-asset guards on every
push, builds the C# edition on every push and PR, and can recapture the README frames from the
real game with `tools/capture-screenshots.sh`.

---

## ٣٫٥ — لعبها المالك، وأصلحنا العيوب العشرة كلها

**الوسم:** `v3.5` · **الإصدار السابق:** `v3.4` · **التاريخ:** ١٧ نوفمبر ٢٠٢٦
**البنيات المكتبية المُصدَّرة:** Godot 4.7.2 .NET · Godot 4.7.2 GDScript.
**Unity 6000.3.0f1 (6.3 LTS):** مصدر فقط — ابنِها محلياً؛ سليمة بالتدقيق فقط، والتجميع والتشغيل
قيد الانتظار.

أصلح الإصدار عيوب المالك العشرة، ومعها سباق تشغيل واحد اكُتشف أثناء التحقق (`_Process` قبل
`_Ready` في RobotCompanion — كان يكسر اسم/نشاط نيكسو منذ v3.4). كل إصلاح مصحوب بلقطات قبل/بعد
في `docs/screenshots/playtest-v3.5/` واختبارات قفل تراجعي في كل محرك.

### عقد التحكم العالمي (F0)
`W`/`Up` يقود المركبة للأمام باتجاه الكاميرا، `S`/`Down` للخلف، `A`/`D` للانعطاف نحو يسار/يمين
الشاشة — مقاساً مقابل قاعدة الكاميرا لا محور محور عالم. المشي كان صحيحاً وثُبِّت بقفل تراجعي.

### أرض السماء الحقيقية (F1، F9)
الأرض كوكب Blue Marble التابع لـ NASA (ملكية عامة)، مضاءة بنفس شمس المستعمرة: جانب نهاري مضىء،
جانب ليلي معتم، طرف أزرق رقيق، بحجم درجتين تقريباً فوق الحافة، ثابتة تقريباً. النجوم نقاط ضوئية
بتوزيع قدري (وفيرة خفيفة وقليلة ساطعة)، بلا وميض، مع حزام درب تبانة خفيف، سماء سوداء، وتخفت
عند النهار القمري الساطع.

### الاستعانة بالـ CC0 الحقيقي (F2)
كل وحدة قابلة للمشي ملتفة بالوحدة المناظرة من حزمة CC0 «Moon Base Colony and Lunar Rovers».
الكتل الصندوقية المسطحة مخفية والجدران/الأبواب الاصطناعية كما هي (التدقيق بالأشعة يؤكدها).
شرفات ملكية من ذهب foil في كل باب، حلقات أضلاع، لوحات مشعة، أوصال، وديكورات داخلية بحسب غرض كل
وحدة (أسرة، أرفف نمو، منضدة عمل، منضدة عينات، سرير طبي، صناديق، طاولات تحكم، مرآب مركبة).
Unity يستخدم هندسة بدائية (لا يوجد glTFast بدون محرر).

### البدلات بحد 1٫75 م (F3)
القياس من ثابت واحد مشترك (2٫311 م من المزلاج إلى القاع) في المحركات الثلاثة. طاقم GDScript
يشغّل لقطات الـ GLB وتدور الحركة، والتنقل بالالتفاف حول البصمات (لا عبر الجدران) مع تتبع ارتفاع
التضاريس كل إطار.

### اللغة المختارة فقط هي الظاهرة (F4)
اللافتات والكونسول وأسماء الطاقم تخزن اللغتين وتعرض المُختارة منهما فقط — تدقيق مزدوج الاتجاهين.

### إعدادات، وكاميرا شخص ثالث تعمل (F5)
عرض الكاميرا، اللغة، الحساسية، مجال الرؤية، الصوت، الكلام، الترجمة، الجودة، ملء الشاشة — تُحفظ
في `user://settings.json` (Unity: `Application.persistentDataPath/settings.json`)، تطبَّق عند
الإقلاع وعند كل تغيير، مع اختبار دورة حفظ في الخلاصات الثلاث. كاميرا شخص ثالث بذراع كتف مع
تصادم (شعاع في GDScript، SpringArm3D في .NET).

### العمل بدل القراءة (F6)
أفعال E-ممدودة للوظائف الفيزيائية مع عدّاد تقدّم؛ إلغاء نظيف عند الإفلات المبكر. طاقة بدلة
EVA تستنزف في الخارج وتعاد تعبئتها في الداخل؛ المثقاب يرفض على بدلة فارغة، مع قيد في اليومية.
احفر في موقع الجليد، احمل العينة، اختم سلسلة الحيازة — `obj_sample` لا يكتمل إلا بسلسلة مختومة
والأرقام الحقيقية في السجل. مهمة الشحنة: خذ الصندوق من منصة الهبوط وسلّمه إلى مكتب قطع الورشة
قبل إغلاق نافذة اللوحة (+15 في الموعد، +8 متأخراً، بكل أمانة). اللوحة ذات الـ14 صفاً كما هي
(H9)؛ تظهر الأعمال الجديدة كخطوات فرعية في اليومية مع توست وصوت تأكيد.

### المركبة تتصل قبل الأجهزة (F7)
هدير بمحرك 55Hz مولّد برمجياً (لا ملفات تنزيل)، يتابع السرعة بالتدرج، مساحيق من العجلات
الخلفية أثناء الحركة، جسم يتدحرج وينقلب على الملامسات الأربع.

### أرض حقيقية، محيط شبه معدوم، إحساس عند القدمين (F8)
الماسح CCC Moon Dusted 03 يتكرر كل ~3٫5 م بكامل قوّته. المحيط القمري قسط دافئ منخفض من ارتداد
الريغوليث مع توهج أرض-شحنة خفيف ورائق. غبار خطوات، ارتجاج هبوط فوق 2٫4 م/ث، انزلاق خفيف فوق
30 درجة. جديد: تحذير تخطيط قبل حافة 118 م، ثم الحافة تصد زوجته (تحذير كل 9 ثوانٍ).

### نيكسو يرفع جسم Quaternius (F10)
CC0 Animated LowPoly Robot (Poly Pizza QCm7qe9uNJ) محل المزخرف الكروي للمحركات. مقاومة أدنى
حجماً، تُرخّص وتُقفل حسب الـ rig بـ 1٫05 م، مع مقبض تحذير/هدف/ضحكة على اكتمال المهمة. النسخة
الاحتياطية الإجرائية محسّنة بدورها (درزات، قناع معتم، حلقة سماوية، عيون متوهجة، هوائي بطرف
برتقالي، زعانف شمسية، تنورة هبوط، جرس دفع). لكل شيء: خفة 1٫6Hz، متابعة مهذبة 3٫4 م، يتطلع
للهدف الحالي، برتقالي ونبض في التنبيهات، تويتر مرح عند إنجاز مهمة.

### التحقق
GDScript: 691 فحص ذاتي، 468 تدقيقاً؛ .NET ROVER TEST 175؛ لب
`LunaQuest.Tests` 445 (كانت 426؛ +19 للوظائف الجديدة)؛ Unity: مقارنة Roslyn مقابل عيّنة
الضبط تُظهر لا أنواعاً تشخيصية جديدة؛ التجميع/التشغيل قيد الانتظار. الحراسات: محتوى
16/7/15/11/17/44، PARITY OK، 8 ملفات مقفلة / 12 فحصاً.

### القيود المعروفة
`LunaQuest-Windows.exe` واحد بنقرة لم يُختبر على ويندوز حقيقي بعد. Unity تُباع كمصدر فقط —
لا ثنائية — والتجميع/التشغيل قيد الانتظار. تحذيرات الـ rig من GRS-1 لا تظهر في السجلات. CI
تبني C# وتشغل الحراسات عند كل push/PR.

---

## ٣٫٤ — ثلاثة محركات، قمر واحد، بلا بناء للمتصفح

**الوسم:** `v3.4` · **الإصدار السابق:** `v3.3` · **التاريخ:** ١٦ نوفمبر ٢٠٢٦
**البنيات المكتبية المُصدَّرة:** Godot 4.7.2 .NET · Godot 4.7.2 GDScript.
**Unity 6000.3.0f1 (6.3 LTS):** مصدر فقط — لا تُصدَّر ثنائية Unity؛ ابنِها محلياً. موجّه الاختبار
موسّع (اللوحة + الحفظ والاستعادة)، وسليم بالتدقيق فقط؛ التجميع والتشغيل قيد الانتظار.

### قمر واحد في كل المحركات

أصبح إصدار GDScript يرسم حقل الارتفاعات المشترك `LunarTerrain` (عبر `scripts/terrain.gd`، نسخة
مطابقة من `LunarTerrain.cs`) بدلاً من حقل ضجيج مسطّح خاص به، ويُبنى سطح التصادم من الشبكة
المرسومة نفسها، فيسير القائد على السطح الحقيقي. أما Unity فيولّد أرضه من الحقل نفسه مع
`MeshCollider` بدلاً من مستوى مسطّح بطول 1600 متر. ويُثبت الحارس الجديد
`tools/check-engine-parity.py` أن هناك محاكاة واحدة ومجموعة محتوى واحدة وقمراً واحداً.

### مركبة طاقم قابلة للقيادة في المحركات الثلاثة

ادخل بـ `E`، وقد بـ `W`/`S`، وانعطف بـ `A`/`D`، وعزّز بـ `Shift`، واضغط باستمرار
على `Space` للفرامل، ثم اضغط `E` مجدداً للخروج. تستهلك المركبة بطاريتها أثناء القيادة،
وتتشحن على أرضية المرآب، وتعدّ المسافة المقطوعة، وتُسجّل إنجاز موقع الجليد. تُحفظ حالتها
وتُستعاد عبر مفاتيح `extras` في مخطط الحفظ 2، ويُحمَّل أي حفظ قديم كُتب قبل وجود المركبة دون
مشكلة. صُحّح اتجاه الانعطاف بحيث يدير `A` العجلة يساراً في كل المحركات، وتتوقف بدلة الرائد
أثناء الجلوس، وتُوضع على الأرض بجانب المركبة عند الخروج. يحتوي إصدار .NET على `RoverAudit`
ويحتوي Unity على `RoverPlayTest`، وكلاهما يعمل بدون واجهة.

### لوحة مهام الوردية

ترسم `hud.gd` و`Hud.cs` و`ColonyHud.cs` لوحة من 14 صفاً: أهداف `content/learning.json`
الإحدى عشرة (الإنجاز = دخول في اليومية)، وإنجازا المركبة، وسطر المهمة. يتقدّم صف الوردية
الحالية، ويسمّي الترويسة تلك الوردية، ويأخذ الصف المنجز علامة صح ولونً أخضر هادئاً، وتتحدث
اللوحة بالإنجليزية والعربية. يبقى `learning.json` نفسه عند 11 هدفاً.

### لا بناء للمتصفح إطلاقاً (H1/A1)

يصدر v3.4 ثلاثة محركات فقط — Godot .NET وGodot GDScript وUnity — ولا شيء يعمل داخل متصفح.
حُذف إعداد تصدير `Web` في GDScript، واختفى `pages.yml` ومهمة المتصفح في CI، وتبني مهمة
الإصدار القطع المكتبية فقط وتُرفِقها وتُنشِرها. ويعيش إصدار Three.js المتقاعد وملصق الفيلم
المقفل تحت `archive/`.

### أصول قمرية حقيقية

تنضم مجموعة `moon_02` من Poly Haven (CC0، 2K: اللون، الإحاطة، الارتفاع، الاتجاه، الخشونة)
إلى الصخور والتراب المسحوب ونماذج المجموعة الموجودة في الشجرة. الفيلم الافتتاحي ونموذج
Cute Astronaut مثبّتان بـ SHA-256 في `docs/design/asset-lock.json`، يحرسهما
`tools/check-immutable-assets.sh` (8 ملفات مقفلة، 12 فحصاً للهاش).

### قطع الإصدار

يُرفِق إصدار GitHub بناء **GDScript** بنقرة واحدة — `LunaQuest-Windows.exe` والثنائية الخام
`LunaQuest-Linux-x86_64` وحزمة Debian `LunaQuest-Linux-amd64.deb` (تُبنى من مشروع GDScript عبر
`tools/package-desktop.sh`؛ ولا تحتاج إلى .NET)، إضافةً إلى أرشيفات Godot .NET لـ Windows/Linux
(بنيات `.NET`) وأرشيفات GDScript لـ Windows/Linux. ويغطي `SHA256SUMS` كل ملف مرفق، وفيلم
الافتتاحية MP4 مرفق دون تغيير.

### التحقق

فحص GDScript الذاتي 659 وفحص المشهد 238 واختبار المركبة في .NET 81 وLunaQuest.Tests 426 —
كل رقم من تشغيل محلي حديث. أما Unity: فموجّه الاختبار موسّع (اللوحة + الحفظ والاستعادة)،
وسليم بالتدقيق فقط؛ التجميع والتشغيل قيد الانتظار — ابنِه محلياً في Unity 6000.3.0f1.

### قيود معروفة

ملف `LunaQuest-Windows.exe` بنقرة واحدة مُتحقَّق منه أنه ثنائي PE32+ واحد ذاتي الاحتواء يُنتِجه
تصدير GDScript، لكنه لم يُشغَّل على جهاز Windows حقيقي بعد — يرجى الإبلاغ عن أي مشكلة تشغيل.
Unity مصدر فقط؛ موجّهه موسّع وخُضع لمراجعة فقط؛ التجميع والتشغيل قيد الانتظار. ابنِه محليًا في
Unity 6000.3.0f1.
يشغّل CI حرّاس التكافؤ والأصول المقفلة عند كل دفع، ويبني
إصدار C# عند كل دفع وطلب دمج، ويمكنه إعادة التقاط لقطات README من اللعبة الحقيقية عبر
`tools/capture-screenshots.sh`.
