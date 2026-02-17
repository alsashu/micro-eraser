using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MicroEraser.Domain.Entities;
using Npgsql;

namespace MicroEraser.Infrastructure.Data;

/// <summary>
/// Seeds the database with sample data for development/testing.
/// Only runs when ASPNETCORE_ENVIRONMENT is Development.
/// </summary>
public class DatabaseSeeder
{
    private readonly AppDbContext _context;
    private readonly ILogger<DatabaseSeeder> _logger;
    private readonly IConfiguration _configuration;

    public DatabaseSeeder(AppDbContext context, ILogger<DatabaseSeeder> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task SeedAsync()
    {
        // First, ensure the database exists by connecting to postgres maintenance db
        await EnsureDatabaseExistsAsync();
        
        // Ensure tables are created (code-first approach)
        await _context.Database.EnsureCreatedAsync();

        // Check if already seeded
        if (await _context.Users.AnyAsync())
        {
            _logger.LogInformation("Database already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding database with sample data...");

        // Create sample users
        var adminUser = new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Email = "admin@microeraser.dev",
            Name = "Admin User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var editorUser = new User
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Email = "editor@microeraser.dev",
            Name = "Editor User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Editor123!"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var viewerUser = new User
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Email = "viewer@microeraser.dev",
            Name = "Viewer User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Viewer123!"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Users.AddRangeAsync(adminUser, editorUser, viewerUser);

        // Create sample workspace
        var workspace = new Workspace
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Name = "Demo Workspace",
            Description = "A sample workspace for exploring MicroEraser features",
            OwnerId = adminUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Workspaces.AddAsync(workspace);

        // Add members to workspace
        var adminMember = new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = adminUser.Id,
            Role = WorkspaceRole.Admin,
            JoinedAt = DateTime.UtcNow
        };

        var editorMember = new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = editorUser.Id,
            Role = WorkspaceRole.Editor,
            JoinedAt = DateTime.UtcNow
        };

        var viewerMember = new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = viewerUser.Id,
            Role = WorkspaceRole.Viewer,
            JoinedAt = DateTime.UtcNow
        };

        await _context.WorkspaceMembers.AddRangeAsync(adminMember, editorMember, viewerMember);

        // Create sample canvases
        var canvas1 = new Canvas
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            WorkspaceId = workspace.Id,
            Name = "Architecture Diagram",
            Description = "System architecture overview",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var canvas2 = new Canvas
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            WorkspaceId = workspace.Id,
            Name = "User Flow",
            Description = "User journey and interaction flow",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var canvas3 = new Canvas
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            WorkspaceId = workspace.Id,
            Name = "Database Schema",
            Description = "Entity relationship diagram",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _context.Canvases.AddRangeAsync(canvas1, canvas2, canvas3);

        // Create initial snapshots with sample Yjs data
        // This is a minimal Y.Doc state with some sample nodes
        var sampleYjsState = CreateSampleYjsState();
        
        var snapshot1 = new CanvasSnapshot
        {
            Id = Guid.NewGuid(),
            CanvasId = canvas1.Id,
            State = sampleYjsState,
            Version = 1,
            CreatedAt = DateTime.UtcNow
        };

        await _context.CanvasSnapshots.AddAsync(snapshot1);

        // Create a sample invite link
        var inviteLink = new Invite
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            Email = null,
            Token = "demo-invite-token-12345",
            Permission = InvitePermission.Edit,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            MaxUses = 10,
            UseCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Invites.AddAsync(inviteLink);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Database seeding completed successfully");
        _logger.LogInformation("Sample credentials:");
        _logger.LogInformation("  Admin: admin@microeraser.dev / Admin123!");
        _logger.LogInformation("  Editor: editor@microeraser.dev / Editor123!");
        _logger.LogInformation("  Viewer: viewer@microeraser.dev / Viewer123!");
    }

    /// <summary>
    /// Creates a sample Yjs document state with predefined nodes and edges.
    /// This represents a simple React Flow diagram with 3 nodes and 2 edges.
    /// 
    /// The state is created programmatically to represent:
    /// - A "Start" node
    /// - A "Process" node  
    /// - An "End" node
    /// - Edges connecting them
    /// </summary>
    private byte[] CreateSampleYjsState()
    {
        // This is a simplified representation.
        // In production, you'd use Yjs on the server to create proper state.
        // For now, we return an empty state that the client will initialize.
        // The React Flow component will populate this with initial data on first load.
        return Array.Empty<byte>();
    }

    /// <summary>
    /// Ensures the database exists by connecting to PostgreSQL's maintenance database
    /// and creating our database if it doesn't exist.
    /// </summary>
    private async Task EnsureDatabaseExistsAsync()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var databaseName = builder.Database;
        
        // Connect to postgres maintenance database
        builder.Database = "postgres";
        
        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        // Check if database exists
        var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = $"SELECT 1 FROM pg_database WHERE datname = '{databaseName}'";
        var exists = await checkCmd.ExecuteScalarAsync();

        if (exists == null)
        {
            _logger.LogInformation("Creating database {DatabaseName}...", databaseName);
            
            var createCmd = connection.CreateCommand();
            createCmd.CommandText = $"CREATE DATABASE \"{databaseName}\"";
            await createCmd.ExecuteNonQueryAsync();
            
            _logger.LogInformation("Database {DatabaseName} created successfully", databaseName);
        }
    }
}

public static class DatabaseSeederExtensions
{
    public static async Task SeedDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
}
