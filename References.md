# Introduction #

One of the drawbacks of object databases is that it becomes more difficult for one object to reference another. Here are two typical domain objects that need maintain references between each other:

```

public class Course
{
    public int Level { get; set; }
    public string Name { get; set; }
    public string Professor { get; set; }
}

public class Professor
{
    public string Department { get; set; }
    public string Name { get; set; }
}

```

In this object model however, we have no idea what the `Professor` property of `Course` is. Is it the professor's plain language name, like "Dr. Joe Blazer"? Or is it a CouchDB `_id` property, like "professor-1234"?

The `Reference` type solves this problem:

```

public class Course
{
    ...
    public Reference<Professor> Professor { get; set; }
}

```

We've now made clear in our domain model what the `Professor` property's purpose is. Furthermore, we've made it far easier and more expressive to work with in our code:

```

var professors = new Repository<Professor>();
var courses = new Repository<Courses>();

var professorBlazer = new Professor { Name = "Dr. Joe Blazer" };
var professorVest = new Professor { Name = "Dr. Dan Vest" };

professors.Save(professorBlazer);
professors.Save(professorVest);

var course300 = new Course
{
    Level = 300,
    Name = "Introduction to CouchDB",
    Professor = Reference.To(professorBlazer)
};

var course301 = new Course
{
    Level = 310,
    Name = "Advanced Hammock Programming",
    Professor = Reference.To(professorVest)
}

courses.Save(course300);
courses.Save(course301);

```

Now when you read your `Course` objects back out, you can easily traverse to their associated professor.

```

var c300 = course.Get("course-300");
ViewData["professorName"] = c300.Professor.Value.Name;

```

# Restrictions #

There are a few things to keep in mind about using `Reference`:

  * Any referenced object must either inherit from `Document` or implement `IHasDocument` (see SubclassingDocument for details on what this means). So to make the previous example work, we would actually need to modify the `Professor` definition slightly:

```

public class Professor : Document
{
    ...
}
 
```

  * `Save` does not cascade, so you must save the referenced object prior to saving the referencing object. This only applies when the referenced object is new, obviously. So this would fail:

```

var bob = new Professor { ... };
var c300 = new Course { Professor = bob };
courses.Save(c300);

```

> To fix it, you would need to call `professors.Save(bob)` before `courses.Save(c300)`.