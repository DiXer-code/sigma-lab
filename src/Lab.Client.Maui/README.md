# MAUI-клієнт зони Z4

Застосунок .NET MAUI для Android і Windows. Локальний доменний доступ використовує
LabDbContext і Fluent-конфігурації з Lab.Infrastructure; окремі доменні моделі клієнт не створює.

## Залежності та запуск

Потрібен .NET SDK 10, MAUI workloads для .NET 10, Android SDK API 36,
build-tools 36.0.0 і JDK 21. Наявність workload у списку не гарантує, що встановлено
SDK потрібної версії: компоненти MAUI для .NET 8/9 не збирають цей проєкт.

    dotnet workload list
    dotnet workload install maui-windows maui-android
    dotnet restore src/Lab.Client.Maui/Lab.Client.Maui.csproj
    dotnet build src/Lab.Client.Maui/Lab.Client.Maui.csproj -c Release -f net10.0-windows10.0.19041.0
    dotnet build src/Lab.Client.Maui/Lab.Client.Maui.csproj -c Release -f net10.0-android
    dotnet run --project src/Lab.Client.Maui/Lab.Client.Maui.csproj -f net10.0-windows10.0.19041.0

Відсутні залежності Android встановлюються стандартною ціллю SDK:

    dotnet build src/Lab.Client.Maui/Lab.Client.Maui.csproj -t:InstallAndroidDependencies -f net10.0-android -p:AndroidSdkDirectory=C:/android-sdk -p:JavaSdkDirectory=C:/jdk -p:AcceptAndroidSdkLicenses=True

Шляхи AndroidSdkDirectory і JavaSdkDirectory можна вибрати у власному каталозі;
для наступних збірок передавайте ті самі властивості. Короткі шляхи до NuGet-кешу
й Android SDK запобігають помилкам читання архівів на Windows.

Клієнт зберігає SQLite файл у приватному каталозі застосунку та застосовує наявні міграції
з Lab.Migrations.Sqlite. Екран показує майданчики з локальної БД і стан черги синхронізації.
Після злиття моделі Experiment у main екран серій має використовувати саме її.

## Черга синхронізації

Операції мають UUID, тип ідентифікованого ресурсу, JSON payload, час створення, кількість спроб,
останню помилку та один зі станів Pending, Sent, Failed. Додавання ідемпотентне за UUID.
Помилкову операцію можна повернути в Pending; статус Sent фіксується після відповіді сервера.

Черга зберігається атомарним JSON-файлом у приватному каталозі клієнта. Вона відокремлена від
доменного LabDbContext, бо поточний спільний контекст і Fluent-модель належать Z1; це не додає
клієнтської копії Experiment або міграцій до чужої зони. Рішення та межі гарантій
описані в docs/decisions-log.md, запис №8. Зараз реалізовано локальне сховище операцій;
мережеве відправлення й політика конфліктів належать наступному issue #14.

## Перевірки

    dotnet build LabStem.slnx -c Release
    dotnet test LabStem.slnx --collect:"XPlat Code Coverage"
    python scripts/coverage-summary.py

LocalDatabaseServiceTests застосовує спільні міграції SQLite й перевіряє читання
збереженого Site. SyncQueueStoreTests перевіряє ідемпотентність після повторного
відкриття, переходи станів, порожню чергу, паралельні записи та пошкоджений файл.
Тести цих сервісів виконуються без MAUI UI через підключення того самого вихідного коду.

## Межі цього етапу

Доменна модель у main зараз має Site. Тому каркас не оголошує Experiment локально;
список серій буде підключено до моделі Z1 після її злиття. Додавання MAUI-проєкту до solution та
CI потребує узгодження інтеграції з власником Z2, оскільки .github/ належить його зоні.
