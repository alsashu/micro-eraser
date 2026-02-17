import { getAuthToken } from './api'

/**
 * Frontend Event Logger
 * 
 * Captures user actions, navigation events, and errors.
 * Sends logs to backend for aggregation in Seq.
 * Supports correlation IDs for end-to-end tracing.
 */

export type LogLevel = 'debug' | 'info' | 'warn' | 'error'

export interface LogEvent {
  eventType: string
  level: LogLevel
  message: string
  timestamp: string
  correlationId?: string
  canvasId?: string
  workspaceId?: string
  metadata?: Record<string, unknown>
}

// Event type constants matching backend
export const EventTypes = {
  // Navigation events
  PAGE_VIEW: 'page_view',
  NAVIGATION_START: 'navigation_start',
  NAVIGATION_END: 'navigation_end',

  // Authentication events
  LOGIN_ATTEMPT: 'login_attempt',
  LOGIN_SUCCESS: 'login_success',
  LOGIN_FAILURE: 'login_failure',
  LOGOUT: 'logout',
  TOKEN_REFRESH: 'token_refresh',

  // Canvas operations
  CANVAS_OPENED: 'canvas_opened',
  CANVAS_CLOSED: 'canvas_closed',
  NODE_CREATED: 'node_created',
  NODE_UPDATED: 'node_updated',
  NODE_DELETED: 'node_deleted',
  EDGE_CREATED: 'edge_created',
  EDGE_DELETED: 'edge_deleted',
  CANVAS_SAVED: 'canvas_saved',

  // Collaboration events
  COLLABORATOR_JOINED: 'collaborator_joined',
  COLLABORATOR_LEFT: 'collaborator_left',
  SYNC_STARTED: 'sync_started',
  SYNC_COMPLETED: 'sync_completed',
  SYNC_ERROR: 'sync_error',
  CONNECTION_LOST: 'connection_lost',
  CONNECTION_RESTORED: 'connection_restored',

  // Workspace events
  WORKSPACE_CREATED: 'workspace_created',
  WORKSPACE_DELETED: 'workspace_deleted',
  MEMBER_INVITED: 'member_invited',
  INVITE_ACCEPTED: 'invite_accepted',

  // Error events
  ERROR: 'error',
  UNHANDLED_ERROR: 'unhandled_error',
  API_ERROR: 'api_error',
} as const

// Generate or retrieve correlation ID for request tracing
let currentCorrelationId: string | null = null

export function generateCorrelationId(): string {
  currentCorrelationId = crypto.randomUUID().replace(/-/g, '')
  return currentCorrelationId
}

export function getCorrelationId(): string {
  if (!currentCorrelationId) {
    currentCorrelationId = generateCorrelationId()
  }
  return currentCorrelationId
}

export function setCorrelationId(id: string): void {
  currentCorrelationId = id
}

// Log buffer for batching
const logBuffer: LogEvent[] = []
let flushTimeout: ReturnType<typeof setTimeout> | null = null
const FLUSH_INTERVAL = 5000 // 5 seconds
const MAX_BUFFER_SIZE = 20

class EventLogger {
  private canvasId: string | null = null
  private workspaceId: string | null = null
  private enabled: boolean = true
  private consoleOutput: boolean = import.meta.env.DEV

  /**
   * Set current canvas context for automatic enrichment
   */
  setCanvasContext(canvasId: string | null, workspaceId?: string | null): void {
    this.canvasId = canvasId
    this.workspaceId = workspaceId ?? null
  }

  /**
   * Clear canvas context
   */
  clearCanvasContext(): void {
    this.canvasId = null
    this.workspaceId = null
  }

  /**
   * Enable or disable logging
   */
  setEnabled(enabled: boolean): void {
    this.enabled = enabled
  }

  /**
   * Enable or disable console output
   */
  setConsoleOutput(enabled: boolean): void {
    this.consoleOutput = enabled
  }

  /**
   * Log a debug message
   */
  debug(eventType: string, message: string, metadata?: Record<string, unknown>): void {
    this.log('debug', eventType, message, metadata)
  }

  /**
   * Log an info message
   */
  info(eventType: string, message: string, metadata?: Record<string, unknown>): void {
    this.log('info', eventType, message, metadata)
  }

  /**
   * Log a warning message
   */
  warn(eventType: string, message: string, metadata?: Record<string, unknown>): void {
    this.log('warn', eventType, message, metadata)
  }

  /**
   * Log an error message
   */
  error(eventType: string, message: string, error?: Error, metadata?: Record<string, unknown>): void {
    const enrichedMetadata = {
      ...metadata,
      ...(error && {
        errorName: error.name,
        errorMessage: error.message,
        errorStack: error.stack,
      }),
    }
    this.log('error', eventType, message, enrichedMetadata)
  }

  /**
   * Track a page view
   */
  trackPageView(path: string, title?: string): void {
    this.info(EventTypes.PAGE_VIEW, `Viewed ${path}`, { path, title })
  }

  /**
   * Track a canvas operation
   */
  trackCanvasOperation(
    operation: 'node_created' | 'node_updated' | 'node_deleted' | 'edge_created' | 'edge_deleted',
    details: Record<string, unknown>
  ): void {
    this.info(operation, `Canvas operation: ${operation}`, details)
  }

  /**
   * Track a collaboration event
   */
  trackCollaboration(
    event: 'collaborator_joined' | 'collaborator_left' | 'sync_started' | 'sync_completed' | 'sync_error' | 'connection_lost' | 'connection_restored',
    details?: Record<string, unknown>
  ): void {
    const level = event.includes('error') || event === 'connection_lost' ? 'warn' : 'info'
    this.log(level, event, `Collaboration: ${event}`, details)
  }

  /**
   * Core logging method
   */
  private log(
    level: LogLevel,
    eventType: string,
    message: string,
    metadata?: Record<string, unknown>
  ): void {
    if (!this.enabled) return

    const event: LogEvent = {
      eventType,
      level,
      message,
      timestamp: new Date().toISOString(),
      correlationId: getCorrelationId(),
      canvasId: this.canvasId ?? undefined,
      workspaceId: this.workspaceId ?? undefined,
      metadata,
    }

    // Console output in development
    if (this.consoleOutput) {
      const consoleMethod = level === 'error' ? console.error 
        : level === 'warn' ? console.warn 
        : level === 'debug' ? console.debug 
        : console.log
      
      consoleMethod(
        `[${event.timestamp}] [${level.toUpperCase()}] ${eventType}: ${message}`,
        metadata || ''
      )
    }

    // Add to buffer
    logBuffer.push(event)

    // Flush if buffer is full
    if (logBuffer.length >= MAX_BUFFER_SIZE) {
      this.flush()
    } else if (!flushTimeout) {
      // Schedule flush
      flushTimeout = setTimeout(() => this.flush(), FLUSH_INTERVAL)
    }
  }

  /**
   * Flush buffered logs to the backend
   */
  async flush(): Promise<void> {
    if (flushTimeout) {
      clearTimeout(flushTimeout)
      flushTimeout = null
    }

    if (logBuffer.length === 0) return

    const logsToSend = [...logBuffer]
    logBuffer.length = 0

    try {
      const token = getAuthToken()
      const headers: HeadersInit = {
        'Content-Type': 'application/json',
        'X-Correlation-ID': getCorrelationId(),
      }
      
      if (token) {
        headers['Authorization'] = `Bearer ${token}`
      }

      await fetch('/api/client-logs/batch', {
        method: 'POST',
        headers,
        body: JSON.stringify({ logs: logsToSend }),
      })
    } catch (error) {
      // Re-add logs to buffer if send failed (but don't exceed max)
      const remaining = MAX_BUFFER_SIZE - logBuffer.length
      if (remaining > 0) {
        logBuffer.unshift(...logsToSend.slice(0, remaining))
      }
      
      if (this.consoleOutput) {
        console.error('Failed to send logs to server:', error)
      }
    }
  }

  /**
   * Send a single log immediately (for critical events)
   */
  async sendImmediate(event: LogEvent): Promise<void> {
    try {
      const token = getAuthToken()
      const headers: HeadersInit = {
        'Content-Type': 'application/json',
        'X-Correlation-ID': event.correlationId || getCorrelationId(),
      }
      
      if (token) {
        headers['Authorization'] = `Bearer ${token}`
      }

      await fetch('/api/client-logs', {
        method: 'POST',
        headers,
        body: JSON.stringify(event),
      })
    } catch (error) {
      if (this.consoleOutput) {
        console.error('Failed to send immediate log:', error)
      }
    }
  }
}

// Singleton instance
export const logger = new EventLogger()

// Set up global error handler
if (typeof window !== 'undefined') {
  window.addEventListener('error', (event) => {
    logger.error(
      EventTypes.UNHANDLED_ERROR,
      event.message || 'Unhandled error',
      event.error,
      {
        filename: event.filename,
        lineno: event.lineno,
        colno: event.colno,
      }
    )
  })

  window.addEventListener('unhandledrejection', (event) => {
    logger.error(
      EventTypes.UNHANDLED_ERROR,
      'Unhandled promise rejection',
      event.reason instanceof Error ? event.reason : new Error(String(event.reason))
    )
  })

  // Flush logs before page unload
  window.addEventListener('beforeunload', () => {
    logger.flush()
  })

  // Flush logs when page becomes hidden
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'hidden') {
      logger.flush()
    }
  })
}

export default logger
