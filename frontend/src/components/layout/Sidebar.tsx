import { useState, useEffect } from 'react'
import { Link, useParams, useNavigate } from 'react-router-dom'
import { motion, AnimatePresence } from 'framer-motion'
import {
  ChevronDown,
  ChevronRight,
  Plus,
  FolderOpen,
  FileText,
  Settings,
  Users,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/Button'
import { api, type Workspace, type Canvas } from '@/lib/api'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/Dialog'
import { Input } from '@/components/ui/Input'
import { Label } from '@/components/ui/Label'
import { toast } from '@/components/ui/Toaster'

interface SidebarProps {
  className?: string
}

export function Sidebar({ className }: SidebarProps) {
  const { workspaceId } = useParams()
  const navigate = useNavigate()
  const [workspaces, setWorkspaces] = useState<Workspace[]>([])
  const [expandedWorkspaces, setExpandedWorkspaces] = useState<Set<string>>(new Set())
  const [canvases, setCanvases] = useState<Record<string, Canvas[]>>({})
  const [isLoading, setIsLoading] = useState(true)
  const [showNewWorkspace, setShowNewWorkspace] = useState(false)
  const [showNewCanvas, setShowNewCanvas] = useState<string | null>(null)
  const [newWorkspaceName, setNewWorkspaceName] = useState('')
  const [newCanvasName, setNewCanvasName] = useState('')

  useEffect(() => {
    loadWorkspaces()
  }, [])

  useEffect(() => {
    if (workspaceId && !expandedWorkspaces.has(workspaceId)) {
      setExpandedWorkspaces((prev) => new Set([...prev, workspaceId]))
    }
  }, [workspaceId])

  async function loadWorkspaces() {
    try {
      const response = await api.get('/workspace')
      setWorkspaces(response.data)
      
      // Auto-expand first workspace
      if (response.data.length > 0 && expandedWorkspaces.size === 0) {
        const firstId = response.data[0].id
        setExpandedWorkspaces(new Set([firstId]))
        loadCanvases(firstId)
      }
    } catch (error) {
      toast({ title: 'Failed to load workspaces', variant: 'destructive' })
    } finally {
      setIsLoading(false)
    }
  }

  async function loadCanvases(wsId: string) {
    if (canvases[wsId]) return
    
    try {
      const response = await api.get(`/canvas/workspace/${wsId}`)
      setCanvases((prev) => ({ ...prev, [wsId]: response.data }))
    } catch (error) {
      toast({ title: 'Failed to load canvases', variant: 'destructive' })
    }
  }

  function toggleWorkspace(wsId: string) {
    const newExpanded = new Set(expandedWorkspaces)
    if (newExpanded.has(wsId)) {
      newExpanded.delete(wsId)
    } else {
      newExpanded.add(wsId)
      loadCanvases(wsId)
    }
    setExpandedWorkspaces(newExpanded)
  }

  async function createWorkspace() {
    if (!newWorkspaceName.trim()) return

    try {
      const response = await api.post('/workspace', { name: newWorkspaceName })
      setWorkspaces((prev) => [response.data, ...prev])
      setNewWorkspaceName('')
      setShowNewWorkspace(false)
      toast({ title: 'Workspace created' })
    } catch (error) {
      toast({ title: 'Failed to create workspace', variant: 'destructive' })
    }
  }

  async function createCanvas(wsId: string) {
    if (!newCanvasName.trim()) return

    try {
      const response = await api.post(`/canvas/workspace/${wsId}`, { name: newCanvasName })
      setCanvases((prev) => ({
        ...prev,
        [wsId]: [response.data, ...(prev[wsId] || [])],
      }))
      setNewCanvasName('')
      setShowNewCanvas(null)
      toast({ title: 'Canvas created' })
      navigate(`/canvas/${response.data.id}`)
    } catch (error) {
      toast({ title: 'Failed to create canvas', variant: 'destructive' })
    }
  }

  if (isLoading) {
    return (
      <div className={cn('w-64 border-r bg-card p-4', className)}>
        <div className="animate-pulse space-y-4">
          <div className="h-8 bg-muted rounded" />
          <div className="h-6 bg-muted rounded w-3/4" />
          <div className="h-6 bg-muted rounded w-1/2" />
        </div>
      </div>
    )
  }

  return (
    <div className={cn('w-64 border-r bg-card flex flex-col', className)}>
      <div className="p-4 border-b">
        <div className="flex items-center justify-between">
          <h2 className="font-semibold text-lg">Workspaces</h2>
          <Dialog open={showNewWorkspace} onOpenChange={setShowNewWorkspace}>
            <DialogTrigger asChild>
              <Button variant="ghost" size="icon" className="h-8 w-8">
                <Plus className="h-4 w-4" />
              </Button>
            </DialogTrigger>
            <DialogContent>
              <DialogHeader>
                <DialogTitle>Create Workspace</DialogTitle>
                <DialogDescription>
                  Create a new workspace to organize your diagrams.
                </DialogDescription>
              </DialogHeader>
              <div className="space-y-4 py-4">
                <div className="space-y-2">
                  <Label htmlFor="workspace-name">Name</Label>
                  <Input
                    id="workspace-name"
                    placeholder="My Workspace"
                    value={newWorkspaceName}
                    onChange={(e) => setNewWorkspaceName(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && createWorkspace()}
                  />
                </div>
              </div>
              <DialogFooter>
                <Button variant="outline" onClick={() => setShowNewWorkspace(false)}>
                  Cancel
                </Button>
                <Button onClick={createWorkspace}>Create</Button>
              </DialogFooter>
            </DialogContent>
          </Dialog>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto p-2">
        <AnimatePresence>
          {workspaces.map((workspace) => (
            <div key={workspace.id} className="mb-1">
              <button
                onClick={() => toggleWorkspace(workspace.id)}
                className={cn(
                  'w-full flex items-center gap-2 px-2 py-1.5 rounded-md text-sm font-medium transition-colors hover:bg-accent',
                  workspaceId === workspace.id && 'bg-accent'
                )}
              >
                {expandedWorkspaces.has(workspace.id) ? (
                  <ChevronDown className="h-4 w-4 text-muted-foreground" />
                ) : (
                  <ChevronRight className="h-4 w-4 text-muted-foreground" />
                )}
                <FolderOpen className="h-4 w-4 text-muted-foreground" />
                <span className="truncate flex-1 text-left">{workspace.name}</span>
              </button>

              <AnimatePresence>
                {expandedWorkspaces.has(workspace.id) && (
                  <motion.div
                    initial={{ height: 0, opacity: 0 }}
                    animate={{ height: 'auto', opacity: 1 }}
                    exit={{ height: 0, opacity: 0 }}
                    transition={{ duration: 0.2 }}
                    className="overflow-hidden"
                  >
                    <div className="ml-4 pl-2 border-l">
                      {canvases[workspace.id]?.map((canvas) => (
                        <Link
                          key={canvas.id}
                          to={`/canvas/${canvas.id}`}
                          className="flex items-center gap-2 px-2 py-1.5 rounded-md text-sm transition-colors hover:bg-accent"
                        >
                          <FileText className="h-4 w-4 text-muted-foreground" />
                          <span className="truncate">{canvas.name}</span>
                        </Link>
                      ))}

                      <Dialog
                        open={showNewCanvas === workspace.id}
                        onOpenChange={(open) => setShowNewCanvas(open ? workspace.id : null)}
                      >
                        <DialogTrigger asChild>
                          <button className="w-full flex items-center gap-2 px-2 py-1.5 rounded-md text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-foreground">
                            <Plus className="h-4 w-4" />
                            <span>New Canvas</span>
                          </button>
                        </DialogTrigger>
                        <DialogContent>
                          <DialogHeader>
                            <DialogTitle>Create Canvas</DialogTitle>
                            <DialogDescription>
                              Create a new diagram canvas in {workspace.name}.
                            </DialogDescription>
                          </DialogHeader>
                          <div className="space-y-4 py-4">
                            <div className="space-y-2">
                              <Label htmlFor="canvas-name">Name</Label>
                              <Input
                                id="canvas-name"
                                placeholder="Untitled"
                                value={newCanvasName}
                                onChange={(e) => setNewCanvasName(e.target.value)}
                                onKeyDown={(e) => e.key === 'Enter' && createCanvas(workspace.id)}
                              />
                            </div>
                          </div>
                          <DialogFooter>
                            <Button variant="outline" onClick={() => setShowNewCanvas(null)}>
                              Cancel
                            </Button>
                            <Button onClick={() => createCanvas(workspace.id)}>Create</Button>
                          </DialogFooter>
                        </DialogContent>
                      </Dialog>

                      <Link
                        to={`/workspace/${workspace.id}`}
                        className="flex items-center gap-2 px-2 py-1.5 rounded-md text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
                      >
                        <Settings className="h-4 w-4" />
                        <span>Settings</span>
                      </Link>
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>
            </div>
          ))}
        </AnimatePresence>
      </div>
    </div>
  )
}
