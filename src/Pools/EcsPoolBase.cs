﻿using DCFApixels.DragonECS.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DCFApixels.DragonECS
{
    public interface IEcsPool
    {
        #region Properties
        public int ComponentID { get; }
        public Type ComponentType { get; }
        public EcsWorld World { get; }
        public int Count { get; }
        public int Capacity { get; }
        #endregion

        #region Methods
        bool Has(int entityID);
        void Del(int entityID);
        void AddRaw(int entityID, object dataRaw);
        object GetRaw(int entityID);
        void SetRaw(int entityID, object dataRaw);
        void Copy(int fromEntityID, int toEntityID);
        void Copy(int fromEntityID, EcsWorld toWorld, int toEntityID);
        #endregion

        #region Add/Remove Listeners
        void AddListener(IEcsPoolEventListener listener);
        void RemoveListener(IEcsPoolEventListener listener);
        #endregion
    }
    public interface IEcsPool<T>
    {
        ref T Add(int entityID);
        ref readonly T Read(int entityID);
        ref T Get(int entityID);
    }
    /// <summary>Only used to implement a custom pool. In other contexts use IEcsPool or IEcsPool<T>.</summary>
    public interface IEcsPoolImplementation : IEcsPool
    {
        void OnInit(EcsWorld world, int componentID);
        void OnWorldResize(int newSize);
        void OnReleaseDelEntityBuffer(ReadOnlySpan<int> buffer);
        void OnWorldDestroy();
    }
    /// <summary>Only used to implement a custom pool. In other contexts use IEcsPool or IEcsPool<T>.</summary>
    /// <typeparam name="T">Component type</typeparam>
    public interface IEcsPoolImplementation<T> : IEcsPool<T>, IEcsPoolImplementation { }

    public static class EcsPoolThrowHalper
    {
        public static void ThrowAlreadyHasComponent<T>(int entityID)
        {
            throw new EcsFrameworkException($"Entity({entityID}) already has component {typeof(T).Name}.");
        }
        public static void ThrowNotHaveComponent<T>(int entityID)
        {
            throw new EcsFrameworkException($"Entity({entityID}) has no component {typeof(T).Name}.");
        }
    }
    public static class IEcsPoolImplementationExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void IncrementEntityComponentCount<T>(this IEcsPoolImplementation<T> self, int entityID)
        {
            self.World.IncrementEntityComponentCount(entityID);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DecrementEntityComponentCount<T>(this IEcsPoolImplementation<T> self, int entityID)
        {
            self.World.DecrementEntityComponentCount(entityID);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrDummy(this IEcsPool self)
        {
            return self == null || self == EcsNullPool.instance;
        }
    }

    #region Dummy EcsNullPool
    namespace Internal
    {
        public struct NullComponent { }
        public sealed class EcsNullPool : IEcsPoolImplementation<NullComponent>
        {
            public static EcsNullPool instance => new EcsNullPool();

            #region Properties
            int IEcsPool.ComponentID => -1;
            Type IEcsPool.ComponentType => typeof(NullComponent);
            EcsWorld IEcsPool.World => throw new NotImplementedException();
            public int Count => -1;
            public int Capacity => -1;
            #endregion

            #region Methods
            bool IEcsPool.Has(int index) => false;
            void IEcsPool.Del(int entityID) => throw new NotImplementedException();
            void IEcsPool.AddRaw(int entityID, object dataRaw) => throw new NotImplementedException();
            object IEcsPool.GetRaw(int entityID) => throw new NotImplementedException();
            void IEcsPool.SetRaw(int entity, object dataRaw) => throw new NotImplementedException();
            void IEcsPool.Copy(int fromEntityID, int toEntityID) => throw new NotImplementedException();
            void IEcsPool.Copy(int fromEntityID, EcsWorld toWorld, int toEntityID) => throw new NotImplementedException();
            ref NullComponent IEcsPool<NullComponent>.Add(int entityID) => throw new NotImplementedException();
            ref readonly NullComponent IEcsPool<NullComponent>.Read(int entityID) => throw new NotImplementedException();
            ref NullComponent IEcsPool<NullComponent>.Get(int entityID) => throw new NotImplementedException();
            #endregion

            #region Callbacks
            void IEcsPoolImplementation.OnInit(EcsWorld world, int componentID) { }
            void IEcsPoolImplementation.OnWorldDestroy() { }
            void IEcsPoolImplementation.OnWorldResize(int newSize) { }
            void IEcsPoolImplementation.OnReleaseDelEntityBuffer(ReadOnlySpan<int> buffer) { }
            #endregion

            #region Listeners
            public void AddListener(IEcsPoolEventListener listener) { }
            public void RemoveListener(IEcsPoolEventListener listener) { }
            #endregion
        }
    }
    #endregion

    #region Reset/Copy interfaces
    public interface IEcsComponentReset<T>
    {
        public void Reset(ref T component);
    }
    public static class EcsComponentResetHandler<T>
    {
        public static readonly IEcsComponentReset<T> instance;
        public static readonly bool isHasHandler;
        static EcsComponentResetHandler()
        {
            Type targetType = typeof(T);
            if (targetType.GetInterfaces().Contains(typeof(IEcsComponentReset<>).MakeGenericType(targetType)))
            {
                instance = (IEcsComponentReset<T>)Activator.CreateInstance(typeof(ComponentResetHandler<>).MakeGenericType(targetType));
                isHasHandler = true;
            }
            else
            {
                instance = new ComponentResetDummyHandler<T>();
                isHasHandler = false;
            }
        }
    }
    internal sealed class ComponentResetDummyHandler<T> : IEcsComponentReset<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset(ref T component) => component = default;
    }
    internal sealed class ComponentResetHandler<T> : IEcsComponentReset<T>
        where T : IEcsComponentReset<T>
    {
        private T _fakeInstnace = default;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset(ref T component) => _fakeInstnace.Reset(ref component);
    }

    public interface IEcsComponentCopy<T>
    {
        public void Copy(ref T from, ref T to);
    }
    public static class EcsComponentCopyHandler<T>
    {
        public static readonly IEcsComponentCopy<T> instance;
        public static readonly bool isHasHandler;
        static EcsComponentCopyHandler()
        {
            Type targetType = typeof(T);
            if (targetType.GetInterfaces().Contains(typeof(IEcsComponentCopy<>).MakeGenericType(targetType)))
            {
                instance = (IEcsComponentCopy<T>)Activator.CreateInstance(typeof(ComponentCopyHandler<>).MakeGenericType(targetType));
                isHasHandler = true;
            }
            else
            {
                instance = new ComponentCopyDummyHandler<T>();
                isHasHandler = false;
            }
        }
    }
    internal sealed class ComponentCopyDummyHandler<T> : IEcsComponentCopy<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy(ref T from, ref T to) => to = from;
    }
    internal sealed class ComponentCopyHandler<T> : IEcsComponentCopy<T>
        where T : IEcsComponentCopy<T>
    {
        private T _fakeInstnace = default;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Copy(ref T from, ref T to) => _fakeInstnace.Copy(ref from, ref to);
    }
    #endregion

    #region Callbacks Interface
    public interface IEcsPoolEventListener
    {
        /// <summary>Called after adding an entity to the pool, but before changing values.</summary>
        void OnAdd(int entityID);
        /// <summary>Is called when EcsPool.Get or EcsPool.Add is called, but before changing values.</summary>
        void OnGet(int entityID);
        /// <summary>Called after deleting an entity from the pool</summary>
        void OnDel(int entityID);
    }
    public static class PoolEventListExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InvokeOnAdd(this List<IEcsPoolEventListener> self, int entityID)
        {
            for (int i = 0, iMax = self.Count; i < iMax; i++) self[i].OnAdd(entityID);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InvokeOnAddAndGet(this List<IEcsPoolEventListener> self, int entityID)
        {
            for (int i = 0, iMax = self.Count; i < iMax; i++)
            {
                self[i].OnAdd(entityID);
                self[i].OnGet(entityID);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InvokeOnGet(this List<IEcsPoolEventListener> self, int entityID)
        {
            for (int i = 0, iMax = self.Count; i < iMax; i++) self[i].OnGet(entityID);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InvokeOnDel(this List<IEcsPoolEventListener> self, int entityID)
        {
            for (int i = 0, iMax = self.Count; i < iMax; i++) self[i].OnDel(entityID);
        }
    }
    #endregion
}
