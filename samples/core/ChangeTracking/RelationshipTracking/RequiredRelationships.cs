// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Required
{
    public class RequiredRelationships
    {
        public static void DeleteOrphan()
        {
            // Put the database into the expected state
            SaveBlogAndPosts();

            using var context = new BlogsContext();

            var dotNetBlog = context.Blogs.Include(e => e.Posts).Single(e => e.Name == ".NET Blog");

            var post = dotNetBlog.Posts.Single(e => e.Title == "Announcing F# 5");
            dotNetBlog.Posts.Remove(post);

            context.ChangeTracker.DetectChanges();
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            context.SaveChanges();
        }

        public static void ReparentPost()
        {
            // Put the database into the expected state
            SaveBlogAndPosts();

            using var context = new BlogsContext();

            var dotNetBlog = context.Blogs.Include(e => e.Posts).Single(e => e.Name == ".NET Blog");
            var vsBlog = context.Blogs.Include(e => e.Posts).Single(e => e.Name == "Visual Studio Blog");

            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            context.ChangeTracker.DeleteOrphansTiming = CascadeTiming.OnSaveChanges;

            var post = vsBlog.Posts.Single(e => e.Title.StartsWith("Disassembly improvements"));
            vsBlog.Posts.Remove(post);

            context.ChangeTracker.DetectChanges();
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            dotNetBlog.Posts.Add(post);

            context.ChangeTracker.DetectChanges();
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            context.SaveChanges();
        }

        public static void ThrowForOrphan()
        {
            // Put the database into the expected state
            SaveBlogAndPosts();

            try
            {
                using var context = new BlogsContext();

                var dotNetBlog = context.Blogs.Include(e => e.Posts).Single(e => e.Name == ".NET Blog");

                context.ChangeTracker.DeleteOrphansTiming = CascadeTiming.Never;

                var post = dotNetBlog.Posts.Single(e => e.Title == "Announcing F# 5");
                dotNetBlog.Posts.Remove(post);

                context.SaveChanges();

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public static void AddNewAsset()
        {
            // Put the database into the expected state
            SaveBlogAndPosts();

            using var context = new BlogsContext();

            var dotNetBlog = context.Blogs.Include(e => e.Assets).Single(e => e.Name == ".NET Blog");
            dotNetBlog.Assets = new BlogAssets();

            context.ChangeTracker.DetectChanges();
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            context.SaveChanges();
        }

        public static void DeleteBlog()
        {
            // Put the database into the expected state
            SaveBlogAndPosts();

            using var context = new BlogsContext();

            var vsBlog = context.Blogs
                .Include(e => e.Posts)
                .Include(e => e.Assets)
                .Single(e => e.Name == "Visual Studio Blog");

            context.Remove(vsBlog);

            Console.WriteLine(context.ChangeTracker.DebugView.LongView);

            context.SaveChanges();
        }


        private static void SaveBlogAndPosts()
        {
            using var context = new BlogsContext();

            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            context.AddRange(
                new Blog
                {
                    Name = ".NET Blog",
                    Assets = new BlogAssets(),
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
                    },
                },
                new Blog
                {
                    Name = "Visual Studio Blog",
                    Assets = new BlogAssets(),
                    Posts =
                    {
                        new Post
                        {
                            Title = "Disassembly improvements for optimized managed debugging",
                            Content = "If you are focused on squeezing out the last bits of performance for your .NET service or..."
                        },
                        new Post
                        {
                            Title = "Database Profiling with Visual Studio",
                            Content = "Examine when database queries were executed and measure how long the take using..."
                        },
                    }
                });

            context.SaveChanges();
        }
    }
    
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
    
        public int BlogId { get; set; } // Foreign key
        public Blog Blog { get; set; } // Reference navigation
    
        public IList<Tag> Tags { get; } = new List<Tag>(); // Collection navigation
    }

    public class Tag
    {
        public int Id { get; set; } // Primary key
        public string Text { get; set; }
    
        public IList<Post> Posts { get; } = new List<Post>(); // Collection navigation
    }

    public class BlogsContext : DbContext
    {
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<BlogAssets> Assets { get; set; }
        public DbSet<Tag> Tags { get; set; }
    
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .EnableSensitiveDataLogging()
                .LogTo(Console.WriteLine, new[] { RelationalEventId.CommandExecuted})
                .UseSqlite("DataSource=test.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }
    }
}