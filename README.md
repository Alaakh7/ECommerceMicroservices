# ECommerce Microservices

منصة تجارة إلكترونية مبنية بأسلوب الخدمات المصغّرة باستخدام **ASP.NET Core 10** و**Entity Framework Core 10** و**PostgreSQL**. تتولى بوابة `ApiGateway` توجيه الطلبات العامة إلى الخدمات، بينما تمتلك كل خدمة قاعدة بيانات مستقلة وتتواصل الخدمات فيما بينها عبر HTTP فقط.

## مكوّنات المشروع

| المكوّن | المسؤولية | منفذ Docker | منفذ التشغيل المحلي | قاعدة البيانات |
| --- | --- | ---: | ---: | ---: |
| `ProductService` | المنتجات، التصنيفات، المخزون وحركاته | `5001` | `5032` | PostgreSQL على `5432` |
| `CustomerService` | العملاء، العناوين، وحالة أهلية العميل | `5002` | `5094` | PostgreSQL على `5433` |
| `CartService` | السلال، العناصر، التحقق، وقفل الدفع | `5003` | `5260` | داخل Docker فقط؛ محليًا `5434` |
| `OrderService` | إنشاء الطلب وتنسيق عملية الدفع واستعادة العمليات المتعثرة | `5004` | `5004` | PostgreSQL على `5435` |
| `ApiGateway` | المسارات العامة، تحديد المعدل، المهلات، وفحوص الصحة | لا يوجد Dockerfile | `5267` | — |

ترتيب الاعتماد بين الخدمات:

```text
ApiGateway
 ├─ ProductService ── productdb
 ├─ CustomerService ─ customerdb
 ├─ CartService ───── cartdb
 │   ├─ ProductService
 │   └─ CustomerService
 └─ OrderService ──── orderdb
     ├─ ProductService
     ├─ CustomerService
     └─ CartService
```

## المتطلبات

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Docker Desktop أو Docker Engine مع Docker Compose v2
- PowerShell 7 أو Windows PowerShell لتشغيل الأوامر كما هي مكتوبة أدناه
- المنافذ `5001` إلى `5004` و`5267` متاحة عند التشغيل بالحاويات
- المنافذ `5432` إلى `5435` و`5004` و`5032` و`5094` و`5260` و`5267` متاحة عند التشغيل المحلي الكامل

تحقق من الأدوات:

```powershell
dotnet --version
docker --version
docker compose version
```

## التشغيل السريع الموصى به

يشغّل هذا المسار الخدمات الأربع وقواعد بياناتها داخل Docker، ثم يشغّل البوابة محليًا. نفّذ جميع الأوامر من مجلد جذر الحل.

### 1. ضبط كلمات مرور قواعد البيانات

تُستخدم هذه القيم في جلسة PowerShell الحالية فقط. غيّرها إلى قيم قوية في أي بيئة غير محلية، ولا تضف الأسرار إلى Git.

```powershell
$env:POSTGRES_PASSWORD = "change_this_product_password"
$env:CUSTOMER_DB_PASSWORD = "change_this_customer_password"
$env:CART_DB_PASSWORD = "change_this_cart_password"
$env:ORDER_DB_PASSWORD = "change_this_order_password"
```

يمكن تخصيص أسماء المستخدمين والمنافذ ببقية المتغيرات الموجودة في ملفات `docker-compose.*.yml`، لكن القيم الافتراضية مناسبة للتطوير المحلي.

### 2. تشغيل الخدمات وقواعد البيانات

استخدم ملفات Compose الأربعة معًا حتى تعمل الخدمات على شبكة `ecommerce-network` نفسها:

```powershell
docker compose `
  -f docker-compose.product.yml `
  -f docker-compose.customer.yml `
  -f docker-compose.cart.yml `
  -f docker-compose.order.yml `
  config

docker compose `
  -f docker-compose.product.yml `
  -f docker-compose.customer.yml `
  -f docker-compose.cart.yml `
  -f docker-compose.order.yml `
  up --build -d
```

تُطبّق ترحيلات قواعد البيانات تلقائيًا داخل الحاويات. كما يضيف `ProductService` منتجًا تجريبيًا وتصنيفات، ويضيف `CustomerService` ثلاثة عملاء تجريبيين لأن الخدمتين تعملان في بيئة `Development` مع تفعيل `SeedData`.

تحقق من الحالة والسجلات:

```powershell
docker compose `
  -f docker-compose.product.yml `
  -f docker-compose.customer.yml `
  -f docker-compose.cart.yml `
  -f docker-compose.order.yml `
  ps

docker compose `
  -f docker-compose.product.yml `
  -f docker-compose.customer.yml `
  -f docker-compose.cart.yml `
  -f docker-compose.order.yml `
  logs -f
```

اخرج من متابعة السجلات باستخدام `Ctrl+C`؛ لن يوقف ذلك الحاويات.

### 3. تشغيل API Gateway

إعداد البوابة الافتراضي يشير إلى منافذ التشغيل المحلي من `launchSettings.json`. عند تشغيل الخدمات داخل Docker يجب توجيهها إلى منافذ Docker المنشورة:

```powershell
Set-Item -Path 'Env:ReverseProxy__Clusters__product-cluster__Destinations__product-destination__Address' -Value 'http://localhost:5001/'
Set-Item -Path 'Env:ReverseProxy__Clusters__customer-cluster__Destinations__customer-destination__Address' -Value 'http://localhost:5002/'
Set-Item -Path 'Env:ReverseProxy__Clusters__cart-cluster__Destinations__cart-destination__Address' -Value 'http://localhost:5003/'
Set-Item -Path 'Env:ReverseProxy__Clusters__order-cluster__Destinations__order-destination__Address' -Value 'http://localhost:5004/'

dotnet run --project .\ApiGateway --launch-profile http
```

العناوين الأساسية بعد التشغيل:

- البوابة: `http://localhost:5267`
- معلومات البوابة: `http://localhost:5267/api/gateway/info`
- حالة الخدمات من البوابة: `http://localhost:5267/api/gateway/status`
- صفحة مستندات البوابة في Development: `http://localhost:5267/docs`
- ProductService: `http://localhost:5001`
- CustomerService: `http://localhost:5002`
- CartService: `http://localhost:5003`
- OrderService: `http://localhost:5004`

> ملاحظة: يعمل `CartService` و`OrderService` في Compose ببيئة `Production`، لذلك لا يفعّلان Swagger. روابط OpenAPI الخاصة بهما في صفحة `/docs` تعمل عند تشغيلهما محليًا في بيئة `Development`.

### 4. فحص التشغيل

افتح نافذة PowerShell جديدة ونفّذ:

```powershell
Invoke-RestMethod http://localhost:5267/health/live
Invoke-RestMethod http://localhost:5267/health/dependencies
Invoke-RestMethod http://localhost:5267/api/gateway/status
Invoke-RestMethod http://localhost:5267/api/products
Invoke-RestMethod http://localhost:5267/api/customers
```

يجب أن تُرجع فحوص الصحة حالة سليمة، وأن تعرض آخر دعوتين بيانات المنتجات والعملاء المزروعة.

### 5. إيقاف المشروع

احتفظ بالبيانات:

```powershell
docker compose `
  -f docker-compose.product.yml `
  -f docker-compose.customer.yml `
  -f docker-compose.cart.yml `
  -f docker-compose.order.yml `
  down
```

لحذف قواعد البيانات والبدء من الصفر أضف `-v`. **هذا يحذف جميع بيانات PostgreSQL المخزنة في volumes الخاصة بالمشروع**:

```powershell
docker compose `
  -f docker-compose.product.yml `
  -f docker-compose.customer.yml `
  -f docker-compose.cart.yml `
  -f docker-compose.order.yml `
  down -v
```

أوقف `ApiGateway` في نافذته باستخدام `Ctrl+C`.

## التشغيل المحلي الكامل للتطوير

استخدم هذا المسار عند الحاجة إلى Swagger لكل خدمة، أو نقاط التوقف Debug، أو إعادة البناء السريع دون بناء صور Docker. تعمل تطبيقات .NET على الجهاز، بينما تعمل قواعد PostgreSQL فقط داخل Docker.

إذا كان مسار التشغيل السريع يعمل، أوقفه أولًا لتجنب تعارض المنافذ. بعد ذلك أنشئ قواعد البيانات:

```powershell
docker run --name ecommerce-productdb-dev --restart unless-stopped `
  -e POSTGRES_DB=productdb -e POSTGRES_USER=productuser -e POSTGRES_PASSWORD=change_this_password `
  -p 5432:5432 -v ecommerce-productdb-dev:/var/lib/postgresql/data -d postgres:18-alpine

docker run --name ecommerce-customerdb-dev --restart unless-stopped `
  -e POSTGRES_DB=customerdb -e POSTGRES_USER=customeruser -e POSTGRES_PASSWORD=change_this_password `
  -p 5433:5432 -v ecommerce-customerdb-dev:/var/lib/postgresql/data -d postgres:18-alpine

docker run --name ecommerce-cartdb-dev --restart unless-stopped `
  -e POSTGRES_DB=cartdb -e POSTGRES_USER=cartuser -e POSTGRES_PASSWORD=change_this_password `
  -p 5434:5432 -v ecommerce-cartdb-dev:/var/lib/postgresql/data -d postgres:17-alpine

docker run --name ecommerce-orderdb-dev --restart unless-stopped `
  -e POSTGRES_DB=orderdb -e POSTGRES_USER=orderuser -e POSTGRES_PASSWORD=change_this_password `
  -p 5435:5432 -v ecommerce-orderdb-dev:/var/lib/postgresql/data -d postgres:18-alpine
```

استعد الحزم وابنِ الحل مرة واحدة:

```powershell
dotnet restore .\ECommerceMicroservices.slnx
dotnet build .\ECommerceMicroservices.slnx --no-restore
```

شغّل كل مكوّن في نافذة PowerShell مستقلة من جذر الحل. متغيرات البيئة في كل كتلة تخص نافذتها فقط.

### النافذة 1 — ProductService

```powershell
$env:ConnectionStrings__ProductDatabase = 'Host=localhost;Port=5432;Database=productdb;Username=productuser;Password=change_this_password'
$env:Database__ApplyMigrationsOnStartup = 'true'
$env:Database__SeedData = 'true'
dotnet run --project .\ProductService --launch-profile http
```

Swagger: `http://localhost:5032/swagger`

### النافذة 2 — CustomerService

```powershell
$env:ConnectionStrings__CustomerDatabase = 'Host=localhost;Port=5433;Database=customerdb;Username=customeruser;Password=change_this_password'
$env:Database__ApplyMigrationsOnStartup = 'true'
$env:Database__SeedData = 'true'
dotnet run --project .\CustomerService --launch-profile http
```

Swagger: `http://localhost:5094/swagger`

### النافذة 3 — CartService

```powershell
$env:ConnectionStrings__CartDatabase = 'Host=localhost;Port=5434;Database=cartdb;Username=cartuser;Password=change_this_password'
$env:Database__ApplyMigrationsOnStartup = 'true'
$env:Services__ProductService__BaseUrl = 'http://localhost:5032'
$env:Services__CustomerService__BaseUrl = 'http://localhost:5094'
dotnet run --project .\CartService --launch-profile http
```

Swagger: `http://localhost:5260/swagger`

### النافذة 4 — OrderService

```powershell
$env:ConnectionStrings__OrderDatabase = 'Host=localhost;Port=5435;Database=orderdb;Username=orderuser;Password=change_this_password'
$env:Database__ApplyMigrationsOnStartup = 'true'
$env:Services__ProductService__BaseUrl = 'http://localhost:5032'
$env:Services__CustomerService__BaseUrl = 'http://localhost:5094'
$env:Services__CartService__BaseUrl = 'http://localhost:5260'
dotnet run --project .\OrderService --launch-profile http
```

Swagger: `http://localhost:5004/swagger`

### النافذة 5 — ApiGateway

في التشغيل المحلي لا تضبط وجهات `ReverseProxy`؛ القيم الموجودة في `reverseproxy.json` تطابق منافذ الخدمات أعلاه.

```powershell
dotnet run --project .\ApiGateway --launch-profile http
```

البوابة: `http://localhost:5267`، والمستندات: `http://localhost:5267/docs`.

لإيقاف قواعد التطوير لاحقًا مع الاحتفاظ بالبيانات:

```powershell
docker stop ecommerce-productdb-dev ecommerce-customerdb-dev ecommerce-cartdb-dev ecommerce-orderdb-dev
```

ولتشغيلها مرة أخرى:

```powershell
docker start ecommerce-productdb-dev ecommerce-customerdb-dev ecommerce-cartdb-dev ecommerce-orderdb-dev
```

## المسارات العامة عبر البوابة

تخفي البوابة الجزء الداخلي `/api/v1` وتعرض المسارات التالية:

| الخدمة | المسار العام |
| --- | --- |
| المنتجات | `/api/products` |
| التصنيفات | `/api/categories` |
| العملاء والعناوين | `/api/customers` |
| السلال | `/api/carts` |
| الطلبات | `/api/orders` |

مثال يضيف معرّف ارتباط لتتبّع الطلب عبر الخدمات:

```powershell
Invoke-RestMethod http://localhost:5267/api/products `
  -Headers @{ 'X-Correlation-ID' = 'local-smoke-test-001' }
```

بعض عمليات التنسيق الداخلية لا تُعرض عبر البوابة عمدًا، مثل إكمال/إلغاء قفل دفع السلة وتعديل المخزون المباشر؛ تستعملها الخدمات فيما بينها.

## فحوص الصحة

كل خدمة توفّر:

- `/health/live`: يتحقق من أن التطبيق يعمل فقط.
- `/health/ready`: يتحقق من جاهزية الخدمة وقاعدة بياناتها.
- `/health/dependencies`: متاح في `CartService` و`OrderService` والبوابة لفحص الخدمات التابعة.

أمثلة مباشرة:

```powershell
Invoke-RestMethod http://localhost:5001/health/ready
Invoke-RestMethod http://localhost:5002/health/ready
Invoke-RestMethod http://localhost:5003/health/dependencies
Invoke-RestMethod http://localhost:5004/health/dependencies
```

استبدل المنافذ بمنافذ التشغيل المحلي عند عدم استخدام Docker.

## ترحيلات قواعد البيانات

الطريقة الأبسط محليًا هي ضبط `Database__ApplyMigrationsOnStartup=true` كما في أوامر التشغيل السابقة. لإدارة الترحيلات يدويًا، ثبّت أداة EF Core المطابقة للإصدار الرئيسي للمشروع إذا لم تكن موجودة:

```powershell
dotnet tool install --global dotnet-ef --version 10.*
```

بعد ضبط connection string الصحيح للخدمة في الجلسة، استخدم مثلًا:

```powershell
dotnet ef migrations list --project .\ProductService --startup-project .\ProductService
dotnet ef database update --project .\ProductService --startup-project .\ProductService
```

استبدل `ProductService` بـ `CustomerService` أو `CartService` أو `OrderService` للخدمات الأخرى. ملفات الترحيل الأولية موجودة بالفعل داخل مجلد `Migrations` في كل خدمة.

## البيانات التجريبية

عند تفعيل `Database__SeedData=true` في بيئة غير Production:

- ينشئ `ProductService` التصنيفات `Electronics` و`Computers` و`Home Appliances` ومنتجًا بالرمز `LAPTOP-DELL-001` ومخزون أولي قدره 15.
- ينشئ `CustomerService` العملاء `CUS-SEED000001` و`CUS-SEED000002` و`CUS-SEED000003`، ولكل منهم عنوان افتراضي.
- لا يزرع `CartService` أو `OrderService` بيانات وهمية.

يمكن الحصول على المعرّفات GUID المولدة من:

```powershell
Invoke-RestMethod http://localhost:5267/api/products
Invoke-RestMethod http://localhost:5267/api/customers
```

## الإعدادات المهمة

يقرأ ASP.NET Core الإعدادات من `appsettings.json` ثم ملف البيئة ثم متغيرات البيئة. لتحويل مفتاح متداخل إلى متغير بيئة استبدل `:` بـ `__`، مثل:

```text
ConnectionStrings:CartDatabase       -> ConnectionStrings__CartDatabase
Services:ProductService:BaseUrl      -> Services__ProductService__BaseUrl
Database:ApplyMigrationsOnStartup    -> Database__ApplyMigrationsOnStartup
```

أهم الإعدادات:

- `ConnectionStrings__*Database`: اتصال PostgreSQL الخاص بكل خدمة.
- `Database__ApplyMigrationsOnStartup`: تطبيق الترحيلات عند بدء الخدمة.
- `Database__SeedData`: بيانات التطوير في Product وCustomer فقط.
- `Services__*__BaseUrl`: عناوين الخدمات التابعة لـ Cart وOrder.
- `CartExpiration__Enabled`: تشغيل معالجة السلال المنتهية.
- `OrderRecovery__Enabled`: تشغيل استعادة الطلبات المتعثرة.
- `Cors__AllowedOrigins__0`: أول أصل CORS مسموح، ثم `__1` وهكذا.
- `RateLimiting__Enabled`: تفعيل تحديد معدل الطلبات.

## البناء والتحقق

```powershell
dotnet restore .\ECommerceMicroservices.slnx
dotnet build .\ECommerceMicroservices.slnx --no-restore --configuration Debug
```

لا يحتوي الحل حاليًا على مشاريع اختبارات آلية؛ لذلك نجاح `dotnet build` وفحوص الصحة هما التحقق المتاح في المستودع حاليًا.

## استكشاف الأخطاء

### Compose يرفض البدء بسبب متغير مفقود

ملفا Product وCustomer يفرضان وجود `POSTGRES_PASSWORD` و`CUSTOMER_DB_PASSWORD`. اضبط متغيرات كلمات المرور الأربعة في جلسة PowerShell نفسها التي تنفّذ منها `docker compose`.

### المنفذ مستخدم بالفعل

اعرف العملية أو الحاوية التي تشغل المنفذ:

```powershell
Get-NetTCPConnection -State Listen | Where-Object LocalPort -In 5001,5002,5003,5004,5267,5432,5433,5434,5435
docker ps --format 'table {{.Names}}\t{{.Ports}}'
```

أوقف التشغيل السابق أو غيّر المنفذ المنشور في متغير Compose المناسب.

### البوابة تُرجع 502 أو 503

- تحقق من `/health/dependencies` ومن أن الخدمات الأربع تعمل.
- عند استخدام Docker، تأكد من ضبط وجهات `ReverseProxy` إلى `5001` و`5002` و`5003` و`5004` قبل تشغيل البوابة.
- عند التشغيل المحلي، أزل overrides السابقة أو افتح نافذة PowerShell جديدة حتى تعود البوابة إلى `5032` و`5094` و`5260` و`5004`.

### خطأ يفيد بأن الجدول غير موجود

الترحيلات لم تُطبّق. اضبط `Database__ApplyMigrationsOnStartup=true` وأعد تشغيل الخدمة، أو نفّذ `dotnet ef database update` بعد ضبط connection string.

### CartService أو OrderService لا يستطيع الوصول إلى خدمة أخرى

في Docker يجب أن تكون العناوين من نمط `http://productservice:8080` لأن الاتصال داخلي عبر شبكة Docker. في التشغيل المحلي استخدم منافذ `5032` و`5094` و`5260` كما في قسم التشغيل المحلي.

### Swagger يعيد 404

Swagger مفعّل فقط في بيئة `Development`. حاويات Cart وOrder تعمل افتراضيًا في `Production`؛ شغّلهما محليًا للحصول على Swagger أو غيّر بيئة الحاوية للتطوير فقط.

### CustomerService يظهر Unhealthy داخل Docker

افحص أولًا `docker compose logs customerservice` ثم جرّب `http://localhost:5002/health/live` مباشرة. فحص Compose يستخدم `curl`، بينما Dockerfile الخاص بالخدمة لا يثبّته صراحةً؛ لذلك قد يفشل فحص الحاوية حتى لو كانت الخدمة نفسها تعمل، حسب محتويات صورة runtime المستخدمة.

