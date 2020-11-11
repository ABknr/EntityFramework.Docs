using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

public class Program
{
    public static void Main()
    {
        QueryAndUpdate();
        QueryAndInsertUpdateDelete();
        
        AddBogAndPosts();
        
        AttachNewAndExistingBlogAndPosts();
        UpdateNewAndExistingBlogAndPosts();
        
        RemoveBlog();
        
        EntityLevelAccess();
        SinglePropertyAccess();

        Find();
        FindComposite();
        
        Entries();
        
        LocalQuery();
        LocalQueryWithDeleted();
        LocalQueryWithChanges();
        DataBindingCollections();

        QueryAndMakeChanges();
        QueryAndMakeChangesWithEf();
    }

    private static void QueryAndUpdate()
    {
        // Put the database into the expected state
        SaveBlogAndPosts();
        
        // Query and update
        using var context = new BlogsContext();
        
        var blog = context.Blogs.Include(e => e.Posts).First(e => e.Name == ".NET Blog");

        blog.Name = ".NET Blog (Updated!)";

        foreach (var post in blog.Posts.Where(e => !e.Title.Contains("5.0")))
        {
            post.Title = post.Title.Replace("5", "5.0");
        }

        context.SaveChanges();
    }

    private static void QueryAndInsertUpdateDelete()
    {
        // Put the database into the expected state
        SaveBlogAndPosts();
        
        // Query and insert/update/delete
        using var context = new BlogsContext();
        
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

    private static void QueryAndMakeChanges()
    {
        // Put the database into the expected state
        SaveBlogAndPosts();
        
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
    }

    private static void QueryAndMakeChangesWithEf()
    {
        // Put the database into the expected state
        SaveBlogAndPosts();
        
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
    }

    private static void AddBogAndPosts()
    {
        using var context = new BlogsContext();
        
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
            
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

        Console.WriteLine("Before SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        context.SaveChanges();

        Console.WriteLine("After SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    }
    
    private static void AttachNewAndExistingBlogAndPosts()
    {
        using (var context = new BlogsContext())
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            context.Attach(
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
                        }
                    }
                });
            
            context.SaveChanges();
        }

        using (var context = new BlogsContext())
        {
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

            Console.WriteLine("Before SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            context.SaveChanges();

            Console.WriteLine("After SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        }
    }
    
    private static void UpdateNewAndExistingBlogAndPosts()
    {
        // Put the database into the expected state
        using (var context = new BlogsContext())
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            context.Attach(
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
                        }
                    }
                });
            
            context.SaveChanges();
        }

        // Use Update
        using (var context = new BlogsContext())
        {
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

            Console.WriteLine("Before SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            context.SaveChanges();

            Console.WriteLine("After SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        }
    }
    
    private static void RemoveBlog()
    {
        // Put the database into the expected state
        SaveBlogAndPosts();

        // Use Remove
        
        using var context = new BlogsContext();
        
        var blog = new Blog
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
        };
            
        context.Attach(blog);
        context.Remove(blog);

        Console.WriteLine("Before SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);

        context.SaveChanges();

        Console.WriteLine("After SaveChanges:");
        Console.WriteLine(context.ChangeTracker.DebugView.LongView);
    }

    private static void EntityLevelAccess()
    {
        // Put the database into the expected state
        SaveBlogAndPosts();
        
        // Obtain EntityEntry example
        using var context = new BlogsContext();
        var blog = context.Blogs.Single();
        var entityEntry = context.Entry(blog);

        // Change state example
        var currentState = context.Entry(blog).State;
        if (currentState == EntityState.Unchanged)
        {
            context.Entry(blog).State = EntityState.Modified;
        }

        // Start tracking example
        var newBlog = new Blog();
        Debug.Assert(context.Entry(newBlog).State == EntityState.Detached);

        context.Entry(newBlog).State = EntityState.Added;
        Debug.Assert(context.Entry(newBlog).State == EntityState.Added);
    }

    private static void SinglePropertyAccess()
    {
        // Put the database into the expected state
        SaveBlogAndPosts();
        
        using var context = new BlogsContext();
        var blog = context.Blogs.Single();
        var entityEntry = context.Entry(blog);

        {
            var propertyEntry1 = context.Entry(blog).Property(e => e.Name);
            var propertyEntry2 = context.Entry(blog).Property<string>("Name");
        }

        {
            string currentValue1 = context.Entry(blog).Property(e => e.Name).CurrentValue;
            context.Entry(blog).Property(e => e.Name).CurrentValue = "1unicorn2";
        }

        {
            var someEntity = (object)blog;
            PropertyEntry propertyEntry3 = context.Entry(someEntity).Property("Name");

            object currentValue2 = context.Entry(someEntity).Property("Name").CurrentValue;
            context.Entry(blog).Property(e => e.Name).CurrentValue = "1unicorn2";
        }
        
        var post = context.Posts.OrderBy(e => e.Title).First();

        {
            ReferenceEntry<Post, Blog> referenceEntry1 = context.Entry(post).Reference(e => e.Blog);
            ReferenceEntry<Post, Blog> referenceEntry2 = context.Entry(post).Reference<Blog>("Blog");
            ReferenceEntry referenceEntry3 = context.Entry(post).Reference("Blog");
        }

        {
            CollectionEntry<Blog, Post> collectionEntry1 = context.Entry(blog).Collection(e => e.Posts);
            CollectionEntry<Blog, Post> collectionEntry2 = context.Entry(blog).Collection<Post>("Posts");
            CollectionEntry collectionEntry3 = context.Entry(blog).Collection("Posts");
        }

        {
            NavigationEntry navigationEntry = context.Entry(blog).Navigation("Posts");
        }

        {
            foreach (var propertyEntry in context.Entry(blog).Properties)
            {
                if (propertyEntry.Metadata.ClrType == typeof(DateTime))
                {
                    propertyEntry.CurrentValue = DateTime.Now;
                }
            }
        }

        {
            var currentValues = context.Entry(blog).CurrentValues;
            var originalValues = context.Entry(blog).OriginalValues;
            var databaseValues = context.Entry(blog).GetDatabaseValues();
        }

        {
            var blogDto = new BlogDto { Id = 1, Name = "1unicorn2" };

            context.Entry(blog).CurrentValues.SetValues(blogDto);
        }

        {
            var databaseValues = context.Entry(blog).GetDatabaseValues();
            context.Entry(blog).CurrentValues.SetValues(databaseValues);
            context.Entry(blog).OriginalValues.SetValues(databaseValues);
        }

        {
            var blogDictionary = new Dictionary<string, object>
            {
                ["Id"] = 1,
                ["Name"] = "1unicorn2"
            };

            context.Entry(blog).CurrentValues.SetValues(blogDictionary);
        }

        {
            var clonedBlog = context.Entry(blog).GetDatabaseValues().ToObject();
        }

        {
            foreach (var navigationEntry in context.Entry(blog).Navigations)
            {
                navigationEntry.Load();
            }
        }

        {
            foreach (var memberEntry in context.Entry(blog).Members)
            {
                Console.WriteLine(
                    $"Member {memberEntry.Metadata.Name} is of type '{memberEntry.Metadata.ClrType.ShortDisplayName()}' and has value '{memberEntry.CurrentValue}'");
            }
        }

        Console.WriteLine(context.Entry(blog).DebugView.LongView);
    }

    public static void Find()
    {
        // Put the database into the expected state
        SaveBlogAndPosts();

        using var context = new BlogsContext();

        Console.WriteLine("First call to Find...");
        var blog1 = context.Blogs.Find(1);

        Console.WriteLine($"...found blog {blog1.Name}");
        
        Console.WriteLine();
        Console.WriteLine("Second call to Find...");
        var blog2 = context.Blogs.Find(1);
        Debug.Assert(blog1 == blog2);
        
        Console.WriteLine("...returned the same instance without executing a query.");
    }

    public static void FindComposite()
    {
        using var context = new BlogsContext();

        var orderId = 3;
        var productId = 4;
        
        var orderline = context.OrderLines.Find(orderId, productId);
    }

    public static void Entries()
    {
        // Put the database into the expected state
        SaveBlogAndPosts();
        
        using var context = new BlogsContext();
        var blogs = context.Blogs.Include(e => e.Posts).ToList();

        foreach (var entityEntry in context.ChangeTracker.Entries())
        {
            Console.WriteLine(
                $"Found {entityEntry.Metadata.Name} entity with ID {entityEntry.Property("Id").CurrentValue}");
        }

        Console.WriteLine();
        
        foreach (var entityEntry in context.ChangeTracker.Entries<Post>())
        {
            Console.WriteLine(
                $"Found {entityEntry.Metadata.Name} entity with ID {entityEntry.Property(e => e.Id).CurrentValue}");
        }
    }

    public static void LocalQuery()
    {
        // Put the database into the expected state
        SaveBlogAndPosts();
        
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
    }

    public static void LocalQueryWithDeleted()
    {
        // Put the database into the expected state
        SaveBlogAndPosts();
        
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
    }

    public static void LocalQueryWithChanges()
    {
        // Put the database into the expected state
        SaveBlogAndPosts();
        
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
    }


    public static void DataBindingCollections()
    {
        // Put the database into the expected state
        SaveBlogAndPosts();
        
        using var context = new BlogsContext();

        context.Posts.Include(e => e.Blog).Load();

        ObservableCollection<Post> observableCollection = context.Posts.Local.ToObservableCollection();
        BindingList<Post> bindingList = context.Posts.Local.ToBindingList();
    }

    private static void SaveBlogAndPosts()
    {
        using var context = new BlogsContext();
        
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
            
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

        context.SaveChanges();
    }
}

public class BlogDto
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Blog
{
    public int Id { get; set; }

    public string Name { get; set; }

    public IList<Post> Posts { get; } = new List<Post>();
}

public class Post
{
    public int Id { get; set; }
    
    public string Title { get; set; }
    public string Content { get; set; }
    
    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}

public class OrderLine
{
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    
    //...
}

public class BlogsContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
    
    public DbSet<OrderLine> OrderLines { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
            .EnableSensitiveDataLogging()
            .LogTo(Console.WriteLine, new[] { RelationalEventId.CommandExecuted})
            .UseSqlite("DataSource=test.db");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<OrderLine>()
            .HasKey(e => new { e.OrderId, e.ProductId });
    }
}
