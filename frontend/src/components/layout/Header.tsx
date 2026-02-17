import { Link } from 'react-router-dom'
import { LogOut, User } from 'lucide-react'
import { useAuth } from '@/contexts/AuthContext'
import { ThemeToggle } from './ThemeToggle'
import { Button } from '@/components/ui/Button'
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/Avatar'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/DropdownMenu'
import { getInitials } from '@/lib/utils'

interface HeaderProps {
  title?: string
  actions?: React.ReactNode
}

export function Header({ title, actions }: HeaderProps) {
  const { user, logout } = useAuth()

  return (
    <header className="h-14 border-b bg-card px-4 flex items-center justify-between">
      <div className="flex items-center gap-4">
        <Link to="/dashboard" className="font-bold text-lg">
          MicroEraser
        </Link>
        {title && (
          <>
            <span className="text-muted-foreground">/</span>
            <span className="font-medium">{title}</span>
          </>
        )}
      </div>

      <div className="flex items-center gap-2">
        {actions}
        <ThemeToggle />
        
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" className="relative h-8 w-8 rounded-full">
              <Avatar className="h-8 w-8">
                <AvatarImage src={user?.avatarUrl} alt={user?.name} />
                <AvatarFallback>{user ? getInitials(user.name) : '?'}</AvatarFallback>
              </Avatar>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent className="w-56" align="end" forceMount>
            <DropdownMenuLabel className="font-normal">
              <div className="flex flex-col space-y-1">
                <p className="text-sm font-medium leading-none">{user?.name}</p>
                <p className="text-xs leading-none text-muted-foreground">
                  {user?.email}
                </p>
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem asChild>
              <Link to="/profile">
                <User className="mr-2 h-4 w-4" />
                Profile
              </Link>
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={logout}>
              <LogOut className="mr-2 h-4 w-4" />
              Log out
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </header>
  )
}
