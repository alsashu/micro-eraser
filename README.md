# MicroEraser - Collaborative Diagram Editor

A production-ready collaborative diagram editor similar to Eraser.io, built with React, ASP.NET Core, and Yjs CRDTs for real-time synchronization.

## Features

- **Real-time Collaboration**: Multiple users can edit the same canvas simultaneously with live sync
- **CRDT-based Sync**: Conflict-free merging using Yjs - no data loss, automatic conflict resolution
- **Presence Indicators**: See other collaborators' cursors and selections in real-time
- **Workspace Management**: Organize diagrams into workspaces with role-based access
- **Invite System**: Share workspaces via email invites or shareable links with permission controls
- **Offline Support**: Local persistence with IndexedDB for offline editing
- **Dark/Light Theme**: System preference detection with manual toggle
- **Modern UI**: Clean, minimal Notion-like interface with Tailwind CSS
- **End-to-End Logging**: Structured logging with Serilog, Seq integration, and correlation IDs
- **Health Monitoring**: ASP.NET Core health checks with UI dashboard
- **API Documentation**: Interactive Swagger/OpenAPI documentation with JWT support

## Tech Stack

### Frontend
- **React 18** with TypeScript
- **React Flow** for the diagram canvas
- **Yjs** for CRDT-based real-time sync
- **Tailwind CSS** for styling
- **Framer Motion** for animations
- **Zustand** for state management
- **Radix UI** for accessible components

### Backend
- **ASP.NET Core 8** Web API
- **SignalR** for WebSocket real-time communication
- **Entity Framework Core** with PostgreSQL
- **JWT** authentication with refresh tokens
- **Serilog** for structured logging
- **Clean Architecture** (API, Application, Domain, Infrastructure layers)

### Observability
- **Serilog** for structured JSON logging
- **Seq** for log aggregation and visualization
- **Health Checks** for API, PostgreSQL, and SignalR monitoring
- **Correlation IDs** for end-to-end request tracing

### Database
- **PostgreSQL** for persistent storage
- **IndexedDB** for client-side caching

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                           Frontend (React)                          │
├─────────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌──────────────┐  ┌─────────────────────────────┐ │
│  │  React Flow │  │  Yjs Y.Doc   │  │   SignalR Client            │ │
│  │  Canvas     │◄─┤  (CRDT)      │◄─┤   (WebSocket transport)     │ │
│  └─────────────┘  └──────────────┘  └─────────────────────────────┘ │
└────────────────────────────┬────────────────────────────────────────┘
                             │ WebSocket
┌────────────────────────────▼────────────────────────────────────────┐
│                        Backend (ASP.NET Core)                       │
├─────────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌──────────────────┐  ┌────────────────────┐  │
│  │  SignalR Hub    │  │  REST API        │  │  JWT Auth          │  │
│  │  (Canvas sync)  │  │  (CRUD ops)      │  │  Middleware        │  │
│  └────────┬────────┘  └────────┬─────────┘  └────────────────────┘  │
│           │                    │                                    │
│  ┌────────▼────────────────────▼─────────────────────────────────┐  │
│  │              Application Services                             │  │
│  │  (AuthService, WorkspaceService, CanvasService, InviteService)│  │
│  └────────────────────────────┬──────────────────────────────────┘  │
│                               │                                     │
│  ┌────────────────────────────▼──────────────────────────────────┐  │
│  │                    Infrastructure Layer                       │  │
│  │  (Repositories, TokenService, DbContext)                      │  │
│  └────────────────────────────┬──────────────────────────────────┘  │
└───────────────────────────────┼─────────────────────────────────────┘
                                │
┌───────────────────────────────▼─────────────────────────────────────┐
│                         PostgreSQL Database                         │
│  ┌─────────┐ ┌────────────┐ ┌─────────┐ ┌──────────────────────┐    │
│  │ Users   │ │ Workspaces │ │ Canvases│ │ CanvasSnapshots      │    │
│  │         │ │            │ │         │ │ (Yjs binary state)   │    │
│  └─────────┘ └────────────┘ └─────────┘ └──────────────────────┘    │
└─────────────────────────────────────────────────────────────────────┘
```

## CRDT Synchronization Flow

```
   Client A                    Server                    Client B
      │                          │                          │
      │  1. Connect (JWT)        │                          │
      ├─────────────────────────►│                          │
      │                          │                          │
      │  2. JoinCanvas(id)       │                          │
      ├─────────────────────────►│                          │
      │                          │                          │
      │  3. InitialState         │                          │
      │◄─────────────────────────┤  (Yjs snapshot from DB)  │
      │                          │                          │
      │  4. User makes edit      │                          │
      │  (Y.Doc updated locally) │                          │
      │                          │                          │
      │  5. SyncUpdate(delta)    │                          │
      ├─────────────────────────►│                          │
      │                          │  6. Broadcast            │
      │                          ├─────────────────────────►│
      │                          │                          │
      │                          │     7. Apply Y.applyUpdate()
      │                          │     (CRDT merge, no conflicts)
      │                          │                          │
      │  8. Periodic snapshot    │                          │
      ├─────────────────────────►│                          │
      │                          │  9. Save to DB           │
      │                          │                          │
```

### Key CRDT Concepts

1. **Y.Doc**: The root Yjs document containing all shared state
2. **Y.Map**: Used for nodes and edges - allows key-value storage with automatic merging
3. **Updates**: Delta-based changes that can be applied in any order
4. **Snapshots**: Full document state saved periodically for recovery

## Project Structure

```
micro-eraser/
├── backend/
│   ├── MicroEraser.sln
│   ├── MicroEraser.Api/           # REST API & SignalR Hub
│   │   ├── Controllers/
│   │   ├── Hubs/
│   │   └── Program.cs
│   ├── MicroEraser.Application/   # Business logic
│   │   ├── DTOs/
│   │   ├── Interfaces/
│   │   └── Services/
│   ├── MicroEraser.Domain/        # Entities
│   │   └── Entities/
│   └── MicroEraser.Infrastructure/ # Data access
│       ├── Data/
│       ├── Repositories/
│       └── Services/
├── frontend/
│   ├── src/
│   │   ├── components/
│   │   │   ├── layout/
│   │   │   └── ui/
│   │   ├── contexts/
│   │   ├── lib/
│   │   ├── pages/
│   │   └── App.tsx
│   ├── package.json
│   └── vite.config.ts
├── database/
│   └── schema.sql
└── README.md
```

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)
- [PostgreSQL 14+](https://www.postgresql.org/)

### Database Setup

1. Create a PostgreSQL database:

```sql
CREATE DATABASE microeraser;
```

2. Update the connection string in `backend/MicroEraser.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=microeraser;Username=postgres;Password=your_password"
  }
}
```

### Backend Setup

```bash
cd backend

# Restore packages
dotnet restore

# Run database migrations (EF Core will auto-migrate on startup in dev)
# Or manually apply schema
psql -d microeraser -f ../database/schema.sql

# Run the API
dotnet run --project MicroEraser.Api
```

The API will start at `http://localhost:5000` with Swagger at `http://localhost:5000/swagger`.

### Frontend Setup

```bash
cd frontend

# Install dependencies
npm install

# Start development server
npm run dev
```

The frontend will start at `http://localhost:5173`.

### Environment Variables

#### Backend (appsettings.json)

| Variable | Description | Default |
|----------|-------------|---------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string | localhost |
| `Jwt:Secret` | JWT signing key (32+ chars) | Generated |
| `Jwt:Issuer` | JWT issuer | MicroEraser |
| `Jwt:Audience` | JWT audience | MicroEraserClient |
| `Jwt:AccessTokenExpirationMinutes` | Access token TTL | 15 |
| `Jwt:RefreshTokenExpirationDays` | Refresh token TTL | 7 |
| `Frontend:Url` | Frontend URL for CORS | http://localhost:5173 |

### Demo Credentials

When running in development mode, the database is seeded with sample data:

| Role | Email | Password |
|------|-------|----------|
| Admin | admin@microeraser.dev | Admin123! |
| Editor | editor@microeraser.dev | Editor123! |
| Viewer | viewer@microeraser.dev | Viewer123! |

## API Endpoints

### Authentication
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Login
- `POST /api/auth/refresh` - Refresh access token
- `GET /api/auth/me` - Get current user
- `POST /api/auth/logout` - Logout (revoke token)

### Workspaces
- `GET /api/workspace` - List user's workspaces
- `GET /api/workspace/{id}` - Get workspace details
- `POST /api/workspace` - Create workspace
- `PUT /api/workspace/{id}` - Update workspace
- `DELETE /api/workspace/{id}` - Delete workspace
- `POST /api/workspace/{id}/members` - Add member
- `DELETE /api/workspace/{id}/members/{userId}` - Remove member

### Canvases
- `GET /api/canvas/workspace/{workspaceId}` - List canvases
- `GET /api/canvas/{id}` - Get canvas details
- `POST /api/canvas/workspace/{workspaceId}` - Create canvas
- `PUT /api/canvas/{id}` - Update canvas
- `DELETE /api/canvas/{id}` - Delete canvas
- `GET /api/canvas/{id}/snapshot` - Get latest Yjs snapshot
- `POST /api/canvas/{id}/snapshot` - Save Yjs snapshot

### Invites
- `GET /api/invite/workspace/{workspaceId}` - List invites
- `POST /api/invite/workspace/{workspaceId}/email` - Create email invite
- `POST /api/invite/workspace/{workspaceId}/link` - Create link invite
- `GET /api/invite/validate/{token}` - Validate invite token
- `POST /api/invite/accept` - Accept invite
- `DELETE /api/invite/{id}` - Delete invite

### SignalR Hub (`/hubs/canvas`)
- `JoinCanvas(canvasId)` - Join a canvas room
- `LeaveCanvas(canvasId)` - Leave a canvas room
- `SyncUpdate(canvasId, update)` - Broadcast Yjs update
- `AwarenessUpdate(canvasId, state)` - Broadcast cursor/selection
- `SaveSnapshot(canvasId, state, version)` - Save snapshot to DB

## Observability & Monitoring

### Logging Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          Frontend (Browser)                              │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  Event Logger → Buffers events → POST /api/client-logs/batch    │   │
│  │  (Tracks: page views, canvas ops, collaboration, errors)        │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                               │ X-Correlation-ID                        │
└───────────────────────────────┼─────────────────────────────────────────┘
                                ▼
┌───────────────────────────────────────────────────────────────────────────┐
│                          Backend (ASP.NET Core)                           │
│  ┌──────────────────┐  ┌──────────────────┐  ┌──────────────────────┐   │
│  │ Correlation ID   │→ │ Request Logging  │→ │ Serilog (Structured) │   │
│  │ Middleware       │  │ Middleware       │  │ JSON Logs            │   │
│  └──────────────────┘  └──────────────────┘  └──────────┬───────────┘   │
└──────────────────────────────────────────────────────────┼───────────────┘
                                                           ▼
                                    ┌────────────────────────────────────┐
                                    │              Seq                    │
                                    │  (Log Aggregation & Visualization) │
                                    │  http://localhost:5341              │
                                    └────────────────────────────────────┘
```

### Correlation IDs

Every request is tagged with a correlation ID that flows through:
- Frontend event logs
- API requests (X-Correlation-ID header)
- Backend processing
- Seq log entries

This enables full traceability of user actions from UI click to database operation.

### Health Checks

| Endpoint | Description |
|----------|-------------|
| `/health` | Full health check (all components) |
| `/health/ready` | Readiness check (database connectivity) |
| `/health/live` | Liveness check (app is running) |
| `/health-ui` | Visual health check dashboard |

### Seq Setup

1. Start Seq using Docker:
```bash
docker-compose up -d seq
```

2. Access Seq at `http://localhost:5341`

3. View logs with filters:
   - By CorrelationId: `CorrelationId = "abc123"`
   - By UserId: `UserId = "user-guid"`
   - By CanvasId: `ClientCanvasId = "canvas-guid"`
   - By Source: `Source = "Frontend"`
   - By EventType: `EventType = "node_created"`

### Frontend Event Types

| Event Type | Description |
|------------|-------------|
| `page_view` | User navigated to a page |
| `login_success` / `login_failure` | Authentication events |
| `canvas_opened` / `canvas_closed` | Canvas lifecycle |
| `node_created` / `node_deleted` | Canvas operations |
| `edge_created` / `edge_deleted` | Connection operations |
| `collaborator_joined` / `collaborator_left` | Collaboration |
| `sync_started` / `sync_completed` / `sync_error` | Sync status |
| `connection_lost` / `connection_restored` | WebSocket status |

### Log Levels

- **Debug**: Detailed diagnostic info (API requests, internal operations)
- **Information**: General operational events (user actions, canvas operations)
- **Warning**: Potential issues (connection lost, login failures)
- **Error**: Failures requiring attention (API errors, sync failures)

## Development

### Building for Production

```bash
# Backend
cd backend
dotnet publish -c Release -o ./publish

# Frontend
cd frontend
npm run build
```

### Running Tests

```bash
# Backend
cd backend
dotnet test

# Frontend
cd frontend
npm test
```

## Deployment

### Docker Compose (Recommended)

Create a `docker-compose.yml`:

```yaml
version: '3.8'
services:
  db:
    image: postgres:14
    environment:
      POSTGRES_DB: microeraser
      POSTGRES_PASSWORD: your_secure_password
    volumes:
      - pgdata:/var/lib/postgresql/data

  backend:
    build: ./backend
    environment:
      - ConnectionStrings__DefaultConnection=Host=db;Database=microeraser;Username=postgres;Password=your_secure_password
      - Jwt__Secret=your_32_char_secret_key_here_!!!
    depends_on:
      - db
    ports:
      - "5000:80"

  frontend:
    build: ./frontend
    ports:
      - "80:80"

volumes:
  pgdata:
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

MIT License - see LICENSE file for details.

## Acknowledgments

- [Yjs](https://github.com/yjs/yjs) - CRDT implementation
- [React Flow](https://reactflow.dev/) - Diagram canvas
- [Radix UI](https://www.radix-ui.com/) - UI primitives
- [Tailwind CSS](https://tailwindcss.com/) - Styling
