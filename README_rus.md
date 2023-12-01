# AutoOverlay AviSynth plugin

### Требования
- AviSynth+ 3.6+: https://github.com/AviSynth/AviSynthPlus/releases/
- AvsFilterNet plugin https://github.com/Asd-g/AvsFilterNet (включено в поставку)
- SIMD Library https://github.com/ermig1979/Simd (включено в поставку)
- warp plugin v0.1b by wonkey_monkey https://forum.doom9.org/showthread.php?t=176031 (включено в поставку)
- Math.NET Numerics (включено в поставку)
- MVTools https://github.com/pinterf/mvtools/releases (для aoInterpolate, не включено в поставку)
- RGTools https://github.com/pinterf/RgTools/releases (для aoInterpolate, не включено в поставку)
- .NET framework 4.8+
- Windows 7+

### Установка
- Скопировать файлы из папок x86/x64 в папки с плагинами AviSynth.
- В свойствах DLL в проводнике Windows может потребоваться "Разблокировать" файлы. 

### Описание
Плагин предназначен для оптимального наложения одного видеоклипа на другой. 
Выравнивание клипов относительно друг друга осуществляется фильтром OverlayEngine путем тестирования различных координат верхнего левого угла наложения, размеров изображений, соотношений сторон и углов вращения для того, чтобы найти оптимальные параметры наложения. Функция сравнения двух участков изображения двух клипов - среднеквадратическое отклонение, которое далее обозначается как diff. Задача автовыравнивания - найти минимальное значение diff. 
Для повышения производительности автовыравнивание разделено на несколько шагов масштабирования для тестирования различных параметров наложения, задаваемых фильтром OverlayConfig. На первом шаге тестируются все возможные комбинации наложения в низком разрешении. На каждом следующем шаге в более высоком разрешении тестируются комбинации на базе лучших с предыдущего шага. Конфигурации OverlayConfig могут объединяться в цепочки. Если определенная конфигурация дала хороший результат, тестирование следующих не выполняется для экономии времени. 
После автовыравнивания один клип может быть наложен на другой различными способами с помощью фильтра OverlayRender.

### Подключение плагина
    LoadPlugin("%plugin folder%\AvsFilterNet.dll")
    LoadNetPlugin("%plugin folder %\AutoOverlay_netautoload.dll")
AviSynth+ поддерживает автоподключение плагинов, если имя файла плагина .NET содержит суффикс `_netautoload`, который по умолчанию есть.

## Фильтры
### OverlayConfig
    OverlayConfig(float minOverlayArea, float minSourceArea, float aspectRatio1, float aspectRatio2, 
                  float angle1, float angle2, clip warpPoints, int warpSteps, int warpOffset, 
				  int minSampleArea, int requiredSampleArea, float maxSampleDiff, 
                  int subpixel, float scaleBase, int branches, float branchMaxDiff, float acceptableDiff, 
                  int correction, int minX, int maxX, int minY, int maxY, int minArea, int maxArea, 
                  bool fixedAspectRatio, bool debug)
     
Фильтр описывает конфигурацию автовыравнивания для OverlayEngine. Он содержит граничные значения параметров наложения таких как: координаты верхнего левого угла накладываемого изображения относительно основного, ширину и высоту накладываемого изображения и угол вращения. Также конфигурация включает параметры работы OverlayEngine. 
Результат работы фильтра - фейковый кадр, в котором закодированы параметры, с помощью чего они могут быть считаны в OverlayEngine. 
Существует возможность объединять несколько конфигурация в цепочки с помошью обычного объединения клипов: OverlayConfig(…) + OverlayConfig(…). В этом случае OverlayEngine будет тестировать каждую конфигуарцию последовательно на каждом шаге пока не будет получен приемлемое (acceptable) значение diff. 

#### Параметры
- **minOverlayArea** - минимальное отношение используемой части к общей площади накладываемого изображения в процентах. По умолчанию рассчитывается таким образом, чтобы накладываемый клип мог полностью перекрыть базовый (режим пансканирования). К примеру, если разрешение основного клипа 1920x1080, а накладываемого 1920x800, то значение параметра будет 800/1080=74%. 
- **minSourceArea** - минимальное отношение используемой части к общей площади основного изображения в процентах. По умолчанию рассчитывается таким образом, чтобы основной клип мог полностью включить накладываемый (режим пансканирования). К примеру, если разрешение основного клипа 1920x1080, а накладываемого 1440x800, то значение параметра будет 1440/1920=75%. 
- **aspectRatio1** and **aspectRatio2** - диапазон допустимых соотношений сторон накладываемого изображения. По умолчанию - соотношение сторон накладываемого клипа. Может быть задан в любом порядке: `aspectRatio1=2.35, aspectRatio2=2.45` то же самое, что и `aspectRatio1=2.45, aspectRatio2=2.35`.
- **angle1** и **angle2** (default 0) - диапазон допустимых углов вращения накладываемого изображения. Может быть задан в любом порядке. Отрицательные значения – вращение по часовой стрелке, положительные – против.
- **warpPoints** (default empty) – последовательность клипов типа Rect, описывающих исходные точки warp трансфорфмаций и возможных отклонения по осям X и Y, которые будут переданы в фильтр warp. Пример: Rect(0,0,3,3) + Rect(1920,800,3,3) + Rect(1920,0,3,3) + Rect(0,800,3,3) + Rect(960,400,3,3). Здесь описаны warp трансформации с максимальной дальностью 3 пикселя по углам и центру изображения размером 1920x800.
- **warpSteps** (default 3) – количество итераций warp трансформаций. Больше лучше, но медленнее.
- **warpOffset** (default 0) – величина свдига warp трансформаций от последнего шага к первому (в меньшем разрешении). Меньше лучше, но медленнее.
- **minSampleArea** (default 1500) – минимальная площадь в пикселях базового изображения на первом шаге. Чем меньше, тем быстрее, но выше риск некорректного результата. Рекомендованный диапазон: 500-3000. 
- **requiredSampleArea** (default 3000) - максимальная площадь в пикселях базового изображения на первом шаге. Чем меньше, тем быстрее, но выше риск некорректного результата. Рекомендованный диапазон: 1000-5000.
- **maxSampleDiff** (default 5) – максимально допустимое значение diff уменьшенного базового изображения между шагами. Если превышает указанное значение, то предыдущий шаг выполнен не будет. Используется для выбора начального размера изображения между minSampleArea и requiredSampleArea и соответственно шага.
- **subpixel** (default 0) – величина наложения с субпиксельной точностью. 0 – точность один пиксель, 1 – половина пикселя, 2 – четверть и т.д. Ноль рекомендуется, если один клип имеет существенно более низкое разрешение, чем другой. 1-3 рекомендуется, если оба клипа имеют примерно одинаковое разрешение. Отрицательные значения тоже поддерживаются, в этом случае наложение будет выполнено с пониженной точностью, но быстрее. 
- **scaleBase** (default 1.5) – основание для расчета уменьшающего коэффициента по формуле `coef=scaleBase^(1 - (maxStep - currentStep))`. Чем ниже, тем большее количество шагов. 
- **branches** (default 1) - какое количество наилучших параметров наложения использовать с предыдущего шага для поиска на текущем. Больше - лучше, но дольше. По сути, глубина ветвления.    
- **branchMaxDiff** (default 0.2) - максимальная разница на текущем шаге между значениями diff наилучших параметров поиска и прочих. Используется для отбрасывания бесперспективных ветвей поиска.  
- **acceptableDiff** (default 5) – приемлемое значение diff, после которого не тестируются последующие конфигурации в цепочке OverlayConfig.
- **correction** (default 1) – величина коррекции некоторого показателя на текущем шаге с предыдущего. Чем выше, тем больше различных параметров тестируется, но это занимает больше времени. 
- **minX**, **maxX**, **minY**, **maxY** - допустимые диапазоны координат левого верхнего угла наложения, по умолчанию не ограничены. 
- **minArea**, **maxArea** - диапазон допустимой площади накладываемого изображения, в пикселях. По умолчанию не ограничен.
- **fixedAspectRatio** (default false) - режим точного соотношения сторон накладываемого клипа, только для случая, когда aspectRatio1=aspectRatio2.
- **debug** (default false) – отображение параметров конфигурации, медленно.

### OverlayEngine                  
    OverlayEngine(clip source, clip overlay, string statFile, int backwardFrames, int forwardFrames, 
                  clip sourceMask, clip overlayMask, float maxDiff, float maxDiffIncrease, float maxDeviation, 
                  int scanDistance, float scanScale, bool stabilize, float stickLevel, float stickDistance, 
                  clip configs, string presize, string resize, string rotate, bool editor, string mode, 
                  int colorAdjust, string sceneFile, bool simd, bool debug)

Фильтр принимает на вход два клипа: основной и накладываемый и выполняет процедуру автовыравнивания с помощью изменения размера, вращения и сдвига накладываемого клипа, чтобы найти наименьшее значение diff. Оптимальные параметры наложения кодируются в выходной кадр, чтобы они могли быть считаны другими фильтрами. Последовательность таких параметров наложения кадра за кадром (статистика) может накапливаться в оперативной памяти, либо в файле для повторного использования без необходимости повторно выполнять дорогостоящую процедуру автовыравнивания. Файл статистики может быть проанализирован и отредактирован во встроенном графическом редакторе. 

#### Параметры
- **source** (required) - первый, основной клип.
- **overlay** (required) - второй, накладываемый клип. Оба клипа должны быть в одном и том же типе цветового пространства (YUV или RGB) и глубине цвета. Поддерживаются планарные YUV (8-16 бит), RGB24 и RGB48 цветовые пространства.
- **statFile** (default empty) – путь к файлу со статистикой параметров наложения. Если не задан, то статистика накапливается только в оперативной памяти в пределах одного сеанса. Рекомендуемый сценарий использования: для начального подбора параметров вручную не использовать файл статистики, а использовать для тестового прогона, чтобы собрать статистику, проанализировать и подправить в редакторе. 
- **backwardFrames** and **forwardFrames** (default 3) – количество анализируемых предыдущих и последующих кадров в одной сцене для стабилизации и ускорения поиска параметров наложения. 
- **sourceMask**, **overlayMask** (default empty) – маски для основного и накладываемого клипа. Если маска задана, то пиксели клипа, которым соответствует значение 0 в маске, игнорируются при расчете DIFF. Подходит, к примеру, для исключения логотипа из расчета diff. В RGB клипах каналы анализируются раздельно. В YUV анализируется только канал яркости. Маска должна быть в полном диапазоне (`ColorYUV(levels="TV->PC")`).
- **maxDiff** (default 5) – diff ниже этого значения интерпретируются как успешные. Используется для детектирования сцен. 
- **maxDiffIncrease** (default 1) – максимально допустимое превышение diff текущего кадра от среднего значения в последовательности (сцене).
- **maxDeviation** (default 1) – максимально допустимая разница в процентах между объединением и пересечением двух конфигураций выравнивания для обнаружения сцен. Более высокие значения могут привести к ошибочному объединению нескольких сцен в одну, но обеспечивают лучшую стабилизацию в пределах сцены. 
- **scanDistance** (default 0) – максимально допустимый сдвиг накладываемого изображения между соседними кадрами в сцене. Используется, если источники не стабилизированы относительно друг друга.
- **scanScale** (default 3) – максимально допустимое изменения размера в промилле накладываемого изображения между соседними кадрами в сцене.
- **stabilize** (default true) – попытка стабилизировать кадры в самом начале сцены, когда еще не накоплено достаточное количество предыдущих кадров. Если true, то параметр `panScanDistance` должен быть 0.
- **stickLevel** (default 0) - максимально допустимая разница между значениями DIFF для наилучших параметров наложения и тех, что приведут к приклеиванию накладываемого изображения к границам основного.
- **stickDistance** (default 1) - максимально допустимое расстояние между краями накладываемого изображения для наилучших параметров наложения и тех, что приведут к приклеиванию накладываемого изображения к границам основного.
- **configs** (по умолчанию OverlayConfig со значениями по умолчанию) – список конфигураций в виде клипа. Пример: `configs=OverlayConfig(subpixel=1, acceptableDiff=10) + OverlayConfig(angle1=-1, angle2=1)`. Если в ходе автовыравнивания после прогона первой конфигурации будет получено значение diff менее 10, то следующая конфигурация с более "тяжелыми" параметрами (вращение) будет пропущена. 
- **presize** (default *BilinearResize*) – функция изменения размера изображения для начальных шагов масштабирования.
- **resize** (default *BicubicResize*) – функция изменения размера изображения для финальных шагов масштабирования.
- **rotate** (default *BilinearRotate*) – функция вращения изображения. В настоящее время по умолчанию используется реализация из библиотеки AForge.NET.
- **editor** (default false). Если true, во время загрузки скрипта запустится визуальный редактор. 
- **mode** (default "default") – режим работы со статистикой:  
DEFAULT – по умолчанию
UPDATE – как предыдущий, но DIFF текущего кадра всегда перерассчитывается
ERASE – стереть статистику (используется для очистки информации об определенных кадрах совместно с функцией Trim)
READONLY – использовать, но не пополнять файл статистики
PROCESSED – включить только уже обработанные кадры
UNPROCESSED – включить только необработанные кадры
- **colorAdjust** - not implemented yet
- **sceneFile** - путь к файлу с ключевыми кадрами для обособления сцен во время процесса автовыравнивания изображений
- **simd** (default true) - использование SIMD Library для повышения производительности в некоторых случаях
- **debug** (default false) - отображение параметров наложения, снижает производительность

#### Принцип работы
*OverlayEngine* ищет оптимальные параметры наложения: координаты верхнего левого угла накладываемого клипа относительного основного, угол вращения в градусах, ширина и высота накладываемого изображения, а также величины обрезки накладываемого изображения по краям для субпиксельного позиционирования.  
Цепочка *OverlayConfig* в виде клипа описывает границы допустимых значений и алгоритм поиска оптимальных. Движок прогоняет каждую конфигурацию друг за другом пока не будут найдены параметры наложения с приемлемым diff. Процесс автовыравнивания для каждой конфигурации содержит несколько шагов. На первом шаге тестируются все возможные комбинации параметров наложения в низком разрешении. На следующий шаг передается некоторое количество наилучших комбинаций, задаваемых параметром `OverlayConfig.branches`. На каждом следующем шаге параметры наложения конкретизируются в более высоком разрешении, область поиска задается параметром *correction*.
Масштабирование изображений выполняется функциями, заданными в параметрах *presize* and *resize*. Первый используется на предварительных шагах автовыравнивания, второй на финальных, когда работа ведется в полном разрешении. На финальных шагах рекомендуется использовать фильтр с хорошей интерполяцией. Функция масштабирования должно иметь следующую сигнарутуру: `Resize(clip clip, int target_width, int target_height, float src_left, float src_top, float src_width, float src_height)`. Допускаются дополнительные параметры. Такая же сигнатура используется в стандартных функциях AviSynth. Крайне рекомендуется использовать плагин ResampleMT, который дает тот же результат, что и встроенные фильтры, но работает значительно быстрее за счет параллельных вычислений.  
В ходе автовыравнивания движок может анализировать соседние кадры с помощью параметров *backwardFrames* and *forwardFrames* parameters according to *maxDiff, maxDiffIncrease, maxDeviation, stabilize, panScanDistance, panScanScale* по следующему алгоритму:  
1. Требуется предоставить параметры наложения текущего кадра.
2. Если в статистике уже есть данные по кадру, возврат из кэша. 
3. Если предыдущие кадры в количестве *backwardFrames* уже обработаны и их параметры наложения одинаковы, а значения diff не превышают *maxDiff*, то будут протестированы такие же параметры наложения и для текущего кадра.
4. Если полученный diff не превышает *maxDiff* и среднее значение сцены более чем на *maxDiffIncrease*, то запускается анализ последующих кадров в количестве *forwardFrames*, иначе кадр будет отмечен как начало новой сцены.
5. Последующие кадры тестируются так же, как и текущий. Если все они подходят, то текущий кадр точно останется в сцене. 
6. Если один из последующих кадров не подойдет, будет запущен процесс автовыравнивания для этого кадра. Если полученные оптимальные параметры наложения не сильно отличаются от текущих, текущий кадр не будет включен в сцену, т.к. возможно тоже слегка смещен относительно предыдущего. Этот процесс регулируется параметром *maxDeviation*.
7. Если текущий кадр отмечен как независимый и включен параметр *stablize=true*, будет произведена попытка выровнять первые *backwardFrames* кадров одинаковым образом, чтобы начать новую сцену. 
8. Если параметр *backwardFrames* равен нулю, каждый кадр обрабатывается индивидуально. Это занимает больше времени и может вызвать дрожание картинки.
9. Если источники не стабилизированы относительно друг друга, необходимо использовать *panScanDistance* и *panScanScale*. 

##### Визуальный редактор
Запускается, если *OverlayEngine.editor*=true.  
Слева превью кадра. Внизу трекбар по количеству кадров и поле ввода текущего кадра. Справа таблица, отображающая кадры с одинаковыми параметрами наложения, объединенные в эпизоды. Между эпизодами можно переключаться. Под гридом панель управления.  
Overlay settings - параметры наложения текущего эпизода. 

Ниже секция *Frame processing*.
Align - автоналожение "с нуля" без учета других кадров
Adjust - коррекция каждого сцены относительно текущего кадра (или первого кадра сцены, если текущий кадр в сцену не входит) с учетом Distance (отклонение X и Y), Scale (отклонение произведения Width и Height) и Max deviation (отклонение площади пересечения от площади объединения кадров)
Scan - режим последовательного сканирования относительно предыдущего кадра, начиная с первого кадра сцены с учетом Distance, Scale и Max deviation

Frame - обработка текущего кадра (в случае Align если сцена Fixed, изменятся параметры всей сцены)
Single - обработка текущего кадра, если сцена Fixed и параметры наложения изменятся, кадр будет вынесен в отдельную сцену
Scene - обработа текущей сцены (текущая строка в таблице)
Clip - обработка всех сцен в таблице

Измененные и несохраненные эпизоды подсвечиваются желтым цветом в гриде. Кнопка save - сохранение изменений. Reset - сброс изменений и повторная загрузка данных. Reload - перезагрузка характеристик для текущего кадра, распространяющиеся на весь эпизод.  
Separate - обособление кадра. Join prev - присоединить кадры предыдущего эпизода. Join next - присоединить кадры следующего эпизода. Join to - присоединить кадры до введенного включительно.  

**Hotkeys**:
* Ctrl + S - save
* Ctrl + R - reload
* D - enable/disable difference
* P - enable/disable preview
* Ctrl + arrow keys - move overlay image
* Ctrl + add/subtract - scale overlay image
* A, Z - next/previous frame

### OverlayRender
    OverlayRender(clip engine, clip source, clip overlay, clip sourceMask, clip overlayMask, clip extraClips, 
                  clip innerBounds, clip outerBounds, float overlayBalanceX, float overlayBalanceY, bool fixedSource, 
                  int overlayOrder, string overlayMode, int width, int height, string pixelType, int gradient, int noise, bool dynamicNoise, 
                  int borderControl, float borderMaxDeviation, clip borderOffset, clip srcColorBorderOffset, clip overColorBorderOffset, 
                  bool maskMode, float opacity, float colorAdjust, string colorInterpolation, float colorExclude, 
                  int colorFramesCount, float colorFramesDiff, float colorMaxDeviation, string adjustChannels, string matrix, 
                  string upsize, string downsize, string rotate, bool simd, bool debug, bool invert, bool extrapolation, 
                  string background, clip backgroundClip, int blankColor, float backBalance, int backBlur, 
                  bool fullScreen, string edgeGradient, int bitDepth)
                  
Фильтр осуществляет рендеринг результата совмещения двух клипов с определенными настройками.

#### Параметры
- **engine** (required) - клип типа *OverlayEngine*, который предоставляет параметры наложения.
- **source** (required) - первый, основной клип.
- **overlay** (required) - второй клип, накладываемый на первый. Поддерживаются планарные YUV (8-16 бит), RGB24 и RGB48 цветовые пространства.
- **sourceMask** and **overlayMask** (default empty) - маски основного и накладываемого клипа. В отличие от OverlayEngine смысл этих масок такой же, как в обычном фильтре *Overlay*. Маски регулируют интенсивность наложения клипов относительно друга друга. Маски должны иметь ту же разрядность, что и накладываемый клип.
- **extraClips** (default empty) - клип из склеенных клипов типа OverlayClip, описывающих дополнительные клипы для наложения.
- **innerBounds** (default 0) - клип типа Rect, ограничивающий длину пустот внутри объединения клипов. Значения в диапазоне от 0 до 1 интерпретируются как коэффициент, а свыше 1 как абсолютное значение в пикселях от объединенной области.
- **outerBounds** (default 0) - клип типа Rect, ограничивающий длину полей относительно результирующего клипа. Значения в диапазоне от 0 до 1 интерпретируются как коэффициент, а свыше 1 как абсолютное значение в пикселях. 
- **overlayBalanceX** (default 0) - центрирование изображения по ширине относительно основного клипа (-1) или накладываемого (1) в диапазоне от -1 до 1.
- **overlayBalanceY** (default 0) - центрирование изображения по высоте относительно основного клипа (-1) или накладываемого (1) в диапазоне от -1 до 1.
- **fixedSource** (default false) - фиксированное центрирование результирующего клипа относительно основного.
- **overlayOrder** (default 0) - номер слоя для накладываемого клипа. Позволяет наложить клип после дополнительных. 
- **overlayMode** (default `blend`) – режим наложения для встроенного фильтра `Overlay`. Для оценки результата наложения рекомендуется использовать `difference`.
- **width** и **height** - ширина и высота выходного изображения. По умолчанию соответствует основному клипу.
- **pixelType** - цветовое пространство результирующего клипа, должен соответствовать типу цветового пространства накладываемых клипов (YUV или RGB). По умолчанию используется цветовое пространство основного клипа. 
- **gradient** (default 0) - длина прозрачного градиента в пикселях по краям накладываемой области. Делает переход между изображениями более плавным.
- **noise** (default 0) - длина градиента шума в пикселях по краям накладываемой области. Делает переход между изображениями более плавным.
- **dynamicNoise** (default true) - динамический шум по краям изображения от кадра к кадру, если *noise* > 0.
- **borderControl** (default 0) – количество соседних кадров в обе стороны для анализа какие стороны маски наложения должны быть включены для текущего кадра с учетом параметра *borderOffset*.
- **borderMaxDeviation** (default 0.5) – максимальное отклонение общей площади текущего и соседнего кадра для использования в последовательности кадров при создании маски наложения.
- **borderOffset** (default empty) - клип типа *Rect* для задания "пустых" границ изображения (left, top, right, bottom), которые будут проигнорированы при расчете градиентной маски.
- **srcColorBorderOffset** (default empty) - (не реализовано) клип типа *Rect* для определения "пустых" границ основного клипа (left, top, right, bottom), которые будут проигнорированы при цветокоррекции.
- **overColorBorderOffset** (default empty) - (не реализовано) клип типа *Rect* для определения "пустых" границ накладываемого клипа (left, top, right, bottom), которые будут проигнорированы при цветокоррекции.
- **maskMode** (defualt false) - если true, замещает все клипы белой маской.
- **opacity** (default 1) - степень непрозрачности накладываемого изображения от 0 до 1.
- **colorAdjust** (default -1, disabled) - вещественное значение между 0 и 1. 0 - стремление к цвету основного клипа. 1 - накладываемого клипа. 0.5 - усредненный цвет. С дополнительными клипами поддерживаются только значения -1, 0, 1. Цветокоррекция основана на сравнении гистограмм области пересечения.
- **colorInterpolation** (default linear) - см. ColorAdjust.interpolation
- **colorExclude** (default 0) - см. ColorAdjust.exclude
- **colorFramesCount** (default 0) - количество соседних кадров в обе стороны, информация о которых включается в построение карты соответствия цветов для цветокоррекции
- **colorFramesDiff** (default 1) -  максимальное среднеквадратическое отклонение гистограмм разницы цветов сэмпла и образца от текущего кадра к соседним для цветокоррекции
- **colorMaxDeviation** (default 1) -  максимальное отклонение общей площади текущего и соседнего кадра для использования в последовательности кадров при цветокоррекции
- **adjustChannels** (default empty) - в каких каналах регулировать цвет. Примеры: "yuv", "y", "rgb", "rg".
- **matrix** (default empty). Если параметр задан, YUV изображение конвертируется в RGB по указанной матрице на время обработки.
- **downsize** и **upsize** (default *BicubicResize*) - функции для уменьшения и увеличения размера изображений. Если задан только один параметр, то второй заполняется тем же значением. 
- **rotate** (default *BilinearRotate*) - функция вращения накладываемого изображения.
- **simd** (default *true*) – использование SIMD Library для повышения производительности в некоторых случаях
- **debug** - вывод параметров наложения.
- **invert** - поменять местами основной и накладываемый клипы, "инвертировать" параметры наложения. 
- **extrapolation** - см. ColorAdjust.extrapolation.
- **background** (default blank) - способ заполнения фона: blank (сплошная заливка), blur (растянутое изображение с заливкой), inpaint (не реализовано). 
- **backgroundClip** (default empty) - если указан, клип используется в качестве фона, должен иметь то же разрешение, что и результирующий клип.
- **blankColor** (default `0x008080` для YUV и `0x000000` для RGB) - цвет в HEX формате для заполнения пустот.
- **backBalance** - вещественное значение в диапазоне от -1 до 1 для задания источника заблюренного фона, если background равен blur. -1 - основной клип, 1 - накладываемый клип.
- **backBlur** (default 15) - сила смазывания, если background равен blur.
- **fullScreen** (default false) - фоновое изображение заполняется на всю площадь изображения, а не только на область объединения изображений.
- **edgeGradient** (default none) - градиент на границах изображений. none - отключено, inside - только внутри объединенной области, full - везде. 
- **bitDepth** (default unused) - глубина цвета выходного изображения, а также входящих после трансформаций, но перед цветокоррекцией, для ее улучшения

### ColorAdjust
    ColorAdjust(clip sample, clip reference, clip sampleMask, clip referenceMask, bool greyMask,
                float intensity, int seed, int adjacentFramesCount, float adjacentFramesDiff, 
	            bool limitedRange, string channels, float dither, float exclude, string interpolation, 
				bool extrapolation, bool dynamicNoise, bool simd, int threads, bool debug)

Автокоррекция цвета. Входной клип, sample и reference клипы должны быть в одном типе цветового диапазона (YUV or RGB). Поддерживаются любые планарные цветовое диапазоны YUV (8-16 bit), RGB24 и RGB48. Входной клип и sample клип должны иметь одинаковую глубину цвета (обычно sample - это весь входной фильтр или его часть). Глубина цвета входного фильтра изменится на глубину цвета клипа reference. Фильтр дает хороший результат только если sample и reference клипы содержат схожее наполнение кадра. Фильтр используется внутри OverlayRender, но может использоваться и независимо. 

#### Параметры
- **clip** (required) - входной клип, цвет которого будет откорректирован
- **sample** (required) - клип, сравнивающийся с клипом-образцом (обычно входной клип, может применяться crop) 
- **reference** (required) - клип-образец с тем же наполнением, что и sample
- **sampleMask** and **referenceMask** (default empty) - 8 битные планарные маски для включения в обработку только участков изображений, значение маски для которых равно 255.
- **greyMask** (default true) - маска только по яркости или по всем каналам
- **intensity** (default 1) - интенсивность цветокоррекции
- **seed** (default is constant) - seed для дизеринга, если фильтр используется многократно для рендеринга одного кадра
- **adjacentFramesCount** (default 0) - количество соседних кадров в обе стороны, информация о которых включается в построение карты соответствия цветов
- **adjacentFramesDiff** (default 1) - максимальное среднеквадратическое отклонение гистограмм разницы цветов сэмпла и образца от текущего кадра к соседним. 
- **limitedRange** (default true) - ТВ диапазон
- **channels** (default yuv or rgb) - плоскости или каналы для обработки. Допустимы любые комбинации y,u,v или r,g,b (пример: y, uv, r, br).
- **dither** (default 0.95) - уровень дизеринга 0 (disable) to 1 (aggressive). 
- **exclude** (default 0) - исключение редко встречающихся в изображениях цветов по формуле: *current_color_pixel_count / total_pixel_count < exclude*.
- **interpolation** (default linear) - алгоритм интерполяции из библиотеки Math.NET Numerics (spline, akima, linear, none).
- **extrapolation** (default false, experimental) - экстраполяция цветов, выходящих за границы сэмплов.
- **dynamicNoise** (default true) - динамический шум, если цветовая карта совпадает у нескольких кадров.
- **simd** (default true) - использование SIMD Library для повышения производительности в некоторых случаях
- **threads** (.NET default) - максимальное число потоков

### ComplexityOverlay
    ComplexityOverlay(clip source, clip overlay, string channels, int steps, float preference, bool mask, 
                      float smooth, int threads, bool debug)
    
Независимый фильтр для совмещения наиболее сложных участков двух клипов. Подходит для совмещения двух источников низкого качества. Клипы должны иметь одинаковые кадрирование, цвет, разрешения и цветовые пространства. 

#### Parameters
- **source** and **overlay** - входные клипы
- **channels** (default yuv or rgb) - плоскости или каналы для обработки. Допустимы любые комбинации y,u,v или r,g,b (пример: y, uv, r, br).
- **steps** (default 1) - количество шагов формирования маски совмещения. 
- **preference** (default 0) - если больше ноля 0 второй клип будет более предпочтителен, иначе первый клип. Рекомендуется: -1 to 1. 
- **mask** (default false) - выводить маску наложения вместо совмещения. 
- **smooth** (default 0) - смазать маску наложения для снижения резкости.
- **threads** (.NET default) - максимальное число потоков

### ComplexityOverlayMany
    ComplexityOverlayMany(clip source, clip[] overlays, string channels, int steps, int threads, bool debug)
	
Аналогичен ComplexityOverlay, но позволяет объединить произвольное количество клипов. 

### OverlayCompare
    OverlayCompare(clip engine, clip source, clip overlay, string sourceText, string overlayText, int sourceColor, 
                   int overlayColor, int borderSize, float opacity, int width, int height, bool debug)
Фильтр позволяет визуализировать совмещение двух клипов.

#### Parameters
- **engine** (required) - *OverlayEngine* clip.
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip.
- **sourceText** (default "Source") - source clip name.
- **overlayText** (default "Source") - overlay clip name.
- **sourceColor** (default 0x0000FF) - source clip border color.
- **overlayColor** (default 0x00FF00) - overlay clip border color.
- **borderSize** (default 2) - border size.
- **opacity** (default 0.51) - opacity of overlay clip.
- **width** (source clip width by default) - output width.
- **height** (source clip height by default) - output height.
- **debug** (default false) - print align settings. 

### StaticOverlayRender
    StaticOverlayRender(clip source, clip overlay, float x, float y, float angle, float overlayWidth, float overlayHeight,
                        string warpPoints, float diff, clip sourceMask, clip overlayMask,
                        clip innerBounds, clip outerBounds, float overlayBalanceX, float overlayBalanceY, bool fixedSource,
                        int overlayOrder, string overlayMode, int width, int height, string pixelType, int gradient, int noise, bool dynamicNoise,
                        int borderControl, float borderMaxDeviation, clip borderOffset, clip srcColorBorderOffset, clip overColorBorderOffset,
                        bool maskMode, float opacity, float colorAdjust, string colorInterpolation, float colorExclude,
                        int colorFramesCount, float colorFramesDiff, float colorMaxDeviation, string adjustChannels, string matrix,
                        string upsize, string downsize, string rotate, bool simd, bool debug, bool invert, bool extrapolation,
                        string background, clip backgroundClip, int blankColor, float backBalance, int backBlur,
                        bool fullScreen, string edgeGradient, int bitDepth)

Аналогичен OverlayRender, но без OverlayEngine, параметры совмещения клипов задаются вручную.

### CustomOverlayRender
    CustomOverlayRender(clip engine, clip source, clip overlay, string function, int width, int height, bool debug)
	
Фильтр позволяет визуализировать результат наложения с помощью пользовательский функции с параметрами `clip engine, clip source, clip overlay, int x, int y, float angle, int overlayWidth, int overlayHeight, float diff)`

#### Parameters
- **engine** (required) - *OverlayEngine* clip.
- **source** (required) - first, base clip.
- **overlay** (required) - second, overlaid clip.
- **function** (required) - user function name. The function must have the following parameters: 
- **width** (source clip width by default) - output clip width.
- **height** (source clip height by default) - output clip height.
- **debug** (default false) - debug mode.

### OverlayClip
    OverlayClip(clip clip, clip mask, float opacity, bool debug)
	
Вспомогательный фильтр, позволяющий указать дополнительный клип, маску и уровень прозрачности для OverlayRender.

### Rect
    Rect(float left, float top, float right, float bottom, bool debug)
    
Вспомогательный фильтр, позволяющий указать параметры для левой, верхней, правой и нижней части изображения соответственно. Если указан только left, то его значение распространяется на все. Если left и top, то right и bottom будут равны им же соответственно. 

### ColorRangeMask
    ColorRangeMask(clip, int low, int high, bool greyMask)
Support filter which provides mask clip by color range: white if pixel value is between low and high arguments. For YUV clips only luma channel is used. For RGB clips all channels are proccessed independently. Output is the clip in the same color space. Limited range is not supported. 

#### Parameters
- **input** (required) - input clip.
- **low** (default 0) - lower bound of color range.
- **high** (default 0) - higher bound of color range.
- **greyMask** (default true) - маска только по яркости или по всем каналам

### BilinearRotate
    BilinearRotate(clip, float)
Support filter for rotation by angle with bilinear interpolation.

#### Parameters
- **input** (required) - input clip.
- **angle** (required) - rotation angle.

### OverlayMask
    OverlayMask(clip template, int width, int height, 
                int left, int top, int right, int bottom, 
                bool noise, bool gradient, int seed)
Support filter which provides mask clip for overlay with gradient or noise at borders.

#### Parameters
- **template** (default empty) - if specified width, height and color space will be used from template clip for output.
- **width** - output clip width if template is not specified. 
- **height** - output clip height if template is not specified.
- **left**, **top**, **right**, **bottom** - border size.
- **noise** - noise generation on borders.
- **gradient** - gradient borders.
- **seed** - seed for noise generation.

### ExtractScenes
	ExtractScenes(string statFile, string sceneFile, int sceneMinLength, float maxDiffIncrease)
Filter to extract and save scene key frames to text file based on stat file of aligning target clip to the target.Trim(1,0) clip. 

#### Parameters
- **statFile** - stat file path
- **sceneFile** - scene file path
- **sceneMinLength** (default 10) - scene minimal length
- **maxDiffIncrease** (default 15) - scene detection DIFF value

## User functions
В дополнение к перечисленным выше фильтрам в файле OverlayUtils.avsi определены пользовательские функции.
Они предназначены для облегчения процесса синхронизиации двух клипов и подготовки исходников для автовыравнивания.

### aoShift
    aoShift(clip clp, int pivot, int length)
Сдвигает кадры, начиная с номера 'pivot', с удалением предыдущих или вставки пустых в зависимости от направления.
Положительное значение 'length' - сдвиг вправо со вставкой пустых кадров перед 'pivot'.
Отрицаительное значение 'length' - сдвиг влево с удалением кадров перед 'pivot'.

### aoDelay
    aoDelay(clip clp, int length)
Частый случай 'aoShift' для вставки или удаления кадров в начале клипа.
Положительное значение 'length' - сколько пустых кадров вставить.
Отрицаительное значение 'length' - сколько кадров выкинуть.

### aoDelete
    aoDelete(clip clp, int start, int end)
Удаление последовательности кадров от 'start' до 'end' включительно.

### aoReplace
    aoReplace(clip clp, clip replace, int start, int "end")
Замена последовательноси кадров аналогичной из другого (синхронизированного) клипа.
Параметр 'end' равен параметру 'start' по умолчанию, удобно для замены только одного кадра.
Явно указанный ноль в параметре 'end' заменяется на последний кадр.

### aoOverwrite
    aoOverwrite(clip clp, clip scene, int frame)
Вставка другого клипа 'scene', начиная с кадра 'frame' текущего с перезаписью.

### aoInsert
    aoInsert(clip clp, clip insert, int start, int "end")
Вставка без перезаписи фрагмента из другого клипа, начиная с позиции 'start' в обоих клипах, заканчивая кадром 'end' во вставляемом клипе. 
Параметр 'end' равен 'start' по умолчанию (вставка одного кадра).
Явно указанный ноль в параметре 'end' заменяется на последний кадр.

### aoTransition
    aoTransition(clip prev, clip next, int transitionStart, int transitionEnd, 
	             int "reverseTransitionStart", int "reverseTransitionEnd")
Плавный переход между синхронизированными клипами. 
transitionStart - начальный кадр перехода, включительно
Положительное значение 'transitionEnd' - последний кадр перехода, включительно
Отрицательное значение 'transitionEnd' - продолжительность перехода в кадрах, включительно
reverseTransitionStart - начальный кадр обратного перехода, по умолчанию обратный переход отсутствует
reverseTransitionEnd - последний кадр или длина обратного перехода, по умолчанию длина обратного перехода равна длине прямого

### aoTransitionScene
    aoTransitionScene(clip prev, clip next, int start, int end, int "length")
Плавный переход указанной длины 'length' между синхронизированными клипами от 'start' до 'end' для замены фрагмента в исходном клипе, частный случай aoTransition.

### aoBorders
    aoBorders(clip clp, int left, int top, int "right", int "bottom", 
	          int "refLength", int "segments", float "blur", float "dither")
Исправление цветового уровня около границ кадра с использованием фильтра ColorAdjust.
Параметры left, top, right, bottom определяют сколько пикселей от границ необходимо обработать.
Параметры right и bottom по умолчанию равны left и top соответственно.
refLength (default 1) задает ширину/высоту области в непосредственной близости от корректируемой, которая будет использоваться как образец цвета.
segments (default 1) позволяет разбить границы на сегменты и обработать каждый из них отдельно с плавными переходами, чтобы избежать ошибок цветокоррекции на большей площади кадра, особенно если кадр содержит много объектов. 
blur (default 0, max 1.5) смазывает ближайшие к границам пиксели для уменьшения шума
dither - уровень дизеринга для фильтра ColorAdjust

### aoInvertBorders
    aoInvertBorders(clip clp, int left, int top, int "right", int "bottom")
Инвертирует цвета по границам кадра, подходит для создания масок.
В YUV клипе обрабатывается только яркость. 
Параметры right и bottom по умолчанию равны left и top соответственно.

### aoInterpolate
    aoInterpolate(clip clp, int length, int "start", int "end", int "removeGrain")
Интерполирует последовательность кадров клипа от 'start' до 'end' включительно, уменьшая или увеличивая их количество с помощью плагина MVTools.

### aoInterpolateScene
    aoInterpolateScene(clip clp, int inStart, int inEnd, int outStart, int outEnd, int "removeGrain")
Интерполирует последовательность кадров клипа от 'inStart' до 'inEnd' включительно, перезаписывая результатом кадры от 'outStart' до 'outEnd' с помощью плагина MVTools.

### aoInterpolateOne
    aoInterpolateOne(clip clp, int frame, bool "insert", int "removeGrain")
Вставляет (по умолчанию) или заменяет один кадр интерполируемым из соседних с помощью плагина MVTools.

### aoDebug
    aoDebug(clip clp)
Функция для дебага остальных функций пакета. 
Для функции aoReplace будут удалены все кадры кроме заменяемых.

### aoExpand
    aoExpand(clip mask, int pixels, string mode, float "blur")
	
Expand the black mask (mode=darken) or white mask (mode=lighten)

## Examples
#### Simple script 
    OM=AviSource("OpenMatte.avs")
    WS=AviSource("Widescreen.avs")
    OverlayEngine(OM, WS, configs = OverlayConfig(subpixel=2)) 
    OverlayRender(OM, WS, debug = true)
#### Three clips 
    OM=AviSource("OpenMatte.avs")
    WS=AviSource("Widescreen.avs")
	FS=AviSource("Fullscreen.avs")
    wsEngine=OverlayEngine(OM, WS)
	fsEngine=OverlayEngine(OM, FS)
    wsEngine.OverlayRender(OM, WS, extraClips=fsEngine.OverlayClip(FS))
#### Analysis pass without render
    OM=AviSource("OpenMatte.avs")
    WS=AviSource("Widescreen.avs")
	# Aspect ratio range was specified
    config=OverlayConfig(aspectRatio1=2.3, aspectRatio2=2.5)
	# Set editor=true after analysis pass to edit align params
    OverlayEngine(OM, WS, configs = config, statFile="Overlay.stat", editor=false)
	# Uncomment to render aligned clips
	# OverlayRender(OM, WS, debug=true, noise=50, upsize="Spline64Resize")
	
## Варианты использования
Описание будет позже. 
### Наложение источника лучшего качества с другим наполнением кадра

### Совмещение источников с одинаковым наполнением кадра для получения результата лучшего качества

### Максимизация наполнения кадра

### Автоматическая цветокоррекция

### Конвертация HDR в SDR

### Конвертация SDR в HDR

### Удаление логотипа

### Сравнение клипов

### Поиск "битых" кадров

### Исправление границ кадра

### Обнаружение сцен

## История изменений
### 01.12.2023 v0.6
1. OverlayRender: упразднен параметр mode
2. OverlayRender: добавлены параметры extraClips, innerBounds, outerBounds, overlayBalanceX, overlayBalanceY, fixedSource, overlayOrder, maskMode, colorInterpolation, colorExclude, backgroundClip, backBalance, fullScreen, edgeGradient.
3. OverlayRender: параметр background заменен на backBalance, а назначение background изменено.
4. OverlayRender: добавлена поддержка наложения произвольного количества клипов.
5. OverlayEngine: параметры panScanDistance и panScanScale переименованы в scanDistance и scanScale.
6. Добавлен фильтр ComplexityOverlayMany.

### 17.01.2022 v0.5.2
1. OverlayRender: исправлен mode=5
2. ExtractScenes: исправлен тип параметра
3. OvrelayEditor: исправлено редактирование параметров warp
4. OverlayEngine: исправлено использование параметров minArea и maxArea
5. AvsFilterNet: лучшая совместимость с AviSynth 3.7.1

### 29.12.2021 v0.5.1
1. .NET 4.7, latest C# level
2. Возможность отключения интерполяции цвета
3. Маска только по каналу яркости теперь опциональна
4. Настройка максимального количества потоков для некоторых фильтров
5. Улучшенный алгоритм исключения редко встречающихся цветов
6. Исправлена внутренняя конвертация в RGB для цветокоррекции
7. Параметры stick level & stick distance для приклеивания накладываемого изображения в границам основного по возможности
8. OverlayEngine: исправлен кэш повторения
9. Overlay editor: мелкие исправления
10. aoDebug function

### 04.09.2021 v0.5.0
1. Пакет пользовательских функций
2. Render: исправление работы с RGB HDR клипами
3. OverlayMask: поддержка HDR
4. Render: новый параметр seed

### 28.08.2021 v0.4.3
1. Editor: редактирование параметра maxDiff.
2. Editor: чекбокс "Defective" теперь имеет три состояния.
3. Engine: режим "только необработанные кадры".
4. Engine: исправлен вывод фейковых кадров.
5. Core: исправлено уничтожение фильтров.
6. Render: исправлена работа с вращением.

### 29.05.2021 v0.4.2
1. Исправлен асинхронный рендеринг кадра в редакторе. 
2. Исправлено использование масок с истчниками с цветовой субдискретизацией.

### 28.03.2021 v0.4.1
1. OverlayEngine: исправлено одновременное использование масок для обоих источников. Для расчета значения DIFF используются пиксели, которым соответствует значение маски 255 или 65535 в зависимости от глубины цвета.
2. OverlayRender: исправлено использование маски без параметров gradient и noise.
3. Прочие минорные исправления и рефакторинг.

### 17.01.2021 v0.4.0
1. Исправлено кэширование клипов, создаваемых внутри плагина для ренедеринга: заметное увеличение скорости работы особенно для "тяжелых" входных клипов.
2. ColorAdjust: анализ соседних кадров для более стабильной цветокррекции (параметр AdjacentFramesCount и AdjacentFramesDiff), особенно при экстраполяции вне области пересечения + общее ускорение работы.
3. ColorAdjust: исправлена обработка однотонных кадров. 
4. OverlayRender: новый алогритм создания маски наложения по границам накладываемого изображения, когда одновременно используются параметры gradient и noise, для более плавного перехода между изображениями. Оптимально использовать значения 50-100 для обоих параметров.  
5. OverlayRender: параметры BorderControl и BorderMaxDeviation для анализа соседних кадров, чтобы стабилизровать маску наложения в течение сцены.
6. OverlayRender: параметры ColorFramesCount, ColorFramesDiff и ColorMaxDeviation для более качественной цветокоррекции в течение сцены.
7. OverlayRender: экспериментальный параметр BitDepth для изменения глубины цвета выходного клипа, а также входных после применения трансформаций. Используется для более качественной цветокоррекции.  
8. ExtractScenes: новый фильтр для детектирования сцен на основе файла статистики.
9. OverlayConfig: параметры WarpPoints, WarpSteps и WarpOffset для "warp" (более гибкие, чем афинные) преобразований изображения с помощью фильтра warp от wonkey_monkey. Поддерживается только 8 битная глубина цвета. 
10. OverlayEngine: параметр SceneFile для использования текстового файла с номерами ключевых кадров сцен для обосбления сцен во время анализа соседних кадров. 
11. Исправлена визулизация исключений. 
12. OverlayEditor: асинхронный рендеринг, новая форма для пакетных операций. 
13. OverlayEditor: новая секция "Frame processing".
14. OverlayEditor: ускорение работы с большим файлом статистики.
15. OverlayEditor: количество записей в гриде по умолчанию увеличено до 2000.
16. OverlayEditor: более интутивно понятная обработка сцен. Кадры объединяются в сцены с помощью параметра "max deviation". 
17. OverlayEditor: новый режим обработки сцен "adjust". Кадры текущей сцены корректируются вокруг выбранного кадра, используя параметры Distance (максимальная величина сдвига) и Scale (коэффициент мастабирования).
18. OverlayEditor: изменен режим обработки сцен "Scan": корректируются кадры, начиная от первого в текущей сцене до последнего, используя warp параметры текущего кадра.
19. OverlayEditor: поле "warp" для настройки точек трансформаций. 
20. OverlayEngine: новая версия файла статистики для поддержки точек warp трансформаций (максимум 16 штук).

### 29.08.2020 v0.3.1
1. Исправлена ошибка кодирования x264 первого кадра.
2. ColorAdjust: исправлена HDR экстраполяция, параметр dynamicNoise.
3. OverlayRender: параметр extrapolation.

### 28.08.2020 v0.3
1. OverlayEngine: presize и resize вместо upsize и downsize.
2. OverlayEngine: новый режим PROCESSED.
3. OverlayEngine: поддержка пансканирования (нестабилизированных источников). 
4. Использование SIMD Library для повышения производительности. 
5. ColorAdjust: новые алгоритмы интерполяции с помощью Math.NET Numerics.
6. AviSynth API v8 
7. OverlayEditor: new features. 
8. OverlayRender: new features.
9. ComplexityOverlay: new filter.
