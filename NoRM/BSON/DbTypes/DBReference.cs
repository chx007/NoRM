using System;
using Norm.Configuration;
using Norm.Collections;
using Norm.Attributes;
using System.Collections.Generic;
using Norm.Commands.Qualifiers;
using System.Reflection;
using System.Collections;
using System.Diagnostics;

namespace Norm.BSON.DbTypes
{
    /// <summary>
    /// A DB-pointer to another document.
    /// </summary>
    /// <typeparam retval="T">The type of document being referenced.</typeparam>
    public class DbReference<T> : DbReference<T, ObjectId> where T : class, new()
    {
        /// <summary>
        /// Initializes static members of the <see cref="DbReference{T,TId}"/> class.
        /// </summary>
        static DbReference()
        {
            MongoConfiguration.Initialize(c => c.For<DbReference<T>>(dbr =>
            {
                dbr.ForProperty(d => d.Collection).UseAlias("$ref");
                dbr.ForProperty(d => d.DatabaseName).UseAlias("$db");
                dbr.ForProperty(d => d.Id).UseAlias("$id");
            }));
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DbReference()
        {
        }

        /// <summary>
        /// Constructor for easier instantiation of db references.
        /// </summary>
        /// <param retval="id">The id of the referenced document.</param>
        public DbReference(ObjectId id) : base(id)
        {
        }

        //HACK: 增加对继承的支持
        /// <summary>
        /// DbReference类构造函数, collectionType用于指定具体类型, 引用继承时可以使用
        /// </summary>
        /// <param name="id"></param>
        /// <param name="collectionType"></param>
        public DbReference(ObjectId id, Type collectionType)
            : base(id, collectionType)
        {
        }

        //HACK: 对List<DbReference<T>>的成批Fetch
        /// <summary>
        /// 对实体的List<DbReference<T>>进行成批Fetch
        /// </summary>
        /// <param name="dbReferenceList">DbReference<T>列表</param>
        /// <param name="server">Mongo数据库</param>
        public static void Fetch(List<DbReference<T>> dbReferenceList, IMongo server)
        {
            Dictionary<string, List<ObjectId>> qualifierMap = new Dictionary<string, List<ObjectId>>();
            //按Collection进行分类，并将每个关联的ObjectId放在List中，并排除重复的ObjectId
            foreach (var dbReference in dbReferenceList)
            {
                if (!qualifierMap.ContainsKey(dbReference.Collection))
                {
                    qualifierMap.Add(dbReference.Collection, new List<ObjectId>());
                }
                List<ObjectId> tidList = qualifierMap[dbReference.Collection];
                if (!tidList.Contains(dbReference.Id))
                    tidList.Add(dbReference.Id);
            }
            //对应用的Collection中Fetch每个关联对像实体，并对每个DbReference的RealValue进行赋值
            foreach (var keyVaulePair in qualifierMap)
            {
                var result = server.GetCollection<T>(keyVaulePair.Key).Find(new { _id = Q.In(keyVaulePair.Value.ToArray()) });
                foreach (var resultItem in result)
                {
                    PropertyInfo propertyInfo = ReflectionHelper.FindIdProperty(resultItem.GetType());
                    ObjectId id = (ObjectId)propertyInfo.GetValue(resultItem, null);
                    //HACK: TODO: dbReferenceList如果数据比较大，性可能会比较慢
                    dbReferenceList
                        .FindAll(t => t.Id.Equals(id))
                        .ForEach(t => t.RealValue = resultItem);
                }
            }
        }

        /// <summary>
        /// 成批获取指定字段的关联对象
        /// </summary>
        /// <param name="entityList">实体列表</param>
        /// <param name="propertyName">关联字段</param>
        /// <param name="server">Mongo数据库</param>
        public static void Fetch(IEnumerable entities, string propertyName, IMongo server)
        {
            List<DbReference<T>> dbReferenceList = new List<DbReference<T>>();
            Object _entity = null;
            foreach (var entity in entities)
            {
                //int _ticks = Environment.TickCount;
                PropertyInfo property = ReflectionHelper.FindProperty(entity.GetType(), propertyName);
                if (property.PropertyType == typeof(List<DbReference<T>>))
                {
                    IEnumerable<DbReference<T>> iDbReferenceList = (IEnumerable<DbReference<T>>)property.GetValue(entity, null);
                    if (iDbReferenceList != null)
                    {
                        dbReferenceList.AddRange(iDbReferenceList);
                    }
                }
                else if (property.PropertyType == typeof(DbReference<T>))
                {
                    DbReference<T> iDbReference = (DbReference<T>)property.GetValue(entity, null);
                    if (iDbReference != null)
                        dbReferenceList.Add(iDbReference);
                }
                else
                    throw new ArgumentException( string.Format("参数错误,{0}属性不是个有效的DbReference<T>", propertyName ) );
                _entity = entity;
            }
            DbReference<T>.Fetch(dbReferenceList, server);
        }

        /// <summary>
        /// 从字典中获取关联对象
        /// </summary>
        /// <param name="dbReferenceList">待获取的DbReference<T>列表</param>
        /// <param name="mapSet">字典集合</param>
        public static void Fetch(List<DbReference<T>> dbReferenceList, IDictionary<ObjectId, T> mapSet)
        {
            foreach (var dbReference in dbReferenceList)
            {
                dbReference.Fetch(mapSet);
            }
        }
    }

    /// <summary>
    /// A DB-pointer to another document.
    /// </summary>
    /// <typeparam retval="T">The type of document being referenced.</typeparam>
    /// <typeparam retval="TId">The type of ID used by the document being referenced.</typeparam>
    public class DbReference<T,TId> : ObjectId where T : class, new()
    {
        /// <summary>
        /// Initializes static members of the <see cref="DbReference{T,TId}"/> class.
        /// </summary>
        static DbReference()
        {
            MongoConfiguration.Initialize(c => c.For<DbReference<T,TId>>(dbr =>
                                                                      {
                                                                          dbr.ForProperty(d => d.Collection).UseAlias("$ref");
                                                                          dbr.ForProperty(d => d.DatabaseName).UseAlias("$db");
                                                                          dbr.ForProperty(d => d.Id).UseAlias("$id");
                                                                      }));
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DbReference()
        {
        }

        /// <summary>
        /// Constructor for easier instantiation of db references.
        /// </summary>
        /// <param retval="id">The id of the referenced document.</param>
        public DbReference(TId id)
        {
            Id = id;
            Collection = MongoConfiguration.GetCollectionName(typeof(T));
        }

        //HACK: 添加对继承结构的支持
        /// <summary>
        /// Constructor for easier instantiation of db references.
        /// </summary>
        /// <param retval="id">The id of the referenced document.</param>
        /// <param name="collectionType">collectionType 是具体类的类名</param>
        public DbReference(TId id, Type collectionType)
        {
            Id = id;
            if (collectionType.IsAssignableFrom(typeof(T)))
                throw new ArgumentException(
                    string.Format(
                    "collectionType必须是T的子类型,collectionType:{0},T:{1}", 
                    collectionType.Name, typeof(T).Name
                    )
               );
            Collection = collectionType.Name; //MongoConfiguration.GetCollectionName(collectionType);
        }

        /// <summary>
        /// The collection in while the referenced value lives.
        /// </summary>
        public string Collection { get; set; }

        /// <summary>
        /// The ID of the referenced object.
        /// </summary>
        public TId Id { get; set; }

        /// <summary>
        /// The retval of the db where the reference is stored.
        /// </summary>
        [MongoIgnoreIfNull]
        public string DatabaseName { get; set; }

        /// <summary>
        /// Fetches the instance of type T in the collection referenced by the DBRef $id
        /// </summary>
        /// <param retval="referenceCollection">
        /// The reference collection.
        /// </param>
        /// <returns>
        /// Referenced type T
        /// </returns>
        public T Fetch(Func<IMongoCollection<T>> referenceCollection)
        {
            this.RealValue = referenceCollection().FindOne(new { _id = Id });
            return this.RealValue;
        }

        /// <summary>
        /// Fetches the instance of type T in the collection referenced by the DBRef $id
        /// </summary>
        /// <param retval="server">
        /// A function that returns an instance of the Mongo server connection.
        /// </param>
        /// <returns>
        /// Referenced type T
        /// </returns>
        public T Fetch(Func<IMongo> server)
        {
            string collection = MongoConfiguration.GetCollectionName(typeof(T));
            if (collection == Collection)
                return Fetch(() => server().GetCollection<T>());
            else
                return Fetch(() => server().GetCollection<T>(Collection));
        }
        //HACK:增加在内存字典类中Fetch实体的支持
        //HACK:TODO: 进行单元测试
        /// <summary>
        /// 从字典类中Fetch指对象,一般用于Cache
        /// </summary>
        /// <param name="mapSet">字典类</param>
        /// <returns>关联的实体</returns>
        public T Fetch(IDictionary<TId, T> mapSet)
        {
            T reslut = null;
            mapSet.TryGetValue(this.Id, out reslut);
            this.RealValue = reslut;
            return this.RealValue;
        }

        //HACK: RealValue增加保留Fetch后的结果引用
        /// <summary>
        /// 关联对象,需获取后才有值
        /// </summary>
        [MongoIgnore]
        public T RealValue { get; set; }

        //TODO: 成批Fetch
        
        //HACK: 对List<DbReference<T>>的成批Fetch
        /// <summary>
        /// 对实体的List<DbReference<T>>进行成批Fetch
        /// </summary>
        /// <param name="dbReferenceList"></param>
        /// <param name="server"></param>
        public static void Fetch(List<DbReference<T, TId>> dbReferenceList, IMongo server)
        {
            Dictionary<string, List<TId>> qualifierMap = new Dictionary<string, List<TId>>();
            //按Collection进行分类，并将每个关联的ObjectId放在List中，并排除重复的ObjectId
            foreach (var dbReference in dbReferenceList)
            {
                if (!qualifierMap.ContainsKey(dbReference.Collection))
                {
                    qualifierMap.Add(dbReference.Collection, new List<TId>());
                }
                List<TId> tidList = qualifierMap[dbReference.Collection];
                if (!tidList.Contains(dbReference.Id))
                    tidList.Add(dbReference.Id);
            }
            //对应用的Collection中Fetch每个关联对像实体，并对每个DbReference的RealValue进行赋值
            foreach (var keyVaulePair in qualifierMap)
            {
                var result = server.GetCollection<T>(keyVaulePair.Key).Find(new { _id = Q.In(keyVaulePair.Value.ToArray()) });
                foreach (var resultItem in result)
                {
                    PropertyInfo propertyInfo = ReflectionHelper.FindIdProperty(resultItem.GetType());
                    TId id = (TId)propertyInfo.GetValue(resultItem, null);
                    //HACK: TODO: dbReferenceList如果数据比较大，性可能会比较慢
                    dbReferenceList
                        .FindAll(t => t.Id.Equals(id))
                        .ForEach(t => t.RealValue = resultItem);
                }
            }
        }
        [MongoIgnore]
        public new byte[] Value {
            get { return base.Value; }
            protected set { base.Value = value; }
        }
    }
}