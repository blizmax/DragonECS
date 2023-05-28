<p align="center">
<img width="660" src="https://github.com/DCFApixels/DragonECS/assets/99481254/176e13c8-20c8-4e7a-8eaf-e8f3ca590231.png">
</p>

# DragonECS - C# Entity Component System Framework

> **ВАЖНО!** Проект в стадии разработки. API может меняться.

## Оглавление
* [Установка](#Установка)
  * [Unity-модуль](#Unity-модуль)
  * [В виде иходников](#В-виде-иходников)
* [Основные концепции](#Основные-концепции)
  * [Entity](#Entity)
  * [Component](#Component)
  * [System](#System)
* [Концепции фреймворка](#Концепции-фреймворка)
  * [Pipeline](#Pipeline)
  * [Процесс](#Процесс)
  * [Группа](#Группа)
  * [Субъект](#Субъект)
  * [Запрос](#Запрос)
* [Debug](#Debug)
  * [Атрибуты](#Атрибуты)
  * [EcsDebugUtility](#EcsDebugUtility)
  * [Debug-Сервис](#Debug-Сервис)
* [Расширения](#Расширения)

# Установка
* ### Unity-модуль
Поддерживается установка в виде Unity-модуля в  при помощи добавления git-URL [в PackageManager](https://docs.unity3d.com/2023.2/Documentation/Manual/upm-ui-giturl.html) или ручного добавления в `Packages/manifest.json`: 
```
https://github.com/DCFApixels/DragonECS.git 
```
* ### В виде иходников
Фреймворк так же может быть добавлен в проект в виде исходников.

# Основные концепции
## Entity
Сущности - это то к чему крепятся данные. Реализованны в виде идентификаторов, которых есть 2 вида:
* `int` - однократный идентификатор, применяется в пределах одного тика. Не рекомендуется хранить `int` идентификаторы, в место этого используйте `entlong`;
* `entlong` - долговременный идентификатор, содержит в себе полный набор информации для однозначной идентификации;

## Component
Компоненты - это данные для сущностей.  Обязаны реализовывать интерфейс IEcsComponent или другой указываюший вид компонента. 
```c#
struct Health : IEcsComponent
{
    public float health;
    public int armor;
}
```
### Виды компонентов
* `IEcsComponent` - Компоненты с данными.
* `IEcsTagComponent` - Компоненты-теги. Без данных.

## System
Системы - это основная логика, тут задается поведение сущьностей. Существуют в виде пользовательских классов, реализующих как минимум один из IEcsInitProcess, IEcsDestroyProcess, IEcsRunProcess интерфейсов.
```c#
class UserSystem : IEcsPreInitProcess, IEcsInitProcess, IEcsRunProcess, IEcsDestroyProcess
{
    public void PreInit (EcsSession session) {
        // Будет вызван один раз в момент работы EcsSession.Init() и до срабатывания IEcsInitProcess.Init()
    }
    public void Init (EcsSession session) {
        // Будет вызван один раз в момент работы EcsSession.Init() и после срабатывания IEcsPreInitProcess.PreInit()
    }
    public void Run (EcsSession session) {
        // Будет вызван один раз в момент работы EcsSession.Run().
    }
    public void Destroy (EcsSession session) {
        // Будет вызван один раз в момент работы EcsSession.Destroy()
    }
    //Для реализации дополнительных процессов используйте Раннеры
}
```
# Концепции фреймворка
## Pipeline
Является контейнером и двжиком систем, определяя поочередность их вызова, предоставляющий механизм для сообщений между системами и механизм внедрения зависимостей в системы.

## Процесс
Процессы - это очереди систем реализующие общий интерфейс. Раннеры запускюат выполнение процессов. Система раннеров и процессов может использоваться для создания реактивного поведения или для управления очередью вызова систем. Встроенные процессы вызываются автоматически, для ручного запуска испольщуйте раннеры получаемые из EcsPipeline.GetRunner<TInterface>().
> Метод GetRunner относительно медленный, поэтому рекомендуется кешировать полученные раннеры.

Встроенные процессы:
* `IEcsPreInitProcess`, `IEcsInitProcess`, `IEcsRunProcess`, `IEcsDestroyProcess` - процессы жизненого цикла Pipeline
* `IEcsPreInject`, `IEcsInject<T>` - процессы системы внедрения зависимостей для Pipeline. Через них прокидываются зависимости
* `IEcsPreInitInjectProcess` - Так же процесс системы внедрения зависимостей, но работает в пределах до выполнения IEcsInitProcess, сигнализирует о инициализации предварительных внедрений и окончании.

### Пользовательские Раннеры и Процессы
Для добавления нового процесса создайте интерфейс наследованный от IEcsSystem и создайте раннер для него. Раннеры это классы реализующие интерфейс запускаемого процесса и наследуемые от EcsRunner<TInterface>. Пример реализации раннера для IEcsRunProcess:
 ```c#
public sealed class EcsRunRunner : EcsRunner<IEcsRunProcess>, IEcsRunProcess
{
    public void Run(EcsSession session)
    {
        foreach (var item in targets) item.Run(session);
    }
}
```
> Раннеры имеют ряд требований к реализации: 
> * Для одного интерфейса может быть только одна реализация раннера;
> * Наследоваться от `EcsRunner<TInterface>` можно только напрямую;
> * Раннер может содержать только один интерфейс(за исключением `IEcsSystem`);
> * Наследуемый класс `EcsRunner<TInterface>,` в качестве `TInterface` должен принимать реализованный интерфейс;
> * Раннер не может быть размещен внутри другого класса.
    
## Группа
Группы это структуры данных для хранения списка сущностей и с быстрыми операциями добавления/удаления/проверки наличия и т.д. Реализованы классом EcsGroup и структурой EcsReadonlyGroup.
    
## Пул
Является контейнером для компонентов, предоставляет методы для добавления/чтения/редактирования/удаления компонентов на сущности. Есть несколько видов пулов, для разных целей
* `EcsPool` - универсальный пул, хранит struct-компоненты реализующие интерфейс IEcsComponent;
* `EcsTagPool` - подходит для хранения пустых компонентов-тегов, в сравнении с EcsPool имеет лучше оптимизацию памяти и дейсвий с пулом, хранит в себе struct-компоненты реализующие IEcsTagComponent;
    
Так же имеется возможность реализации пользовательского пула 
## Субъект
Это классы наследуемые от EcsSubject, которые используются как посредник для взаимодейсвия с сушностями. 
    
## Запросы
Используйте метод-запрос `EcsWorld.Where<TSubject>(out TSubject subject)` для получения необходимого системе набора сущностей. Запросы работают в связке с субъектами, субъекты определяют ограничения запросов, результатом запроса становится группа сущностей удовлетворяющия условиям субъекта. По умолчанию запрос делает выборку из всех сущностей в мире, но так же можно сделать выборку из определенной группы сущностей, для этого используйте `EcsWorld.WhereFor<TSubject>(EcsReadonlyGroup sourceGroup, out TSubject subject)`

# Debug
Фреймворк предоставляет дополнительные интрументы для отладки и логирования.
## Атрибуты
В чистом виде атрибуты не имеют применения, но будут использоваться в интеграциях с движками для задания отображения в отладочных интурментах и редакторах.
* `DebugNameAttribute` - Задает пользовательское название типа, по умолчанию используется имя типа.
* `DebugColorAttribute` - Задает цвет типа в системе rgb, где каждый канал принимает занчение от 0 до 255, по умолчанию белый. Задать цвет можно как вручную, так и использовать заранее заготовленные цвета в enum DebugColor.
* `DebugDescriptionAttribute` - Добавляет описание типу.
* `DebugHideAttribute` - Скрывает тип.
## EcsDebugUtility
Статические класс EcsDebugUtility имеет набор методов для упрощения получения данных из Debug-Aтрибутов.
## Debug-Сервис
  
# Расширения
* [Автоматическое внедрение зависимостей](https://github.com/DCFApixels/DragonECS-AutoInjections)
* Интеграция с движком Unity (Work in progress)
