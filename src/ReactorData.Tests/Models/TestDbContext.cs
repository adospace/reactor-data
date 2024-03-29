﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData.Tests.Models;

class TestDbContext : DbContext
{
    public DbSet<Blog> Blogs => Set<Blog>();

    public DbSet<Movie> Movies => Set<Movie>();

    public DbSet<Director> Directors => Set<Director>();

    public TestDbContext() { }

    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=Database.db");
        }

        base.OnConfiguring(optionsBuilder);
    }
}
