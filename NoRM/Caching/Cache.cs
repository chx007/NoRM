using System;
using System.Linq;
using System.Text;
using System.Threading;
using TypeHelper = Norm.BSON.ReflectionHelper;
using Norm.BSON;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections;

namespace Norm.Caching
{
    public class Cache<T>
    {
        private ConcurrentDictionary<object, CacheItem<T>> innerCache;
        private TimeSpan slidingExpiration;
        private System.Timers.Timer timer;
        internal TimeSpan SlidingExpiration
        {
            get { return slidingExpiration; }
            set {
                if (slidingExpiration.TotalMilliseconds < 1000)
                    throw new ArgumentOutOfRangeException("SlidingExpiration 过期时间不得小于1秒!");
                slidingExpiration = value;
                this.timer.Stop();
                this.timer.Interval = slidingExpiration.TotalMilliseconds;
                this.timer.Start();
            }
        }
        private MagicProperty idProperty;
        
        private int concurrencyLevel = 10;
        private bool isClearing = false;
        
        public Cache(string name, int capacity, TimeSpan slidingExpiration )
        {
            this.innerCache = new ConcurrentDictionary<object, CacheItem<T>>(concurrencyLevel, capacity);
            this.slidingExpiration = slidingExpiration;
            var helper = TypeHelper.GetHelperForType(typeof(T));
            idProperty = helper.FindIdProperty();
            timer = new System.Timers.Timer(slidingExpiration.TotalMilliseconds);
            timer.Elapsed += new System.Timers.ElapsedEventHandler(timer_Elapsed);
            timer.AutoReset = true;
            timer.Enabled = true;
            timer.Start();
        }

        void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Debug.WriteLine("timer_Elapsed");
            if (!this.isClearing)
                ClearExpiredItems();
        }
        
        #region 暂时不发布的代码
        //public T Get(TId key)
        //{
        //    CacheItem<T> cacheItem = null;
        //    T result = default(T);
        //    if (innerCache.TryGetValue(key, out cacheItem))
        //    {
        //        result = cacheItem.Get();
        //    }
        //    return result;
        //}

        //internal List<TId> In(List<TId> idList)
        //{
        //    List<TId> result = new List<TId>();
        //    foreach (TId id in idList)
        //    {
        //        if (innerCache.ContainsKey(id))
        //            result.Add(id);
        //    }
        //    return result;
        //}

        //internal List<TId> NotIn(List<TId> idList)
        //{
        //    List<TId> result = new List<TId>();
        //    foreach (TId id in idList)
        //    {
        //        if (!innerCache.ContainsKey(id))
        //            result.Add(id);
        //    }
        //    return result;
        //}

        #endregion
        
        public bool Update(T entity)
        {
            bool result = false;
            var cacheItem = new CacheItem<T>(entity);
            object key = idProperty.Getter(entity);
            innerCache.AddOrUpdate(key, cacheItem, (iKey, item) => item.Update());
            result = true;
            return result;
        }

        public bool Remove(T entity)
        {
            CacheItem<T> deleteEntity;
            object key = idProperty.Getter(entity);
            return innerCache.TryRemove(key, out deleteEntity);
        }

        public List<object> GetList(IEnumerable<T> entities, out List<T> result)
        {
            result = new List<T>();
            List<object> notInIdList = new List<object>();
            foreach (T entity in entities)
            {
                object id = idProperty.Getter(entity); 
                if (!innerCache.ContainsKey(id))
                    notInIdList.Add(id);
                else
                    result.Add(innerCache[id].Get());
            }
            return notInIdList;
        }

        public void UpdateList(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                object id = idProperty.Getter(entity);
                CacheItem<T> cacheItem = new CacheItem<T>(entity);
                innerCache.AddOrUpdate(id, cacheItem, (key, item) => item.Update());
            }
        }

        public void RemoveList(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                object id = idProperty.Getter(entity);
                CacheItem<T> deleteEntity;
                innerCache.TryRemove(id, out deleteEntity);
            }
        }
        
        public void ClearExpiredItems()
        {
            isClearing = true;
            DateTime now = DateTime.Now;
            List<object> keys = new List<object>(); 
            foreach (var keyValuePair in innerCache)
            {
                if (now - keyValuePair.Value.HitTime 
                    >= slidingExpiration)
                {
                    keys.Add(keyValuePair.Key);        
                }
            }
            CacheItem<T> removeItem;
            keys.ForEach(t => 
                {
                    innerCache.TryRemove(t, out removeItem);
                    Debug.WriteLine(removeItem.ToString());
                }
                );
            isClearing = false;
        }

        #region IDisposable 成员

        public void Dispose()
        {
            this.innerCache.Clear();
            this.innerCache = null;
            this.idProperty = null;
        }

        #endregion
    }

    internal class CacheItem<T>
    {
        private T entity;
        private DateTime hitTime;
        internal CacheItem(T entity)
        {
            this.entity = entity;
            this.hitTime = DateTime.Now;
        }

        internal T Get()
        {
            this.hitTime = DateTime.Now;
            return this.entity;
        }
        internal CacheItem<T> Update()
        {
            this.hitTime = DateTime.Now;
            return this;
        }
        internal DateTime HitTime { get { return hitTime; } }
    }

}
