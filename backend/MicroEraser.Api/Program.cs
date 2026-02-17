using System.Text;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MicroEraser.Api.HealthChecks;
using MicroEraser.Api.Hubs;
using MicroEraser.Api.Middleware;
using MicroEraser.Application.Interfaces;
using MicroEraser.Application.Services;
using MicroEraser.Infrastructure.Data;
using MicroEraser.Infrastructure.Repositories;
using MicroEraser.Infrastructure.Services;
using Serilog;
using Serilog.Events;

// Configure Serilog - single console output with structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("HealthChecks", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId:l} {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting MicroEraser API...");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog - replace bootstrap logger with full configuration
    var seqUrl = builder.Configuration["Seq:ServerUrl"];
    
    builder.Host.UseSerilog((context, services, configuration) => 
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "MicroEraser")
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId:l} {Message:lj}{NewLine}{Exception}");
        
        // Add Seq sink if URL is configured (optional - works without Seq)
        if (!string.IsNullOrWhiteSpace(seqUrl))
        {
            configuration.WriteTo.Seq(seqUrl, queueSizeLimit: 1000);
        }
    });

    // Add services to the container

    // Database
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Repositories
    builder.Services.AddScoped<IUserRepository, UserRepository>();
    builder.Services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
    builder.Services.AddScoped<ICanvasRepository, CanvasRepository>();
    builder.Services.AddScoped<IInviteRepository, InviteRepository>();
    builder.Services.AddScoped<ITokenService, TokenService>();

    // Application Services
    builder.Services.AddScoped<AuthService>();
    builder.Services.AddScoped<WorkspaceService>();
    builder.Services.AddScoped<CanvasService>();
    builder.Services.AddScoped<InviteService>();

    // Database Seeder
    builder.Services.AddScoped<DatabaseSeeder>();

    // JWT Authentication
    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ClockSkew = TimeSpan.Zero
        };

        // Allow SignalR to receive token from query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Log.Warning("Authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddAuthorization();

    // Health Checks with reasonable timeouts
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            builder.Configuration.GetConnectionString("DefaultConnection")!,
            name: "postgresql",
            tags: new[] { "db", "sql", "postgresql" },
            timeout: TimeSpan.FromSeconds(5)) // Faster timeout for health checks
        .AddCheck<SignalRHealthCheck>(
            "signalr",
            tags: new[] { "realtime", "signalr" })
        .AddCheck<DatabaseSeedHealthCheck>(
            "database-seed",
            tags: new[] { "db", "seed" });

    // Health Checks UI - polls the health endpoint periodically
    builder.Services.AddHealthChecksUI(options =>
    {
        options.SetEvaluationTimeInSeconds(60); // Reduce polling frequency
        options.MaximumHistoryEntriesPerEndpoint(30);
        // Use full URL for the health endpoint
        options.AddHealthCheckEndpoint("MicroEraser API", "http://localhost:5000/health");
    }).AddInMemoryStorage();

    // SignalR for real-time communication
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB for large Yjs updates
    });

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            policy.WithOrigins(
                    builder.Configuration["Frontend:Url"] ?? "http://localhost:5173"
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .WithExposedHeaders("X-Correlation-ID");
        });
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    
    // Enhanced Swagger Configuration
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "MicroEraser API",
            Version = "v1",
            Description = "Collaborative Diagram Editor API with real-time sync, workspace management, and invite system.",
            Contact = new OpenApiContact
            {
                Name = "MicroEraser Team",
                Email = "support@microeraser.dev"
            }
        });
        
        // Enable annotations
        options.EnableAnnotations();
        
        // Add JWT authentication to Swagger
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter 'Bearer' followed by space and JWT token.\n\nExample: Bearer eyJhbGciOiJIUzI1NiIs..."
        });
        
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // Add operation filters for better documentation
        options.TagActionsBy(api =>
        {
            if (api.GroupName != null)
                return new[] { api.GroupName };

            if (api.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerActionDescriptor)
                return new[] { controllerActionDescriptor.ControllerName };

            return new[] { "Other" };
        });
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline
    
    // Add correlation ID first (before any logging)
    app.UseCorrelationId();
    
    // Add Serilog request logging (enriched with correlation ID and user context)
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
            
            if (httpContext.Items.TryGetValue("CorrelationId", out var correlationId))
            {
                diagnosticContext.Set("CorrelationId", correlationId);
            }
            
            // Add user context if authenticated
            var userId = httpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userId))
            {
                diagnosticContext.Set("UserId", userId);
            }
        };
        
        // Exclude health check endpoints from request logging to reduce noise
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (httpContext.Request.Path.StartsWithSegments("/health"))
            {
                return ex != null ? LogEventLevel.Error : LogEventLevel.Debug;
            }
            return ex != null ? LogEventLevel.Error 
                : httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
                : httpContext.Response.StatusCode >= 400 ? LogEventLevel.Warning
                : LogEventLevel.Information;
        };
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "MicroEraser API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "MicroEraser API Documentation";
            options.EnableDeepLinking();
            options.DisplayRequestDuration();
        });
        
        // Seed database in development
        try
        {
            await app.Services.SeedDatabaseAsync();
            DatabaseSeedHealthCheck.MarkAsSeeded();
            Log.Information("Database seeded successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to seed database");
        }
    }

    app.UseCors("AllowFrontend");

    app.UseAuthentication();
    app.UseAuthorization();

    // Health check endpoints
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        Predicate = _ => true
    });

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        Predicate = check => check.Tags.Contains("db")
    });

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        Predicate = _ => false // Liveness check - just return 200 if app is running
    });

    // Health Checks UI
    app.MapHealthChecksUI(options =>
    {
        options.UIPath = "/health-ui";
        options.ApiPath = "/health-api";
    });

    app.MapControllers();
    app.MapHub<CanvasHub>("/hubs/canvas");

    Log.Information("MicroEraser API started on {Urls}", string.Join(", ", app.Urls));
    
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
