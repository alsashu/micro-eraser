import { useState, useEffect } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { Plus, FileText, Users, Share2, Trash2 } from 'lucide-react'
import { Header } from '@/components/layout/Header'
import { Sidebar } from '@/components/layout/Sidebar'
import { Button } from '@/components/ui/Button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/Card'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/Avatar'
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
import { api, type WorkspaceDetail, type Canvas } from '@/lib/api'
import { formatRelativeTime, getInitials, roleToString, permissionToString } from '@/lib/utils'

export default function DashboardPage() {
  const { workspaceId } = useParams()
  const navigate = useNavigate()
  const [workspace, setWorkspace] = useState<WorkspaceDetail | null>(null)
  const [recentCanvases, setRecentCanvases] = useState<Canvas[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [showInviteDialog, setShowInviteDialog] = useState(false)
  const [inviteEmail, setInviteEmail] = useState('')
  const [inviteLink, setInviteLink] = useState('')

  useEffect(() => {
    if (workspaceId) {
      loadWorkspace(workspaceId)
    } else {
      loadRecentCanvases()
    }
  }, [workspaceId])

  async function loadWorkspace(id: string) {
    setIsLoading(true)
    try {
      const response = await api.get(`/workspace/${id}`)
      setWorkspace(response.data)
    } catch (error) {
      toast({ title: 'Failed to load workspace', variant: 'destructive' })
    } finally {
      setIsLoading(false)
    }
  }

  async function loadRecentCanvases() {
    setIsLoading(true)
    try {
      const workspacesResponse = await api.get('/workspace')
      const workspaces = workspacesResponse.data
      
      if (workspaces.length > 0) {
        const canvasesResponse = await api.get(`/canvas/workspace/${workspaces[0].id}`)
        setRecentCanvases(canvasesResponse.data.slice(0, 6))
      }
    } catch (error) {
      toast({ title: 'Failed to load canvases', variant: 'destructive' })
    } finally {
      setIsLoading(false)
    }
  }

  async function createInviteLink() {
    if (!workspaceId) return
    
    try {
      const response = await api.post(`/invite/workspace/${workspaceId}/link`, {
        permission: 1, // Edit
        expiryHours: 168, // 7 days
      })
      setInviteLink(`${window.location.origin}/invite/${response.data.token}`)
    } catch (error) {
      toast({ title: 'Failed to create invite link', variant: 'destructive' })
    }
  }

  async function sendEmailInvite() {
    if (!workspaceId || !inviteEmail) return
    
    try {
      await api.post(`/invite/workspace/${workspaceId}/email`, {
        email: inviteEmail,
        permission: 1,
      })
      toast({ title: 'Invite sent!' })
      setInviteEmail('')
    } catch (error) {
      toast({ title: 'Failed to send invite', variant: 'destructive' })
    }
  }

  async function copyInviteLink() {
    if (inviteLink) {
      await navigator.clipboard.writeText(inviteLink)
      toast({ title: 'Link copied!' })
    }
  }

  if (isLoading) {
    return (
      <div className="h-screen flex">
        <Sidebar />
        <div className="flex-1 flex flex-col">
          <Header />
          <main className="flex-1 p-6">
            <div className="animate-pulse space-y-4">
              <div className="h-8 bg-muted rounded w-1/4" />
              <div className="grid grid-cols-3 gap-4">
                {[1, 2, 3].map((i) => (
                  <div key={i} className="h-40 bg-muted rounded" />
                ))}
              </div>
            </div>
          </main>
        </div>
      </div>
    )
  }

  // Workspace detail view
  if (workspaceId && workspace) {
    return (
      <div className="h-screen flex">
        <Sidebar />
        <div className="flex-1 flex flex-col overflow-hidden">
          <Header
            title={workspace.name}
            actions={
              <Dialog open={showInviteDialog} onOpenChange={setShowInviteDialog}>
                <DialogTrigger asChild>
                  <Button variant="outline" size="sm" onClick={createInviteLink}>
                    <Share2 className="h-4 w-4 mr-2" />
                    Invite
                  </Button>
                </DialogTrigger>
                <DialogContent>
                  <DialogHeader>
                    <DialogTitle>Invite to {workspace.name}</DialogTitle>
                    <DialogDescription>
                      Invite collaborators to this workspace
                    </DialogDescription>
                  </DialogHeader>
                  <div className="space-y-4 py-4">
                    <div className="space-y-2">
                      <Label>Invite by email</Label>
                      <div className="flex gap-2">
                        <Input
                          placeholder="email@example.com"
                          value={inviteEmail}
                          onChange={(e) => setInviteEmail(e.target.value)}
                        />
                        <Button onClick={sendEmailInvite}>Send</Button>
                      </div>
                    </div>
                    <div className="relative">
                      <div className="absolute inset-0 flex items-center">
                        <span className="w-full border-t" />
                      </div>
                      <div className="relative flex justify-center text-xs uppercase">
                        <span className="bg-background px-2 text-muted-foreground">Or</span>
                      </div>
                    </div>
                    <div className="space-y-2">
                      <Label>Share invite link</Label>
                      <div className="flex gap-2">
                        <Input value={inviteLink} readOnly />
                        <Button variant="outline" onClick={copyInviteLink}>Copy</Button>
                      </div>
                    </div>
                  </div>
                </DialogContent>
              </Dialog>
            }
          />
          <main className="flex-1 overflow-y-auto p-6">
            <div className="max-w-5xl mx-auto space-y-8">
              {/* Canvases */}
              <section>
                <div className="flex items-center justify-between mb-4">
                  <h2 className="text-lg font-semibold">Canvases</h2>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  {workspace.canvases.map((canvas, index) => (
                    <motion.div
                      key={canvas.id}
                      initial={{ opacity: 0, y: 20 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ delay: index * 0.05 }}
                    >
                      <Link to={`/canvas/${canvas.id}`}>
                        <Card className="cursor-pointer hover:shadow-lg transition-all duration-200 hover:-translate-y-1">
                          <CardHeader>
                            <div className="flex items-start justify-between">
                              <FileText className="h-8 w-8 text-muted-foreground" />
                            </div>
                            <CardTitle className="text-base">{canvas.name}</CardTitle>
                            <CardDescription>
                              {canvas.description || 'No description'}
                            </CardDescription>
                          </CardHeader>
                          <CardContent>
                            <p className="text-xs text-muted-foreground">
                              Updated {formatRelativeTime(canvas.updatedAt)}
                            </p>
                          </CardContent>
                        </Card>
                      </Link>
                    </motion.div>
                  ))}
                </div>
              </section>

              {/* Members */}
              <section>
                <div className="flex items-center justify-between mb-4">
                  <h2 className="text-lg font-semibold">Members</h2>
                  <span className="text-sm text-muted-foreground">
                    {workspace.members.length} member{workspace.members.length !== 1 ? 's' : ''}
                  </span>
                </div>
                <Card>
                  <CardContent className="p-0">
                    <div className="divide-y">
                      {workspace.members.map((member) => (
                        <div
                          key={member.userId}
                          className="flex items-center justify-between p-4"
                        >
                          <div className="flex items-center gap-3">
                            <Avatar>
                              <AvatarImage src={member.avatarUrl} />
                              <AvatarFallback>{getInitials(member.name)}</AvatarFallback>
                            </Avatar>
                            <div>
                              <p className="font-medium">{member.name}</p>
                              <p className="text-sm text-muted-foreground">{member.email}</p>
                            </div>
                          </div>
                          <span className="text-sm text-muted-foreground">
                            {roleToString(member.role)}
                          </span>
                        </div>
                      ))}
                    </div>
                  </CardContent>
                </Card>
              </section>
            </div>
          </main>
        </div>
      </div>
    )
  }

  // Default dashboard view
  return (
    <div className="h-screen flex">
      <Sidebar />
      <div className="flex-1 flex flex-col overflow-hidden">
        <Header />
        <main className="flex-1 overflow-y-auto p-6">
          <div className="max-w-5xl mx-auto space-y-8">
            <section>
              <h1 className="text-2xl font-bold mb-2">Welcome to MicroEraser</h1>
              <p className="text-muted-foreground">
                Select a workspace from the sidebar or create a new one to get started.
              </p>
            </section>

            {recentCanvases.length > 0 && (
              <section>
                <h2 className="text-lg font-semibold mb-4">Recent Canvases</h2>
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  {recentCanvases.map((canvas, index) => (
                    <motion.div
                      key={canvas.id}
                      initial={{ opacity: 0, y: 20 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ delay: index * 0.05 }}
                    >
                      <Link to={`/canvas/${canvas.id}`}>
                        <Card className="cursor-pointer hover:shadow-lg transition-all duration-200 hover:-translate-y-1">
                          <CardHeader>
                            <FileText className="h-8 w-8 text-muted-foreground mb-2" />
                            <CardTitle className="text-base">{canvas.name}</CardTitle>
                            <CardDescription>
                              {canvas.description || 'No description'}
                            </CardDescription>
                          </CardHeader>
                          <CardContent>
                            <p className="text-xs text-muted-foreground">
                              Updated {formatRelativeTime(canvas.updatedAt)}
                            </p>
                          </CardContent>
                        </Card>
                      </Link>
                    </motion.div>
                  ))}
                </div>
              </section>
            )}
          </div>
        </main>
      </div>
    </div>
  )
}
