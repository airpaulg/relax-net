Hammock is a .NET [CouchDB](http://couchdb.apache.org/) library modeled directly on [NHibernate](http://nhforge.org/)  and strives for much of the same functionality while avoiding the vast complexity of mapping object data to a relational system.

If you haven't already, install CouchDB from http://wiki.apache.org/couchdb/Installing_on_Windows.

Using Hammock is very straightforward. First, create a `Connection`:

```
var c = new Connection(new Uri("http://localhost:5984")) };
```

Connections represent a single CouchDB server. You can query for existing databases and create or delete databases directly:

```
if (!c.ListDatabases().Contains("sample-db"))
{
    c.CreateDatabase("sample-db");
}
```

Here we're checking to see if a database named 'sample-db' already exists, and creating it if it doesn't.

Once you know the name of the database you want to work with, use the `Connection` to a create a `Session` on that database:

```
var s = c.CreateSession("sample-db");
```

A `Session` in Hammock is very much like an `ISession` in NHibernate; you can persist objects to them using `Save()`, read them using `Load()` and `List()`, and of course `Delete()` them.

```
var p = new Product { Name = "PS3", Cost = 299.95 };
s.Save(p);
```

Unlike NHibernate, persistent objects in Hammock can be entirely [POCO](http://en.wikipedia.org/wiki/Plain_Old_CLR_Object). There is no need to use either a base class or virtual methods. The nature of CouchDB eliminates the need for [lazy loading](http://flux88.com/blog/blame-nhibernate-why-not/) and partial updates, and so there's no reason to create proxies of your objects nor require virtual properties and methods.

Here's an example persistent object in Hammock:

```
class Product
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public IList<string> Categories { get; set; }
}
```

Because CouchDB is a [document database](http://en.wikipedia.org/wiki/Document-oriented_database), you can include complex data structures like lists and dictionaries in your persistent objects and they will be persisted automatically.

Although we've done away with the need to 'map' your object as in NHibernate, you can exert some control over exactly how your objects are serialized to [JSON](http://json.org/) (the native storage format in CouchDB). Hammock uses [Json.NET](http://james.newtonking.com/projects/json-net.aspx) for all serialization, so simply apply the various Json.NET serialization attributes to your classes.

```
    [JsonProperty("name")]
    public string Name { get; set; }
```

The `Repository` object maps a peristent type to a CouchDB design document. You can use a `Repository` for convenient, strongly typed `Saving()`ing and `Load()`ing, and also issue complex queries to CouchDB using a fluent syntax:

```
var r = new Repository<Product>(s);
var z = r.Where(x => x.Name).Eq("PS3")
         .And(x => x.Price).Le(400)
         .List();
```

Hammock will update CouchDB's design document with a new view:

```
{
    "_id": "_design/product",
    "language": "javascript",
    "views": {
        "by-name-price": {
            "map": "
                function(doc) {
                    if (doc._id.indexOf('product-') === 0) {
                        emit([doc.Name, doc.Price], null);
                    }
                }",
            "reduce": null
        }
    }
}
```

And then execute a request against that view, using the parameters specified:

```
GET /sample-db/_design/product/_view/by-name-cost?startkey=["PS3",null]&endkey=["PS3",400]
```