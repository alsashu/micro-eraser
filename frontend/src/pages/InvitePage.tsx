import { useState, useEffect } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useAuth } from '@/contexts/AuthContext'
import { Button } from '@/components/ui/Button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/Card'
import { ThemeToggle } from '@/components/layout/ThemeToggle'
import { toast } from '@/components/ui/Toaster'
import { api } from '@/lib/api'
import { permissionToString } from '@/lib/utils'
import { CheckCircle, XCircle, Loader2 } from 'lucide-react'

interface InviteValidation {
  isValid: boolean
  workspaceName?: string
  permission?: number
  errorMessage?: string
}

export default function InvitePage() {
  const { token } = useParams<{ token: string }>()
  const navigate = useNavigate()
  const { isAuthenticated, isLoading: authLoading } = useAuth()
  
  const [validation, setValidation] = useState<InviteValidation | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isAccepting, setIsAccepting] = useState(false)

  useEffect(() => {
    if (!token) {
      navigate('/dashboard')
      return
    }

    validateInvite()
  }, [token])

  async function validateInvite() {
    try {
      const response = await api.get(`/invite/validate/${token}`)
      setValidation(response.data)
    } catch (error) {
      setValidation({
        isValid: false,
        errorMessage: 'Failed to validate invite',
      })
    } finally {
      setIsLoading(false)
    }
  }

  async function acceptInvite() {
    if (!token) return

    setIsAccepting(true)
    try {
      await api.post('/invite/accept', { token })
      toast({ title: 'Welcome!', description: `You've joined ${validation?.workspaceName}` })
      navigate('/dashboard')
    } catch (error) {
      toast({
        title: 'Failed to accept invite',
        description: error instanceof Error ? error.message : 'Please try again',
        variant: 'destructive',
      })
    } finally {
      setIsAccepting(false)
    }
  }

  // Show loading state
  if (isLoading || authLoading) {
    return (
      <div className="min-h-screen flex flex-col">
        <header className="h-14 border-b px-4 flex items-center justify-between">
          <Link to="/" className="font-bold text-lg">
            MicroEraser
          </Link>
          <ThemeToggle />
        </header>
        <main className="flex-1 flex items-center justify-center">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </main>
      </div>
    )
  }

  // Redirect to login if not authenticated
  if (!isAuthenticated && validation?.isValid) {
    return (
      <div className="min-h-screen flex flex-col">
        <header className="h-14 border-b px-4 flex items-center justify-between">
          <Link to="/" className="font-bold text-lg">
            MicroEraser
          </Link>
          <ThemeToggle />
        </header>
        <main className="flex-1 flex items-center justify-center p-4">
          <Card className="w-full max-w-md animate-fade-in">
            <CardHeader className="text-center">
              <div className="mx-auto mb-4 h-12 w-12 rounded-full bg-primary/10 flex items-center justify-center">
                <CheckCircle className="h-6 w-6 text-primary" />
              </div>
              <CardTitle>You're invited!</CardTitle>
              <CardDescription>
                You've been invited to join <strong>{validation.workspaceName}</strong> with{' '}
                <strong>{permissionToString(validation.permission || 0)}</strong> access.
              </CardDescription>
            </CardHeader>
            <CardContent className="text-center text-sm text-muted-foreground">
              Sign in or create an account to accept this invitation.
            </CardContent>
            <CardFooter className="flex flex-col gap-2">
              <Button asChild className="w-full">
                <Link to={`/login?redirect=/invite/${token}`}>Sign in</Link>
              </Button>
              <Button variant="outline" asChild className="w-full">
                <Link to={`/register?redirect=/invite/${token}`}>Create account</Link>
              </Button>
            </CardFooter>
          </Card>
        </main>
      </div>
    )
  }

  // Show invalid invite
  if (!validation?.isValid) {
    return (
      <div className="min-h-screen flex flex-col">
        <header className="h-14 border-b px-4 flex items-center justify-between">
          <Link to="/" className="font-bold text-lg">
            MicroEraser
          </Link>
          <ThemeToggle />
        </header>
        <main className="flex-1 flex items-center justify-center p-4">
          <Card className="w-full max-w-md animate-fade-in">
            <CardHeader className="text-center">
              <div className="mx-auto mb-4 h-12 w-12 rounded-full bg-destructive/10 flex items-center justify-center">
                <XCircle className="h-6 w-6 text-destructive" />
              </div>
              <CardTitle>Invalid Invite</CardTitle>
              <CardDescription>
                {validation?.errorMessage || 'This invite link is invalid or has expired.'}
              </CardDescription>
            </CardHeader>
            <CardFooter>
              <Button asChild className="w-full">
                <Link to="/dashboard">Go to Dashboard</Link>
              </Button>
            </CardFooter>
          </Card>
        </main>
      </div>
    )
  }

  // Show invite acceptance for authenticated users
  return (
    <div className="min-h-screen flex flex-col">
      <header className="h-14 border-b px-4 flex items-center justify-between">
        <Link to="/" className="font-bold text-lg">
          MicroEraser
        </Link>
        <ThemeToggle />
      </header>
      <main className="flex-1 flex items-center justify-center p-4">
        <Card className="w-full max-w-md animate-fade-in">
          <CardHeader className="text-center">
            <div className="mx-auto mb-4 h-12 w-12 rounded-full bg-primary/10 flex items-center justify-center">
              <CheckCircle className="h-6 w-6 text-primary" />
            </div>
            <CardTitle>Join Workspace</CardTitle>
            <CardDescription>
              You've been invited to join <strong>{validation.workspaceName}</strong> with{' '}
              <strong>{permissionToString(validation.permission || 0)}</strong> access.
            </CardDescription>
          </CardHeader>
          <CardFooter className="flex flex-col gap-2">
            <Button className="w-full" onClick={acceptInvite} disabled={isAccepting}>
              {isAccepting ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Joining...
                </>
              ) : (
                'Accept Invite'
              )}
            </Button>
            <Button variant="outline" asChild className="w-full">
              <Link to="/dashboard">Cancel</Link>
            </Button>
          </CardFooter>
        </Card>
      </main>
    </div>
  )
}
