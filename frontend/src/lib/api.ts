import { getCorrelationId, logger, EventTypes } from './logger'

const API_BASE_URL = '/api'

let accessToken: string | null = null

export function setAuthToken(token: string) {
  accessToken = token
}

export function clearAuthToken() {
  accessToken = null
}

export function getAuthToken() {
  return accessToken
}

async function handleResponse(response: Response, endpoint: string) {
  // Capture correlation ID from response if present
  const responseCorrelationId = response.headers.get('X-Correlation-ID')
  
  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'An error occurred' }))
    const errorMessage = error.message || `HTTP ${response.status}`
    
    // Log API error
    logger.error(EventTypes.API_ERROR, `API error: ${endpoint}`, new Error(errorMessage), {
      endpoint,
      status: response.status,
      responseCorrelationId,
    })
    
    throw new Error(errorMessage)
  }
  
  // Handle 204 No Content
  if (response.status === 204) {
    return null
  }
  
  return response.json()
}

function getHeaders(): HeadersInit {
  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    'X-Correlation-ID': getCorrelationId(),
  }
  
  if (accessToken) {
    headers['Authorization'] = `Bearer ${accessToken}`
  }
  
  return headers
}

export const api = {
  async get(endpoint: string) {
    logger.debug('api_request', `GET ${endpoint}`, { method: 'GET', endpoint })
    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
      method: 'GET',
      headers: getHeaders(),
    })
    return { data: await handleResponse(response, endpoint) }
  },

  async post(endpoint: string, data?: unknown) {
    logger.debug('api_request', `POST ${endpoint}`, { method: 'POST', endpoint })
    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
      method: 'POST',
      headers: getHeaders(),
      body: data ? JSON.stringify(data) : undefined,
    })
    return { data: await handleResponse(response, endpoint) }
  },

  async put(endpoint: string, data?: unknown) {
    logger.debug('api_request', `PUT ${endpoint}`, { method: 'PUT', endpoint })
    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
      method: 'PUT',
      headers: getHeaders(),
      body: data ? JSON.stringify(data) : undefined,
    })
    return { data: await handleResponse(response, endpoint) }
  },

  async delete(endpoint: string) {
    logger.debug('api_request', `DELETE ${endpoint}`, { method: 'DELETE', endpoint })
    const response = await fetch(`${API_BASE_URL}${endpoint}`, {
      method: 'DELETE',
      headers: getHeaders(),
    })
    return { data: await handleResponse(response, endpoint) }
  },
}

// Types for API responses
export interface Workspace {
  id: string
  name: string
  description?: string
  ownerId: string
  ownerName: string
  memberCount: number
  canvasCount: number
  createdAt: string
  updatedAt: string
}

export interface WorkspaceDetail {
  id: string
  name: string
  description?: string
  ownerId: string
  ownerName: string
  members: WorkspaceMember[]
  canvases: Canvas[]
  createdAt: string
  updatedAt: string
}

export interface WorkspaceMember {
  userId: string
  name: string
  email: string
  avatarUrl?: string
  role: number
  joinedAt: string
}

export interface Canvas {
  id: string
  workspaceId: string
  name: string
  description?: string
  createdAt: string
  updatedAt: string
}

export interface CanvasDetail {
  id: string
  workspaceId: string
  workspaceName: string
  name: string
  description?: string
  hasSnapshot: boolean
  latestVersion?: number
  createdAt: string
  updatedAt: string
}

export interface Invite {
  id: string
  workspaceId: string
  workspaceName: string
  email?: string
  token: string
  permission: number
  expiresAt: string
  isUsed: boolean
  maxUses?: number
  useCount: number
  createdAt: string
}
