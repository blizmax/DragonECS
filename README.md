
<p align="center">
<img src="https://user-images.githubusercontent.com/99481254/228309579-729cc600-af83-41e2-8474-96fb96859ae6.png">
</p>

# DragonECS - C# Entity Component System Framework

> **ВАЖНО!** Проект в стадии разработки. API может меняться. README так же не завершен.

# Основные концепции
## Сущьность
Сущьности - это идентификаторы, к которым крепятся данные. Есть2 вида идентификатора:
* `int` - однократный идентификатор, применяется в пределах одного тика. Не рекомендуется хранить `int` идентификаторы, в место этого используйте `entlong`;
* `entlong` - долговременный идентификатор, содержит в себе полный набор информации для однозначной идентификации;

## Компонент
Компоненты - это даные для сущностей. Могут быть тольно struct и обязаны реализовывать интерфейс IEcsComponent или другой указываюший разновидность кмпонента. 
```c#
struct Health : IEcsComponent
{
    public float health;
    public int armor;
}
```
## Система
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
    
    //Для реализации дополнительных сообщений используйте Раннеры
}
```

## Pipeline
Является двжиком систем, определяя поочередность их вызова, предоставляющий механизм для сообщений между системами и механизм внедрения зависимостей в системы.

## Процесс/Раннер
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
