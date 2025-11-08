# Troubleshooting Guide - Notification Hub Test Page

## Error: "Failed to complete negotiation with the server: TypeError: Failed to fetch"

This error typically occurs due to SSL certificate or CORS issues. Follow these steps:

### Step 1: Accept SSL Certificate

1. Open a new browser tab
2. Navigate to: `https://localhost:5104/health`
3. You'll see a warning about the certificate
4. Click "Advanced" → "Proceed to localhost (unsafe)" or similar option
5. You should see: `{"status":"healthy","service":"notification"}`
6. Now return to the test page and try connecting again

### Step 2: Serve the HTML Page Properly

The test page should NOT be opened as a `file://` URL. Instead, serve it via HTTP:

#### Option A: Using Python (Recommended)
```bash
# Navigate to the test folder
cd c:\Users\YOUR_USERNAME\Desktop\Projects\MicroservicesArchitecture\src\Services\Notification\Notification.API\test

# Start a simple HTTP server
python -m http.server 8080

# Open browser to: http://localhost:8080/hub-test.html
```

#### Option B: Using Node.js
```bash
# Install http-server globally (one time)
npm install -g http-server

# Navigate to test folder
cd c:\Users\YOUR_USERNAME\Desktop\Projects\MicroservicesArchitecture\src\Services\Notification\Notification.API\test

# Start server
http-server -p 8080

# Open browser to: http://localhost:8080/hub-test.html
```

#### Option C: Using VS Code Live Server Extension
1. Install "Live Server" extension in VS Code
2. Right-click `hub-test.html`
3. Select "Open with Live Server"

### Step 3: Verify CORS Configuration

Check the Notification service `appsettings.json` or `Program.cs` to ensure CORS allows your origin.

If you're using `http://localhost:8080`, the CORS should allow this origin or use `AllowAnyOrigin()`.

Current CORS in Program.cs:
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});
```

This should work for testing. For production, restrict to specific origins.

### Step 4: Check Browser Console

1. Open browser Developer Tools (F12)
2. Go to Console tab
3. Look for detailed error messages
4. Common errors:
   - `net::ERR_CERT_AUTHORITY_INVALID` → Need to accept SSL cert (Step 1)
   - `CORS policy` → CORS configuration issue
   - `Mixed content` → Serving from HTTP but connecting to HTTPS (expected, should work)

### Step 5: Test Direct API Call

Before testing SignalR, verify the API is accessible:

```bash
# Test health endpoint
curl -k https://localhost:5104/health

# Should return:
# {"status":"healthy","service":"notification"}
```

### Step 6: Test with Swagger

1. Navigate to: `https://localhost:5104/swagger`
2. Accept the SSL certificate if needed
3. Try the `/api/notifications/send` endpoint
4. If this works, the service is functioning correctly

## Quick Checklist

- [ ] Notification service is running (check with `netstat -ano | findstr :5104`)
- [ ] SSL certificate accepted (visit `https://localhost:5104/health`)
- [ ] Test page served via HTTP server (NOT file://)
- [ ] CORS allows your origin
- [ ] Browser console shows no errors
- [ ] Health endpoint returns success

## Alternative: Use HTTP Instead of HTTPS

For easier testing during development, you can use HTTP (port 5004):

1. Update Hub URL to: `http://localhost:5004/hubs/notifications`
2. Update API URL to: `http://localhost:5004/api/notifications/send`
3. No SSL certificate issues!

Note: The service must be configured to listen on HTTP (check `launchSettings.json`).

## Still Having Issues?

### Enable CORS for file:// Protocol (NOT RECOMMENDED FOR PRODUCTION)

If you must open the HTML file directly (file:// protocol), you can temporarily disable browser security:

**Chrome:**
```bash
chrome.exe --disable-web-security --user-data-dir="C:/temp/chrome-dev"
```

**Edge:**
```bash
msedge.exe --disable-web-security --user-data-dir="C:/temp/edge-dev"
```

⚠️ **WARNING**: Only use this for testing! Always close these windows when done.

### Check Service Logs

Check the Notification service console output for any errors or warnings about CORS, authentication, or SignalR connections.

### Test with Postman

1. Import the API endpoint to Postman
2. Set method to POST
3. URL: `https://localhost:5104/api/notifications/send`
4. Headers:
   - `Content-Type: application/json`
   - `Authorization: Bearer YOUR_JWT_TOKEN`
5. Body (JSON):
   ```json
   {
     "tenantId": null,
     "userId": null,
     "title": "Test",
     "message": "Testing from Postman",
     "priority": 0,
     "deliveryType": 0
   }
   ```

If Postman works but the browser doesn't, it's definitely a CORS or SSL certificate issue.

## Common Solutions Summary

| Error | Solution |
|-------|----------|
| Failed to fetch | Accept SSL certificate at `https://localhost:5104/health` |
| CORS error | Ensure `AllowAnyOrigin()` in CORS config |
| file:// protocol issues | Serve via HTTP server (Python, Node, Live Server) |
| Mixed content | This is OK - HTTP page can connect to HTTPS WebSocket |
| Certificate invalid | Accept certificate in browser |

## Contact

If none of these solutions work, check:
1. Firewall settings
2. Antivirus blocking connections
3. Service actually running (`dotnet run` in Notification.API folder)
4. Correct port in URLs
