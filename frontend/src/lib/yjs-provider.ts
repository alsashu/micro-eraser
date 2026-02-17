import * as Y from 'yjs'
import { IndexeddbPersistence } from 'y-indexeddb'
import {
  connectToCanvas,
  disconnectFromCanvas,
  sendSyncUpdate,
  sendAwarenessUpdate,
  saveSnapshot,
  type UserPresence,
} from './signalr'

/**
 * Yjs Document Provider for MicroEraser
 * 
 * CRDT Sync Architecture:
 * 
 * 1. Document Structure:
 *    - Y.Map<'nodes'>: React Flow nodes stored as a Yjs Map
 *    - Y.Map<'edges'>: React Flow edges stored as a Yjs Map
 *    - Y.Map<'viewport'>: Canvas viewport state
 * 
 * 2. Sync Flow:
 *    a. Client connects via SignalR
 *    b. Server sends latest snapshot (full Yjs state)
 *    c. Client merges snapshot with local state using Y.applyUpdate()
 *    d. Local changes trigger Y.Doc update events
 *    e. Updates are encoded and broadcast via SignalR
 *    f. Other clients apply updates to their Y.Doc
 *    g. Periodic snapshots saved to server
 * 
 * 3. Conflict Resolution:
 *    - Yjs uses CRDT (Conflict-free Replicated Data Types)
 *    - All updates are commutative and eventually consistent
 *    - No manual conflict resolution needed
 *    - Concurrent edits automatically merge
 */

export interface AwarenessState {
  cursor?: { x: number; y: number }
  selectedNodes?: string[]
  user?: { id: string; name: string; color: string }
}

export class CanvasYjsProvider {
  public doc: Y.Doc
  public nodes: Y.Map<unknown>
  public edges: Y.Map<unknown>
  public viewport: Y.Map<unknown>
  
  private canvasId: string
  private version: number = 0
  private persistence: IndexeddbPersistence | null = null
  private isConnected: boolean = false
  private saveInterval: number | null = null
  private awarenessStates: Map<string, AwarenessState> = new Map()
  
  // Callbacks
  public onNodesChange?: (nodes: Map<string, unknown>) => void
  public onEdgesChange?: (edges: Map<string, unknown>) => void
  public onAwarenessChange?: (states: Map<string, AwarenessState>) => void
  public onUsersChange?: (users: UserPresence[]) => void
  public onConnectionChange?: (connected: boolean) => void

  constructor(canvasId: string) {
    this.canvasId = canvasId
    this.doc = new Y.Doc()
    
    // Initialize shared types
    this.nodes = this.doc.getMap('nodes')
    this.edges = this.doc.getMap('edges')
    this.viewport = this.doc.getMap('viewport')

    // NOTE: Disabled IndexedDB persistence for now to avoid stale data conflicts
    // When offline support is needed, we need to implement proper sync logic
    // this.persistence = new IndexeddbPersistence(`canvas-${canvasId}`, this.doc)
    
    // Listen for document changes
    this.setupDocumentListeners()
  }

  private setupDocumentListeners() {
    // Listen for node changes
    this.nodes.observe((event) => {
      console.log('YjsProvider: Nodes changed, count:', this.nodes.size, 'keys changed:', event.keysChanged)
      this.onNodesChange?.(new Map(this.nodes.entries()))
    })

    // Listen for edge changes
    this.edges.observe((event) => {
      console.log('YjsProvider: Edges changed, count:', this.edges.size, 'keys changed:', event.keysChanged)
      this.onEdgesChange?.(new Map(this.edges.entries()))
    })

    // Listen for any document update to broadcast
    this.doc.on('update', (update: Uint8Array, origin: unknown) => {
      console.log('YjsProvider: Document update event, origin:', origin, 'isConnected:', this.isConnected, 'bytes:', update.length)
      // Only broadcast updates that originated locally
      if (origin !== 'remote' && this.isConnected) {
        const encoded = this.encodeUpdate(update)
        console.log('YjsProvider: Broadcasting update to other clients')
        sendSyncUpdate(this.canvasId, encoded)
        this.version++
      }
    })
  }

  async connect(): Promise<void> {
    try {
      console.log('CanvasYjsProvider: Connecting to canvas', this.canvasId)
      
      // Clear any stale IndexedDB data to ensure we use server state
      try {
        const dbName = `canvas-${this.canvasId}`
        const databases = await indexedDB.databases()
        if (databases.some(db => db.name === dbName)) {
          await new Promise<void>((resolve, reject) => {
            const request = indexedDB.deleteDatabase(dbName)
            request.onsuccess = () => resolve()
            request.onerror = () => reject(request.error)
          })
          console.log('CanvasYjsProvider: Cleared stale IndexedDB data')
        }
      } catch (e) {
        console.warn('Could not clear IndexedDB:', e)
      }
      
      await connectToCanvas(this.canvasId, {
        onInitialState: (state, version) => {
          console.log('CanvasYjsProvider: Received initial state, version:', version, 'state length:', state?.length ?? 0)
          if (state) {
            // Apply server state to local document
            const decoded = this.decodeUpdate(state)
            console.log('CanvasYjsProvider: Applying initial state, decoded bytes:', decoded.length)
            Y.applyUpdate(this.doc, decoded, 'remote')
          }
          this.version = version
          
          // Explicitly trigger callbacks with current state (in case observers didn't fire)
          console.log('CanvasYjsProvider: Initial state applied, nodes:', this.nodes.size, 'edges:', this.edges.size)
          this.onNodesChange?.(new Map(this.nodes.entries()))
          this.onEdgesChange?.(new Map(this.edges.entries()))
        },

        onSyncUpdate: (update, userId) => {
          console.log('CanvasYjsProvider: Received sync update from user:', userId, 'update length:', update.length)
          // Apply remote update to local document
          try {
            const decoded = this.decodeUpdate(update)
            console.log('CanvasYjsProvider: Applying update, decoded bytes:', decoded.length)
            Y.applyUpdate(this.doc, decoded, 'remote')
            console.log('CanvasYjsProvider: Update applied, nodes count:', this.nodes.size, 'edges count:', this.edges.size)
            
            // Explicitly trigger callbacks to ensure UI updates
            this.onNodesChange?.(new Map(this.nodes.entries()))
            this.onEdgesChange?.(new Map(this.edges.entries()))
          } catch (err) {
            console.error('CanvasYjsProvider: Failed to apply update:', err)
          }
        },

        onAwarenessUpdate: (state, userId) => {
          try {
            const parsed = JSON.parse(state) as AwarenessState
            this.awarenessStates.set(userId, parsed)
            this.onAwarenessChange?.(this.awarenessStates)
          } catch (e) {
            console.error('Failed to parse awareness state:', e)
          }
        },

        onUserJoined: (user) => {
          console.log('CanvasYjsProvider: User joined:', user.userName, user.userId)
          // Add to current users if not already present
          const existingIndex = this.currentUsers.findIndex(u => u.userId === user.userId)
          if (existingIndex === -1) {
            this.currentUsers = [...this.currentUsers, {
              userId: user.userId,
              userName: user.userName,
              connectionId: '',
              canEdit: user.canEdit,
              joinedAt: new Date().toISOString()
            }]
            this.onUsersChange?.(this.currentUsers)
          }
        },

        onUserLeft: (user) => {
          console.log('CanvasYjsProvider: User left:', user.userName, user.userId)
          // Remove from current users
          this.currentUsers = this.currentUsers.filter(u => u.userId !== user.userId)
          this.onUsersChange?.(this.currentUsers)
          this.awarenessStates.delete(user.userId)
          this.onAwarenessChange?.(this.awarenessStates)
        },

        onCurrentUsers: (users) => {
          console.log('CanvasYjsProvider: Current users:', users.length)
          this.currentUsers = users
          this.onUsersChange?.(users)
        },

        onError: (message) => {
          console.error('Canvas hub error:', message)
        },
      })

      console.log('CanvasYjsProvider: Successfully connected')
      this.isConnected = true
      this.onConnectionChange?.(true)

      // Start periodic snapshot saving (every 30 seconds)
      this.saveInterval = window.setInterval(() => {
        this.saveSnapshotToServer()
      }, 30000)

    } catch (error) {
      console.error('CanvasYjsProvider: Failed to connect:', error)
      this.isConnected = false
      this.onConnectionChange?.(false)
      throw error
    }
  }
  
  private currentUsers: UserPresence[] = []
  
  private getCurrentUsers(): UserPresence[] {
    return this.currentUsers
  }

  async disconnect(): Promise<void> {
    // Save final snapshot before disconnecting
    await this.saveSnapshotToServer()

    if (this.saveInterval) {
      clearInterval(this.saveInterval)
      this.saveInterval = null
    }

    await disconnectFromCanvas(this.canvasId)
    this.isConnected = false
    this.onConnectionChange?.(false)
  }

  /**
   * Update local awareness state and broadcast to others.
   * Used for cursor position, selection, etc.
   */
  updateAwareness(state: AwarenessState): void {
    if (this.isConnected) {
      const encoded = JSON.stringify(state)
      sendAwarenessUpdate(this.canvasId, encoded)
    }
  }

  /**
   * Add or update a node in the canvas.
   */
  setNode(id: string, node: unknown): void {
    this.nodes.set(id, node)
  }

  /**
   * Remove a node from the canvas.
   */
  deleteNode(id: string): void {
    this.nodes.delete(id)
  }

  /**
   * Add or update an edge in the canvas.
   */
  setEdge(id: string, edge: unknown): void {
    this.edges.set(id, edge)
  }

  /**
   * Remove an edge from the canvas.
   */
  deleteEdge(id: string): void {
    this.edges.delete(id)
  }

  /**
   * Get all nodes as an array.
   */
  getNodes(): unknown[] {
    return Array.from(this.nodes.values())
  }

  /**
   * Get all edges as an array.
   */
  getEdges(): unknown[] {
    return Array.from(this.edges.values())
  }

  /**
   * Save current state as a snapshot to the server.
   */
  private async saveSnapshotToServer(): Promise<void> {
    if (!this.isConnected) return

    try {
      const state = Y.encodeStateAsUpdate(this.doc)
      const encoded = this.encodeUpdate(state)
      await saveSnapshot(this.canvasId, encoded, this.version)
      console.log('Snapshot saved, version:', this.version)
    } catch (error) {
      console.error('Failed to save snapshot:', error)
    }
  }

  /**
   * Encode Yjs update to base64 for transmission.
   */
  private encodeUpdate(update: Uint8Array): string {
    return btoa(String.fromCharCode(...update))
  }

  /**
   * Decode base64 string back to Yjs update.
   */
  private decodeUpdate(encoded: string): Uint8Array {
    const binary = atob(encoded)
    const bytes = new Uint8Array(binary.length)
    for (let i = 0; i < binary.length; i++) {
      bytes[i] = binary.charCodeAt(i)
    }
    return bytes
  }

  /**
   * Cleanup resources.
   */
  destroy(): void {
    if (this.saveInterval) {
      clearInterval(this.saveInterval)
    }
    if (this.persistence) {
      this.persistence.destroy()
    }
    this.doc.destroy()
    this.currentUsers = []
    this.awarenessStates.clear()
  }
}
