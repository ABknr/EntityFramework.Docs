---
title: Change detection and notifications - EF Core
description: Detecting property and relationship changes using DetectChanges or notifications  
author: ajcvickers
ms.date: 25/12/2020
uid: core/change-tracking/change-detection
---

# Change detection and notifications

Each `DbContext` instance tracks changes made to entities. These tracked entities in turn drive the changes to the database when `SaveChanges` is called. This is covered in [Change Tracking in EF Core](), and this document assumes that entity states and the basics of EF core change tracking are understood. 

Tracking property and relationship changes requires that the DbContext is able to detect these changes. This document covers how this detection happens, as well as how to use property notifications to force immediate detection of changes.

## Snapshot change tracking

By default, EF Core creates a snapshot of every entity's property values when it is tracked by a DbContext instance. The values stored in this snapshot are then compared against the current values of the entity in order to determine which property values have changed.

This detection of changes happens when `SaveChanges` is called to ensure all changed values are detected before sending updates to the database. However, it also happens at other times to ensure the application is working with up-to-date tracking information, and can be forced at any time by calling `ChangeTracker.DetectChanges`.

## When is change detection needed?

Detection of changes is needed when a property or navigation has been changed _without using EF to make this change_. For example, consider loading blogs and posts and then making changes to these entities:

```c#
        using var context = new BlogsContext();
        var blog = context.Blogs.Include(e => e.Posts).First(e => e.Name == ".NET Blog");
        
        // Change a property value
        blog.Name = ".NET Blog (Updated!)";
        
        // Add a new entity to a navigation
        blog.Posts.Add(new Post
        {
            Title = "What’s next for System.Text.Json?",
            Content = ".NET 5.0 was released recently and has come with many..."
        });
        
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        context.ChangeTracker.DetectChanges();
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
```

Looking at the change tracker debug view before calling `ChangeTracker.DetectChanges()` shows that the changes made have not been detected and hence are not reflected in the entity states and modified property data: 

```
Blog {Id: 1} Unchanged
  Id: 1 PK
  Name: '.NET Blog (Updated!)' Originally '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}, <not found>]
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

Specifically, the state of the blog entry is still `Unchanged`, and the new Post does not appear as a tracked entity. (The astute will notice properties report their new values, even though these changes have not yet been detected by EF. This is because the debug view is reading current values directly from the entity instance.)

Contrast this with the debug view after calling `ChangeTracker.DetectChanges()`:

```
Blog {Id: 1} Modified
  Id: 1 PK
  Name: '.NET Blog (Updated!)' Modified Originally '.NET Blog'
  Posts: [{Id: 1}, {Id: 2}, {Id: 3}, {Id: -2147482599}]
Post {Id: -2147482599} Added
  Id: -2147482599 PK Temporary
  BlogId: 1 FK
  Content: '.NET 5.0 was released recently and has come with many...'
  Title: 'What’s next for System.Text.Json?'
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
Post {Id: 3} Unchanged
  Id: 3 PK
  BlogId: 1 FK
  Content: '.NET 5.0 includes many enhancements, including single file a...'
  Title: 'Announcing .NET 5.0'
  Blog: {Id: 1}
```

Now the Blog is correctly marked as `Modified` and the new `Post` has been detected and tracked.

At the start of this section we stated that detecting changes is need when not using _using EF to make the change_. This is what is happening in the code above. That is, the change to the property and navigation are made directly on the entity instances, and not by using any EF methods.

Contrast this to the following code which modifies the entities using EF methods:

```c#
        using var context = new BlogsContext();
        var blog = context.Blogs.Include(e => e.Posts).First(e => e.Name == ".NET Blog");
        
        // Change a property value
        context.Entry(blog).Property(e => e.Name).CurrentValue = ".NET Blog (Updated!)";
        
        // Add a new entity to the DbContext
        context.Add(
            new Post
            {
                Blog = blog,
                Title = "What’s next for System.Text.Json?",
                Content = ".NET 5.0 was released recently and has come with many..."
            });
        
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
```

In this case the change tracker debug view shows that all entity states and property modifications are known by EF even though detection of changes has not happened. This is because calling the `CurrentValue` setter is using EF to make the change. This means that EF immediately knows about the change. Likewise, calling the `DbContext.Add` method allows EF immediately know about the new entity and track it appropriately.

> [! TIP]
> Do **not** attempt to avoid detecting changes by always using EF methods to make entity changes. Doing so is often more cumbersome and less performant than making changes to entity graphs in the normal way. The intention of this document is to inform as to when detecting changes is needed and when it is not. The intention is not to encourage avoidance of change detection.

## Methods that automatically detect changes

`DetectChanges` is called automatically by methods where doing so is likely to impact the results. These methods are:

- `SaveChanges` and `SaveChangesAsync`, to ensure that all changes are detecting before updating the database.
- `ChangeTracker.Entities` and `ChangeTracker.Entities<TEntity>`, to ensure entity states and modified properties are up-to-date.
- `ChangeTracker.HasChanges`, to ensure that the result is accurate.
- `ChangeTracker.CascadeChanges`, to ensure correct entity states for principal/parent entities before cascading. 
- `DbSet.Local`, to ensure that the tracked graph of objects is up-to-date.

There are also some places om the code that detect changes only on a single entity instance, rather than scanning the entire graph of tracked entities. These places are:

- When using `DbContext.Entry` or `DbContext.Entry<TEntity>`, to ensure that the entity's state and modified properties are up-to-date.
- When using `EntityEntry` or `EntityEntry<TEntity>` methods such as `Property`, `Collection`, `Reference` or `Member` to ensure property modifications, current values, etc. are up-to-date.
- When an dependent/child entity is going to be deleted because a required relationship has been severed. This detects when an entity should not be deleted because it has been re-parented.

Detection of changes for a single entity can be triggered explicitly by calling `EntityEntry.DetectChanges`. 

> [!NOTE]
> Local detect changes can miss some changes to a single entity if these happen as cascading actions resulting from changes to other entities in the graph. In such situations the application may need to force a full scan of all entities by explicitly calling `ChangeTracker.DetectChanges`. 

## Disabling automatic change detection

The performance of detecting changes is not a bottleneck for most applications. However, detecting changes can become a performance problem for some applications that track thousands of entities. For this reason the automatic detection of changes can be disabled using `ChangeTracker.AutoDetectChangesEnabled`. For example, consider processing join entities in a many-to-many relationship with payloads:

```c#
        public override int SaveChanges()
        {
            foreach (var entityEntry in ChangeTracker.Entries<PostTag>()) // Detects changes automatically
            {
                if (entityEntry.State == EntityState.Added)
                {
                    entityEntry.Entity.TaggedBy = "ajcvickers";
                    entityEntry.Entity.TaggedOn = DateTime.Now;
                }
            }

            try
            {
                ChangeTracker.AutoDetectChangesEnabled = false;
                return base.SaveChanges(); // Avoid automatically detecting changes again here
            }
            finally
            {
                ChangeTracker.AutoDetectChangesEnabled = true;
            }
        }
```

As we know from the previous section, both `ChangeTracker.Entries` and `DbContext.SaveChanges` automatically detect changes. However, after calling `ChangeTracker.Entries`, the code does not then make any entity or property state changes. (Setting normal property values on Added entities does not cause any state changes.) The code therefore disables unnecessary automatic change detection when calling down into the base `SaveChanges` method. The code also makes use of a try/finally block to ensure that the default setting is restored even if `SaveChanges` fails.

> [!TIP]
> Do not assume that your code must disable automatic change detection to to be performant. This is only normally needed when profiling an application tracking many entities indicates that performance is an issue. 

## Detecting changes and value conversions

To uses snapshot change tracking it must be possible to:

- Make a snapshot of each property value
- Compare this value to the current value of the property
- Generate a hash code for the value



- Value comparers
- IEquatable<> and IComparable<> for keys

## Notification entities

### Change-tracking proxies


