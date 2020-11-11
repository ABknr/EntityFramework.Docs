// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace WithJoinEntity
{
    public class ExplicitJoinEntity
    {
        public static void AssociateByFk()
        {
            // Put the database into the expected state
            SaveBlogAndPosts();

            using var context = new BlogsContext();

            var post = context.Posts.Single(e => e.Id == 3);
            var tag = context.Tags.Single(e => e.Id == 1);

            context.Add(new PostTag { PostId = post.Id, TagId = tag.Id });
            
            context.ChangeTracker.DetectChanges();
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        }

        public static void AssociateByReference()
        {
            // Put the database into the expected state
            SaveBlogAndPosts();

            using var context = new BlogsContext();

            var post = context.Posts.Single(e => e.Id == 3);
            var tag = context.Tags.Single(e => e.Id == 1);

            context.Add(new PostTag { Post = post, Tag = tag });
            
            context.ChangeTracker.DetectChanges();
            Console.WriteLine(context.ChangeTracker.DebugView.LongView);
        }

        public static void SaveBlogAndPosts()
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
                },
                new Tag
                {
                    Text = ".NET"
                },
                new Tag
                {
                    Text = "Visual Studio"
                },
                new Tag
                {
                    Text = "EF Core"
                });

            context.SaveChanges();
        }
    }

    public class Blog
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public IList<Post> Posts { get; } = new List<Post>();
        public BlogAssets Assets { get; set; }
    }

    public class BlogAssets
    {
        public int Id { get; set; }
        public byte[] Banner { get; set; }

        public int? BlogId { get; set; }
        public Blog Blog { get; set; }
    }

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

    public class BlogsContext : DbContext
    {
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<BlogAssets> Assets { get; set; }
        public DbSet<Tag> Tags { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .EnableSensitiveDataLogging()
                .LogTo(Console.WriteLine, new[] { RelationalEventId.CommandExecuted })
                .UseSqlite("DataSource=test.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PostTag>().HasKey(e => new { e.PostId, e.TagId });
        }
    }
}