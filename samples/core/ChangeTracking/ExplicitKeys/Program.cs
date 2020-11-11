using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public class Program
{
    public static void Main()
    {
        AddBlog();
        AddBlogAndPosts();

        AttachBlog();
        AttachBlogAndPosts();

        UpdateBlog();
        UpdateBlogAndPosts();
        
        RemovePostStub();
        RemovePost();
        RemovePostFromGraph();
        RemoveBlog();
    }

    private static void AddBlog()
    {
        using (var context = new BlogsContext())
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            context.Add(
                new Blog
                {
                    Id = 1,
                    Name = ".NET Blog",
                });

            Console.WriteLine("Before SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            context.SaveChanges();

            Console.WriteLine("After SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        }
    }

    private static void AddBlogAndPosts()
    {
        using (var context = new BlogsContext())
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

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

            Console.WriteLine("Before SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            context.SaveChanges();

            Console.WriteLine("After SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        }
    }

    private static void AttachBlog()
    {
        using (var context = new BlogsContext())
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            context.Attach(
                new Blog
                {
                    Id = 1,
                    Name = ".NET Blog",
                });

            Console.WriteLine("Before SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            context.SaveChanges();

            Console.WriteLine("After SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        }
    }

    private static void AttachBlogAndPosts()
    {
        // Put the database into the expected state
        using (var context = new BlogsContext())
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

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

            context.SaveChanges();
        }

        // Use Attach
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
                            Id = 3,
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

    private static void UpdateBlog()
    {
        // Put the database into the expected state
        using (var context = new BlogsContext())
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            context.Add(
                new Blog
                {
                    Id = 1,
                    Name = ".NET Blog"
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
                    Name = ".NET Blog"
                });

            Console.WriteLine("Before SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            context.SaveChanges();

            Console.WriteLine("After SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        }
    }

    private static void UpdateBlogAndPosts()
    {
        // Put the database into the expected state
        using (var context = new BlogsContext())
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

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
                            Id = 3,
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

    private static void RemovePostStub()
    {
        // Put the database into the expected state
        using (var context = new BlogsContext())
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

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

            context.SaveChanges();
        }

        // Use Remove
        using (var context = new BlogsContext())
        {
            context.Remove(
                new Post
                {
                    Id = 2
                });

            Console.WriteLine("Before SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            context.SaveChanges();

            Console.WriteLine("After SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        }
    }
    
    private static void RemovePost()
    {
        // Put the database into the expected state
        using (var context = new BlogsContext())
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

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

            context.SaveChanges();
        }

        // Use Remove
        using (var context = new BlogsContext())
        {
            var post = new Post
            {
                Id = 2,
                Title = "Announcing F# 5",
                Content = "F# 5 is the latest version of F#, the functional programming language..."
            };
            
            context.Attach(post);
            context.Remove(post);

            Console.WriteLine("Before SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            context.SaveChanges();

            Console.WriteLine("After SaveChanges:");
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        }
    }
    
    private static void RemovePostFromGraph()
    {
        // Put the database into the expected state
        using (var context = new BlogsContext())
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

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

            context.SaveChanges();
        }

        // Use Remove
        using (var context = new BlogsContext())
        {
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
            
            // Attach a blog and associated posts
            context.Attach(blog);
            
            // Mark one post as Deleted
            context.Remove(blog.Posts[1]);

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
        using (var context = new BlogsContext())
        {
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

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

            context.SaveChanges();
        }

        // Use Remove
        using (var context = new BlogsContext())
        {
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
    }
}

public class Blog
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    public string Name { get; set; }

    public IList<Post> Posts { get; } = new List<Post>();
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

public class BlogsContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
            .EnableSensitiveDataLogging()
            .LogTo(Console.WriteLine, new[] { RelationalEventId.CommandExecuted})
            .UseSqlite("DataSource=test.db");
}
