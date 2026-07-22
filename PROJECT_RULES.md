# SP Converter — Правила, Память и Особенности Проекта

В этом файле собраны все важные правила, архитектурные особенности, настройки пользователя и процедуры для избежания галлюцинаций и сохранения контекста проекта **SP Converter**.

---

## 1. Заголовок и Именование
- **Название программы в заглавии (MainWindow Title):** `SP Converter` (СТРОГО без ", версия 1.0" и тому подобное).
- **Формат указания версии:** `Версия 1.1` (обязательно с пробелом).
- **GitHub Репозиторий:** `uasdiuduiasdasd/SP-onverter`.

---

## 2. Настройки Программы
- **Хранение настроек:** Все настройки программы **обязательно** хранятся в локальной папке программы (для портативности).
- **Прозрачность (Mica/Acrylic):** Эффект прозрачности окна **по умолчанию выключен** (`EnableTransparency = false`).

---

## 3. Размеры и Стилистика Окна (`MainWindow.xaml`)
- **Габариты окна:** 
  - `Height="810"`
  - `Width="570"`
  - `MinHeight="650"`
  - `MinWidth="460"`
- **Принцип UI:** Все элементы управления (включая выбор форматов, переключатели и кнопки конвертации) должны помещаться на экране **без появления вертикального скроллбара**.
- **Фреймворк UI:** WPF UI (`Wpf.Ui`) + `CommunityToolkit.Mvvm`.

---

## 4. Поддержка Форматов и Многостраничность
- **Входные форматы (Input):** JPG, JPEG, PNG, WEBP, AVIF, HEIC, BMP, TGA, TIFF, ICO, JXL, PDF, GIF, RAW (CR2, NEF, ARW, DNG).
- **Выходные форматы (Output):** JPG, JPEG, PNG, WEBP, BMP, TGA, AVIF, HEIC, TIFF, ICO, JXL, PDF, GIF.
- **Извлечение многостраничных/многокадровых файлов (PDF, TIFF, GIF, ICO):**
  - Регулируется переключателем `ExtractAllPages` ("Извлечь все кадры/страницы").
  - По умолчанию переключатель **ВЫКЛЮЧЕН** (извлекается только 1-й кадр, чтобы не создавать лишних файлов).
  - Если включен: используется `MagickImageCollection`, а выходные файлы именуются с суффиксами `_page1`, `_page2` и т.д.

---

## 5. Чтение Файлов и Защита от Ошибок (Magick.NET)
- **Правило чтения файлов:** В `MagickImageConverter` файлы ВСЕГДА читаются через `FileStream` (`File.OpenRead(filePath)`), а не напрямую по пути строкой `filePath`.
- **Причина:** Если файл переименован (например, WebP под видом `.gif`), чтение строкой заставляет Magick.NET верить расширению и падать с ошибкой `improper image header @ error/gif.c/ReadGIFImage`. Чтение через `FileStream` заставляет Magick.NET проверять **Magic Bytes** (байты заголовка) и корректно определять реальный формат.

---

## 6. Процедура Сборки и Публикации (Release Workflow)
- **Главное правило Git:** Сборка локальных коммитов выполняется по мере работы, но **git push** и публикация на GitHub происходят **ТОЛЬКО по явному запросу пользователя**.
- **Инструмент публикации:** GitHub CLI (`gh`). Потребитель авторизован как `uasdiuduiasdasd`.
- **Инструмент сборки инсталлятора (Inno Setup):** `ISCC.exe` находится по пути:
  `C:\Users\Hser\.nuget\packages\tools.innosetup\6.4.2\tools\ISCC.exe`
- **Команды сборки:**
  - **Portable exe:** `dotnet publish src/SPConverter.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish\portable`
  - **Setup.exe:** `& "C:\Users\Hser\.nuget\packages\tools.innosetup\6.4.2\tools\ISCC.exe" SPConverter.iss`
  - **ZIP-архив:** `Compress-Archive -Path publish\portable\* -DestinationPath publish\SPConverter_v1.1_Portable.zip -Force`

---

## 7. Тестирование
- Тесты находятся в проекте `tests/SPConverter.Tests`.
- Запуск тестов: `dotnet test tests/SPConverter.Tests/SPConverter.Tests.csproj`.
- Все 20 тестов должны быть зелеными перед каждым релизом.
