# Introduction #

One feature of Hammock is that it does not require that entities inherit from a base class or implement any interface. However, a side effect of this feature is that by default an entity does not contain its CouchDB Id or Revision information. This is generally considered a feature as well, since database id's and revision numbers are persistence details, and don't normally need to be exposed in the application's domain objects.

However, in cases where it is desirable that a domain object has id and revision information closely associated to it, the object may either inherit from the Document class or implement IHasDocument. Both approaches have exactly the same functionality, so choosing between them is simply a matter of which works best in your object model.

# Document #

The easiest way to give your entities awareness of their CouchDB Id and Revision is to subclass them from Document:

```
public class MyEntity : Document
{
    ...
}
```

You can know access `.Id` and `.Revision` properties of your entities.

# IHasDocument #

The IHasDocument interface defines only a single property, a Document:

```
public interface IHasDocument
{
  [JsonIgnore] Document Document { get; set; }
}
```

When you implement IHasDocument in one of your entities, Hammock will fill the document property with the Id and Revision information of the entity at the time it was loaded:

```
public class MyEntity : IHasDocument
{
  [JsonIgnore] public Document Document { get; set; }  
  
  public string MyDomainProperty { get; set; }
  ...
}

var x = session.Load<MyEntity>("an-entity-with-this-id");
Assert.That(x.Document.Id, Is.EqualTo("an-entity-with-this-id");
```

It's important to include the `[JsonIgnore]` attribute so that the Document property is not serialized -- Hammock knows your entities' Id and Revision information and encodes them the way that CouchDB likes (as `_`id and `_`rev) and allowing the Document property to be serialized would just add noise to your documents in the store.