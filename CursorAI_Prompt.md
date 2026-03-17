# Запрос для Cursor AI: Помощь с реверс-инжинирингом USB HID макропада

Привет, Cursor! Мой предыдущий ИИ-ассистент (Antigravity) провел глубокое расследование, но мы зашли в тупик с программированием китайского 6-кнопочного макропада (MINI KeyBoard) из C#.
Оригинальная программа `MINI KeyBoard.exe` (которую мы декомпилировали) успешно шьет устройство. Наш самописный [MacroPadService.cs](file:///i:/WEAKEST%20LINK%20SOPFTWARE%20AI%20TESTERING%20FINALE/Core/Services/MacroPadService.cs) на базе `HidLibrary` — нет. 

**Контекст, который выяснил Antigravity:**
1. Устройство использует `VID_1189`. 
2. Оригинальная программа использует библиотеку `HidLibrary` и обнаруживает устройство по пути, содержащему `mi_00`.
3. При обнаружении пути `mi_00` оригинальный драйвер включает "Протокол 1" вместо базовых команд. 
4. Согласно декомпилированному коду [Download_Click](file:///C:/Users/Timontiy/AppData/Local/Temp/MiniKB_src/HIDTester/FormMain.cs#686-960), программа формирует пакет на 65 байт, где первый байт полезной нагрузки равен `254` (`0xFE`), далее идут номер клавиши, слой, тип, модификаторы и сам код клавиши (`Array[11]`). Фрагмент их кода: `array[0] = 254; array[1] = KeyNum; array[2] = 1; array[3] = 1; ... array[11] = KeyCode;`.
5. Они создают отчет `wDevice.CreateReport()`, указывают `ReportId = 0` (или `0x03` на другом пути), прокидывают данные в `hidReport.Data` и отправляют через `WriteReport(hidReport, 500)`.
6. Мы переписали наш [MacroPadService.cs](file:///i:/WEAKEST%20LINK%20SOPFTWARE%20AI%20TESTERING%20FINALE/Core/Services/MacroPadService.cs) с точно такой же логикой (посылаем пакет с началом `0xFE`, массивом на 65 байт), но макропад никак не реагирует на наши репорты.

**Твоя задача:**
1. Помочь мне "подсмотреть", какие именно байты шлет оригинальная программа `MINI KeyBoard.exe` в USB-порт, когда я жму кнопку "Download" (Синхронизировать).
2. Напиши для этого либо Python сниффер для перехвата API вызовов (например, используя библиотеку `frida` для хука функции `WriteFile` в `kernel32.dll` или функции `HidD_SetFeature` в `hid.dll` для процесса `MINI KeyBoard.exe`), либо помоги пропатчить/собрать декомпилированный код [FormMain.cs](file:///C:/Users/Timontiy/AppData/Local/Temp/MiniKB_src/HIDTester/FormMain.cs), чтобы он выводил команды в консоль перед [WriteDevice()](file:///C:/Users/Timontiy/AppData/Local/Temp/MiniKB_src/HIDTester/HidLib.cs#194-220).
3. Либо вместе со мной найди ошибку в нашем [MacroPadService.cs](file:///i:/WEAKEST%20LINK%20SOPFTWARE%20AI%20TESTERING%20FINALE/Core/Services/MacroPadService.cs) — возможно, выравнивание `ReportId`, скрытые Control Transfer вместо Interrupt OUT или длина репорта.

Сравни наш текущий код [MacroPadService.cs](file:///i:/WEAKEST%20LINK%20SOPFTWARE%20AI%20TESTERING%20FINALE/Core/Services/MacroPadService.cs) с тем, что ты можешь узнать о `MINI KeyBoard.exe` в папке `%TEMP%\\MiniKB_src`. Жду твоих идей и скрипт для "прослушки" оригинальной программы!
