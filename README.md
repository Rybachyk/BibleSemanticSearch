# Bible Semantic Search

Вебзастосунок для семантичного пошуку біблійних віршів українською мовою на основі перекладу Івана Огієнка.

На відміну від звичайного пошуку за ключовими словами, користувач може сформулювати запит природною мовою, а система знаходить біблійні уривки, близькі за змістом.

## Можливості

- Семантичний пошук за змістом запиту.
- Двохетапний пошук: спочатку пошук релевантних чанків, потім відбір найбільш відповідних окремих віршів.
- Фільтрація результатів за біблійною книгою та заповітом.
- Перегляд повного розділу після вибору знайденого вірша.
- Імпорт біблійного тексту та підготовка даних для векторного пошуку.

## Технології

**Backend**

- C#
- ASP.NET Core Web API (.NET 8)
- MongoDB Atlas
- MongoDB Vector Search

**Embeddings**

- OpenRouter API
- `openai/text-embedding-3-small`
- Розмірність векторів: `1536`

**Frontend**

- React
- TypeScript
- Vite

## Структура проєкту

```text
BibleSemanticSearch/
├── api/
│   └── BibleSearch.Api/
│       ├── Controllers/
│       ├── Data/
│       ├── Models/
│       ├── Services/
│       ├── Properties/
│       ├── Program.cs
│       └── appsettings.json
├── frontend/
│   ├── public/
│   ├── src/
│   ├── package.json
│   └── vite.config.ts
└── README.md
```

## Вимоги

Перед запуском встановіть:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js](https://nodejs.org/) (рекомендовано LTS)
- Обліковий запис [MongoDB Atlas](https://www.mongodb.com/atlas)
- API-ключ [OpenRouter](https://openrouter.ai/)

## Налаштування

### 1. Клонування репозиторію

```bash
git clone <REPOSITORY_URL>
cd BibleSemanticSearch
```

### 2. Налаштування MongoDB Atlas

1. Створіть кластер у MongoDB Atlas.
2. Створіть користувача бази даних і надайте йому необхідні права.
3. Додайте свою IP-адресу до Network Access.
4. Отримайте connection string для підключення до кластера.
5. Створіть базу даних `BibleKnowledgeBase`.

Рядок підключення до MongoDB містить облікові дані, тому його не можна додавати до Git.

### 3. Налаштування секретів backend

У каталозі API виконайте:

```bash
cd api/BibleSearch.Api
dotnet user-secrets init
```

Додайте connection string та OpenRouter API-ключ через User Secrets:

```bash
dotnet user-secrets set "MongoDB:ConnectionString" "<YOUR_MONGODB_CONNECTION_STRING>"
dotnet user-secrets set "OpenRouter:ApiKey" "<YOUR_OPENROUTER_API_KEY>"
```

> Назви ключів у командах мають відповідати назвам, які читає конфігурація у `MongoDbService` та `EmbeddingService`. Якщо у вихідному коді використовуються інші ключі, замініть назви в командах на відповідні. Не вставляйте реальні ключі у README або файли, що потрапляють до Git.

Поверніться до кореня репозиторію:

```bash
cd ../..
```

### 4. Налаштування MongoDB Vector Search

У MongoDB Atlas створіть Vector Search індекси для колекцій:

- `chunks` — індекс `chunk_vector_index`
- `verses` — індекс `verse_vector_index`

Параметри індексів мають відповідати полю з embedding-вектором у документах та параметрам `$vectorSearch` у коді API. Для використаної embedding-моделі розмірність вектора — `1536`.

> Перед запуском пошуку переконайтеся, що обидва індекси створені та мають статус Ready.

### 5. Налаштування frontend

У каталозі `frontend` створіть локальний файл `.env.local`:

```env
VITE_API_URL=https://localhost:7097
```

Якщо backend запущено за іншою адресою, вкажіть адресу API, яка відповідає вашому профілю запуску.

Змінні з префіксом `VITE_` доступні клієнтському коду браузера. Не зберігайте в них паролі, connection strings або приватні API-ключі.

## Запуск проєкту

Відкрийте два термінали з кореня репозиторію.

### Backend

```bash
cd api/BibleSearch.Api
dotnet restore
dotnet run
```

Swagger UI доступний за адресою, яку виведе застосунок у терміналі. Для стандартного HTTPS-профілю проєкту це `https://localhost:7097/swagger`; HTTP-профіль використовує `http://localhost:5074`.

### Frontend

```bash
cd frontend
npm install
npm run dev
```

Vite виведе локальну адресу frontend у терміналі (зазвичай `http://localhost:5173`).

## Дані

Проєкт використовує біблійні тексти перекладу Івана Огієнка. Дані потрібно імпортувати до MongoDB перед виконанням пошуку.

У каталозі backend є файли з даними та засоби імпорту. Використовуйте передбачені API-ендпоїнти імпорту, щоб завантажити текст, створити embeddings і підготувати документи та чанки для пошуку.

Якщо файли біблійного тексту не включені до вашої копії репозиторію, підготуйте їх локально у форматі, який очікує імпортер. Переконайтеся, що маєте право поширювати будь-які текстові дані, які додаєте до публічного репозиторію.

## Принцип роботи пошуку

1. Користувач вводить запит природною мовою.
2. Backend отримує embedding запиту через OpenRouter.
3. MongoDB Vector Search знаходить найбільш релевантні чанки.
4. Система виконує пошук окремих віршів у межах знайденого контексту.
5. API повертає релевантні вірші, а не весь вміст чанка.
6. Користувач може відкрити повний розділ, з якого походить знайдений вірш.

## Конфігурація

Основні несекретні параметри зберігаються в `api/BibleSearch.Api/appsettings.json`:

- назва бази даних: `BibleKnowledgeBase`
- адреса OpenRouter API
- embedding-модель: `openai/text-embedding-3-small`
- розмірність embedding-вектора: `1536`
- пороги подібності для пошуку чанків і віршів

Секретні значення (MongoDB connection string та OpenRouter API key) мають зберігатися локально через .NET User Secrets або змінні середовища.

## Безпека

Не додавайте до репозиторію:

- MongoDB connection string з логіном і паролем;
- OpenRouter API key;
- `.env.local` та інші локальні файли середовища;
- `appsettings.Development.json`, якщо він містить локальні налаштування або секрети;
- каталоги `bin`, `obj`, `node_modules` та зібрані frontend-файли.

Перевірте `.gitignore` перед першим commit.

## Ліцензія та біблійний текст

Код проєкту та біблійні текстові дані можуть мати різні умови використання. Перед публічним поширенням тексту перекладу Івана Огієнка перевірте умови його використання та поширення.
