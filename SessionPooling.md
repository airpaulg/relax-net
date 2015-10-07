# Introduction #

The Repository and Query capabilities of Hammock require it to load (and update) your database's various design documents. The first time you use a ` Repository<Foo> ` on any given Session, that Session needs to load the design document for Foo. In order to prevent this from occurring on each and every page request, Hammock provides the familiar connection pooling system to recycle your Session instances.

# Enabling Session Pooling #

Session pooling works automatically whenever you create a Session from a Connection. Simply use the Session normally, and when the Session is garbage collected by the runtime (or manually disposed) it will be returned to the Connection for reuse.

```
var c = new Connection(new Uri("http://localhost:5984"));
using (var s = c.CreateSession())
{
    // session will be recycled when the using {} block exits.
    var u = s.Load<User>("user-foo");
    u.SetPassword("bar");
    s.Save(u);
}
```

If you then request a new Session, you will actually receive a reference to the previous Session.

```
// t and s now reference the same Session object
var t = c.CreateSession();
```

However you request the same User entity from the Session you will receive a _new_ reference. This is because the Session ejects all non-design documents from its cache when it recycles.

```
// u and v are /different/ objects
var v = t.Load<User>("user-foo");
t.Save(u); // throws InvalidOperationException: u isn't enrolled in this Session anymore
t.Save(v); // works
```

# Controlling Session Lifecycle #

Sometimes you'll want to keep a Session from being recycled. The intended use case here is that a Session needs to be used for a long running task on a worker thread, and would otherwise be disposed (and therefore recycled) by the IoC that created it.

To prevent a Session from being recycled, take a Lock() on it. Lock returns an IDisposable. Until the IDisposable is disposed, the Session will not be recycled. Lock() uses an internal reference count, so you may take as many Lock()s as you wish, ensuring to Dispose() each returned object.

```
using (var s = c.CreateSession())
{
    var lok = s.Lock();
    new Thread(() => {
        try
        {
            // do long running work with session here.. s will not
            // be recycled, even after the using {} block exits.
        }
        finally
        {
            lok.Dispose();
        }
    }).Start();
}

```