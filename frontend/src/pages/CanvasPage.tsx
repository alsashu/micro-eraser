import { useState, useEffect, useCallback, useRef } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import ReactFlow, {
  Node,
  Edge,
  Controls,
  Background,
  MiniMap,
  useNodesState,
  useEdgesState,
  addEdge,
  Connection,
  NodeChange,
  EdgeChange,
  applyNodeChanges,
  applyEdgeChanges,
  ReactFlowProvider,
  useReactFlow,
  Panel,
} from 'reactflow'
import 'reactflow/dist/style.css'
import { motion, AnimatePresence } from 'framer-motion'
import {
  ArrowLeft,
  Plus,
  Square,
  Circle,
  Diamond,
  Type,
  Minus,
  Save,
  Users,
  Wifi,
  WifiOff,
  Undo,
  Redo,
} from 'lucide-react'
import { Button } from '@/components/ui/Button'
import { ThemeToggle } from '@/components/layout/ThemeToggle'
import { Avatar, AvatarFallback } from '@/components/ui/Avatar'
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/Tooltip'
import { toast } from '@/components/ui/Toaster'
import { api, type CanvasDetail } from '@/lib/api'
import { CanvasYjsProvider, type AwarenessState } from '@/lib/yjs-provider'
import { type UserPresence } from '@/lib/signalr'
import { cn, getInitials, generateRandomColor } from '@/lib/utils'
import { useAuth } from '@/contexts/AuthContext'
import { logger, EventTypes } from '@/lib/logger'

// Custom node types
const nodeTypes = {
  custom: CustomNode,
}

function CustomNode({ data, selected }: { data: { label: string; shape: string; color: string }; selected: boolean }) {
  const shapeClasses = {
    rectangle: 'rounded-lg',
    circle: 'rounded-full',
    diamond: 'rotate-45',
  }

  return (
    <div
      className={cn(
        'px-4 py-2 border-2 bg-card shadow-sm transition-all duration-200 min-w-[100px] min-h-[40px] flex items-center justify-center',
        shapeClasses[data.shape as keyof typeof shapeClasses] || 'rounded-lg',
        selected ? 'border-primary ring-2 ring-primary/20' : 'border-border',
        data.shape === 'diamond' && '-rotate-45'
      )}
      style={{ backgroundColor: data.color || 'var(--card)' }}
    >
      <span className={cn(data.shape === 'diamond' && 'rotate-45')}>{data.label}</span>
    </div>
  )
}

// Initial nodes for new canvases
const defaultNodes: Node[] = [
  {
    id: 'welcome',
    type: 'custom',
    position: { x: 250, y: 200 },
    data: { label: 'Welcome! Start adding nodes', shape: 'rectangle', color: '' },
  },
]

function CanvasEditor() {
  const { canvasId } = useParams<{ canvasId: string }>()
  const navigate = useNavigate()
  const { user } = useAuth()
  const reactFlowInstance = useReactFlow()
  
  const [canvas, setCanvas] = useState<CanvasDetail | null>(null)
  const [nodes, setNodes, onNodesChange] = useNodesState(defaultNodes)
  const [edges, setEdges, onEdgesChange] = useEdgesState([])
  const [isConnected, setIsConnected] = useState(false)
  const [collaborators, setCollaborators] = useState<UserPresence[]>([])
  const [awarenessStates, setAwarenessStates] = useState<Map<string, AwarenessState>>(new Map())
  const [selectedTool, setSelectedTool] = useState<string>('select')
  const [isLoading, setIsLoading] = useState(true)
  
  const providerRef = useRef<CanvasYjsProvider | null>(null)
  const userColorRef = useRef(generateRandomColor())

  // Load canvas details
  useEffect(() => {
    if (!canvasId) return

    // Set logger context
    logger.setCanvasContext(canvasId)
    logger.info(EventTypes.CANVAS_OPENED, 'Opening canvas', { canvasId })

    async function loadCanvas() {
      try {
        const response = await api.get(`/canvas/${canvasId}`)
        setCanvas(response.data)
        logger.info(EventTypes.CANVAS_OPENED, 'Canvas loaded successfully', { 
          canvasId, 
          canvasName: response.data.name,
          workspaceId: response.data.workspaceId 
        })
      } catch (error) {
        logger.error(EventTypes.ERROR, 'Failed to load canvas', error instanceof Error ? error : undefined, { canvasId })
        toast({ title: 'Failed to load canvas', variant: 'destructive' })
        navigate('/dashboard')
      } finally {
        setIsLoading(false)
      }
    }

    loadCanvas()

    // Cleanup on unmount
    return () => {
      logger.info(EventTypes.CANVAS_CLOSED, 'Closing canvas', { canvasId })
      logger.clearCanvasContext()
    }
  }, [canvasId, navigate])

  // Track collaborators with a ref to avoid stale closures
  const collaboratorsRef = useRef<UserPresence[]>([])
  collaboratorsRef.current = collaborators

  // Initialize Yjs provider and connect
  useEffect(() => {
    if (!canvasId || !user) return

    // Avoid double connection in React Strict Mode
    let isActive = true
    
    const provider = new CanvasYjsProvider(canvasId)
    providerRef.current = provider

    // Set up callbacks
    provider.onNodesChange = (nodesMap) => {
      if (!isActive) return
      const nodesArray = Array.from(nodesMap.values()) as Node[]
      console.log('CanvasPage: onNodesChange called, nodes count:', nodesArray.length)
      // Always update nodes, even if empty (to handle deletions)
      setNodes(nodesArray)
    }

    provider.onEdgesChange = (edgesMap) => {
      if (!isActive) return
      const edgesArray = Array.from(edgesMap.values()) as Edge[]
      console.log('CanvasPage: onEdgesChange called, edges count:', edgesArray.length)
      setEdges(edgesArray)
    }

    provider.onAwarenessChange = (states) => {
      if (!isActive) return
      setAwarenessStates(new Map(states))
    }

    provider.onUsersChange = (users) => {
      if (!isActive) return
      // Filter out current user
      const otherUsers = users.filter((u) => u.userId !== user.id)
      
      // Log new collaborators using ref to avoid stale closure
      const currentCollaborators = collaboratorsRef.current
      otherUsers.forEach((u) => {
        if (!currentCollaborators.find((c) => c.userId === u.userId)) {
          logger.trackCollaboration('collaborator_joined', { 
            collaboratorId: u.userId, 
            collaboratorName: u.userName 
          })
        }
      })
      
      // Log left collaborators
      currentCollaborators.forEach((c) => {
        if (!otherUsers.find((u) => u.userId === c.userId)) {
          logger.trackCollaboration('collaborator_left', { 
            collaboratorId: c.userId, 
            collaboratorName: c.userName 
          })
        }
      })
      
      setCollaborators(otherUsers)
    }

    provider.onConnectionChange = (connected) => {
      if (!isActive) return
      setIsConnected(connected)
      if (connected) {
        logger.trackCollaboration('connection_restored')
      } else {
        logger.trackCollaboration('connection_lost')
      }
    }

    // Connect to server
    logger.trackCollaboration('sync_started')
    provider.connect()
      .then(() => {
        if (isActive) {
          logger.trackCollaboration('sync_completed')
        }
      })
      .catch((error) => {
        if (isActive) {
          logger.trackCollaboration('sync_error', { error: error instanceof Error ? error.message : 'Unknown error' })
          console.error('Connection failed:', error)
          toast({ title: 'Failed to connect', description: 'Retrying...', variant: 'destructive' })
        }
      })

    // Cleanup on unmount
    return () => {
      isActive = false
      provider.disconnect()
      provider.destroy()
      providerRef.current = null
    }
  }, [canvasId, user?.id]) // Only depend on canvasId and user.id, not the setter functions

  // Handle node changes and sync to Yjs
  const handleNodesChange = useCallback(
    (changes: NodeChange[]) => {
      // Apply changes locally
      const updatedNodes = applyNodeChanges(changes, nodes)
      setNodes(updatedNodes)

      // Sync to Yjs and log changes
      if (providerRef.current) {
        for (const change of changes) {
          if (change.type === 'remove') {
            providerRef.current.deleteNode(change.id)
            logger.trackCanvasOperation('node_deleted', { nodeId: change.id })
          } else if (change.type === 'position' && change.position) {
            const node = updatedNodes.find((n) => n.id === change.id)
            if (node) {
              providerRef.current.setNode(node.id, node)
              // Don't log every position change (too noisy)
            }
          } else if (change.type === 'dimensions' || change.type === 'select') {
            const node = updatedNodes.find((n) => n.id === change.id)
            if (node) {
              providerRef.current.setNode(node.id, node)
            }
          }
        }
      }
    },
    [nodes, setNodes]
  )

  // Handle edge changes and sync to Yjs
  const handleEdgesChange = useCallback(
    (changes: EdgeChange[]) => {
      const updatedEdges = applyEdgeChanges(changes, edges)
      setEdges(updatedEdges)

      if (providerRef.current) {
        for (const change of changes) {
          if (change.type === 'remove') {
            providerRef.current.deleteEdge(change.id)
            logger.trackCanvasOperation('edge_deleted', { edgeId: change.id })
          }
        }
      }
    },
    [edges, setEdges]
  )

  // Handle new connections
  const onConnect = useCallback(
    (connection: Connection) => {
      const newEdge = {
        ...connection,
        id: `edge-${Date.now()}`,
        type: 'smoothstep',
      } as Edge

      setEdges((eds) => addEdge(newEdge, eds))

      if (providerRef.current) {
        providerRef.current.setEdge(newEdge.id, newEdge)
      }

      logger.trackCanvasOperation('edge_created', { 
        edgeId: newEdge.id, 
        source: connection.source, 
        target: connection.target 
      })
    },
    [setEdges]
  )

  // Add new node
  const addNode = useCallback(
    (shape: string) => {
      const id = `node-${Date.now()}`
      const position = reactFlowInstance.screenToFlowPosition({
        x: window.innerWidth / 2,
        y: window.innerHeight / 2,
      })

      const newNode: Node = {
        id,
        type: 'custom',
        position,
        data: {
          label: shape === 'text' ? 'Text' : 'New Node',
          shape: shape === 'text' ? 'rectangle' : shape,
          color: '',
        },
      }

      setNodes((nds) => [...nds, newNode])

      if (providerRef.current) {
        providerRef.current.setNode(id, newNode)
      }

      logger.trackCanvasOperation('node_created', { 
        nodeId: id, 
        shape, 
        position 
      })
    },
    [setNodes, reactFlowInstance]
  )

  // Update cursor awareness
  const handleMouseMove = useCallback(
    (event: React.MouseEvent) => {
      if (!providerRef.current || !user) return

      const position = reactFlowInstance.screenToFlowPosition({
        x: event.clientX,
        y: event.clientY,
      })

      providerRef.current.updateAwareness({
        cursor: position,
        user: { id: user.id, name: user.name, color: userColorRef.current },
      })
    },
    [reactFlowInstance, user]
  )

  if (isLoading) {
    return (
      <div className="h-screen flex items-center justify-center">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary" />
      </div>
    )
  }

  return (
    <div className="h-screen flex flex-col">
      {/* Top Toolbar */}
      <header className="h-14 border-b bg-card px-4 flex items-center justify-between z-10">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => navigate('/dashboard')}>
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <div>
            <h1 className="font-semibold">{canvas?.name}</h1>
            <p className="text-xs text-muted-foreground">{canvas?.workspaceName}</p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {/* Connection status */}
          <TooltipProvider>
            <Tooltip>
              <TooltipTrigger asChild>
                <div className={cn('p-2 rounded-full', isConnected ? 'text-green-500' : 'text-red-500')}>
                  {isConnected ? <Wifi className="h-4 w-4" /> : <WifiOff className="h-4 w-4" />}
                </div>
              </TooltipTrigger>
              <TooltipContent>
                {isConnected ? 'Connected - changes sync automatically' : 'Disconnected - reconnecting...'}
              </TooltipContent>
            </Tooltip>
          </TooltipProvider>

          {/* Collaborators */}
          {collaborators.length > 0 && (
            <div className="flex items-center gap-1 px-2">
              <Users className="h-4 w-4 text-muted-foreground" />
              <div className="flex -space-x-2">
                {collaborators.slice(0, 3).map((collab) => (
                  <TooltipProvider key={collab.connectionId}>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <Avatar className="h-7 w-7 border-2 border-background">
                          <AvatarFallback className="text-xs">
                            {getInitials(collab.userName)}
                          </AvatarFallback>
                        </Avatar>
                      </TooltipTrigger>
                      <TooltipContent>{collab.userName}</TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                ))}
                {collaborators.length > 3 && (
                  <Avatar className="h-7 w-7 border-2 border-background">
                    <AvatarFallback className="text-xs">+{collaborators.length - 3}</AvatarFallback>
                  </Avatar>
                )}
              </div>
            </div>
          )}

          <ThemeToggle />
        </div>
      </header>

      {/* Main Canvas Area */}
      <div className="flex-1 relative" onMouseMove={handleMouseMove}>
        <ReactFlow
          nodes={nodes}
          edges={edges}
          onNodesChange={handleNodesChange}
          onEdgesChange={handleEdgesChange}
          onConnect={onConnect}
          nodeTypes={nodeTypes}
          fitView
          snapToGrid
          snapGrid={[15, 15]}
          defaultEdgeOptions={{
            type: 'smoothstep',
            animated: false,
          }}
        >
          <Background gap={15} size={1} />
          <Controls />
          <MiniMap
            nodeColor={(node) => node.data?.color || 'var(--muted)'}
            maskColor="rgba(0, 0, 0, 0.1)"
          />

          {/* Tool Panel */}
          <Panel position="top-left" className="bg-card border rounded-lg shadow-md p-1 flex gap-1">
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant={selectedTool === 'rectangle' ? 'secondary' : 'ghost'}
                    size="icon"
                    onClick={() => {
                      setSelectedTool('rectangle')
                      addNode('rectangle')
                    }}
                  >
                    <Square className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Rectangle</TooltipContent>
              </Tooltip>

              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant={selectedTool === 'circle' ? 'secondary' : 'ghost'}
                    size="icon"
                    onClick={() => {
                      setSelectedTool('circle')
                      addNode('circle')
                    }}
                  >
                    <Circle className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Circle</TooltipContent>
              </Tooltip>

              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant={selectedTool === 'diamond' ? 'secondary' : 'ghost'}
                    size="icon"
                    onClick={() => {
                      setSelectedTool('diamond')
                      addNode('diamond')
                    }}
                  >
                    <Diamond className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Diamond</TooltipContent>
              </Tooltip>

              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    variant={selectedTool === 'text' ? 'secondary' : 'ghost'}
                    size="icon"
                    onClick={() => {
                      setSelectedTool('text')
                      addNode('text')
                    }}
                  >
                    <Type className="h-4 w-4" />
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Text</TooltipContent>
              </Tooltip>
            </TooltipProvider>
          </Panel>
        </ReactFlow>

        {/* Collaborator Cursors */}
        <AnimatePresence>
          {Array.from(awarenessStates.entries()).map(([id, state]) => {
            if (!state.cursor || !state.user || state.user.id === user?.id) return null

            const screenPos = reactFlowInstance.flowToScreenPosition(state.cursor)

            return (
              <motion.div
                key={id}
                className="collaborator-cursor"
                initial={{ opacity: 0, scale: 0.5 }}
                animate={{ opacity: 1, scale: 1 }}
                exit={{ opacity: 0, scale: 0.5 }}
                style={{
                  left: screenPos.x,
                  top: screenPos.y,
                }}
              >
                <div
                  className="collaborator-cursor-dot"
                  style={{ backgroundColor: state.user.color }}
                />
                <span
                  className="collaborator-cursor-name"
                  style={{ backgroundColor: state.user.color }}
                >
                  {state.user.name}
                </span>
              </motion.div>
            )
          })}
        </AnimatePresence>
      </div>
    </div>
  )
}

export default function CanvasPage() {
  return (
    <ReactFlowProvider>
      <CanvasEditor />
    </ReactFlowProvider>
  )
}
