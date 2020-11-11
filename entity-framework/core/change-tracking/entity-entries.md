---
title: Accessing tracked entities - EF Core
description: Using EntityEntry, DbContext.Entries, and DbSet.Local to access tracked entities
author: ajcvickers
ms.date: 21/12/2020
uid: core/change-tracking/entity-entries
---

# Accessing tracked entities

> [!TIP]
> This document assumes that entity states and the basics of EF core change tracking are understood. See [Change Tracking in EF Core]() for more information on these topics.

There are four main APIs for accessing entities already tracked by a `DbContext`:

- `DbContext.Entry` returns an `EntityEntry` for a given entity
- `DbContext.ChangeTracker.Entities` returns `EntityEntry` instances for all tracked entities, or all tracked entities of a given type
- `Find` or `FindAsync` on `DbSet` or `DbContext` finds a single entity by primary key, first looking in tracked entities
- `DbSet.Local` returns actual entities (not `EntityEntry` instances) for entities of the `DbSet`

Each of these are described in more detail below.

## Using DbContext.Entry and EntityEntry instances

For each tracked entity, EF Core keeps track of:
- The overall state of the entity. This is one of `Unchanged`, `Modified`, `Added`, or `Deleted`; see [Change Tracking in EF Core]() for more information.
- The relationships between tracked entities. For example, the Blog to which a Post belongs.
- The "current values" of properties.
- The "original values" of properties, when this information is available. Original values are the property values that existed when entity was queried from the database.
- Which property values have been modified since they were queried
- Other information about property values, such as whether or not the value is a temporary value that will be replaced when SaveChanges is called.

Passing an entity instance to `DbContext.Entry` results in an `EntityEntry` providing access to this information for the given entity. For example:

```c#
        using var context = new BlogsContext();
        var blog = context.Blogs.Single();
        var entityEntry = context.Entry(blog);
```

The following sections show how to use an EntityEntry to access and manipulate entity state, as well as the state of the entity's properties and navigations.

### Work with the entity

The most common use of `EntityEntry` is to access the current `EntityState` of an entity. For example:

```c#
        var currentState = context.Entry(blog).State;
        if (currentState == EntityState.Unchanged)
        {
            context.Entry(blog).State = EntityState.Modified;
        }
```

`DbContext.Entry` can be used on entities that are not yet tracked by the DbContext. This does _not_ start tracking the entity; the state of the entity is still `Detatched`. However, the entry can now be used to change the entity state, at which point the entity will become tracked in the given state. For example, the following code will start tracking a Blog instance as `Added`:

```c#
        var newBlog = new Blog();
        Debug.Assert(context.Entry(newBlog).State == EntityState.Detached);

        context.Entry(newBlog).State = EntityState.Added;
        Debug.Assert(context.Entry(newBlog).State == EntityState.Added);
```

> [!TIP]
> Unlike in EF6, setting the state of an individual entity will not cause all connected entities to be tracked. This makes setting the state this way a lower-level operation than calling `Add`, `Attach`, or `Update`, which operate on an entire graph of entities.

The following table summarizes ways to use an `EntityEntry`:

| EntityEntry member                               | Description
|:-------------------------------------------------|----------------------
| `EntityEntry.State`                              | The `EntityState` of the entity                    
| `EntityEntry.Entity`                             | The entity instance                    
| `EntityEntry.Context`                            | The `DbContext` that is tracking this entity
| `EntityEntry.Metadata`                           | `IEntityType` metadata for the type of entity 
| `EntityEntry.IsKeySet`                           | Whether or not the entity has had its key value set 
| `EntityEntry.Reload()`                           | Overwrites property values with values read from the database 
| `EntityEntry.DetectChanges()`                    | Forces local detection of chnages for this entity 

### Work with a single property

Several overloads of the `EntityEntry.Property` method allow access to information about an individual property of an entity. For example, using a fluent-like API:

```c#
        var propertyEntry1 = context.Entry(blog).Property(e => e.Name);
```

The property name can instead be passed as a string. For example:

```c#
        var propertyEntry2 = context.Entry(blog).Property<string>("Name");
```

The returned `PropertyEntry` can then be used to access information about the property. For example, it can be used to get and set the current value of the property on this entity:

```c#
        string currentValue1 = context.Entry(blog).Property(e => e.Name).CurrentValue;
        context.Entry(blog).Property(e => e.Name).CurrentValue = "1unicorn2";
```

Both of these methods above return a strongly-typed generic `PropertyEntry<TEntity, TProperty>` instance. Using the generic type is preferred because it allows access to property values without boxing value types. However, if the type of entity or property is not known at compile-time, then a non-generic `PropertyEntry` can be obtained:

```c#
        PropertyEntry propertyEntry3 = context.Entry(someEntity).Property("Name");
```

This allows access to property information for any property regardless of its type, at the expense of boxing value types. For example:

```c#
        object currentValue2 = context.Entry(someEntity).Property("Name").CurrentValue;
        context.Entry(blog).Property(e => e.Name).CurrentValue = "1unicorn2";
```

The following table summarizes property information exposed by `PropertyEntry`:

| PropertyEntry member                               | Description
|:-------------------------------------------------|----------------------
| `PropertyEntry.CurrrentValue`                      | The current value of the property
| `PropertyEntry.OriginalValue`                      | The original value of the property, if available
| `PropertyEntry.EntityEntry`                        | A back reference to the `EntityEntry` for the entity
| `PropertyEntry.Metadata`                           | `IProperty` metadata for the property
| `PropertyEntry.IsModified`                         | Indicates whether this property is marked as modified
| `PropertyEntry.IsTemporary`                        | Indicates whether this property is marked as temporary

Notes:

- The original value of a property is the value the property had when the entity was queried from the database. However, original values are not available if the entity was disconnected and then explicitly attached to a DbContext, for example with Attach or Update. In this case, the original value returned will be the same as the current value.
- SaveChanges will only update properties marked as modified. Set `IsModified` to true to force EF to update a given property value, or set it to false to prevent EF from updating the property value.
- Temporary values are typically generated by EF Core [value generators](). Setting the current value of a property with a temporary value will replace the temporary value with the permanent value set. Set `IsTemporary` to true to force a value to be temporary even after it has been explicitly set.

### Work with a single navigation

Several overloads of `EntityEntry.Reference`, `EntityEntry.Collection`, and `EntityEntry.Navigation` allow access to information about an individual navigation. Navigations to a single related entity are accessed through the `Reference` methods. Reference navigations are used for the "one" sides of one-to-one, or one-to-many relationships. For example:

```c#
        ReferenceEntry<Post, Blog> referenceEntry1 = context.Entry(post).Reference(e => e.Blog);
        ReferenceEntry<Post, Blog> referenceEntry2 = context.Entry(post).Reference<Blog>("Blog");
        ReferenceEntry referenceEntry3 = context.Entry(post).Reference("Blog");
```

Navigations can also be collections or related entities when used for the "many" sides of one-to-many and man=y-to-many relationships. The `Collection` methods are used to access collection navigations. For example:

```c#
        CollectionEntry<Blog, Post> collectionEntry1 = context.Entry(blog).Collection(e => e.Posts);
        CollectionEntry<Blog, Post> collectionEntry2 = context.Entry(blog).Collection<Post>("Posts");
        CollectionEntry collectionEntry3 = context.Entry(blog).Collection("Posts");
```

Some operations are common for all navigations. These can be accessed for both reference and collection navigations using the `Navigation` method. Note that only non-generic access is available when accessing all navigations together. For example: 

```c#
        NavigationEntry navigationEntry = context.Entry(blog).Navigation("Posts");
```

The following table summarizes ways to use `EntityEntry.Reference`, `EntityEntry.Collection`, and `EntityEntry.Navigation`:

| NavigationEntry member                             | Description
|:---------------------------------------------------|----------------------
| `NavigationEntry.CurrrentValue`                    | The current value of the navigation property, which will be a collection for collection navigations
| `NavigationEntry.Metadata`                         | `INavigationBase` metadata for the navigation
| `NavigationEntry.IsLoaded`                         | Indicates whether the related entity or collection has been fully loaded from the database
| `NavigationEntry.Load()`                           | Loads the related entity or collection from the database. See [Explicit Loading]().
| `NavigationEntry.Query()`                          | The query EF would use to load this navigation as an `IQueryable` that can be further composed. See [Explicit Loading]().

### Work with a all properties of an entity

`EntityEntry.Properties` returns an `IEnumerable<PropertyEntry>` for every property of the entity. This can be used to perform some action for every property. For example, to set any `DateTime` property to `DateTime.Now`:

```c#
        foreach (var propertyEntry in context.Entry(blog).Properties)
        {
            if (propertyEntry.Metadata.ClrType == typeof(DateTime))
            {
                propertyEntry.CurrentValue = DateTime.Now;
            }
        }
```

In addition, `EntityEntry` contains several methods to get and set all property values at the same time. These methods use the `PropertyValues` class, which represents a collection of properties and their values. A `PropertyValues` can be obtained for current or original values, or for the values as currently stored in the database. For example:

```c#
        var currentValues = context.Entry(blog).CurrentValues;
        var originalValues = context.Entry(blog).OriginalValues;
        var databaseValues = context.Entry(blog).GetDatabaseValues();
```

These `PropertyValues` objects are not very useful on their own. However, they can be combined to perform common operations needed when manipulating entities. This is useful when working with data transfer objects (DTOs) and when resolving optimistic concurrency conflicts. The following sections show some examples.

#### Setting current or original values from another object

The current or original values of an entity can be updated by copying values from another object. For example:

```c#
        var blogDto = new BlogDto { Id = 1, Name = "1unicorn2" };

        context.Entry(blog).CurrentValues.SetValues(blogDto);
```

This technique is sometimes used when updating an entity with values obtained from a service call or a client in an n-tier application. Note that the object used does not have to be of the same type as the entity so long as it has properties whose names match those of the entity. In the example above, an instance of the DTO BlogDto is used to set the current values of a tracked Blog entity. 

Note that only properties that are set to different values when copied from the other object will be marked as modified.

#### Setting current or original values from the database

The current or original values of aN entity can be updated with the latest values from the database by calling `GetDataabaseValues()` and setting the returned object on `CurrentValues`, `OriginalValues`, or both. For example:

```c#
        var databaseValues = context.Entry(blog).GetDatabaseValues();
        context.Entry(blog).CurrentValues.SetValues(databaseValues);
        context.Entry(blog).OriginalValues.SetValues(databaseValues);
```

#### Setting current or original values from a dictionary

The current or original values of a tracked entity can be updated by copying values from a dictionary. For example:

```c#
        var blogDictionary = new Dictionary<string, object>
        {
            ["Id"] = 1, 
            ["Name"] = "1unicorn2"
        };

        context.Entry(blog).CurrentValues.SetValues(blogDictionary);
```

#### Creating a cloned object containing current, original, or database values

The `PropertyValues` object returned from `CurrentValues`, `OriginalValues`, or `GetDatabaseValues` can be used to create a clone of the entity. For example:

```c#
var clonedBlog = context.Entry(blog).GetDatabaseValues().ToObject();
```

Note that `ToObject()` returns a new instance that is not tracked by the DbContext. The returned object also does not have any relationships set to other objects.

The cloned object can be useful for resolving issues related to concurrent updates to the database, especially where a UI that involves data binding to objects of a certain type is being used. See [optimistic concurrency]() for more information.

### Work with a all navigations of an entity

`EntityEntry.Navigations` returns an `IEnumerable<NavigationEntry>` for all navigations of the entity. `EntityEntry.References` and `EntityEntry.Collections` do the same thing, but restricted to rteference or collection navigations respectively. This can be used to perform some action for every navigation. For example, to force loading of all related entities:

```c#
        foreach (var navigationEntry in context.Entry(blog).Navigations)
        {
            navigationEntry.Load();
        }
```

### Work with a all members of an entity

Regular properties and navigation properties have very different state and behavior. It is therefore common to process navigations and non-navigations separately, as shown in the sections above. However, sometimes it can be useful to do something with any member of the entity, regardless of whether it is a regular property or navigation. The `Member` and `Members` methods support this. For example:

```c#
        foreach (var memberEntry in context.Entry(blog).Members)
        {
            Console.WriteLine(
                $"Member {memberEntry.Metadata.Name} is of type {memberEntry.Metadata.ClrType.ShortDisplayName()} and has value {memberEntry.CurrentValue}");
        }
```

Running this code on a blog from the sample generates the following output:

```
Member Id is of type 'int' and has value '1'
Member Name is of type 'string' and has value '1unicorn2'
Member Posts is of type 'IList<Post>' and has value 'System.Collections.Generic.List`1[Post]'
```

### EntityEntry DebugView

The `EntityEntry.DevbugView` property shows entity state in a human-readable form. This is very convenient when debugging. For example, rather than writing the code in the preceding example, one can simply look at the `DebugView`:

```
Blog {Id: 1} Modified
  Id: 1 PK
  Name: '1unicorn2' Modified Originally '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
```

## Find and FindAsync

`DbContext.Find`, `DbSet.Find`, and their async equivalents are designed for efficient lookup of a single entity when its primary key is known. It first checks if the entity is already being tracked and if so returns the entity immediately. A database query is only made if the entity is not found locally. For example, consider this code that calls `Find` twice for the same entity:

```c#
        using var context = new BlogsContext();

        Console.WriteLine("First call to Find...");
        var blog1 = context.Blogs.Find(1);

        Console.WriteLine($"...found blog {blog1.Name}");
        
        Console.WriteLine();
        Console.WriteLine("Second call to Find...");
        var blog2 = context.Blogs.Find(1);
        Debug.Assert(blog1 == blog2);
        
        Console.WriteLine("...returned the same instance without executing a query.");
```

The output from this code (including EF logging) when using SQLite is:

```
First call to Find...
info: 12/23/2020 10:04:27.192 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command) 
      Executed DbCommand (0ms) [Parameters=[@__p_0='1' (DbType = String)], CommandType='Text', CommandTimeout='30']
      SELECT "b"."Id", "b"."Name"
      FROM "Blogs" AS "b"
      WHERE "b"."Id" = @__p_0
      LIMIT 1
...found blog .NET Blog
Second call to Find...
...returned the same instance without executing a query.
```

Notice that the first call does not find the entity locally and so executes a database query to bring back the entity. On the other hand, the second call returns the same instance without querying the database because it is already being tracked locally. 

Find returns null if an entity with the given key is not tracked locally and does not exist in the database.

### Composite keys

Find can also be used with composite keys. For example, consider an `OrderLine` entity with a composite key consisting of the order ID and the product ID:

```c#
public class OrderLine
{
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    
    //...
}
```

The composite key must be configured in OnModelCreating to define the key parts _and their order_. For example:

```c#
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<OrderLine>()
            .HasKey(e => new { e.OrderId, e.ProductId });
    }
```

Notice that `OrderId` is the first part of the key and `ProductId` is the second part of the key. This order must be used when passing key values to Find. For example:

```c#
        var orderline = context.OrderLines.Find(orderId, productId);
```
## Using ChangeTracker.Entries to access all tracked entities

So far we have accessed only a single `EntityEntry` at a time. `ChangeTracker.Entries` can be used to obtain an `EntityEntry` for every entity currently tracked by the context. For example:

```c#
        using var context = new BlogsContext();
        var blogs = context.Blogs.Include(e => e.Posts).ToList();

        foreach (var entityEntry in context.ChangeTracker.Entries())
        {
            Console.WriteLine($"Found {entityEntry.Metadata.Name} entity with ID {entityEntry.Property("Id").CurrentValue}");
        }
```

This code generates the following output:

```
Found Blog entity with ID 1
Found Post entity with ID 1
Found Post entity with ID 2
Found Post entity with ID 3
```

Notice that entries for both blogs and posts are returned. This can instead be filtered to a specific entity type using the generic overload:

```c#
        foreach (var entityEntry in context.ChangeTracker.Entries<Post>())
        {
            Console.WriteLine(
                $"Found {entityEntry.Metadata.Name} entity with ID {entityEntry.Property(e => e.Id).CurrentValue}");
        }
```

The output from this code shows that only posts have been returned:

```
Found Post entity with ID 1
Found Post entity with ID 2
Found Post entity with ID 3
```

Also, using the generic overload returns generic `EntityEntry<TEntity>` instances. This is what allows that fluent-like access to the `Id` property in this example. The generic type used to filter does not have to be a mapped entity type; an unmapped base type of interface can also be used.

## Using DbSet.Local to query tracked entities

Normal EF Core queries are always executed on the database. DbSet.Local provides a mechanism to query the DbContext for local, tracked entities.

Since DbSet.Local is used to query tracked entities, it is typical to load entities into the DbContext and then work with those loaded entities. This is especially true for data binding, but can also be useful in other situations. For example, in the following code the database is first queried for all blogs and posts. The `Load` extension method is used to execute this query with the results tracked by the context without being returned directly from the query. (Using `ToList` or similar has the same effect but with the overhead of creating the returned list, which is not needed here.) The example then uses `DbSet.Local` to access the locally tracked entities.

```c#
        using var context = new BlogsContext();
        
        context.Blogs.Include(e => e.Posts).Load();

        foreach (var blog in context.Blogs.Local)
        {
            Console.WriteLine($"Blog: {blog.Name}");
        }

        foreach (var post in context.Posts.Local)
        {
            Console.WriteLine($"Post: {post.Title}");
        }
```

Notice that, unlike the Entries method, DbSet.Local returns entity instances, not `EntityEntry` instances. An EntityEntry can, of course, always be obtained for the returned entity by calling the Entry method.

### The local view

`DbSet.Local` provides a view of locally tracked entities that reflects the current EntityState of those entities. Specifically, this means that:

- `Added` entities are included. Note that this is not the case for normal EF queries, since Added entities do not yet exist in the database and so are never returned by a database query.
- `Deleted` entities are excluded. Note that this is again not the case for normal EF queries, since Deleted entities still exist in the database and so _are_ returned by a database query.

The result of this is that `DbSet.Local` provides a view over the data that reflects what the database will look like when SaveChanges is called. This is typically the ideal view for data binding, since it presents to the user the data as they understand it based ong the changes made by the app. For example, once the application has marked an entity as `Deleted` then it is removed from the view even though that change has not yet been persisted to the database.

The following code demonstrates this my marking one Post as Deleted and then adding a new Post:

```c#
        using var context = new BlogsContext();

        var posts = context.Posts.Include(e => e.Blog).ToList();

        Console.WriteLine("Local view after loading posts:");

        foreach (var post in context.Posts.Local)
        {
            Console.WriteLine($"  Post: {post.Title}");
        }

        context.Remove(posts[1]);
        
        context.Add(new Post
        {
            Title = "What’s next for System.Text.Json?",
            Content = ".NET 5.0 was released recently and has come with many...",
            Blog = posts[0].Blog
        });

        Console.WriteLine("Local view after adding and deleting posts:");

        foreach (var post in context.Posts.Local)
        {
            Console.WriteLine($"  Post: {post.Title}");
        }
```

The output from this code is:

```
Local view after loading posts:
  Post: Announcing the Release of EF Core 5.0
  Post: Announcing F# 5
  Post: Announcing .NET 5.0
Local view after adding and deleting posts:
  Post: What’s next for System.Text.Json?
  Post: Announcing the Release of EF Core 5.0
  Post: Announcing .NET 5.0
```

Notice that the deleted post is removed from the local view, and the added post is included.

### Using Local to add and remove entities

`DbSet.Local` returns an instance of `LocalView<TEntity>`. This is an implementation of `ICollection<TEntity>` that generates and responds to notifications when entities are added and removed from the collection. (This is the same concept as the `ObervableCollection` class, but implemented as a view over EF local data.) The local view's notifications are hooked into DbContext change tracking such that the local view stays in sync with the DbContext. Specifically:

- Adding a new entity to `DbSet.Local` causes it to be tracked by the DbContext, typically in the Added state.
- Removing an entity from `DbSet.Local` causes it to be marked as deleted.
- An entity that becomes tracked by the DbContext will automatically appear in the `DbSet.Local` collection. For example, executing a query to bring in more entities automatically causes the local view to be updated.
- An entity that is marked as `Deleted` will be automatically removed from the local collection. 

This means we can use the local view to manipulate tracked entities simply by adding and removing from the collection.

For example, lets modify the previous example code to add and remove posts from the local collection:

```c#
        using var context = new BlogsContext();

        var posts = context.Posts.Include(e => e.Blog).ToList();

        Console.WriteLine("Local view after loading posts:");

        foreach (var post in context.Posts.Local)
        {
            Console.WriteLine($"  Post: {post.Title}");
        }

        context.Posts.Local.Remove(posts[1]);

        context.Posts.Local.Add(new Post
        {
            Title = "What’s next for System.Text.Json?",
            Content = ".NET 5.0 was released recently and has come with many...",
            Blog = posts[0].Blog
        });

        Console.WriteLine("Local view after adding and deleting posts:");

        foreach (var post in context.Posts.Local)
        {
            Console.WriteLine($"  Post: {post.Title}");
        }
```

The output remains unchanged from the previous example because changes made to the local view are synced with the DbContext.

### Using the local view for Windows Forms or WPF data binding

DbSet.Local forms the basis for data binding to EF Core entities. However, both Windows Forms and WPF work best when used with the type of notification collection they expect. LocalView supports creating these specific collection types:

- `LocalView.ToObservableCollection` returns an `ObservableCollection<T>` for WPF data binding.
- `LocalView.ToBindingList` returns an `ToBindingList<T>` for Windows Forms data binding.

For example:

```c#
        ObservableCollection<Post> observableCollection = context.Posts.Local.ToObservableCollection();
        BindingList<Post> bindingList = context.Posts.Local.ToBindingList();
```

See [data binding with WPF]() and [data binding with Windows Forms] for more information on data binding to EF Core.

> [!TIP]
> The local view for a given DbSet instance is created lazily when first accessed and then cached. LocalView creation is fast and it does not use significant memory. The collections created by `ToObservableCollection` and `ToBindingList` are also created lazily and and then cached. However, both of these methods create new collections, which can be slow and use a lot of memory when many thousands of entities are involved.
