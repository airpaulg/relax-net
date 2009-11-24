﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Relax.Design;

namespace Relax
{
    public class Document
    {
        public Session Session { get; set; }
        public string Id { get; set; }
        public string Revision { get; set; }

        public string Location
        {
            get
            {
                return Session.Connection.GetDatabaseLocation(Session.Database) +
                    (Id.StartsWith("_design/")
                        ? "_design/" + Id.Substring(8).Replace("/", "%2F")
                        : Id.Replace("/", "%2F"));
            }
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode() ^ (Revision ?? "-").GetHashCode();
        }

        public override bool Equals(object obj)
        {
            var d = (Document)obj;
            return null == d
                       ? base.Equals(obj)
                       : d.Id == Id &&
                         d.Revision == Revision;
        }
    }

    public class Session
    {
        private class __DocumentResponse
        {
            public bool ok { get; set; }
            public string id { get; set; }
            public string rev { get; set; }

            public Document ToDocument(Session session)
            {
                return new Document
                           {
                               Session = session,
                               Id = id,
                               Revision = rev
                           };
            }
        }

        public Connection Connection { get; private set; }
        public string Database { get; private set; }

        private int _locks;
        private Dictionary<object, Document> _entities = new Dictionary<object, Document>(100);

        public Session(Connection connection, string database)
        {
            InvalidDatabaseNameException.Validate(database);
            Connection = connection;
            Database = database;
        }

        public IList<Document> List()
        {
            var request = (HttpWebRequest)WebRequest.Create(Connection.GetDatabaseLocation(Database) + "_all_docs");
            using (var reader = request.GetCouchResponse())
            {
                var a = new List<Document>();
                var o = JObject.Load(reader);
                var rows = o["rows"];
                foreach (JObject r in rows)
                {
                    a.Add(new Document()
                              {
                                  Id = r.Value<string>("id"),
                                  Revision = r["value"].Value<string>("rev")
                              });
                }
                return a;
            }
        }
        
        public Document Save<TEntity>(TEntity entity, string id) where TEntity : class
        {
            if (_entities.ContainsKey(entity))
            {
                var d = _entities[entity];
                if (d.Id != id)
                {
                    throw new Exception("This entity is already saved under the id '" + d.Id + "' and cannot be reassigned the new id '" + id + "' in this session.");
                }
                return Save(entity, d);
            }
            return Save(entity, new Document {Session = this, Id = id});
        }
            
        public Document Save<TEntity>(TEntity entity) where TEntity : class
        {
            var d = _entities.ContainsKey(entity)
                ? _entities[entity]
                : new Document
                {
                    Session = this,
                    Id = typeof(TEntity).Name.ToLowerInvariant() + "-" + Guid.NewGuid(),
                    Revision = null,
                };
            
            return Save(entity, d);
        }

        private Document Save<TEntity>(TEntity entity, Document d) where TEntity : class
        {
            var serializer = new JsonSerializer(); 
            var request = (HttpWebRequest) WebRequest.Create(d.Location);
            request.Method = "PUT";
            using (var writer = new JsonTextWriter(new StreamWriter(request.GetRequestStream())))
            {
                if (!String.IsNullOrEmpty(d.Revision))
                {
                    var jwriter = new JTokenWriter();
                    serializer.Serialize(jwriter, entity);
                    var o = jwriter.Token;
                    o.First.AddBeforeSelf(new JProperty("_rev", d.Revision));
                    o.First.AddBeforeSelf(new JProperty("_id", d.Id));
                    o.WriteTo(writer);
                }
                else
                {
                    serializer.Serialize(writer, entity);
                }
            }
            using (var reader = request.GetCouchResponse())
            {
                var response = (__DocumentResponse)serializer.Deserialize(reader, typeof(__DocumentResponse));
                d = response.ToDocument(this);
            }
    
            if (_entities.ContainsKey(entity))
            {
                _entities.Remove(entity);
            }
            _entities.Add(entity, d);

            return d;
        }

        public TEntity Load<TEntity>(string id) where TEntity : class
        {
            foreach (var p in _entities)
            {
                if (p.Value.Id == id)
                {
                    var e = p.Key as TEntity;
                    if (null == e)
                    {
                        throw new InvalidCastException();
                    }
                    return e;
                }
            }

            var d = new Document
                        {
                            Session = this,
                            Id = id,
                        };
            var request = (HttpWebRequest)WebRequest.Create(d.Location);
            using (var reader = request.GetCouchResponse())
            {
                var o = JToken.ReadFrom(reader);
                d.Id = (string)o["_id"];
                d.Revision = (string) o["_rev"];
                var serializer = new JsonSerializer();
                var e = (TEntity)serializer.Deserialize(new JTokenReader(o), typeof(TEntity));
                _entities[e] = d;
                return e;
            }
        }

        public void Delete<TEntity>(TEntity entity) where TEntity : class
        {
            if (!_entities.ContainsKey(entity))
            {
                throw new IndexOutOfRangeException("The entity is not currently enrolled in this session.");
            }
            var e = _entities[entity];
            var request = (HttpWebRequest)WebRequest.Create(e.Location);
            request.Method = "DELETE";
            request.Headers[HttpRequestHeader.IfMatch] = e.Revision;
            using (var reader = request.GetCouchResponse())
            {
                var serializer = new JsonSerializer();
                var response = (__DocumentResponse)serializer.Deserialize(reader, typeof(__DocumentResponse));
                if (response.ok)
                {
                    _entities.Remove(entity);
                }
                else
                {
                    throw new Exception();
                }
            }
        }

        public bool IsEnrolled(string id)
        {
            return _entities.Where(x => x.Value.Id == id).Count() == 1;
        }

        public bool IsEnrolled<TEntity>(TEntity entity) where TEntity : class
        {
            return _entities.ContainsKey(entity);
        }

        public void Enroll<TEntity>(Document d, TEntity entity) where TEntity : class
        {
            if (IsEnrolled(d.Id))
            {
                throw new Exception("A entity with this key is already enrolled.");
            }
            _entities.Add(entity, d);
        }

        public void Reset()
        {
            _entities = _entities.Where(x => x.Value.Id.StartsWith("_design/")).ToDictionary(x => x.Key, x => x.Value);
        }

        private class __SessionLock : IDisposable
        {
            private Session _sx;

            public __SessionLock(Session sx)
            {
                _sx = sx;
            }

            public void Dispose()
            {
                _sx.Release();
            }
        }

        public IDisposable Lock()
        {
            Interlocked.Increment(ref _locks);
            return new __SessionLock(this);
        }

        private void Release()
        {
            var l = Interlocked.Decrement(ref _locks);
            if (l < 1)
            {
                Connection.ReturnSession(this);
            }
        }
    }
}
