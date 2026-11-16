# Luna-Quest — Changelog

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

The GitHub Release attaches the Godot .NET Windows/Linux archives, the GDScript Windows/Linux
archives, the raw Linux binary, a Debian package built by `tools/package-desktop.sh`,
`SHA256SUMS` covering every attached file, and the unchanged intro film MP4.

### Verification

GDScript self-test 659 checks, scene audit 238, .NET rover test 81 and LunaQuest.Tests 426 —
each count comes from a fresh local run. Unity: harness extended (board + save/restore),
compile-clean by review only; compile/runtime PENDING — build locally in Unity 6000.3.0f1.
CI runs the parity and immutable-asset guards on every
push, builds the C# edition on every push and PR, and can recapture the README frames from the
real game with `tools/capture-screenshots.sh`.

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

يُرفِق إصدار GitHub أرشيفات Godot .NET لـ Windows/Linux، وأرشيفات GDScript لـ Windows/Linux،
وثنائية Linux الخام، وحزمة Debian التي يبنيها `tools/package-desktop.sh`، و`SHA256SUMS` لكل
ملف مرفق، وفيلم الافتتاحية MP4 دون تغيير.

### التحقق

فحص GDScript الذاتي 659 وفحص المشهد 238 واختبار المركبة في .NET 81 وLunaQuest.Tests 426 —
كل رقم من تشغيل محلي حديث. أما Unity: فموجّه الاختبار موسّع (اللوحة + الحفظ والاستعادة)،
وسليم بالتدقيق فقط؛ التجميع والتشغيل قيد الانتظار — ابنِه محلياً في Unity 6000.3.0f1.
يشغّل CI حرّاس التكافؤ والأصول المقفلة عند كل دفع، ويبني
إصدار C# عند كل دفع وطلب دمج، ويمكنه إعادة التقاط لقطات README من اللعبة الحقيقية عبر
`tools/capture-screenshots.sh`.
