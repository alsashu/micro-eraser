import * as signalR from '@microsoft/signalr'
import { getAuthToken } from './api'

let connection: signalR.HubConnection | null = null
let isConnecting = false
let currentCanvasId: string | null = null

export interface UserPresence {
  userId: string
  userName: string
  connectionId: string
  canEdit: boolean
  joinedAt: string
}

export interface CanvasHubCallbacks {
  onInitialState?: (state: string | null, version: number) => void
  onSyncUpdate?: (update: string, userId: string) => void
  onAwarenessUpdate?: (state: string, userId: string) => void
  onUserJoined?: (user: { userId: string; userName: string; canEdit: boolean }) => void
  onUserLeft?: (user: { userId: string; userName: string }) => void
  onCurrentUsers?: (users: UserPresence[]) => void
  onError?: (message: string) => void
}

/**
 * Create and manage SignalR connection for real-time canvas collaboration.
 * 
 * Connection flow:
 * 1. Connect to hub with JWT token
 * 2. Join canvas room
 * 3. Receive initial state (Yjs snapshot)
 * 4. Exchange updates with other clients
 * 5. Broadcast awareness (cursor position, etc)
 */
export async function connectToCanvas(
  canvasId: string,
  callbacks: CanvasHubCallbacks
): Promise<signalR.HubConnection> {
  // Prevent multiple simultaneous connections
  if (isConnecting) {
    console.log('Connection already in progress, waiting...')
    await new Promise(resolve => setTimeout(resolve, 500))
    if (connection && connection.state === signalR.HubConnectionState.Connected) {
      return connection
    }
  }
  
  // If already connected to same canvas, return existing connection
  if (connection && 
      connection.state === signalR.HubConnectionState.Connected && 
      currentCanvasId === canvasId) {
    console.log('Already connected to canvas:', canvasId)
    return connection
  }
  
  // Disconnect existing connection if connecting to different canvas
  if (connection && currentCanvasId !== canvasId) {
    console.log('Disconnecting from previous canvas:', currentCanvasId)
    try {
      await connection.stop()
    } catch (e) {
      console.warn('Error stopping previous connection:', e)
    }
    connection = null
  }
  
  const token = getAuthToken()
  
  if (!token) {
    throw new Error('Not authenticated')
  }
  
  isConnecting = true
  currentCanvasId = canvasId

  // Build connection - use URL query param for token (most compatible approach)
  connection = new signalR.HubConnectionBuilder()
    .withUrl(`/hubs/canvas?access_token=${encodeURIComponent(token)}`, {
      // Allow all transport types for better compatibility
      skipNegotiation: false,
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext) => {
        // Exponential backoff: 0s, 2s, 4s, 8s, 16s, then 30s max
        const delays = [0, 2000, 4000, 8000, 16000, 30000]
        return delays[Math.min(retryContext.previousRetryCount, delays.length - 1)]
      },
    })
    .withServerTimeout(30000) // 30 second server timeout
    .withKeepAliveInterval(15000) // 15 second keep-alive
    .configureLogging(signalR.LogLevel.Information)
    .build()

  // Register event handlers
  connection.on('InitialState', (state: string | null, version: number) => {
    console.log('SignalR: Received InitialState event, version:', version, 'state length:', state?.length ?? 0)
    callbacks.onInitialState?.(state, version)
  })

  connection.on('SyncUpdate', (update: string, userId: string) => {
    console.log('SignalR: Received SyncUpdate event, userId:', userId, 'update length:', update?.length)
    callbacks.onSyncUpdate?.(update, userId)
  })

  connection.on('AwarenessUpdate', (state: string, userId: string) => {
    callbacks.onAwarenessUpdate?.(state, userId)
  })

  connection.on('UserJoined', (user: { userId: string; userName: string; canEdit: boolean }) => {
    callbacks.onUserJoined?.(user)
  })

  connection.on('UserLeft', (user: { userId: string; userName: string }) => {
    callbacks.onUserLeft?.(user)
  })

  connection.on('CurrentUsers', (users: UserPresence[]) => {
    console.log('SignalR: Received CurrentUsers event, count:', users?.length, 'users:', JSON.stringify(users))
    callbacks.onCurrentUsers?.(users)
  })

  connection.on('Error', (message: string) => {
    callbacks.onError?.(message)
  })

  // Handle reconnection
  connection.onreconnected(async (connectionId) => {
    console.log('Reconnected to canvas hub, connectionId:', connectionId)
    await connection?.invoke('JoinCanvas', canvasId)
  })

  connection.onreconnecting((error) => {
    console.log('Reconnecting to canvas hub...', error)
  })

  connection.onclose((error) => {
    console.log('Connection closed:', error)
  })

  // Start connection with retry logic
  const maxRetries = 3
  let lastError: unknown = null
  
  for (let attempt = 1; attempt <= maxRetries; attempt++) {
    try {
      console.log(`SignalR connection attempt ${attempt}/${maxRetries}...`)
      
      // Start the connection
      await connection.start()
      console.log('SignalR connected, state:', connection.state, 'connectionId:', connection.connectionId)
      
      // Wait for connection to stabilize
      await new Promise(resolve => setTimeout(resolve, 200))
      
      // Verify connection is still active
      if (connection.state !== signalR.HubConnectionState.Connected) {
        throw new Error(`Connection not in Connected state: ${connection.state}`)
      }
      
      // Join canvas room
      console.log('Invoking JoinCanvas with canvasId:', canvasId)
      await connection.invoke('JoinCanvas', canvasId)
      console.log('Successfully joined canvas:', canvasId)
      
      // Success - exit retry loop
      lastError = null
      break
    } catch (err) {
      console.error(`SignalR attempt ${attempt} failed:`, err)
      lastError = err
      
      // Stop connection before retry
      if (connection.state !== signalR.HubConnectionState.Disconnected) {
        try {
          await connection.stop()
        } catch (stopErr) {
          console.warn('Error stopping connection:', stopErr)
        }
      }
      
      if (attempt < maxRetries) {
        // Wait before retry with exponential backoff
        const delay = Math.min(1000 * Math.pow(2, attempt - 1), 5000)
        console.log(`Retrying in ${delay}ms...`)
        await new Promise(resolve => setTimeout(resolve, delay))
      }
    }
  }
  
  if (lastError) {
    console.error('All SignalR connection attempts failed')
    isConnecting = false
    currentCanvasId = null
    throw lastError
  }
  
  isConnecting = false
  return connection
}

export async function disconnectFromCanvas(canvasId: string) {
  console.log('Disconnecting from canvas:', canvasId)
  
  if (connection && connection.state === signalR.HubConnectionState.Connected) {
    try {
      await connection.invoke('LeaveCanvas', canvasId)
    } catch (error) {
      console.warn('Error leaving canvas:', error)
    }
    try {
      await connection.stop()
    } catch (error) {
      console.warn('Error stopping connection:', error)
    }
  }
  connection = null
  currentCanvasId = null
  isConnecting = false
}

export async function sendSyncUpdate(canvasId: string, update: string) {
  if (connection && connection.state === signalR.HubConnectionState.Connected) {
    try {
      console.log('SignalR: Sending SyncUpdate for canvas', canvasId, 'update length:', update.length)
      await connection.invoke('SyncUpdate', canvasId, update)
    } catch (err) {
      console.error('SignalR: Failed to send SyncUpdate:', err)
    }
  } else {
    console.warn('SignalR: Cannot send SyncUpdate - connection state:', connection?.state)
  }
}

export async function sendAwarenessUpdate(canvasId: string, awarenessState: string) {
  if (connection && connection.state === signalR.HubConnectionState.Connected) {
    await connection.invoke('AwarenessUpdate', canvasId, awarenessState)
  }
}

export async function saveSnapshot(canvasId: string, state: string, version: number) {
  if (connection && connection.state === signalR.HubConnectionState.Connected) {
    await connection.invoke('SaveSnapshot', canvasId, state, version)
  }
}

export function getConnectionState(): signalR.HubConnectionState | null {
  return connection?.state ?? null
}
