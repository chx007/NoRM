using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace Norm.Caching
{
    public class CacheManager
    {
        private Hashtable cacheSet;
        private TimeSpan defaultSlidingExpiration;
        
        private CacheManager()
        {
            cacheSet = new Hashtable(10);
            
        }
        private static CacheManager instance;
        static readonly object padlock = new object();
        /// <summary>
        /// 缓存管理的单例
        /// </summary>
        public static CacheManager Instance
        {
            get
            {
                if (instance == null)
                {
                    lock (padlock)
                    {
                        if (instance == null)
                        {
                            instance = new CacheManager();
                        }
                    }
                }
                return instance;
            }
        }

        /// <summary>
        /// 注册并设置缓存的参数,
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TId"></typeparam>
        /// <param name="collectionName">数据集合的名移</param>
        /// <param name="slidingExpiration">过期时间,以毫秒计</param>
        public void SetCache<T>(string collectionName, TimeSpan slidingExpiration)where T : class, new()
        {
            Cache<T> cache = GetCache<T>(collectionName);
            if (cache == null)
            {
                cache = new Cache<T>(collectionName, 100, slidingExpiration);
                Hashtable.Synchronized(this.cacheSet).Add(collectionName, cache);
            }
            cache.SlidingExpiration = slidingExpiration;
        }

        /// <summary>
        /// 查找是否存在指定名称的缓存
        /// </summary>
        /// <param name="collectionName">缓存集合名</param>
        /// <returns>布尔值,true表示存在, false表示不存在</returns>
        public bool HasCache(string collectionName)
        {
            return cacheSet.ContainsKey(collectionName);
        }

        /// <summary>
        /// 获取指定的数据
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="collectionName">集合名</param>
        /// <param name="entities">要查找的集合</param>
        /// <param name="result">缓存中未找的结果</param>
        /// <returns>不在缓存中的ID列表</returns>
        internal List<object> GetCacheData<T>(string collectionName, IEnumerable<T> entities, out List<T> result)
        {
            Cache<T> cache = GetCache<T>(collectionName);
            return cache.GetList(entities, out result);
        }
        /// <summary>
        /// 更新指定缓存中的一个数据项
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collectionName"></param>
        /// <param name="entity"></param>
        internal void UpdateItem<T>(string collectionName, T entity)
        {
            GetCache<T>(collectionName).Update( entity );
        }

        /// <summary>
        /// 删除指定缓存中的一个数据项
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collectionName"></param>
        /// <param name="entity"></param>
        internal void RemoveItem<T>(string collectionName, T entity)
        {
            GetCache<T>(collectionName).Remove(entity);
        }
        /// <summary>
        /// 成批更新缓存中的数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collectionName"></param>
        /// <param name="entites"></param>
        internal void UpdateCacheData<T>(string collectionName, IEnumerable<T> entites)
        {
            var cache = GetCache<T>(collectionName);
            cache.UpdateList(entites);
        }
        /// <summary>
        /// 成批删除缓存中的数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collectionName"></param>
        /// <param name="entites"></param>
        internal void RemoveCacheData<T>(string collectionName, IEnumerable<T> entites)
        {
            var cache = GetCache<T>(collectionName);
            cache.RemoveList(entites);
        }
        /// <summary>
        /// 关闭缓存
        /// </summary>
        public void Close()
        {
            foreach (var a in cacheSet)
            {
                ((IDisposable)a).Dispose();
            }
            cacheSet.Clear();
        }

        private Cache<T> GetCache<T>(string collectionName)
        {
            if (cacheSet.ContainsKey(collectionName))
            {
                return (Cache<T>)(cacheSet[collectionName]);
            }
            return null;
        }
    }
}
