﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace DCFApixels.DragonECS
{
    public sealed class EcsSystems
    {
        private IEcsSystem[] _allSystems;
        private Dictionary<Type, IEcsRunner> _runners;
        private IEcsRunSystem _runRunnerCache;

        private ReadOnlyCollection<IEcsSystem> _allSystemsSealed;
        private ReadOnlyDictionary<Type, IEcsRunner> _allRunnersSealed;

        private bool _isDestoryed;

        #region Properties
        public ReadOnlyCollection<IEcsSystem> AllSystems => _allSystemsSealed;
        public ReadOnlyDictionary<Type, IEcsRunner> AllRunners => _allRunnersSealed;
        public bool IsDestoryed => _isDestoryed;
        #endregion

        #region Constructors
        private EcsSystems(IEcsSystem[] systems)
        {
            _allSystems = systems;
            _runners = new Dictionary<Type, IEcsRunner>();

            _allSystemsSealed = new ReadOnlyCollection<IEcsSystem>(_allSystems);
            _allRunnersSealed = new ReadOnlyDictionary<Type, IEcsRunner>(_runners);

            _isDestoryed = false;

            GetRunner<IEcsPreInitSystem>().PreInit(this);
            GetRunner<IEcsInitSystem>().Init(this);

            _runRunnerCache = GetRunner<IEcsRunSystem>();
        }
        #endregion

        #region Runners
        public T GetRunner<T>() where T : IEcsSystem
        {
            Type type = typeof(T);
            if (_runners.TryGetValue(type, out IEcsRunner result))
                return (T)result;
            result = (IEcsRunner)EcsRunner<T>.Instantiate(_allSystems);
            _runners.Add(type, result);
            return (T)result;
        }
        #endregion

        #region LifeCycle

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Run()
        {
#if DEBUG || !DRAGONECS_NO_SANITIZE_CHECKS
            CheckAfterDestroyForMethod(nameof(Run));
#endif
            _runRunnerCache.Run(this);
        }
        public void Destroy()
        {
#if DEBUG || !DRAGONECS_NO_SANITIZE_CHECKS
            CheckAfterDestroyForMethod(nameof(Destroy));
#endif
            _isDestoryed = true;
            GetRunner<IEcsDestroySystem>().Destroy(this);
        }
        #endregion

        #region StateChecks
#if DEBUG || !DRAGONECS_NO_SANITIZE_CHECKS
        private void CheckAfterDestroyForMethod(string methodName)
        {
            if (_isDestoryed)
                throw new MethodAccessException($"It is forbidden to call method {methodName}, after destroying {nameof(EcsSystems)}");
        }
#endif
        #endregion

        #region Builder
        public static Builder New()
        {
            return new Builder();
        }
        public class Builder
        {
            private const int KEYS_CAPACITY = 4;
            private readonly HashSet<object> _declaredBlockKeys;
            private readonly List<object> _blockExecutionOrder;
            private readonly Dictionary<object, List<IEcsSystem>> _systems;
            private readonly object _basicBlocKey;
            private bool _isBasicBlockDeclared;
            private bool _isOnlyBasicBlock;
            public Builder()
            {
                _basicBlocKey = new object();
                _declaredBlockKeys = new HashSet<object>(KEYS_CAPACITY);
                _blockExecutionOrder = new List<object>(KEYS_CAPACITY);
                _systems = new Dictionary<object, List<IEcsSystem>>(KEYS_CAPACITY);
                _isBasicBlockDeclared = false;
                _isOnlyBasicBlock = true;
            }

            public Builder Add(IEcsSystem system, object blockKey = null)
            {
                if (blockKey == null) blockKey = _basicBlocKey;
                List<IEcsSystem> list;
                if (!_systems.TryGetValue(blockKey, out list))
                {
                    list = new List<IEcsSystem>();
                    _systems.Add(blockKey, list);
                }
                list.Add(system);
                return this;
            }

            public Builder Add(IEcsModule module)
            {
                module.ImportSystems(this);
                return this;
            }

            public Builder BasicSystemsBlock()
            {
                _isBasicBlockDeclared = true;
                _blockExecutionOrder.Add(_basicBlocKey);
                return this;
            }
            public Builder SystemsBlock(object blockKey)
            {
                if (blockKey == null)
                    return BasicSystemsBlock();

                _isOnlyBasicBlock = false;
                _blockExecutionOrder.Add(blockKey);
                return this;
            }

            public EcsSystems Build()
            {
                if (_isOnlyBasicBlock)
                {
                    return new EcsSystems(_systems[_basicBlocKey].ToArray());
                }

                if(_isBasicBlockDeclared == false)
                    _blockExecutionOrder.Insert(0, _basicBlocKey);

                List<IEcsSystem> result = new List<IEcsSystem>(32);

                List<IEcsSystem> basicBlockList = _systems[_basicBlocKey];

                foreach (var item in _systems)
                {
                    if (!_blockExecutionOrder.Contains(item.Key))
                    {
                        basicBlockList.AddRange(item.Value);
                    }
                }
                foreach (var item in _blockExecutionOrder)
                {
                    result.AddRange(_systems[item]);
                }

                return new EcsSystems(result.ToArray());
            }
        }
        #endregion
    }

    public interface IEcsModule
    {
        public void ImportSystems(EcsSystems.Builder builder);
    }

    public static class EcsSystemsExt
    {
        public static bool IsNullOrDestroyed(this EcsSystems self)
        {
            return self == null || self.IsDestoryed;
        }
        public static EcsSystems.Builder Add(this EcsSystems.Builder self, IEnumerable<IEcsSystem> range, object blockKey = null)
        {
            foreach (var item in range)
            {
                self.Add(item, blockKey);
            }
            return self;
        }
    }
}
