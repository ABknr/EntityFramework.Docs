---
title: Changing foreign keys and navigations - EF Core
description: How to change relationships between entities by manipulating foreign keys and navigations  
author: ajcvickers
ms.date: 23/12/2020
uid: core/change-tracking/relationship-changes
---

# Changing foreign keys and navigations

## Overview of foreign keys and navigations

> [!TIP]
> This document assumes that entity states and the basics of EF core change tracking are understood. See [Change Tracking in EF Core]() for more information on these topics.

Relationships in an EF model are represented using foreign keys (FKs). An FK consists of one or more properties on the dependent or child entity in the relationship. This dependent/child entity is associated with a given principal/parent entity when the values of the foreign key properties on the dependent/child match the values of primary or alternate key properties on the principal/parent.

Foreign keys are a good way to store and manipulate relationships in the database, but are not very friendly when working with multiple related entities in application code. Therefore, most EF models also layer "navigations" over the FK representation. Navigations form references between entity instances that reflect the associations found by matching foreign key values to primary or alternate key values. 

For example, the following model contains four entity types with relationships between them:

```c#
public class Blog
{
    public int Id { get; set; } // Primary key
    public string Name { get; set; }

    public IList<Post> Posts { get; } = new List<Post>(); // Collection navigation
    public BlogAssets Assets { get; set; } // Reference navigation
}

public class BlogAssets
{
    public int Id { get; set; } // Primary key
    public byte[] Banner { get; set; }
    
    public int BlogId { get; set; } // Foreign key
    public Blog Blog { get; set; } // Reference navigation
}

public class Post
{
    public int Id { get; set; } // Primary key
    public string Title { get; set; }
    public string Content { get; set; }
    
    public int? BlogId { get; set; } // Foreign key
    public Blog Blog { get; set; } // Reference navigation
    
    public IList<Tag> Tags { get; } = new List<Tag>(); // Skip collection navigation
}

public class Tag
{
    public int Id { get; set; } // Primary key
    public string Text { get; set; }
    
    public IList<Post> Posts { get; } = new List<Post>(); // Skip collection navigation
}
```

The relationships are:

- Each Blog can have many Posts (one-to-many):
  - Post is the dependent/child in this relationship. It contains the FK property `Post.BlogId`.
  - Blog is the principal/parent in this relationship. The `Post.BlogId` value must match the `Blog.Id` value of a Blog for a Post to be associated with that Blog.
  - `Post.Blog` is a reference navigation to the associated Blog. That is, the `BlogId` of the Post matches the `Id` of the Blog.
  - `Blog.Posts` is a collection navigation to all the associated Posts. That is, the `BlogId` of each Post matches the `Id` of the Blog.
- Each Blog can have one BlogAssets (one-to-one):
  - BlogAssets is the dependent/child in this relationship. It contains the FK property `BlogAssets.BlogId`.
  - Blog is the principal/parent in this relationship. The `BlogAssets.BlogId` value must match the `Blog.Id` value of a Blog for a BlogAssets to be associated with that Blog. 
  - `BlogAssets.Blog` is a reference navigation to the associated Blog. That is, the `BlogId` of the BlogAssets matches the `Id` of the Blog.
  - `Blog.Assets` is a collection navigation to all the associated BlogAssets. That is, the `BlogId` of the BlogAssets matches the `Id` of the Blog.
- Each Post can be associated with many Tags and each Tag can be associated with many Posts (many-to-many):
  - Many-to-many relationships are a further layer over two one-to-many relationships. Many-to-many relationships are covered later in this document.

See [modelling relationships]() for more information on how to configure relationships.

## Relationship fixup

EF Core keeps navigations in alignment with foreign key values. That is, if a foreign key value changes such that it now refers to a different principal/parent entity, then the navigations are updated to reflect this change. Likewise, if a navigation is changed, then the foreign key values of the entities involved are updated to reflect this change. This is called "relationship fixup".

The first time fixup occurs is when entities are queried from the database. The database has only foreign key values, so when EF creates an entity instance from the database it uses the foreign key values found to set reference navigations and add entities to collection navigations as appropriate.

For example, consider a query for blogs and its associated posts and assets:

```c#
        using var context = new BlogsContext();

        var blogs = context.Blogs
            .Include(e => e.Posts)
            .Include(e => e.Assets)
            .ToList();
        
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
```

For each Blog, EF will first create a Blog instance not associated with an asset or any posts. Then, as each Post is loaded from the database its `Post.Blog` reference navigation is set to point to the associated Blog. Likewise, the Post is added to the `Blog.Posts` collection navigation. The same thing happens with BlogAssets, except in this case both navigations are references. The `Blog.Assets` navigation is set to point to the BlogAssets instance, and the `BlogAsserts.Blog` navigation is set to point to the Blog navigation.

Looking at the ChangeTracker DebugView after this query shows two Blogs, each with one BlogAssets and two Posts being tracked:

```
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Assets: {Id: 1}
  Posts: [{Id: 1}, {Id: 2}]
Blog {Id: 2} Unchanged
  Id: 2 PK
  Name: 'Visual Studio Blog'
  Assets: {Id: 2}
  Posts: [{Id: 3}, {Id: 4}]
BlogAssets {Id: 1} Unchanged
  Id: 1 PK
  Banner: <null>
  BlogId: 1 FK
  Blog: {Id: 1}
BlogAssets {Id: 2} Unchanged
  Id: 2 PK
  Banner: <null>
  BlogId: 2 FK
  Blog: {Id: 2}
Post {Id: 1} Unchanged
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
  Tags: []
Post {Id: 2} Unchanged
  Id: 2 PK
  BlogId: 1 FK
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: {Id: 1}
  Tags: []
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: 2 FK
  Content: 'If you are focused on squeezing out the last bits of perform...'
  Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 2}
  Tags: []
Post {Id: 4} Unchanged
  Id: 4 PK
  BlogId: 2 FK
  Content: 'Examine when database queries were executed and measure how ...'
  Title: 'Database Profiling with Visual Studio'
  Blog: {Id: 2}
  Tags: []
```

The debug view shows both key values and navigations. Navigations are shown using the primary key values of the related entities. For example, `Posts: [{Id: 1}, {Id: 2}]` in the output above indicates that the `Blog.Posts` collection navigation contains two related posts with primary keys 1 and 2 respectively. Similarly, for each post associated with the first blog, the `Blog: {Id: 1}` line indicates that the `Post.Blog` navigation references the Blog with primary key 1.

Relationship fixup also happens between entities returned from a tracking query and entities already tracked by the DbContext. For example, consider executing three separate queries for blogs, posts, and assets:

```c#
        using var context = new BlogsContext();

        var blogs = context.Blogs.ToList();
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        var assets = context.Assets.ToList();
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        var posts = context.Posts.ToList();
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
```

Looking again at the debug views, after the first query only the two Blogs are tracked:

```
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Assets: <null>
  Posts: []
Blog {Id: 2} Unchanged
  Id: 2 PK
  Name: 'Visual Studio Blog'
  Assets: <null>
  Posts: []
```

The `Blog.Assets` reference navigations are null, and the `Blog.Posts` collection navigations are empty because no associated entities are currently being tracked by the context.

After the second query, the `Blogs.Assets` reference navigations have been fixed up to point to the newly tracked `BlogAsset` instances. Likewise, the `BlogAssets.Blog` reference navigations are set to point to the appropriate already tracked Blog.

```
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Assets: {Id: 1}
  Posts: []
Blog {Id: 2} Unchanged
  Id: 2 PK
  Name: 'Visual Studio Blog'
  Assets: {Id: 2}
  Posts: []
BlogAssets {Id: 1} Unchanged
  Id: 1 PK
  Banner: <null>
  BlogId: 1 FK
  Blog: {Id: 1}
BlogAssets {Id: 2} Unchanged
  Id: 2 PK
  Banner: <null>
  BlogId: 2 FK
  Blog: {Id: 2}
```

Finally, after the third query, the `Blog.Posts` collections now contain all related posts, and the `Post.Blog` references point to the appropriate blog:

```
log {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Assets: {Id: 1}
  Posts: [{Id: 1}, {Id: 2}]
Blog {Id: 2} Unchanged
  Id: 2 PK
  Name: 'Visual Studio Blog'
  Assets: {Id: 2}
  Posts: [{Id: 3}, {Id: 4}]
BlogAssets {Id: 1} Unchanged
  Id: 1 PK
  Banner: <null>
  BlogId: 1 FK
  Blog: {Id: 1}
BlogAssets {Id: 2} Unchanged
  Id: 2 PK
  Banner: <null>
  BlogId: 2 FK
  Blog: {Id: 2}
Post {Id: 1} Unchanged
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
  Tags: []
Post {Id: 2} Unchanged
  Id: 2 PK
  BlogId: 1 FK
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: {Id: 1}
  Tags: []
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: 2 FK
  Content: 'If you are focused on squeezing out the last bits of perform...'
  Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 2}
  Tags: []
Post {Id: 4} Unchanged
  Id: 4 PK
  BlogId: 2 FK
  Content: 'Examine when database queries were executed and measure how ...'
  Title: 'Database Profiling with Visual Studio'
  Blog: {Id: 2}
  Tags: []
```

This is the same end-state as was achieved with the original single query, since EF Core fixed up navigations as entities were tracked, even when coming from multiple different queries.

> [!NOTE]
> Fixup never causes more data to be returned from the database. It only connects entities that are part of the query or already tracked by the DbContext.

## Changing relationships using navigations

The easiest way to manipulate the relationships between objects is through interactions with navigations, while leaving EF Core to fixup the FK values appropriately. This can be done by:

- Adding or removing an entity from a collection navigation
- Changing a reference navigation to point to a different entity, or setting it to null

### Adding or removing from collection navigations

For example, let's move one of the posts from the Visual Studio blog to the .NET blog. This requires first loading the blogs and posts, and then moving the post from the navigation collection of posts on one blog to the navigation collection on the other blog: 

```c#
        using var context = new BlogsContext();

        var dotNetBlog = context.Blogs.Include(e => e.Posts).Single(e => e.Name == ".NET Blog");
        var vsBlog = context.Blogs.Include(e => e.Posts).Single(e => e.Name == "Visual Studio Blog");

        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        var post = vsBlog.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));
        vsBlog.Posts.Remove(post);
        dotNetBlog.Posts.Add(post);

        context.ChangeTracker.DetectChanges();
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
```

> [!TIP]
> A call to DetectChanges is needed here because accessing the debug view does not cause DetectChanges to run automatically. See [detecting changes]() for more information.

This is the debug view printed after running this code:

```
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Assets: <null>
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}]
Blog {Id: 2} Unchanged
  Id: 2 PK
  Name: 'Visual Studio Blog'
  Assets: <null>
  Posts: [{Id: 4}]
Post {Id: 1} Unchanged
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
  Tags: []
Post {Id: 2} Unchanged
  Id: 2 PK
  BlogId: 1 FK
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: {Id: 1}
  Tags: []
Post {Id: 3} Modified
  Id: 3 PK
  BlogId: 1 FK Modified Originally 2
  Content: 'If you are focused on squeezing out the last bits of perform...'
  Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 1}
  Tags: []
Post {Id: 4} Unchanged
  Id: 4 PK
  BlogId: 2 FK
  Content: 'Examine when database queries were executed and measure how ...'
  Title: 'Database Profiling with Visual Studio'
  Blog: {Id: 2}
  Tags: []
```
 
The Blog.Posts navigation on the .NET Blog now has three posts (`Posts: [{Id: 1}, {Id: 2}, {Id: 3}]`). Conversely, the Blog.Posts navigation on the Visual Studio blog only has one post (`Posts: [{Id: 4}]`). This is to be expected since the code explicitly changes these collections.

More interestingly, even though the code did explicitly not change the Post.Blog navigation, it has been fixed-up to point to the Visual Studio blog (`Blog: {Id: 1}`). Also, the `Post.BlogId` foreign key value has been updated to match the primary key value of the .NET blog. This change to the Fk value in then persisted to the database when `SaveChanges` is called:

```
info: 12/24/2020 10:31:38.245 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command) 
      Executed DbCommand (0ms) [Parameters=[@p1='3' (DbType = String), @p0='1' (Nullable = true) (DbType = String)], CommandType='Text', CommandTimeout='30']
      UPDATE "Posts" SET "BlogId" = @p0
      WHERE "Id" = @p1;
      SELECT changes();
```

### Changing reference navigations

In the previous example, a post was moved from one blog to another by manipulating the collection navigation of posts on each blog. The same thing can be achieved by instead changing the `Post.Blog` reference navigation to point to the new blog. For example:

```c#
        var post = vsBlog.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));
        post.Blog = dotNetBlog;
```

The debug view after this change is _exactly the same_ as was the case for the previous example. This because EF detected the reference navigation change and then fixed up the collection navigations and FK value to match. 

## Changing relationships using foreign key values

In the previous section, relationships were manipulated by navigations leaving foreign key values to be updated automatically. This is the recommended way to manipulate relationships in EF Core. However, it is also possible to manipulate FK values directly. For example, we can move a post from one blog to another by changing the `Post.BlogId` foreign key value.

```c#
        var post = vsBlog.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));
        post.BlogId = dotNetBlog.Id;
```

Notice how this is very similar to changing the reference navigation, as shown in the previous example.

The debug view after this change is _exactly the same_ as was the case for the previous two examples. This because EF detected the FK value change and then fixed up both the reference and collection navigations to match.

> [!TIP]
> Do not write code to manipulate all navigations and FK values each time a relationship changes. Such code is more complicated and must ensure consistent changes to foreign keys and navigations in every case. If possible, just manipulate navigations. If needed, just manipulate foreign key values. Don't do both.

## Fixup for added or deleted entities 

### Adding to a collection navigation

EF performs the following actions when it detects that a new dependent/child entity has been added to a collection navigation:

- If the entity is not tracked, then it is tracked. (The entity will usually be in the `Added` state. However, if the entity type is configured to use generated keys and the primary key value is set, then the entity is tracked in the `Unchanged` state.)
- If the entity is associated with a different principal/parent, then that relationship is severed.
- The entity is associated with the principal/parent that owns the collection navigation to which the entity was added.
- Navigations and foreign key values are fixed up for all entities involved.

Based on this we can see that to move a post from one blog to another we don't actually need to remove it from the old collection navigation before adding it to the new. So the code from the example above can be changed from:

```c#
        var post = vsBlog.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));
        vsBlog.Posts.Remove(post);
        dotNetBlog.Posts.Add(post);
```

To:

```c#
        var post = vsBlog.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));
        dotNetBlog.Posts.Add(post);
```

EF Core sees that the post has been added to a new blog and automatically removes if from the collection on the first blog.

### Removing from a collection navigation

Removing a dependent/child entity from the collection navigation of the principal/parent causes severing of the relationship to that principal/parent. What happens next depends on how the relationship is configured.

#### Optional relationships

By default for optional relationships, the foreign key value is set to null. This means that the dependent/child t is no longer associated with _any_ principal/parent. For example, let's load a blog and posts and then remove one of the posts from the `Blog.Posts` collection navigation:

```c#
        var post = dotNetBlog.Posts.Single(e => e.Title == "Announcing F# 5");
        dotNetBlog.Posts.Remove(post);
```

Looking at the debug view after this change shows that:

- The `Post.BlogId` FK has been set to null (`BlogId: <null> FK Modified Originally 1`)
- The `Post.Blog` reference navigation has been set to null (`Blog: <null>`)
- The post has been removed from `Blog.Posts` collection navigation (`Posts: [{Id: 1}]`)

```
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Assets: <null>
  Posts: [{Id: 1}]
Post {Id: 1} Unchanged
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
  Tags: []
Post {Id: 2} Modified
  Id: 2 PK
  BlogId: <null> FK Modified Originally 1
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: <null>
  Tags: []
```

Notice that the post is _not_ marked as `Deleted`. It is marked as `Modified` so that the FK value in the database will be set to null when SaveChanges is called.

#### Required relationships

Setting the FK value to null is not allowed (and is usually not possible) for required relationships. Severing a required relationship means that the dependent/child entity must be removed from the database when SaveChanges is called otherwise a referential constraint exception will occur. This is known as "deleting orphans", and is the default behavior in EF Core for required relationships.

For example, let's change the relationship between blog and posts to be required and then run the same code as in the previous example:

```c#
        var post = dotNetBlog.Posts.Single(e => e.Title == "Announcing F# 5");
        dotNetBlog.Posts.Remove(post);
```

Looking at the debug view after this change shows that:

- The post has been marked as `Deleted` such that it will be deleted from the database when SaveChanges is called
- The `Post.Blog` reference navigation has been set to null (`Blog: <null>`)
- The post has been removed from `Blog.Posts` collection navigation (`Posts: [{Id: 1}]`)

```
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Assets: <null>
  Posts: [{Id: 1}]
Post {Id: 1} Unchanged
  Id: 1 PK
  BlogId: 1 FK
  Content: 'Announcing the release of EF Core 5.0, a full featured cross...'
  Title: 'Announcing the Release of EF Core 5.0'
  Blog: {Id: 1}
  Tags: []
Post {Id: 2} Deleted
  Id: 2 PK
  BlogId: 1 FK
  Content: 'F# 5 is the latest version of F#, the functional programming...'
  Title: 'Announcing F# 5'
  Blog: <null>
  Tags: []
```

Notice that the `Post.BlogId` remains unchanged since for a required relationship it cannot be set to null.

Calling SaveChanges results in the orphaned post being deleted:

```
info: 12/24/2020 11:53:47.621 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command) 
      Executed DbCommand (0ms) [Parameters=[@p0='2' (DbType = String)], CommandType='Text', CommandTimeout='30']
      DELETE FROM "Posts"
      WHERE "Id" = @p0;
      SELECT changes();
```

#### Delete orphans timing and re-parenting

By default, marking orphans as `Deleted` happens as soon as the relationship change is detected. However, this process can be delayed until SaveChanges is actually called. This can be useful to avoid making orphans of entities that have been removed from one principal/parent, but will be associated with a new principal/parent before SaveChanges is called. `ChangeTracker.DeleteOrphansTiming` is used to set this timing. For example:

```c#
        context.ChangeTracker.DeleteOrphansTiming = CascadeTiming.OnSaveChanges;
        
        var post = vsBlog.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));
        vsBlog.Posts.Remove(post);

        context.ChangeTracker.DetectChanges();
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        
        dotNetBlog.Posts.Add(post);

        context.ChangeTracker.DetectChanges();
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        context.SaveChanges();
```

After removing the post from the first collection the object is not marked as `Deleted` as it was in the previous example. Instead, EF is tracking that the relationship is severed _even though this is a required relationship_. (The FK value is considered null by EF Core even though it cannot really be null because the type is not nullable. This is known as a "conceptual null".)

```
Post {Id: 3} Modified
  Id: 3 PK
  BlogId: <null> FK Modified Originally 2
  Content: 'If you are focused on squeezing out the last bits of perform...'
  Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  Tags: []
```

Calling SaveChanges at this time would result in the orphaned post being deleted at that time. However, if as in the example able, post is added to a associated with a new blog before SaveChanges is called, then it will be fixed up appropriately to that new blog and is no longer considered an orphan:

```
Post {Id: 3} Modified
  Id: 3 PK
  BlogId: 1 FK Modified Originally 2
  Content: 'If you are focused on squeezing out the last bits of perform...'
  Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 1}
  Tags: []
```

SaveChanges called at this point will update the post in the database rather than deleting it.

It is also possible to turn off automatic deletion of orphans. This will result in an exception if SaveChanges is called while an orphan is being tracked. For example, this code:

```c#
            var dotNetBlog = context.Blogs.Include(e => e.Posts).Single(e => e.Name == ".NET Blog");

            context.ChangeTracker.DeleteOrphansTiming = CascadeTiming.Never;

            var post = dotNetBlog.Posts.Single(e => e.Title == "Announcing F# 5");
            dotNetBlog.Posts.Remove(post);

            context.SaveChanges();
```

Will throw this exception:

```
System.InvalidOperationException: The association between entities 'Blog' and 'Post' with the key value '{BlogId: 1}' has been severed, but the relationship is either marked as required or is implicitly required because the foreign key is not nullable. If the dependent/child entity should be deleted when a required relationship is severed, configure the relationship to use cascade deletes.
   at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.InternalEntityEntry.HandleConceptualNulls(Boolean sensitiveLoggingEnabled, Boolean force, Boolean isCascadeDelete)
   at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.CascadeChanges(Boolean force)
   at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.GetEntriesToSave(Boolean cascadeChanges)
   at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.SaveChanges(DbContext _, Boolean acceptAllChangesOnSuccess)
   at Microsoft.EntityFrameworkCore.Storage.NonRetryingExecutionStrategy.Execute[TState,TResult](TState state, Func`3 operation, Func`3 verifySucceeded)
   at Microsoft.EntityFrameworkCore.ChangeTracking.Internal.StateManager.SaveChanges(Boolean acceptAllChangesOnSuccess)
   at Microsoft.EntityFrameworkCore.DbContext.SaveChanges(Boolean acceptAllChangesOnSuccess)
   at Microsoft.EntityFrameworkCore.DbContext.SaveChanges()
   at Program.ThrowForOrphan() in /home/ajcvickers/dotnet/efdocs/samples/core/RequiredRelationshipTracking/Program.cs:line 83
```

### Changing a reference navigation

Changing the reference navigation of a one-to-many relationship behaves the same as changing the collection navigation on the other end of the relationship. Setting the reference navigation of dependent/child to null is equivalent to removing the entity from the collection navigation of the principal/parent. All fixup and database changes happen as described in the previous section, including making the entity an orphan if the relationship is required.

#### Optional one-to-one relationships

For one-to-one relationships, changing a reference navigation causes any previous relationship to be severed. For optional relationships, this means that the FK value on the previously related dependent/child is set to null. For example:

```c#
        using var context = new BlogsContext();

        var dotNetBlog = context.Blogs.Include(e => e.Assets).Single(e => e.Name == ".NET Blog");
        dotNetBlog.Assets = new BlogAssets();

        context.ChangeTracker.DetectChanges();
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        context.SaveChanges();
```

The debug view before calling SaveChanges shows that the new `BlogAssets` has replaced the existing `BlogAssets`, which is now marked as `Modified` with a null `BlogAssets.BlogId` FK value:

```
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Assets: {Id: -2147482631}
  Posts: []
BlogAssets {Id: -2147482631} Added
  Id: -2147482631 PK Temporary
  Banner: <null>
  BlogId: 1 FK
  Blog: {Id: 1}
BlogAssets {Id: 1} Modified
  Id: 1 PK
  Banner: <null>
  BlogId: <null> FK Modified Originally 1
  Blog: <null>
```

This results in an update and an insert when SaveChanges is called:

```
info: 12/24/2020 12:30:05.466 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command) 
      Executed DbCommand (0ms) [Parameters=[@p1='1' (DbType = String), @p0=NULL], CommandType='Text', CommandTimeout='30']
      UPDATE "Assets" SET "BlogId" = @p0
      WHERE "Id" = @p1;
      SELECT changes();
info: 12/24/2020 12:30:05.466 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command) 
      Executed DbCommand (0ms) [Parameters=[@p2=NULL, @p3='1' (Nullable = true) (DbType = String)], CommandType='Text', CommandTimeout='30']
      INSERT INTO "Assets" ("Banner", "BlogId")
      VALUES (@p2, @p3);
      SELECT "Id"
      FROM "Assets"
      WHERE changes() = 1 AND "rowid" = last_insert_rowid();
```

#### Required one-to-one relationships

Running the same code as in the previous example, but this time with a required one-to-one relationship, shows that the previously associated `BlogAssets` is now marked as `Deleted`, since it becomes an orphan when the new `BlogAssets` takes its place. 

```
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog'
  Assets: {Id: -2147482639}
  Posts: []
BlogAssets {Id: -2147482639} Added
  Id: -2147482639 PK Temporary
  Banner: <null>
  BlogId: 1 FK
  Blog: {Id: 1}
BlogAssets {Id: 1} Deleted
  Id: 1 PK
  Banner: <null>
  BlogId: 1 FK
  Blog: <null>
```

This then results in an delete an and insert when SaveChanges is called:

```
info: 12/24/2020 12:35:40.916 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command) 
      Executed DbCommand (0ms) [Parameters=[@p0='1' (DbType = String)], CommandType='Text', CommandTimeout='30']
      DELETE FROM "Assets"
      WHERE "Id" = @p0;
      SELECT changes();
info: 12/24/2020 12:35:40.916 RelationalEventId.CommandExecuted[20101] (Microsoft.EntityFrameworkCore.Database.Command) 
      Executed DbCommand (0ms) [Parameters=[@p1=NULL, @p2='1' (DbType = String)], CommandType='Text', CommandTimeout='30']
      INSERT INTO "Assets" ("Banner", "BlogId")
      VALUES (@p1, @p2);
      SELECT "Id"
      FROM "Assets"
      WHERE changes() = 1 AND "rowid" = last_insert_rowid();
```

The timing of marking orphans as deleted can be changed in the same way as shown for collection navigations and has the same effects.

### Deleting an entity

#### Optional relationships

When an entity is marked as `Deleted`, for example by calling `DbContext.Remove`, then references to the deleted entity are removed from the navigations of other entities. For optional relationships, the FK values in dependent entities are set to null.

For example, let's mark the Visual Studio blog as `Deleted`:

```c#
        using var context = new BlogsContext();

        var vsBlog = context.Blogs
            .Include(e => e.Posts)
            .Include(e => e.Assets)
            .Single(e => e.Name == "Visual Studio Blog");

        context.Remove(vsBlog);

        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        context.SaveChanges();
```

The debug view before calling SaveChanges:

```
Blog {Id: 2} Deleted
  Id: 2 PK
  Name: 'Visual Studio Blog'
  Assets: {Id: 2}
  Posts: [{Id: 3}, {Id: 4}]
BlogAssets {Id: 2} Modified
  Id: 2 PK
  Banner: <null>
  BlogId: <null> FK Modified Originally 2
  Blog: <null>
Post {Id: 3} Modified
  Id: 3 PK
  BlogId: <null> FK Modified Originally 2
  Content: 'If you are focused on squeezing out the last bits of perform...'
  Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  Tags: []
Post {Id: 4} Modified
  Id: 4 PK
  BlogId: <null> FK Modified Originally 2
  Content: 'Examine when database queries were executed and measure how ...'
  Title: 'Database Profiling with Visual Studio'
  Blog: <null>
  Tags: []
```

Shows that:

- The `Blog` is marked as `Deleted`
- The `BlogAssets` related to the deleted blog has a null FK value (`BlogId: <null> FK Modified Originally 2`) and a null reference navigation (`Blog: <null>`)
- Each 'Post' related to the deleted blog has a null FK value (`BlogId: <null> FK Modified Originally 2`) and a null reference navigation (`Blog: <null>`)

#### Required relationships

The fixup behavior for required relationships is the same as for optional relationships except that the dependent/child entities are marked as `Deleted` since they cannot exist without a principal/parent and must be removed from the database when SaveChanges is called to avoid a referential constraint exception. This is known as "cascade delete", and is the default behavior in EF Core for required relationships. For example, running the same code as in the previous example but with a required relationship results in the following debug view before SaveChanges is called:

```
Blog {Id: 2} Deleted
  Id: 2 PK
  Name: 'Visual Studio Blog'
  Assets: {Id: 2}
  Posts: [{Id: 3}, {Id: 4}]
BlogAssets {Id: 2} Deleted
  Id: 2 PK
  Banner: <null>
  BlogId: 2 FK
  Blog: {Id: 2}
Post {Id: 3} Deleted
  Id: 3 PK
  BlogId: 2 FK
  Content: 'If you are focused on squeezing out the last bits of perform...'
  Title: 'Disassembly improvements for optimized managed debugging'
  Blog: {Id: 2}
  Tags: []
Post {Id: 4} Deleted
  Id: 4 PK
  BlogId: 2 FK
  Content: 'Examine when database queries were executed and measure how ...'
  Title: 'Database Profiling with Visual Studio'
  Blog: {Id: 2}
  Tags: []
```

As expected, the dependents/children are now marked as `Deleted`. However, notice that the navigations on the deleted entities have _not_ changed. This may seem strange, but it avoids completely shredding a deleted graph of entities by clearing all navigations. That is, the blog, asset, and posts still form a graph of entities even after having been deleted. This makes it much easier to un-delete a graph of entities than was the case in EF6 where the graph was shredded.

#### Cascade delete timing and re-parenting

By default, cascade delete happens as soon as the parent/principal is marked as Deleted. This is the same as for deleting orphans, as described previously. As with deleting orphans, this process can be delayed until `SaveChanges` is called, or even disabled entirely, by setting `ChangeTracker.CascadeDeleteTiming` appropriately. This is useful in the same way as it is for deleting orphans, including for re-parenting children/dependents after deletion of a principal/parent.

> [!TIP]
> Cascade delete and deleting orphans are closely related. Both result in deleting dependent/child entities when the relationship to their required principal/parent is severed. For cascade delete, this severing happens because the principal/parent is itself deleted. For orphans, the principal/parent entity still exists, but is no longer related to the dependent/principal entities.  

## Many-to-many relationships

Many-to-many relationships in EF Core are implemented using a join entity. Each side the many-to-many relationship is related to this join entity with a one-to-many relationship. Before EF Core 5.0, this join entity had to explicitly defined. Starting with EF Core 5.0, it can be created implicitly and hidden. However, in both cases the underlying behavior is the same. We will look at this underlying behavior first to understand how tracking of many-to-many relationships works.

### How many-to-many relationships work

Consider this EF Core model that relates creates a many-to-many relationship between Post and Tag using an explicitly defined join entity:

```c#
    public class Post
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public int? BlogId { get; set; }
        public Blog Blog { get; set; }

        public IList<PostTag> PostTags { get; } = new List<PostTag>(); // Collection navigation
    }

    public class Tag
    {
        public int Id { get; set; }
        public string Text { get; set; }

        public IList<PostTag> PostTags { get; } = new List<PostTag>(); // Collection navigation
    }

    public class PostTag
    {
        public int PostId { get; set; } // Foreign key to Post
        public int TagId { get; set; } // Foreign key to Tag
        
        public Post Post { get; set; } // Reference navigation
        public Tag Tag { get; set; } // Reference navigation
    }
```

Notice that the `PostTag` join entity contains two foreign key properties. In this model, for a Post to be related to a Tag, there must be a PostTag join entity where the `PostTag.PostId` foreign key value matches the `Post.Id` primary key value, and where the `PostTag.TagId` foreign key value matches the `Tag.Id` primary key value. For example:

```c#
            using var context = new BlogsContext();

            var post = context.Posts.Single(e => e.Id == 3);
            var tag = context.Tags.Single(e => e.Id == 1);

            context.Add(new PostTag { PostId = post.Id, TagId = tag.Id });
            
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
```

Looking at the debug view after running this code shows that the Post and Tag are related by the new PostTag join entity:

```
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: 2 FK
  Content: 'If you are focused on squeezing out the last bits of perform...'
  Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  PostTags: [{PostId: 3, TagId: 1}]
PostTag {PostId: 3, TagId: 1} Added
  PostId: 3 PK FK
  TagId: 1 PK FK
  Post: {Id: 3}
  Tag: {Id: 1}
Tag {Id: 1} Unchanged
  Id: 1 PK
  Text: '.NET'
  PostTags: [{PostId: 3, TagId: 1}]
```

Notice that the collection navigations on Post and Tag have been fixed up, as have the reference navigations on PostTag. These relationships can be manipulated by navigations instead of FK values, just as in all the preceding examples in this document. For example, the code above can be modified to add the relationship by setting the reference navigations on the join entity:

```c#
            context.Add(new PostTag { Post = post, Tag = tag });
```

This results in exactly the same change to FKs and navigations as in the previous example.

### Skip navigations

Manipulating the join table manually can be cumbersome. Starting with EF Core 5.0, many-to-many relationships can be manipulated directly using special collection navigations that "skip over" the join entity. For example, two skip navigations can be added to the model above; one from Post to Tags, and the other from Tag to Posts:

```c#
    public class Post
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public int? BlogId { get; set; }
        public Blog Blog { get; set; }

        public IList<Tag> Tags { get; } = new List<Tag>(); // Skip collection navigation
        public IList<PostTag> PostTags { get; } = new List<PostTag>(); // Collection navigation
    }

    public class Tag
    {
        public int Id { get; set; }
        public string Text { get; set; }

        public IList<Post> Posts { get; } = new List<Post>(); // Skip collection navigation
        public IList<PostTag> PostTags { get; } = new List<PostTag>(); // Collection navigation
    }

    public class PostTag
    {
        public int PostId { get; set; } // Foreign key to Post
        public int TagId { get; set; } // Foreign key to Tag
        
        public Post Post { get; set; } // Reference navigation
        public Tag Tag { get; set; } // Reference navigation
    }
```

This many-to-many relationship requires the following configuration. 

```c#
            modelBuilder.Entity<Post>()
                .HasMany(p => p.Tags)
                .WithMany(p => p.Posts)
                .UsingEntity<PostTag>(
                    j => j.HasOne(t => t.Tag).WithMany(p => p.PostTags).HasForeignKey(j => j.TagId),
                    j => j.HasOne(t => t.Post).WithMany(p => p.PostTags).HasForeignKey(j => j.PostId),
                    j => j.HasKey(j => new { j.PostId, j.TagId }));
```

See [modelling relationships]() for more information on mapping many-to-many relationships.

Skip navigations look and behave like normal collection navigations. However, the way they work with foreign key values is different. Let's associate a Post with a Tag as before, but this time using the skip navigations:

```c#
            using var context = new BlogsContext();

            var post = context.Posts.Single(e => e.Id == 3);
            var tag = context.Tags.Single(e => e.Id == 1);

            post.Tags.Add(tag);
            
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
```

Notice that this code doesn't use the join entity. It instead just adds an entity to a navigation collection in the same way as would be done if this were a one-to-many relationship. The resulting debug view is essentially the same as before:

```
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: 2 FK
  Content: 'If you are focused on squeezing out the last bits of perform...'
  Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  PostTags: [{PostId: 3, TagId: 1}]
  Tags: [{Id: 1}]
PostTag {PostId: 3, TagId: 1} Added
  PostId: 3 PK FK
  TagId: 1 PK FK
  Post: {Id: 3}
  Tag: {Id: 1}
Tag {Id: 1} Unchanged
  Id: 1 PK
  Text: '.NET'
  PostTags: [{PostId: 3, TagId: 1}]
  Posts: [{Id: 3}]
```

Notice that an instance of the `PostTag` join entity was created automatically with foreign key values set to the PK values of the Tag and Post that are now associated. All the normal reference and collection navigations have been fixed up to match these FK values. Also, since this model contains skip navigations, these have also been fixed up. Specifically, even though we added the Tag with the `Post.Tags` skip navigation, the `Tag.Posts` skip navigation on the other side of this relationship has also been fixed up to contain the associated Post.

It is worth noting that the underlying many-to-many relationships can still be manipulated directly even when skip navigations have been layered on top. For example, the Tag and Post could be associated as we did before introducing skip navigations:

```c#
            context.Add(new PostTag { Post = post, Tag = tag });
```

This will still result in the skip navigations being fixed up correctly, resulting in the same debug view output as in the previous example.

### Skip navigations only

In the previous section we added skip navigations _in addition to_ fully defining the two underlying one-to-many relationships. This is useful to illustrate what happens to FK values, but is often unnecessary. Instead, the many-to-many relationship can be defined using _only_ skip navigations. This is how the many-to-many is defined in the model at the very top of this document. Using this model, we can again associate a Post and a Tag using by adding a post to the `Tag.Posts` skip navigation (or adding a tag to the `Post.Tags` skip navigation):

```c#
            post.Tags.Add(tag);
```

Looking at the debug view after making this change reveals that EF Core has created an instance of `Dictionary<string, object>` to represent the join entity. This entity contains both `PostId` and `TagId` foreign key properties which have been set to match the PK values of the Post and Tag that are associated.  

```
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: 2 FK
  Content: 'If you are focused on squeezing out the last bits of perform...'
  Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  Tags: [{Id: 1}]
Tag {Id: 1} Unchanged
  Id: 1 PK
  Text: '.NET'
  Posts: [{Id: 3}]
PostTag (Dictionary<string, object>) {PostsId: 3, TagsId: 1} Added
  PostsId: 3 PK FK
  TagsId: 1 PK FK
```

See [modelling relationships]() for more information about implicit join entities, and [shared-type entity types]() for more information on mapping `Dictionary<string, object>` entity types.

### Join entities with payloads

So far all the examples have used a join entity (whether explicit or implicit) that contains only the two foreign key properties needed to associate the entities on either end of the many-to-many relationship. Neither of these properties need to be explicitly set by the application when manipulating relationships using navigations. Instead, these values are populated from the primary key values of the related entities. This allows EF Core to create instances of the join entity without missing data.

#### Payloads with generated values

It is also possible to add additional properties to the join entity type. This is known as giving the join entity a "payload". For example, let's add `TaggedOn` property to the `PostTag` join entity:

```c#
    public class PostTag
    {
        public int PostId { get; set; } // Foreign key to Post
        public int TagId { get; set; } // Foreign key to Tag
        
        public DateTime TaggedOn { get; set; } // Payload
    }
```

Now when EF creates an instance of this join entity we need make sure that the payload property is also set. The most common way to ensure this it to use payload properties with automatically generated values. For example, the `TaggedOn` property can be configured to use a store-generated timestamp when each new entity is inserted:

```c#
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Post>()
                .HasMany(p => p.Tags)
                .WithMany(p => p.Posts)
                .UsingEntity<PostTag>(
                    j => j.HasOne<Tag>().WithMany(),
                    j => j.HasOne<Post>().WithMany(),
                    j => j.Property(e => e.TaggedOn).HasDefaultValueSql("CURRENT_TIMESTAMP"));
        }
```

A Post can now be Tagged in the same way as before:

```c#
            using var context = new BlogsContext();

            var post = context.Posts.Single(e => e.Id == 3);
            var tag = context.Tags.Single(e => e.Id == 1);

            post.Tags.Add(tag);

            context.SaveChanges();

            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
```

The change tracker debug view after calling SaveChanges shows that the payload property has been set appropriately:

```
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: 2 FK
  Content: 'If you are focused on squeezing out the last bits of perform...'
  Title: 'Disassembly improvements for optimized managed debugging'
  Blog: <null>
  Tags: [{Id: 1}]
PostTag {PostId: 3, TagId: 1} Unchanged
  PostId: 3 PK FK
  TagId: 1 PK FK
  TaggedOn: '12/25/2020 5:32:24 PM'
Tag {Id: 1} Unchanged
  Id: 1 PK
  Text: '.NET'
  Posts: [{Id: 3}]
```

#### Explicitly setting payload values

Following on from the previous example, let's add a payload property that does not use automatically generated values:

```c#
    public class PostTag
    {
        public int PostId { get; set; } // Foreign key to Post
        public int TagId { get; set; } // Foreign key to Tag
        
        public DateTime TaggedOn { get; set; } // Payload
        public string TaggedBy { get; set; } // Payload
    }
```

A Post can now be Tagged in the same way as before, and the join entity will still be created automatically. This entity can then be accessed using one of the mechanisms described in [accessing local data](). For example, the code below uses `Find` to access the join table instance:

```
            using var context = new BlogsContext();

            var post = context.Posts.Single(e => e.Id == 3);
            var tag = context.Tags.Single(e => e.Id == 1);

            post.Tags.Add(tag);

            context.ChangeTracker.DetectChanges();
            var joinEntity = context.Set<PostTag>().Find(post.Id, tag.Id);

            joinEntity.TaggedBy = "ajcvickers";
            
            context.SaveChanges();

            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
```

Once the join entity has been located it can be manipulated in the normal way. In this example, to set the `TaggedBy` payload property before calling SaveChanges.

> [!NOTE]
> Note that a call to DetectChanges is required here to give EF a chance to detect the navigation property change. See [detecting changes]() for more information.

Alternately, the join entity can be created explicitly to associate a post with a tag. For example:

```c#
            using var context = new BlogsContext();

            var post = context.Posts.Single(e => e.Id == 3);
            var tag = context.Tags.Single(e => e.Id == 1);

            context.Add(new PostTag()
            {
                PostId = post.Id,
                TagId = tag.Id,
                TaggedBy = "ajcvickers"
            });
            
            context.SaveChanges();

            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
```

Finally, another way to set payload data is by either overriding SaveChanges or using the `SavingChanges` event to process entities before updating the database. For example:

```c#
        public override int SaveChanges()
        {
            foreach (var entityEntry in ChangeTracker.Entries<PostTag>())
            {
                if (entityEntry.State == EntityState.Added)
                {
                    entityEntry.Entity.TaggedBy = "ajcvickers";
                }
            }
            
            return base.SaveChanges();
        }
```

The end result is the same for both these examples as it was for the previous example.
