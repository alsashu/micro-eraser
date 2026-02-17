using Microsoft.EntityFrameworkCore;
using MicroEraser.Domain.Entities;

namespace MicroEraser.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<Canvas> Canvases => Set<Canvas>();
    public DbSet<CanvasSnapshot> CanvasSnapshots => Set<CanvasSnapshot>();
    public DbSet<Invite> Invites => Set<Invite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.AvatarUrl).HasMaxLength(2048);
        });

        // RefreshToken configuration
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.Property(e => e.Token).HasMaxLength(512).IsRequired();
            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Workspace configuration
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2048);
            entity.HasOne(e => e.Owner)
                .WithMany(u => u.OwnedWorkspaces)
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // WorkspaceMember configuration
        modelBuilder.Entity<WorkspaceMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.WorkspaceId, e.UserId }).IsUnique();
            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Members)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany(u => u.WorkspaceMemberships)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Canvas configuration
        modelBuilder.Entity<Canvas>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2048);
            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Canvases)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CanvasSnapshot configuration
        modelBuilder.Entity<CanvasSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CanvasId, e.Version });
            entity.Property(e => e.State).IsRequired();
            entity.HasOne(e => e.Canvas)
                .WithMany(c => c.Snapshots)
                .HasForeignKey(e => e.CanvasId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Invite configuration
        modelBuilder.Entity<Invite>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token).IsUnique();
            entity.Property(e => e.Token).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.HasOne(e => e.Workspace)
                .WithMany(w => w.Invites)
                .HasForeignKey(e => e.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
