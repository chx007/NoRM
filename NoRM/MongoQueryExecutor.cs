using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Norm.BSON;
using Norm.Protocol.Messages;
using System;
using Norm.Caching;
using System.Diagnostics;

namespace Norm
{
    public class MongoQueryExecutor<T, U> : MongoQueryExecutor<T, U, T>
    {
        public MongoQueryExecutor(QueryMessage<T,U> message)
            : base(message, y => y)
        {

        }
    }

    /// <summary>
    /// Acts as a proxy for query execution so additional paramaters like
    /// hints can be added with a more fluent syntax around IEnumerable
    /// and IQueryable.
    /// </summary>
    /// <typeparam retval="T">The type to query</typeparam>
    /// <typeparam retval="U">Document template type</typeparam>
    /// <typeparam retval="O">The output type.</typeparam>
    public class MongoQueryExecutor<T, U, O> : IEnumerable<O>
    {
        internal String CollectionName { get; set; }

        private readonly Expando _hints = new Expando();

        public MongoQueryExecutor(QueryMessage<T, U> message, Func<T, O> projection)
        {
            this.Message = message;
            this.Translator = projection;
        }

        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        /// <value>The message.</value>
        public QueryMessage<T, U> Message { get; private set; }

        /// <summary>
        /// Adds a query hint.
        /// </summary>
        /// <param retval="hint">The hint.</param>
        /// <param retval="direction">The index direction; ascending or descending.</param>
        public void AddHint(string hint, IndexOption direction)
        {
            _hints.Set(hint, direction);
        }

        private Func<T, O> Translator
        {
            get;
            set;
        }

        //HACK: 为了使replyMessage不为空时，不重复查询，将GetEnumerator()中的replyMessage局部变量，改为成员变量
        //ReplyMessage<T> replyMessage = null;
        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1"/> that can be used to iterate through the collection.
        /// </returns>
        public IEnumerator<O> GetEnumerator()
        {
            ReplyMessage<T> replyMessage;
            List<T> results = null;
            Stopwatch stopWatcher;
            //if (replyMessage == null)
            {
                if (_hints.AllProperties().Count() == 0) // No hints - just run the query
                {
                    //HACK:增加Cache的支持
                    //replyMessage = Message.Execute();
                    if (Message.FieldSelection == null
                        && Caching.CacheManager.Instance.HasCache(this.CollectionName)
                        )
                    {
                        MagicProperty idProperty = ReflectionHelper.GetHelperForType(typeof(T)).FindIdProperty();
                        Expando expando = new Expando();
                        expando[idProperty.Name] = 1;
                        Message.FieldSelection = expando;

                        stopWatcher = Stopwatch.StartNew();
                        replyMessage = Message.Execute();
                        stopWatcher.Stop();
                        Debug.WriteLine("向数据库查ID:" + stopWatcher.ElapsedMilliseconds);
                        stopWatcher = Stopwatch.StartNew();
                        List<object> notInIdList = CacheManager.Instance.GetCacheData<T>(
                            this.CollectionName,
                            replyMessage.Results,
                            out results);
                        stopWatcher.Stop();
                        Debug.WriteLine("向缓存ID:" + stopWatcher.ElapsedMilliseconds);
                        
                        if (notInIdList != null && notInIdList.Count > 0)
                        {
                            Expando query = new Expando();
                            Expando qin = new Expando();
                            qin["$in"] = notInIdList;
                            query[idProperty.Name] = qin;
                            QueryMessage<T, Expando> qm
                                = new QueryMessage<T, Expando>(
                                    Message.Connection,
                                    Message.Collection)
                            {
                                NumberToTake = Message.NumberToTake,
                                NumberToSkip = Message.NumberToSkip,
                                Query = query,
                                OrderBy = Message.OrderBy
                            };
                            stopWatcher = Stopwatch.StartNew();
                            replyMessage = qm.Execute();
                            stopWatcher.Stop();
                            Debug.WriteLine("向数据库查数据:" + stopWatcher.ElapsedMilliseconds);

                            stopWatcher = Stopwatch.StartNew();
                            results.AddRange(replyMessage.Results);
                            CacheManager.Instance.UpdateCacheData<T>(
                                this.CollectionName,
                                replyMessage.Results);
                            stopWatcher.Stop();
                            Debug.WriteLine("更新缓存:" + stopWatcher.ElapsedMilliseconds);
                        }
                    }
                    else
                    {
                        stopWatcher = Stopwatch.StartNew();
                        replyMessage = Message.Execute();
                        Debug.WriteLine("向数据库查数据:" + stopWatcher.ElapsedMilliseconds);
                    }
                }
                else // Add hints.  Other commands can go here as needed.
                {
                    var query = Message.Query;

                    var queryWithHint = new Expando();
                    queryWithHint["$query"] = query;
                    queryWithHint["$hint"] = _hints;
                    replyMessage = Message.Execute();
                    //results = (List<T>)replyMessage.Results;
                }
            }
            if (results != null)
            {
                foreach (var r in results)
                {
                    yield return this.Translator(r);
                }
            }
            else
            {
                foreach (var r in replyMessage.Results)
                {
                    yield return this.Translator(r);
                }
            }
            yield break;
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        /// <returns>
        /// An <see cref="T:System.Collections.IEnumerator"/> object that can be used to iterate through the collection.
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
