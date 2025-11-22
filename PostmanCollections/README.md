# Postman Collections for Microservices Architecture

This directory contains Postman collections for all the microservices in the architecture. Each service has its own collection file that can be imported into Postman.

## Collections

### 1. FileManager Service (`FileManager_Service.postman_collection.json`)

- **Base URL**: `http://localhost:5005`
- **Endpoints**:
  - File upload/download operations
  - File metadata management
  - Admin operations (global access)
  - Internal service-to-service endpoints

### 2. Identity Service (`Identity_Service.postman_collection.json`)

- **Base URL**: `http://localhost:5001`
- **Endpoints**:
  - Authentication (login, register, refresh token, logout)
  - Profile management
  - Admin user management
  - Device token management

### 3. Notification Service (`Notification_Service.postman_collection.json`)

- **Base URL**: `http://localhost:5004`
- **Endpoints**:
  - Send notifications (service/admin)
  - User notification management
  - Queue status and management
  - SignalR hub connection

### 4. Tenant Service (`Tenant_Service.postman_collection.json`)

- **Base URL**: `http://localhost:5002`
- **Endpoints**:
  - Tenant configuration retrieval
  - Tenant management (admin only)
  - Public tenant information

## Setup Instructions

1. **Import Collections**:

   - Open Postman
   - Click "Import" button
   - Select "File" tab
   - Choose the collection JSON files from this directory

2. **Configure Variables**:
   Each collection has predefined variables that need to be set:

   - `baseUrl`: Service URL (already set to correct localhost ports)
   - `tenantId`: Your tenant identifier (update this)
   - `authToken`: JWT token for authentication (obtain from login endpoints)
   - `refreshToken`: Refresh token (obtain from login/register)
   - `serviceSecret`: Shared secret for service-to-service communication

3. **Authentication Flow**:
   - Start with Identity Service collection
   - Use "Login" or "Register" endpoint to get tokens
   - Set the `authToken` variable with the access token
   - Set the `refreshToken` variable if needed
   - Use authenticated endpoints in other services

## Multi-Tenancy

This architecture supports multi-tenancy. Most endpoints require:

- `x-tenant-id` header: Identifies which tenant's data to access
- Appropriate JWT token: Either global (for service operations) or tenant-specific

## Service-to-Service Communication

Some endpoints use `X-Service-Secret` header instead of JWT for internal service communication.

## Notes

- All collections include example request bodies with placeholder data
- Update variable values according to your environment
- Some endpoints may require specific roles (User, Admin, SuperAdmin, Service)
- Rate limiting is enabled on most endpoints
- File upload endpoints expect multipart/form-data

## Testing Workflow

1. Start all services locally
2. Import collections into Postman
3. Set tenant ID variable
4. Use Identity service to authenticate
5. Test other services with obtained tokens
6. Use admin endpoints for management operations

## Environment Setup

Make sure your services are running on the specified ports:

- Identity: 5001
- Tenant: 5002
- Notification: 5004
- FileManager: 5005

You can use the provided batch files in the services directories to start them.
