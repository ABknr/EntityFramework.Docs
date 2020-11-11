---
title: Change Tracking - EF Core
description: Overview of change tracking for EF Core  
author: ajcvickers
ms.date: 10/11/2020
uid: core/change-tracking/index
---

# Change Tracking in EF Core

Each `DbContext` instance tracks changes made to entities. These tracked entities in turn drive the changes to the database when `SaveChanges` is called.

This article provides an overview of Entity Framework Core (EF Core) change tracking and how it relates to queries and updates.

## How to track entities

Entity instances become tracked when they are:

- Returned from a query against the database
- Explicitly attached to the DbContext by Add, Attach, or Update
- Detected as new entities connected to existing tracked entities

Entity instances are no longer tracked when:

- The DbContext is disposed
- The change tracker is cleared (EF Core 5.0 and later)
- The entities are explicitly detached

`DbContext` is designed to represent a short-lived unit-of-work, as described in [DbContext Initialization and Configuration](xref:core/dbcontext-configuration/index). This means that disposing the `DbContext` is _the normal way_ to stop tracking entities. In other words, the lifetime of a DbContext should be:

1. Create the DbContext instance
2. Track some entities
3. Make some changes to the entities
4. Call SaveChanges to update the database
5. Dispose the DbContext instance

> [!TIP]
> It is not necessary to clear the change tracker or explicitly detach entity instances when taking this approach. However, if you do need to detach entities, then calling ChangeTracker.Clear is more efficient than detaching entities one-by-one.

## Entity states

Every entity is is associated with a given <xref:Microsoft.EntityFrameworkCore.EntityState>:

- `Detached` entities are not being tracked by the DbContext.
- `Added` entities are new and have not yet been inserted into the database. This means they will be inserted when <xref:Microsoft.EntityFrameworkCore.DbContext.SaveChanges%2A> is called.
- `Unchanged` entities have not been changed since they were queried from the database. All entities returned from queries are initially in this state.
- `Modified` entities have been changed since they were queried from the database. This means they will be updated when SaveChanges is called.
- `Deleted` entities exist in the database, but are marked to be deleted when SaveChanges is called.

EF Core tracks changes at the property level. For example, if only a single property value is modified, then the database update will change only that value. However, properties can only be marked as modified when the entity itself is in the `Modified` state. (Or, from an alternate perspective, the `Modified` state means that at least one property value has been changed.)

The following table summarizes the different states:

| Entity state     | Tracked by DbContext | Exists in database | Properties modified | Action on SaveChanges*
|:-----------------|----------------------|--------------------|---------------------|-----------------------
| `Detached`       | No                   | -                  | -                   | -
| `Added`          | Yes                  | No                 | -                   | Insert
| `Unchanged`      | Yes                  | Yes                | No                  | -
| `Modified`       | Yes                  | Yes                | Yes                 | Update
| `Deleted`        | Yes                  | Yes                | -                   | Delete

> [!NOTE]
> This text uses relational database terms for clarity. NoSQL databases typically support similar operations but possibly with different names. Consult your database provider documentation for more information.

# Tracking from queries

EF Core change tracking works best when the same <xref:Microsoft.EntityFrameworkCore.DbContext> instance is used to both query for entities and update them by calling SaveChanges. This is because EF Core automatically tracks the state of queried entities and then automatically detects and changes made to these entities when SaveChanges is called.

This approach has several advantages over explicit tracking (see next section):

- It is simple. Entity states rarely need to be manipulated explicitly; EF Core takes care of state changes.
- Updates are limited to only those values that have actually changed.
- The values of [shadow-state properties]() are preserved and used as needed. This is especially relevant when foreign keys are stored in shadow-state.
- The original values of properties are preserved automatically.

## Simple query and update

For example, consider a simple Blog/Posts model:

```c#
public class Blog
{
    public int Id { get; set; }

    public string Name { get; set; }

    public ICollection<Post> Posts { get; } = new List<Post>();
}

public class Post
{
    public int Id { get; set; }
    
    public string Title { get; set; }
    public string Content { get; set; }
    
    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}
```

We can use this model to query for Blogs and Posts and then make some updates to the database:

```c#
        using (var context = new BlogsContext())
        {
            var blog = context.Blogs.Include(e => e.Posts).First(e => e.Name == ".NET Blog");

            blog.Name = ".NET Blog (Updated!)";

            foreach (var post in blog.Posts.Where(e => !e.Title.Contains("5.0")))
            {
                post.Title = post.Title.Replace("5", "5.0");
            }

            context.SaveChanges();
        }
```

Calling SaveChanges results in the following database updates, using SQLite as an example database:

```
info: 12/21/2020 14:39:21.132 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command) 
      Executed DbCommand (0ms) [Parameters=[@p1='1' (DbType = String), @p0='.NET Blog (Updated!)' (Size = 20)], CommandType='Text', CommandTimeout='30']
      UPDATE "Blogs" SET "Name" = @p0
      WHERE "Id" = @p1;
      SELECT changes();
info: 12/21/2020 14:39:21.132 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command) 
      Executed DbCommand (0ms) [Parameters=[@p1='2' (DbType = String), @p0='Announcing F# 5.0' (Size = 17)], CommandType='Text', CommandTimeout='30']
      UPDATE "Posts" SET "Title" = @p0
      WHERE "Id" = @p1;
      SELECT changes();
```

Notice that:
- There are no calls to set explicit entity states or mark property values as modified.
- Updates are only sent to the database for the `Blog.Name` property that has changed, and the `Post.Title` property for the Post that has changed. Other properties and entities that have not changed are not updated in the database. 

## Query then insert, update, and delete

Updates like those in the previous example can be combined with inserts and deletes in the same unit-of-work. For example:

```c#
        using (var context = new BlogsContext())
        {
            var blog = context.Blogs.Include(e => e.Posts).First(e => e.Name == ".NET Blog");

            // Modify property values
            blog.Name = ".NET Blog (Updated!)";

            // Insert a new Post
            blog.Posts.Add(new Post
            {
                Title = "What’s next for System.Text.Json?",
                Content = ".NET 5.0 was released recently and has come with many..."
            });
            
            // Mark an existing Post as Deleted

            var postToDelete = blog.Posts.Single(e => e.Title == "Announcing F# 5");
            context.Remove(postToDelete);
            
            context.SaveChanges();
        }
```

In this example:
- A Blog and related Posts are tracked from the database
- The `blog.Name` property is changed. This [change will be detected]() when `SaveChanges` is called.
- A new Post is added to the collection of existing posts for the blog. This [change will also be detected]() when SaveChanges is called.
- An existing Post is marked for deletion by calling `DbContext.Remove`.

This results in the following database updates when SaveChanges is called:

```
info: 12/21/2020 14:59:47.903 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command) 
      Executed DbCommand (0ms) [Parameters=[@p1='1' (DbType = String), @p0='.NET Blog (Updated!)' (Size = 20)], CommandType='Text', CommandTimeout='30']
      UPDATE "Blogs" SET "Name" = @p0
      WHERE "Id" = @p1;
      SELECT changes();
info: 12/21/2020 14:59:47.903 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command) 
      Executed DbCommand (0ms) [Parameters=[@p0='2' (DbType = String)], CommandType='Text', CommandTimeout='30']
      DELETE FROM "Posts"
      WHERE "Id" = @p0;
      SELECT changes();
info: 12/21/2020 14:59:47.903 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command) 
      Executed DbCommand (0ms) [Parameters=[@p0='1' (DbType = String), @p1='.NET 5.0 was released recently and has come with many...' (Size = 56), @p2='What’s next for System.Text.Json?' (Size = 33)], CommandType='Text', CommandTimeout='30']
      INSERT INTO "Posts" ("BlogId", "Content", "Title")
      VALUES (@p0, @p1, @p2);
      SELECT "Id"
      FROM "Posts"
      WHERE changes() = 1 AND "rowid" = last_insert_rowid();
```

See following sections on _Explicit tracking_ for more information on inserting and deleting entities. See [Detecting changes]() for more information on how EF Core automatically detects changes like this.

# Explicit tracking

Entities can be explicitly "attached" to a <xref:Microsoft.EntityFrameworkCore.DbContext> such that the context then tracks those entities. This is primarily useful when:

1. Creating new entities that will be inserted into the database.
2. Re-attaching disconnected entities that were previously queried by a _different_ DbContext instance.

The first of these will be needed by most applications, and is primary handled by the `Add` methods.

The second is only needed by applications that change entities or their relationships _while the entities are not being tracked_. For example, a web application may send entities to the web client where the user makes changes and sends the entities back. These entities are referred to as "disconnected" since they were originally queried from a DbContext, but were then disconnected from that context when sent to the client. The web application must then re-attach these entities and indicate the changes that have been made such that <xref:Microsoft.EntityFrameworkCore.DbContext.SaveChanges%2A> can make appropriate updates to the database. This is primarily handled by the <xref:Microsoft.EntityFrameworkCore.DbContext.Attach%2A> and <xref:Microsoft.EntityFrameworkCore.DbContext.Update%2A> methods.

> [!TIP]
> Attaching entities to the _same DbContext instance_ that they were queried from should not normally be needed. Do not routinely perform a no-tracking query and then attach the returned entities to the context. This will be both slower and harder to get right than using a tracking query. 

## Generated vs explicit key values

By default, integer and GUID properties are configured to use [automatically generated key values](). This has a major advantage for change tracking: an unset key value indicates that the entity is "new". That is, it has not yet been inserted into the database. 

Two models are used in the following sections. The first is configured to use non-generated key values:

```c#
public class Blog
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    public string Name { get; set; }

    public ICollection<Post> Posts { get; } = new List<Post>();
}

public class Post
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }
    
    public string Title { get; set; }
    public string Content { get; set; }
    
    public int? BlogId { get; set; }
    public Blog Blog { get; set; }
}
```

Non-generated key values are shown first in each example because everything is very explicit and easy to follow. This is then followed by an example where generated key values are used:

```c#
public class Blog
{
    public int Id { get; set; }

    public string Name { get; set; }

    public ICollection<Post> Posts { get; } = new List<Post>();
}

public class Post
{
    public int Id { get; set; }
    
    public string Title { get; set; }
    public string Content { get; set; }
    
    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}
```

Notice that the key properties need no additional configuration here since generated key values is the default for integer keys.

## Inserting new entities

### Explicit key values

For an entity to be inserted by `SaveChanges` it must be tracked in the `Added` state, as described earlier. Entities are typically put in the Added state by calling one of the `Add`, `AddRange`, or `AddAsync` methods on either a <xref:Microsoft.EntityFrameworkCore.DbContext> or a <xref:Microsoft.EntityFrameworkCore.DbSet>. For example, to start tracking a new Blog:

```c#
            context.Add(
                new Blog
                {
                    Id = 1,
                    Name = ".NET Blog",
                });
```

Inspecting the [ChangeTracker.DebugView]() following this call shows that the context is tracking the new entity in the `Added` state:

```output
Blog {Id: 1} Added
  Id: 1 PK
  Name: '.NET Blog'
  Posts: []
```

However, the `Add` method doesn't just work on an individual entity. It actually starts tracking an entire graph of related entities, putting them all to the `Added` state. For example, to insert a new Blog and associated new Posts:

```c#
            context.Add(
                new Blog
                {
                    Id = 1,
                    Name = ".NET Blog",
                    Posts =
                    {
                        new Post
                        {
                            Id = 1,
                            Title = "Announcing the Release of EF Core 5.0",
                            Content = "Announcing the release of EF Core 5.0, a full featured cross-platform..."
                        },
                        new Post
                        {
                            Id = 2,
                            Title = "Announcing F# 5",
                            Content = "F# 5 is the latest version of F#, the functional programming language..."
                        },
                        new Post
                        {
                            Id = 3,
                            Title = "Announcing .NET 5.0",
                            Content = ".NET 5.0 includes many enhancements, including single file applications, more..."
                        },
                    }
                });
```

The context is now tracking all these entities as `Added`:

```output
Blog {Id: 1} Added
  Id: 1 PK
  Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
Post {Id: 1} Added
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Added
  Id: 2 PK
  BlogId: 1 FK
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: {Id: 1}
Post {Id: 3} Added
  Id: 3 PK
  BlogId: 1 FK
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
```

Notice that explicit values have been set for the `Id` key properties in the examples above. This is because the model here has been configured to use explicitly set key values, rather than automatically generated key values. When not using generated keys, the key properties must be explicitly in this way _before_ calling `Add`. These key values are then inserted when `SaveChanges` is called. For example, when using SQLite:

```sql
INSERT INTO "Blogs" ("Id", "Name")
VALUES (@p0, @p1);

INSERT INTO "Posts" ("Id", "BlogId", "Content", "Title")
VALUES (@p2, @p3, @p4, @p5);

INSERT INTO "Posts" ("Id", "BlogId", "Content", "Title")
VALUES (@p0, @p1, @p2, @p3);

INSERT INTO "Posts" ("Id", "BlogId", "Content", "Title")
VALUES (@p0, @p1, @p2, @p3);
```

After SaveChanges completes, all of the entities are now tracked in the `Unchanged` state, since they now match the state in the database:

```output
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
Post {Id: 1} Unchanged
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
  Id: 2 PK
  BlogId: 1 FK
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: {Id: 1}
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: 1 FK
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
```

### Generated key values

As mentioned above, by default, integer and GUID properties are configured to use [automatically generated key values](). This means that the application _must not set any key value explicitly_. For example, to insert a new Blog and new Posts all with generated key values:

```c#
            context.Add(
                new Blog
                {
                    Name = ".NET Blog",
                    Posts =
                    {
                        new Post
                        {
                            Title = "Announcing the Release of EF Core 5.0",
                            Content = "Announcing the release of EF Core 5.0, a full featured cross-platform..."
                        },
                        new Post
                        {
                            Title = "Announcing F# 5",
                            Content = "F# 5 is the latest version of F#, the functional programming language..."
                        },
                        new Post
                        {
                            Title = "Announcing .NET 5.0",
                            Content = ".NET 5.0 includes many enhancements, including single file applications, more..."
                        },
                    }
                });
```

As with explicit key values, the context is now tracking all these entities as `Added`:

```output
Blog {Id: -2147482647} Added
  Id: -2147482647 PK Temporary
  Name: '.NET Blog'
  Posts: [{Id: -2147482647}, {Id: -2147482646}, {Id: -2147482645}]
Post {Id: -2147482647} Added
  Id: -2147482647 PK Temporary
  BlogId: -2147482647 FK Temporary
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: -2147482647}
Post {Id: -2147482646} Added
  Id: -2147482646 PK Temporary
  BlogId: -2147482647 FK Temporary
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: {Id: -2147482647}
Post {Id: -2147482645} Added
  Id: -2147482645 PK Temporary
  BlogId: -2147482647 FK Temporary
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: {Id: -2147482647}
```

Notice in this case that temporary key values have been generated for each entity. These values are used by EF Core until SaveChanges is called, at which point real key values are read back from the database. For example, when using SQLite:

```sql
INSERT INTO "Blogs" ("Name")
VALUES (@p0);
SELECT "Id"
FROM "Blogs"
WHERE changes() = 1 AND "rowid" = last_insert_rowid();

INSERT INTO "Posts" ("BlogId", "Content", "Title")
VALUES (@p1, @p2, @p3);
SELECT "Id"
FROM "Posts"
WHERE changes() = 1 AND "rowid" = last_insert_rowid();

INSERT INTO "Posts" ("BlogId", "Content", "Title")
VALUES (@p0, @p1, @p2);
SELECT "Id"
FROM "Posts"
WHERE changes() = 1 AND "rowid" = last_insert_rowid();

INSERT INTO "Posts" ("BlogId", "Content", "Title")
VALUES (@p0, @p1, @p2);
SELECT "Id"
FROM "Posts"
WHERE changes() = 1 AND "rowid" = last_insert_rowid();
```

After SaveChanges completes, all of the entities have been updated with their real key values and are tracked in the `Unchanged` state since they now match the state in the database:

```output
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
Post {Id: 1} Unchanged
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
  Id: 2 PK
  BlogId: 1 FK
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: {Id: 1}
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: 1 FK
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
```

You might notice that this is exactly the same end-state as the example using non-generated key values.

> [!TIP]
> An explicit key value can still be set even when using generated key values. EF Core will then attempt to insert using this key value. Some database configurations, including SQL Server with Identity columns, do not support such inserts and will throw.

## Attaching existing entities

### Explicit key values

Entities returned from queries are tracked in the `Unchanged` state. The `Unchanged` state means that the entity has not been modified since it was queried, as described earlier. A disconnected entity, perhaps returned from a web client in an HTTP request, can be put into this state using either `Attach` or `AttachRange`. For example, to start tracking an existing Blog:

```c#
            context.Attach(
                new Blog
                {
                    Id = 1,
                    Name = ".NET Blog",
                });
```

> [!NOTE]
> The examples here are creating entities explicitly with `new`. Normally the entity instances will have come from another source, such as being deserialized from a client, or being created from data in an HTTP Post.

Inspecting the [ChangeTracker.DebugView]() following this call shows that the context is tracking the entity in the `Unchanged` state:

```output
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Posts: []
```

Just like `Add`, `Attach` actually sets an entire graph of connected entities to the `Unchanged` state. For example, to attach an existing Blog and associated existing Posts:

```c#
            context.Attach(
                new Blog
                {
                    Id = 1,
                    Name = ".NET Blog",
                    Posts =
                    {
                        new Post
                        {
                            Id = 1,
                            Title = "Announcing the Release of EF Core 5.0",
                            Content = "Announcing the release of EF Core 5.0, a full featured cross-platform..."
                        },
                        new Post
                        {
                            Id = 2,
                            Title = "Announcing F# 5",
                            Content = "F# 5 is the latest version of F#, the functional programming language..."
                        },
                        new Post
                        {
                            Id = 3,
                            Title = "Announcing .NET 5.0",
                            Content = ".NET 5.0 includes many enhancements, including single file applications, more..."
                        },
                    }
                });
```

The context is now tracking all these entities as `Unchanged`:

```output
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
Post {Id: 1} Unchanged
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
  Id: 2 PK
  BlogId: 1 FK
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: {Id: 1}
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: 1 FK
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
```

Calling `SaveChanges` at this point will have no effect. All the entities are marked as `Unchanged`, so there is nothing to update in the database.

### Generated key values

As mentioned above, by default, integer and GUID properties are configured to use [automatically generated key values](). This has a major advantage when working with disconnected entities: an unset key value indicates that the entity has not yet been inserted into the database. This allows the DbContext to automatically detect new entities and put them in the `Added` state. For example, consider attaching this graph of a Blog and Posts:

```c#
            context.Attach(
                new Blog
                {
                    Id = 1,
                    Name = ".NET Blog",
                    Posts =
                    {
                        new Post
                        {
                            Id = 1,
                            Title = "Announcing the Release of EF Core 5.0",
                            Content = "Announcing the release of EF Core 5.0, a full featured cross-platform..."
                        },
                        new Post
                        {
                            Id = 2,
                            Title = "Announcing F# 5",
                            Content = "F# 5 is the latest version of F#, the functional programming language..."
                        },
                        new Post
                        {
                            Title = "Announcing .NET 5.0",
                            Content = ".NET 5.0 includes many enhancements, including single file applications, more..."
                        },
                    }
                });
```

The Blog has a key value of `1`, indicating that it already exists in the database. Two of the posts also have key values set, but the third does not. EF Core will see this key value as `0`, the CLR default for an integer. This results in EF Core marking the new entity as `Added` instead of `Unchanged`:

```output
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: -2147482644}]
Post {Id: -2147482644} Added
  Id: -2147482644 PK Temporary
  BlogId: 1 FK
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
Post {Id: 1} Unchanged
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
  Id: 2 PK
  BlogId: 1 FK
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: {Id: 1}
```

Calling `SaveChanges` at this point does nothing with the `Unchanged` entities, but inserts the new entity into the database. For example, when using SQLite:

```sql
INSERT INTO "Posts" ("BlogId", "Content", "Title")
VALUES (@p0, @p1, @p2);
SELECT "Id"
FROM "Posts"
WHERE changes() = 1 AND "rowid" = last_insert_rowid();
```

The important point to notice here is that, with generated key values, EF Core is able to automatically distinguish new from existing entities in a disconnected graph. In essence, by default EF Core will always insert an entity when that entity has no key value set.

After `SaveChanges`, all entities are in the `Unchanged` state as usual:

```output
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
Post {Id: 1} Unchanged
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Unchanged
  Id: 2 PK
  BlogId: 1 FK
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: {Id: 1}
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: 1 FK
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
```

## Updating existing entities

### Explicit key values

`Update` behaves exactly as `Attach` except that entities are put into the `Modfied` instead of the `Unchanged` state. For example, to start tracking an existing blog as `Modified`:

```c#
            context.Update(
                new Blog
                {
                    Id = 1,
                    Name = ".NET Blog"
                });
```

Inspecting the [ChangeTracker.DebugView]() following this call shows that the context is tracking this existing entity in the `Modified` state:

```output
Blog {Id: 1} Modified
  Id: 1 PK
  Name: '.NET Blog' Modified
  Posts: []
```

Just like `Add` and `Attach`, `Update` actually marks an _entire graph_ of related entities as `Modified`. For example, to attach an existing blog and associated existing posts as `Modified`:

```c#
            context.Update(
                new Blog
                {
                    Id = 1,
                    Name = ".NET Blog",
                    Posts =
                    {
                        new Post
                        {
                            Id = 1,
                            Title = "Announcing the Release of EF Core 5.0",
                            Content = "Announcing the release of EF Core 5.0, a full featured cross-platform..."
                        },
                        new Post
                        {
                            Id = 2,
                            Title = "Announcing F# 5",
                            Content = "F# 5 is the latest version of F#, the functional programming language..."
                        },
                        new Post
                        {
                            Id = 3,
                            Title = "Announcing .NET 5.0",
                            Content = ".NET 5.0 includes many enhancements, including single file applications, more..."
                        },
                    }
                });
```

The context is now tracking all these entities as `Modified`:

```output
Blog {Id: 1} Modified
  Id: 1 PK
  Name: '.NET Blog' Modified
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
Post {Id: 1} Modified
  Id: 1 PK
  BlogId: 1 FK Modified Originally <null>
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...' Modified
  Title: 'Announcing the Release of EF Core 5.0' Modified
  Blog: {Id: 1}
Post {Id: 2} Modified
  Id: 2 PK
  BlogId: 1 FK Modified Originally <null>
  Content: 'F# 5 is the latest version of F#, the functional programming...' Modified
  Title: 'Announcing F# 5' Modified
  Blog: {Id: 1}
Post {Id: 3} Modified
  Id: 3 PK
  BlogId: 1 FK Modified Originally <null>
  Content: '.NET 5.0 includes many enhancements, including single file a...' Modified
  Title: 'Announcing .NET 5.0' Modified
  Blog: {Id: 1}
```

Calling SaveChanges at this point will cause updates to be sent to the database for all these entities. For example, when using SQLite:

```sql
UPDATE "Blogs" SET "Name" = @p0
WHERE "Id" = @p1;
SELECT changes();

UPDATE "Posts" SET "BlogId" = @p0, "Content" = @p1, "Title" = @p2
WHERE "Id" = @p3;
SELECT changes();

UPDATE "Posts" SET "BlogId" = @p0, "Content" = @p1, "Title" = @p2
WHERE "Id" = @p3;
SELECT changes();

UPDATE "Posts" SET "BlogId" = @p0, "Content" = @p1, "Title" = @p2
WHERE "Id" = @p3;
SELECT changes();
```

### Generated key values

As mentioned above, by default, integer and GUID properties are configured to use [automatically generated key values](). As with `Attach`, this has a major benefit for `Update`: an unset key value indicates that the entity is new and has not yet been inserted into the database. As with `Attach`, this allows the DbContext to automatically detect new entities and put them in the `Added` state. For example, consider calling `Update` with this graph of a blog and posts:

```c#
            context.Update(
                new Blog
                {
                    Id = 1,
                    Name = ".NET Blog",
                    Posts =
                    {
                        new Post
                        {
                            Id = 1,
                            Title = "Announcing the Release of EF Core 5.0",
                            Content = "Announcing the release of EF Core 5.0, a full featured cross-platform..."
                        },
                        new Post
                        {
                            Id = 2,
                            Title = "Announcing F# 5",
                            Content = "F# 5 is the latest version of F#, the functional programming language..."
                        },
                        new Post
                        {
                            Title = "Announcing .NET 5.0",
                            Content = ".NET 5.0 includes many enhancements, including single file applications, more..."
                        },
                    }
                });
```

As with the `Attach` example, the post with no key value set is detected as new and set to the `Added` state. The other entities are marked as `Modified`:  

```output
Blog {Id: 1} Modified
  Id: 1 PK
  Name: '.NET Blog' Modified
  Posts: [{Id: 1}, {Id: 2}, {Id: -2147482639}]
Post {Id: -2147482639} Added
  Id: -2147482639 PK Temporary
  BlogId: 1 FK
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
Post {Id: 1} Modified
  Id: 1 PK
  BlogId: 1 FK Modified Originally <null>
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...' Modified
  Title: 'Announcing the Release of EF Core 5.0' Modified
  Blog: {Id: 1}
Post {Id: 2} Modified
  Id: 2 PK
  BlogId: 1 FK Modified Originally <null>
  Content: 'F# 5 is the latest version of F#, the functional programming...' Modified
  Title: 'Announcing F# 5' Modified
  Blog: {Id: 1}
```

Calling `SaveChanges` at this point will cause updates to be sent to the database for all the existing entities, while the new entity is inserted. For example, when using SQLite:

```sql
UPDATE "Blogs" SET "Name" = @p0
WHERE "Id" = @p1;
SELECT changes();

UPDATE "Posts" SET "BlogId" = @p0, "Content" = @p1, "Title" = @p2
WHERE "Id" = @p3;
SELECT changes();

UPDATE "Posts" SET "BlogId" = @p0, "Content" = @p1, "Title" = @p2
WHERE "Id" = @p3;
SELECT changes();

INSERT INTO "Posts" ("BlogId", "Content", "Title")
VALUES (@p0, @p1, @p2);
SELECT "Id"
FROM "Posts"
WHERE changes() = 1 AND "rowid" = last_insert_rowid();
```

This is a very easy way to generate updates and inserts from a disconnected graph. However, it results in updates or inserts being sent to the database for every property of every entity in the graph, even when some property values may not have been changed. Don't be too scared by this; for many applications with small graphs, this can be an easy and pragmatic way of generating updates. That being said, other more complex patterns can sometimes result in more efficient updates, as described in the [Disconnected Entities]() documentation. 

## Deleting existing entities

For an entity to be deleted by `SaveChanges` it must be tracked in the `Deleted` state, as described earlier. Entities are typically put in the `Deleted` state by calling one of the `Remove` or `RemoveRange` methods on either a <xref:Microsoft.EntityFrameworkCore.DbContext> or a <xref:Microsoft.EntityFrameworkCore.DbSet>. For example, to mark an existing Post as `Deleted`:

```c#
            context.Remove(
                new Post
                {
                    Id = 2
                });
```

> [!NOTE]
> The examples here are creating entities explicitly with `new`. Normally the entity instances will have come from another source, such as being deserialized from a client, or being created from data in an HTTP Post.

Inspecting the [ChangeTracker.DebugView]() following this call shows that the context is tracking the entity in the `Deleted` state:

```output
Post {Id: 2} Deleted
  Id: 2 PK
  BlogId: <null> FK
  Content: <null>
  Title: <null>
  Blog: <null>
``` 

This entity will be deleted when SaveChanges is called. For example, when using SQLite:

```sql
DELETE FROM "Posts"
WHERE "Id" = @p0;
SELECT changes();
```

After SaveChanges completes, the deleted entity is detached from the DbContext since it no longer exists in the database. Output from `ChangeTracker.DebugView` shows that the change tracker is not tracking any entities:

```output
```

### Deleting child/dependent entities

As mentioned above, it is unusual to call `Remove` on an entity created with `new`. Further, unlike `Add`/`Attach`/`Update`, it is uncommon to call `Remove` on an entity that isn't already tracked in the `Unchanged` or `Modified` state. Instead it is typical to start track a single entity or graph of related entities, and then call `Remove` on the entities that should be deleted. This graph of tracked entities is typically created by:

1. Running a query for the entities
2. Using the `Attach` or `Update` methods on a graph of disconnected entities, as described in the preceding sections.

Updating the example above to use this pattern for a single entity:

```c#
            context.Attach(post);
            context.Remove(post);
```

This behaves exactly the same way as the previous example, since calling `Remove` on an untracked entity causes it to first be attached and then marked as `Deleted`.

In more realistic examples, a graph of entities is first attached, and then some of those entities are marked as deleted. For example:

```c#
            // Attach a blog and associated posts
            context.Attach(blog);
            
            // Mark one post as Deleted
            context.Remove(blog.Posts[1]);
```
Inspecting the `ChangeTracker.DebugView` following the call to Remove shows that only that entity is marked as `Deleted`:

```output
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
Post {Id: 1} Unchanged
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Deleted
  Id: 2 PK
  BlogId: 1 FK
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: {Id: 1}
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: 1 FK
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
``` 

This entity will be deleted when SaveChanges is called. For example, when using SQLite:

```sql
DELETE FROM "Posts"
WHERE "Id" = @p0;
SELECT changes();
```

After SaveChanges completes, the deleted entity is detached from the DbContext since it no longer exists in the database. Other entities remain in the `Unchanged` state:

```output
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 3}]
Post {Id: 1} Unchanged
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: 1 FK
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
```

### Deleting parent/principal entities

Each relationship that connects two entity types has a principal or parent end, and a dependent or child end. For the purpose of this discussion, the child/dependent entity is the one with the foreign key property. In a one-to-many relationship, the principal/parent is on the "one" side, and the dependent/child is on the "many" side. See [modelling relationships]() for full details.

In the preceding examples we were deleting a Post, which is a dependent/child entity in the Blog-Posts one-to-many relationship. This is relatively straightforward since removal of a child/dependent entity does not have any impact on other entities. On the other hand, deleting a principal/parent entity must also impact any child/dependent entities. Not doing so would leave a foreign key value referencing a primary key value that no longer exists. This is an invalid model state and results in a referential constraint error in most databases.

This invalid model state can be handled in two ways:

1. Setting FK values to null. This indicates that the dependents/children are no longer related to any principal/parent. This is the default for optional relationships where the foreign key must be nullable. Setting the FK to null is not valid for required relationships, where the foreign key is typically non-nullable.
2. Deleting the the dependents/children. This is the default for required relationships, and is also valid for optional relationships.

#### Optional relationships

The `Post.BlogId` foreign key property is nullable in  the model we have been using. This means the relationship is optional, and hence the default behavior of EF Core is to null the `BlogId` foreign key properties when the Blog is deleted. For example:

```c#
            context.Attach(blog);
            context.Remove(blog);
```

Inspecting the `ChangeTracker.DebugView` following the call to Remove shows that, as expected, the Blog is now marked as `Deleted`:

```
Blog {Id: 1} Deleted
  Id: 1 PK
  Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
Post {Id: 1} Modified
  Id: 1 PK
  BlogId: <null> FK Modified Originally 1
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: <null>
Post {Id: 2} Modified
  Id: 2 PK
  BlogId: <null> FK Modified Originally 1
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: <null>
Post {Id: 3} Modified
  Id: 3 PK
  BlogId: <null> FK Modified Originally 1
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: <null>
```

More interestingly, all the related Posts are now marked as `Modified`. This is because the foreign key property in each entity has been set to null. Calling SaveChanges causes these updates to the database to set these nulls, before then deleting the Blog:

```sql
UPDATE "Posts" SET "BlogId" = @p0
WHERE "Id" = @p1;
SELECT changes();

UPDATE "Posts" SET "BlogId" = @p0
WHERE "Id" = @p1;
SELECT changes();

UPDATE "Posts" SET "BlogId" = @p0
WHERE "Id" = @p1;
SELECT changes();

DELETE FROM "Blogs"
WHERE "Id" = @p2;
SELECT changes();
```

After SaveChanges completes, the deleted entity is detached from the DbContext since it no longer exists in the database. Other entities are now marked as `Unchanged` with null foreign key values, which matches the state of the database:

```
Post {Id: 1} Unchanged
  Id: 1 PK
  BlogId: <null> FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: <null>
Post {Id: 2} Unchanged
  Id: 2 PK
  BlogId: <null> FK
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: <null>
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: <null> FK
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: <null>
```

#### Required relationships

If `Post.BlogId` foreign key property is non-nullable, then the relationship between Blogs and Posts becomes "required". In this situation, EF Core will, by default, delete dependent/child entities when the principal/parent is deleted. For example, deleting a Blog with related posts as in the previous example:

```c#
            context.Attach(blog);
            context.Remove(blog);
```

Inspecting the `ChangeTracker.DebugView` following the call to Remove shows that, as expected, the Blog is again marked as `Deleted`:

```
Blog {Id: 1} Deleted
  Id: 1 PK
  Name: '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
Post {Id: 1} Deleted
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
Post {Id: 2} Deleted
  Id: 2 PK
  BlogId: 1 FK
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: {Id: 1}
Post {Id: 3} Deleted
  Id: 3 PK
  BlogId: 1 FK
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
```

More interestingly in this case is that all related Posts have also been marked as `Deleted`. Calling SaveChanges causes the Blog and all related Posts to be deleted from the database:

```sql
DELETE FROM "Posts"
WHERE "Id" = @p0;
SELECT changes();

DELETE FROM "Posts"
WHERE "Id" = @p0;
SELECT changes();

DELETE FROM "Posts"
WHERE "Id" = @p0;
SELECT changes();

DELETE FROM "Blogs"
WHERE "Id" = @p1;
SELECT changes();
```

After SaveChanges completes, all the deleted entities are detached from the DbContext since they no longer exists in the database. Output from `ChangeTracker.DebugView` shows that the change tracker is not tracking any entities:

```output
```

> [!NOTE]
> See [modeling relationships]() for more information on configuring optional and required relationships, and [cascade deletes]() for more information on updating/deleting dependent/child entities when calling SaveChanges.  



# Miscellaneous
## Using AddAsync
## Default values (new pattern)
## Using AddRange, AttachRange, etc.
## DbContext vs DbSet methods (Shared-type entity types)
## Temporary values
## Query and apply changes.
## Property access modes
## State change events


# Identity resolution
## Intro, single instance
## Append-only semantics
## TrackGraph
## Overriding object equality
- Equals/GetHashCode
- Reference equality in equals

